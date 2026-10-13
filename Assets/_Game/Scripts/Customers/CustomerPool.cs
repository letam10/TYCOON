using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace TYCOON
{
    [Serializable]
    public sealed class CustomerShopRoute
    {
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private Transform entrance;
        [SerializeField] private Transform exit;
        [SerializeField] private Shelf[] shelves = Array.Empty<Shelf>();
        [SerializeField] private CheckoutStation checkout;
        public Transform SpawnPoint => spawnPoint;
        public Transform Entrance => entrance;
        public Transform Exit => exit;
        public Shelf[] Shelves => shelves;
        public CheckoutStation Checkout => checkout;
        public bool IsConfigured => spawnPoint != null && entrance != null && exit != null && checkout != null;

        public CustomerShopRoute(Transform spawn, Transform entry, Transform departure, Shelf[] stock, CheckoutStation station)
        {
            spawnPoint = spawn; entrance = entry; exit = departure; checkout = station;
            shelves = stock == null ? Array.Empty<Shelf>() : (Shelf[])stock.Clone();
        }

        public bool CanVisit()
        {
            if (!IsConfigured || !checkout.isActiveAndEnabled) return false;
            foreach (var shelf in shelves)
                if (shelf != null && shelf.isActiveAndEnabled && shelf.AvailableCount > 0) return true;
            return false;
        }
    }

    public sealed class CustomerPool : MonoBehaviour
    {
        [SerializeField] private CustomerAgent prefab;
        [SerializeField] private CustomerAgent[] variants = Array.Empty<CustomerAgent>();
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private Transform entrance;
        [SerializeField] private Transform exit;
        [SerializeField] private Shelf[] shelves = Array.Empty<Shelf>();
        [SerializeField] private CheckoutStation checkout;
        [SerializeField] private CustomerShopRoute[] shops = Array.Empty<CustomerShopRoute>();
        [SerializeField, Range(10, 15)] private int size = 12;
        [SerializeField, Min(0.1f)] private float interval = 3f;
        private readonly List<CustomerAgent> customers = new List<CustomerAgent>();
        private readonly HashSet<CustomerAgent> active = new HashSet<CustomerAgent>();
        private float nextSpawn;
        private bool initialized;
        private bool suspended;
        private int nextShop;
        public int ActiveCount => active.Count;
        public int Capacity => customers.Count;
        public bool IsSuspended => suspended;
        public IReadOnlyList<CustomerAgent> Customers => customers;

        public void Configure(CustomerAgent customerPrefab, Transform spawn, Transform shopEntrance, Transform shopExit,
            Shelf[] shopShelves, CheckoutStation station, int poolSize = 12, float spawnInterval = 3f)
        {
            if (customerPrefab == null || spawn == null || shopEntrance == null || shopExit == null || station == null ||
                poolSize < 10 || poolSize > 15 || spawnInterval <= 0f) throw new ArgumentException(
                    $"Customer pool requires setup, 10 to 15 slots and a positive interval. Prefab: {customerPrefab != null}, spawn: {spawn != null}, entrance: {shopEntrance != null}, exit: {shopExit != null}, checkout: {station != null}, size: {poolSize}, interval: {spawnInterval}.");
            if (initialized) throw new InvalidOperationException("Customer pool is fixed after initialization.");
            prefab = customerPrefab;
            spawnPoint = spawn;
            entrance = shopEntrance;
            exit = shopExit;
            shelves = shopShelves == null ? Array.Empty<Shelf>() : (Shelf[])shopShelves.Clone();
            checkout = station;
            size = poolSize;
            interval = spawnInterval;
            shops = Array.Empty<CustomerShopRoute>(); nextShop = 0;
        }

        public void ConfigureShops(params CustomerShopRoute[] routes)
        {
            if (initialized) throw new InvalidOperationException("Customer shops are fixed after initialization.");
            if (routes == null || routes.Length == 0) throw new ArgumentException("Shared customers require shop routes.");
            foreach (var route in routes)
                if (route == null || !route.IsConfigured) throw new ArgumentException("Customer shop route is incomplete.");
            shops = (CustomerShopRoute[])routes.Clone(); nextShop = 0;
        }

        private void Start() { if (prefab != null) Initialize(); }
        private void OnEnable() => ResumeSpawning();

        public void ConfigureVariants(params CustomerAgent[] models)
        {
            if (initialized) throw new InvalidOperationException("Customer models are fixed after initialization.");
            if (models == null) throw new ArgumentNullException(nameof(models));
            foreach (var model in models)
                if (model == null) throw new ArgumentException("Customer variants cannot contain a missing model.");
            variants = (CustomerAgent[])models.Clone();
        }

        public void Initialize()
        {
            if (initialized) return;
            if (prefab == null || spawnPoint == null || entrance == null || exit == null || checkout == null)
                throw new InvalidOperationException("Configure pool before initialization.");
            for (var index = 0; index < size; index++)
            {
                var model = variants.Length > 0 ? variants[index % variants.Length] : prefab;
                var customer = Instantiate(model, spawnPoint.position, spawnPoint.rotation, transform);
                customer.gameObject.SetActive(false);
                customer.Configure(shelves, checkout, entrance, exit, "customer." + index);
                customers.Add(customer);
            }
            initialized = true;
        }

        private void Update()
        {
            if (!initialized || suspended || (shops.Length == 0 && !checkout.isActiveAndEnabled)) return;
            nextSpawn -= Time.deltaTime;
            if (nextSpawn > 0f) return;
            nextSpawn = interval;
            TrySpawn();
        }

        public bool TrySpawn()
        {
            Initialize();
            if (suspended || (shops.Length == 0 && !checkout.isActiveAndEnabled)) return false;
            foreach (var customer in customers)
            {
                if (active.Contains(customer) || customer.Cart.TotalCount != 0) continue;
                Transform spawn = spawnPoint;
                if (shops.Length > 0)
                {
                    if (!TryChooseShop(out var shop)) return false;
                    // Chỉ đổi shop giữa hai lượt mua; giỏ và reservation cũ đã được trả.
                    customer.Configure(shop.Shelves, shop.Checkout, shop.Entrance, shop.Exit);
                    spawn = shop.SpawnPoint;
                }
                customer.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
                customer.gameObject.SetActive(true);
                var agent = customer.GetComponent<NavMeshAgent>();
                if (NavMesh.SamplePosition(spawn.position, out var hit, 1.5f, agent.areaMask)) agent.Warp(hit.position);
                active.Add(customer);
                customer.BeginVisit(Return);
                return true;
            }
            return false;
        }

        public bool TryRestoreCustomer(CustomerOrderSnapshot snapshot,
            IReadOnlyDictionary<string, ItemDefinition> definitions)
        {
            Initialize();
            if (snapshot == null || definitions == null || active.Count >= customers.Count) return false;
            CustomerAgent customer = null;
            foreach (var candidate in customers)
                if (candidate != null && candidate.StableId == snapshot.customerId) { customer = candidate; break; }
            if (customer == null || active.Contains(customer) || customer.Cart.TotalCount != 0) return false;

            Transform spawn = spawnPoint;
            if (shops.Length > 0)
            {
                CustomerShopRoute route = null;
                foreach (var candidate in shops)
                    if (candidate != null && candidate.Checkout != null &&
                        snapshot.order != null && candidate.Checkout.StableId == snapshot.order.checkoutId)
                    { route = candidate; break; }
                if (route == null) return false;
                customer.Configure(route.Shelves, route.Checkout, route.Entrance, route.Exit, customer.StableId);
                spawn = route.SpawnPoint;
            }
            else customer.Configure(shelves, checkout, entrance, exit, customer.StableId);

            customer.transform.SetPositionAndRotation(new Vector3(snapshot.x, snapshot.y, snapshot.z),
                Quaternion.Euler(0f, snapshot.yaw, 0f));
            customer.gameObject.SetActive(true);
            var agent = customer.GetComponent<NavMeshAgent>();
            if (NavMesh.SamplePosition(customer.transform.position, out var hit, 1.5f, agent.areaMask))
                agent.Warp(hit.position);
            if (!customer.TryRestoreOrderSnapshot(snapshot, definitions, Return))
            {
                customer.gameObject.SetActive(false);
                return false;
            }
            if (snapshot.queueIndex >= 0 &&
                !customer.CurrentOrder.Equals(null) &&
                !FindCheckout(snapshot.order.checkoutId).TryJoinRestored(customer, snapshot.queueIndex))
            {
                customer.CancelVisit();
                customer.gameObject.SetActive(false);
                return false;
            }
            active.Add(customer);
            return true;
        }

        private bool TryChooseShop(out CustomerShopRoute route)
        {
            for (int offset = 0; offset < shops.Length; offset++)
            {
                int index = (nextShop + offset) % shops.Length;
                if (!shops[index].CanVisit()) continue;
                route = shops[index]; nextShop = (index + 1) % shops.Length; return true;
            }
            route = null; return false;
        }

        private CheckoutStation FindCheckout(string id)
        {
            if (checkout != null && checkout.StableId == id) return checkout;
            foreach (var route in shops)
                if (route != null && route.Checkout != null && route.Checkout.StableId == id) return route.Checkout;
            return null;
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

        public bool PrepareForRestore()
        {
            suspended = true;
            var current = new List<CustomerAgent>(active);
            foreach (var customer in current)
            {
                customer.PrepareForRestore();
                customer.gameObject.SetActive(false);
            }
            active.Clear();
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
