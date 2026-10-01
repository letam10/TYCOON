using System;
using System.Collections.Generic;
using UnityEngine;

namespace TYCOON
{
    public sealed class CheckoutStation : MonoBehaviour
    {
        [SerializeField] private string stableId = "checkout.main";
        [SerializeField] private Transform servicePoint;
        [SerializeField] private Transform[] queuePoints = Array.Empty<Transform>();
        [SerializeField, Min(1)] private int capacity = 15;
        private readonly List<CustomerAgent> queue = new List<CustomerAgent>();
        private readonly CheckoutLedger ledger = new CheckoutLedger();
        private WorkerAgent cashier;
        public event Action Changed;
        public Transform ServicePoint => servicePoint != null ? servicePoint : transform;
        public string StableId => stableId;
        public long PendingRevenue => ledger.PendingRevenue;
        public int QueueCount => queue.Count;
        public WorkerAgent Cashier => cashier;

        public void Configure(Transform point, Transform[] positions, int queueCapacity = 15, string id = "checkout.main")
        {
            if (!TYCOON.StableId.IsValid(id)) throw new ArgumentException("Checkout requires a stable ID.");
            if (queueCapacity < 1) throw new ArgumentOutOfRangeException(nameof(queueCapacity));
            if (queue.Count != 0) throw new InvalidOperationException("Cannot change an occupied checkout queue.");
            servicePoint = point;
            queuePoints = positions == null ? Array.Empty<Transform>() : (Transform[])positions.Clone();
            capacity = queueCapacity;
            stableId = id;
        }

        public bool TryJoin(CustomerAgent customer)
        {
            if (customer == null || customer.Cart.TotalCount == 0) return false;
            if (queue.Contains(customer)) return true;
            if (queue.Count >= capacity) return false;
            queue.Add(customer);
            Changed?.Invoke();
            return true;
        }

        public void Leave(CustomerAgent customer)
        {
            if (queue.Remove(customer)) Changed?.Invoke();
        }

        public int QueueIndex(CustomerAgent customer) => queue.IndexOf(customer);

        public Vector3 GetQueuePosition(CustomerAgent customer)
        {
            var index = QueueIndex(customer);
            if (index < 0) return transform.position;
            if (index < queuePoints.Length && queuePoints[index] != null) return queuePoints[index].position;
            return transform.position - transform.forward * (0.9f + index * 0.8f);
        }

        public bool TryServeNext()
        {
            while (queue.Count > 0 && (queue[0] == null || !queue[0].isActiveAndEnabled)) queue.RemoveAt(0);
            if (queue.Count == 0 || !queue[0].IsReadyForCheckout) return false;
            var customer = queue[0];
            if (!ledger.TryCompleteSale(customer.VisitId, customer.Cart, out _)) return false;
            queue.RemoveAt(0);
            customer.CompletePayment();
            Changed?.Invoke();
            return true;
        }

        public bool TryAssignCashier(WorkerAgent worker)
        {
            if (worker == null || (cashier != null && cashier != worker && cashier.isActiveAndEnabled)) return false;
            cashier = worker;
            return true;
        }

        public void ReleaseCashier(WorkerAgent worker)
        {
            if (cashier == worker) cashier = null;
        }

        public long CollectMoney(Wallet wallet)
        {
            var amount = ledger.CollectMoney(wallet);
            if (amount > 0) Changed?.Invoke();
            return amount;
        }

        public CheckoutSnapshot CaptureState()
        {
            if (queue.Count != 0) throw new InvalidOperationException("Suspend customers before saving checkout.");
            return new CheckoutSnapshot { checkoutId = stableId, ledger = ledger.CreateSnapshot() };
        }

        public bool TryRestoreState(CheckoutSnapshot snapshot)
        {
            if (snapshot == null || queue.Count != 0 || snapshot.checkoutId != stableId || !ledger.TryLoadSnapshot(snapshot.ledger)) return false;
            Changed?.Invoke();
            return true;
        }

        private void OnDisable()
        {
            var waiting = queue.ToArray();
            queue.Clear();
            cashier = null;
            foreach (var customer in waiting) if (customer != null) customer.CancelVisit();
        }
    }
}
