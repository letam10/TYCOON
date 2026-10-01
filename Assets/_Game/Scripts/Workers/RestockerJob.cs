using System;
using UnityEngine;

namespace TYCOON
{
    public sealed class RestockerJob : WorkerJob
    {
        [SerializeField] private InventoryStore storage;
        [SerializeField] private Shelf[] shelves = Array.Empty<Shelf>();
        private Shelf target;
        private bool collecting;

        public void Configure(InventoryStore source, Shelf[] destinations)
        {
            if (source == null || destinations == null) throw new ArgumentException("Restocker requires storage and shelves.");
            CancelWork();
            storage = source;
            shelves = (Shelf[])destinations.Clone();
        }

        public override bool TryGetDestination(out Vector3 destination)
        {
            destination = transform.position;
            if (Owner == null || storage == null) { BlockedReason = "Restocker is not configured."; return false; }
            collecting = Owner.Inventory.TotalCount == 0;
            foreach (var shelf in shelves)
            {
                if (shelf == null || !shelf.isActiveAndEnabled || shelf.Product == null || shelf.RestockCapacity == 0) continue;
                var count = collecting ? storage.Inventory.GetCount(shelf.Product) : Owner.Inventory.GetCount(shelf.Product);
                if (count == 0 || !TryReserve(shelf)) continue;
                target = shelf;
                destination = collecting ? storage.transform.position : shelf.ApproachPoint.position;
                return true;
            }
            CancelWork();
            BlockedReason = collecting ? "No stocked goods or free shelf space." : "Shelves are full; carried goods retained.";
            return false;
        }

        public override bool Perform()
        {
            if (Owner == null || storage == null || target == null) return false;
            if (collecting)
            {
                var amount = Mathf.Min(storage.Inventory.GetCount(target.Product), Owner.Inventory.RemainingCapacity, target.RestockCapacity);
                return amount > 0 && storage.Inventory.TryTransferTo(Owner.Inventory, target.Product, amount);
            }
            var quantity = Mathf.Min(Owner.Inventory.GetCount(target.Product), target.RestockCapacity);
            var result = quantity > 0 && target.TryStock(Owner.Inventory, quantity);
            CancelWork();
            return result;
        }

        public override void CancelWork() { base.CancelWork(); target = null; }
    }
}
