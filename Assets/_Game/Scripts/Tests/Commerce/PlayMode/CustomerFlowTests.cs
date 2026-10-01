using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TYCOON.Tests
{
    public sealed class CustomerFlowTests
    {
        private GameObject testRoot;
        private NavMeshSurface surface;
        private ItemDefinition carrot;
        private Shelf shelf;
        private CheckoutStation checkout;
        private Transform spawn;
        private Transform entrance;
        private Transform exit;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            testRoot = new GameObject("CommerceTestRoot");
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "CommerceTestFloor";
            floor.transform.SetParent(testRoot.transform);
            floor.transform.position = new Vector3(0, -0.5f, 0);
            floor.transform.localScale = new Vector3(22, 1, 22);
            surface = testRoot.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();
            Assert.IsTrue(NavMesh.SamplePosition(Vector3.zero, out _, 0.5f, NavMesh.AllAreas), "Test NavMesh was not built.");
            carrot = ScriptableObject.CreateInstance<ItemDefinition>();
            carrot.Configure("carrot", "Carrot", 5);
            shelf = Owner("Shelf", new Vector3(2, 0, 0)).AddComponent<Shelf>();
            shelf.Store.Configure("shelf.test", 20, carrot, 12);
            shelf.Configure(carrot, shelf.Store);
            shelf.SetApproachPoint(Point("ShelfApproach", new Vector3(1.5f, 0, 0)));
            checkout = Owner("Checkout", new Vector3(-2, 0, 0)).AddComponent<CheckoutStation>();
            var positions = Enumerable.Range(0, 15).Select(index => Point("Queue" + index, new Vector3(-1.4f, 0, index * 0.65f))).ToArray();
            checkout.Configure(Point("CashierPoint", new Vector3(-2.2f, 0, 0)), positions);
            spawn = Point("Spawn", new Vector3(0, 0, -4));
            entrance = Point("Entrance", new Vector3(0, 0, -3));
            exit = Point("Exit", new Vector3(0, 0, 3));
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (surface != null) surface.RemoveData();
            if (testRoot != null) Object.Destroy(testRoot);
            if (carrot != null) Object.Destroy(carrot);
            yield return null;
        }

        private GameObject Owner(string name, Vector3 position)
        {
            var owner = new GameObject(name);
            owner.transform.SetParent(testRoot.transform);
            owner.transform.position = position;
            return owner;
        }

        private Transform Point(string name, Vector3 position) => Owner(name, position).transform;

        private CustomerAgent Customer(string name, bool active)
        {
            var owner = Owner(name, spawn.position);
            owner.SetActive(false);
            var customer = owner.AddComponent<CustomerAgent>();
            var agent = customer.GetComponent<NavMeshAgent>();
            agent.speed = 8f;
            agent.acceleration = 40f;
            agent.radius = 0.15f;
            agent.stoppingDistance = 0.1f;
            customer.Configure(new[] { shelf }, checkout, entrance, exit);
            owner.SetActive(active);
            return customer;
        }

        private static IEnumerator Await(Func<bool> condition, string failure, float seconds = 12f)
        {
            var deadline = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(condition(), failure);
        }

        [UnityTest]
        public IEnumerator CustomerWalksTakesQueuesPaysAndExits()
        {
            var customer = Customer("Customer", true);
            var returned = false;
            customer.BeginVisit(actor => { returned = true; actor.gameObject.SetActive(false); });
            yield return Await(() => customer.IsReadyForCheckout, "Customer did not reach queue head.");
            Assert.AreEqual(1, customer.Cart.TotalCount);
            Assert.AreEqual(11, shelf.AvailableCount);
            Assert.AreEqual(0, checkout.PendingRevenue);
            Assert.IsTrue(checkout.TryServeNext());
            Assert.AreEqual(0, customer.Cart.TotalCount);
            Assert.AreEqual(5, checkout.PendingRevenue);
            Assert.AreEqual(0, checkout.QueueCount);
            yield return Await(() => returned, "Paid customer did not exit.");
            var wallet = new Wallet();
            Assert.AreEqual(5, checkout.CollectMoney(wallet));
            Assert.AreEqual(5, wallet.Balance);
            Assert.AreEqual(0, checkout.CollectMoney(wallet));
        }

        [UnityTest]
        public IEnumerator StockoutCustomerExitsWithoutPayment()
        {
            shelf.Store.Inventory.TryRemove(carrot, 12);
            var customer = Customer("StockoutCustomer", true);
            var returned = false;
            customer.BeginVisit(actor => { returned = true; actor.gameObject.SetActive(false); });
            yield return Await(() => returned, "Stockout customer did not exit.");
            Assert.AreEqual(0, customer.Cart.TotalCount);
            Assert.IsFalse(customer.PaymentComplete);
            Assert.AreEqual(0, checkout.PendingRevenue);
        }

        [UnityTest]
        public IEnumerator PoolSaveBarrierReturnsCartsAndReusesTheSameInstances()
        {
            var template = Customer("CustomerTemplate", false);
            var pool = Owner("Pool", Vector3.zero).AddComponent<CustomerPool>();
            pool.Configure(template, spawn, entrance, exit, new[] { shelf }, checkout, 12, 300f);
            pool.Initialize();
            Assert.AreEqual(12, pool.Capacity);
            var entityIds = pool.Customers.Select(customer => customer.GetEntityId()).ToArray();
            Assert.IsTrue(pool.SuspendAndReturnCarts());
            pool.ResumeSpawning();
            Assert.IsTrue(pool.TrySpawn());
            Assert.IsTrue(pool.TrySpawn());
            yield return Await(() => pool.Customers.Count(customer => customer.Cart.TotalCount > 0) == 2,
                "Pooled customers did not take real shelf goods.");
            Assert.AreEqual(12, shelf.AvailableCount + pool.Customers.Sum(customer => customer.Cart.TotalCount));
            Assert.IsTrue(pool.SuspendAndReturnCarts());
            Assert.AreEqual(0, pool.ActiveCount);
            Assert.AreEqual(12, shelf.AvailableCount);
            Assert.IsTrue(pool.Customers.All(customer => customer.Cart.TotalCount == 0 && !customer.gameObject.activeSelf));
            Assert.AreEqual(0, checkout.QueueCount);
            Assert.AreEqual(0, checkout.PendingRevenue);
            Assert.IsFalse(pool.TrySpawn());
            pool.ResumeSpawning();
            Assert.IsTrue(pool.TrySpawn());
            CollectionAssert.AreEqual(entityIds, pool.Customers.Select(customer => customer.GetEntityId()).ToArray());
            Assert.AreEqual(12, pool.Capacity);
        }
    }
}
