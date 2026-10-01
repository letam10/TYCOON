using System;
using System.Linq;
using UnityEngine;

namespace TYCOON
{
    public sealed class FarmerJob : WorkerJob
    {
        [SerializeField] private HarvestNode[] nodes = Array.Empty<HarvestNode>();
        [SerializeField] private InventoryStore storage;
        private HarvestNode target;

        public void Configure(HarvestNode[] harvestNodes, InventoryStore destination)
        {
            if (harvestNodes == null || destination == null) throw new ArgumentException("Farmer requires nodes and storage.");
            CancelWork();
            nodes = (HarvestNode[])harvestNodes.Clone();
            storage = destination;
        }

        public override bool TryGetDestination(out Vector3 destination)
        {
            destination = transform.position;
            if (Owner == null || storage == null) { BlockedReason = "Farmer is not configured."; return false; }
            if (Owner.Inventory.TotalCount > 0)
            {
                ReleaseReservation();
                if (storage.Inventory.RemainingCapacity == 0) { BlockedReason = "Storage is full."; return false; }
                destination = storage.transform.position;
                return true;
            }
            foreach (var node in nodes)
            {
                if (node == null || !node.isActiveAndEnabled || !node.IsReady || !TryReserve(node)) continue;
                target = node;
                destination = node.transform.position;
                return true;
            }
            BlockedReason = "No unreserved harvest is ready.";
            return false;
        }

        public override bool Perform()
        {
            if (Owner == null || storage == null) return false;
            if (Owner.Inventory.TotalCount > 0)
            {
                var transferred = false;
                foreach (var stack in Owner.Inventory.Stacks.ToArray())
                {
                    var quantity = Mathf.Min(stack.Quantity, storage.Inventory.RemainingCapacity);
                    if (quantity > 0) transferred |= Owner.Inventory.TryTransferTo(storage.Inventory, stack.Item, quantity);
                }
                if (!transferred) BlockedReason = "Storage is full.";
                return transferred;
            }
            if (target == null) return false;
            var amount = Mathf.Min(target.AvailableCount, Owner.Inventory.RemainingCapacity);
            var harvested = amount > 0 && target.TryHarvest(Owner.Inventory, amount);
            CancelWork();
            if (!harvested) BlockedReason = "Harvest was taken before arrival.";
            return harvested;
        }

        public override void CancelWork() { base.CancelWork(); target = null; }
    }
}
