using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace TYCOON
{
    public sealed class CustomerPool : MonoBehaviour
    {
        [SerializeField] private CustomerAgent prefab;
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private Transform entrance;
        [SerializeField] private Transform exit;
        [SerializeField] private Shelf[] shelves = Array.Empty<Shelf>();
        [SerializeField] private CheckoutStation checkout;
        [SerializeField, Range(10, 15)] private int size = 12;
        [SerializeField, Min(0.1f)] private float interval = 3f;
        private readonly List<CustomerAgent> customers = new List<CustomerAgent>();
        private readonly HashSet<CustomerAgent> active = new HashSet<CustomerAgent>();
        private float nextSpawn;
        private bool initialized;
        private bool suspended;
        public int ActiveCount => active.Count;
        public int Capacity => customers.Count;
        public bool IsSuspended => suspended;
        public IReadOnlyList<CustomerAgent> Customers => customers;

        public void Configure(CustomerAgent customerPrefab, Transform spawn, Transform shopEntrance, Transform shopExit,
            Shelf[] shopShelves, CheckoutStation station, int poolSize = 12, float spawnInterval = 3f)
        {
            if (customerPrefab == null || spawn == null || shopEntrance == null || shopExit == null || station == null ||
                poolSize < 10 || poolSize > 15 || spawnInterval <= 0f) throw new ArgumentException("Customer pool requires setup, 10 to 15 slots and a positive interval.");
            if (initialized) throw new InvalidOperationException("Customer pool is fixed after initialization.");
            prefab = customerPrefab;
            spawnPoint = spawn;
            entrance = shopEntrance;
            exit = shopExit;
            shelves = shopShelves == null ? Array.Empty<Shelf>() : (Shelf[])shopShelves.Clone();
            checkout = station;
            size = poolSize;
            interval = spawnInterval;
        }

        private void Start() { if (prefab != null) Initialize(); }
        private void OnEnable() => ResumeSpawning();

        public void Initialize()
        {
            if (initialized) return;
            if (prefab == null || spawnPoint == null || entrance == null || exit == null || checkout == null)
                throw new InvalidOperationException("Configure pool before initialization.");
            for (var index = 0; index < size; index++)
            {
                var customer = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation, transform);
                customer.gameObject.SetActive(false);
                customer.Configure(shelves, checkout, entrance, exit);
                customers.Add(customer);
            }
            initialized = true;
        }

        private void Update()
        {
            if (!initialized || suspended || !checkout.isActiveAndEnabled) return;
            nextSpawn -= Time.deltaTime;
            if (nextSpawn > 0f) return;
            nextSpawn = interval;
            TrySpawn();
        }

        public bool TrySpawn()
        {
            Initialize();
            if (suspended || !checkout.isActiveAndEnabled) return false;
            foreach (var customer in customers)
            {
                if (active.Contains(customer) || customer.Cart.TotalCount != 0) continue;
                customer.transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);
                customer.gameObject.SetActive(true);
                var agent = customer.GetComponent<NavMeshAgent>();
                if (NavMesh.SamplePosition(spawnPoint.position, out var hit, 1.5f, agent.areaMask)) agent.Warp(hit.position);
                active.Add(customer);
                customer.BeginVisit(Return);
                return true;
            }
            return false;
        }

        public void Return(CustomerAgent customer)
        {
            if (customer == null || !active.Contains(customer) || !customer.CancelVisit()) return;
            active.Remove(customer);
            customer.gameObject.SetActive(false);
        }

        public bool SuspendAndReturnCarts()
        {
            // Dừng spawn và trả hàng chưa bán trước khi coordinator chụp toàn bộ trạng thái save.
            suspended = true;
            var visiting = new List<CustomerAgent>(active);
            foreach (var customer in visiting) Return(customer);
            return active.Count == 0;
        }

        public void ResumeSpawning()
        {
            suspended = false;
            nextSpawn = interval;
        }

        private void OnDisable()
        {
            SuspendAndReturnCarts();
        }
    }
}
