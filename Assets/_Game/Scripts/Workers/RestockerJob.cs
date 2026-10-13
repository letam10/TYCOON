using System;
using UnityEngine;

namespace TYCOON
{
    public sealed class RestockerJob : WorkerJob
    {
        [SerializeField] private InventoryStore storage;
        [SerializeField] private InventoryStore[] sources = Array.Empty<InventoryStore>();
        [SerializeField] private ProductionMachine[] productionConsumers = Array.Empty<ProductionMachine>();
        [SerializeField] private Shelf[] shelves = Array.Empty<Shelf>();
        private Shelf target;
        private InventoryStore collectingSource;
        private bool collecting;
        private int nextShelf;
        private WorkerAgent[] workers;

        public void Configure(InventoryStore source, Shelf[] destinations)
        {
            if (source == null || destinations == null) throw new ArgumentException("Restocker requires storage and shelves.");
            Configure(new[] { source }, destinations);
        }

        public void Configure(InventoryStore[] sourceStores, Shelf[] destinations, ProductionMachine[] consumers = null)
        {
            if (sourceStores == null || sourceStores.Length == 0 || destinations == null)
                throw new ArgumentException("Restocker requires sources and shelves.");
            foreach (var source in sourceStores)
                if (source == null) throw new ArgumentException("Restocker sources cannot contain null.");
            CancelWork();
            sources = (InventoryStore[])sourceStores.Clone(); storage = sources[0];
            shelves = (Shelf[])destinations.Clone();
            productionConsumers = consumers == null ? Array.Empty<ProductionMachine>() : (ProductionMachine[])consumers.Clone();
            nextShelf = 0;
        }

        public override bool TryGetDestination(out Vector3 destination)
        {
            destination = transform.position;
            if (Owner == null || storage == null) { BlockedReason = "Restocker is not configured."; return false; }
            collecting = Owner.Inventory.TotalCount == 0;
            for (int offset = 0; offset < shelves.Length; offset++)
            {
                int index = (nextShelf + offset) % shelves.Length;
                var shelf = shelves[index];
                if (shelf == null || !shelf.isActiveAndEnabled || shelf.Product == null || shelf.RestockCapacity == 0) continue;
                var count = collecting ? AvailableForSale(shelf.Product, shelf.Store) : Owner.Inventory.GetCount(shelf.Product);
                if (count == 0 || !TryReserve(shelf)) continue;
                if (collecting)
                {
                    collectingSource = FindSource(shelf.Product, shelf);
                    if (collectingSource == null) continue;
                }
                target = shelf;
                nextShelf = (index + 1) % shelves.Length;
                destination = collecting ? collectingSource.transform.position : shelf.ApproachPoint.position;
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
                if (collectingSource == null || !collectingSource.isActiveAndEnabled) return false;
                var amount = Mathf.Min(collectingSource.Inventory.GetCount(target.Product), Owner.Inventory.RemainingCapacity,
                    target.RestockCapacity, AvailableForSale(target.Product, target.Store));
                return amount > 0 && collectingSource.Inventory.TryTransferTo(Owner.Inventory, target.Product, amount);
            }
            var quantity = Mathf.Min(Owner.Inventory.GetCount(target.Product), target.RestockCapacity);
            var result = quantity > 0 && target.TryStock(Owner.Inventory, quantity);
            CancelWork();
            return result;
        }

        private InventoryStore FindSource(ItemDefinition item, Shelf destination)
        {
            InventoryStore destinationStore = destination == null ? null : destination.Store;
            if (destinationStore == null) return null;
            if (sources.Length == 0) return storage != null && storage.isActiveAndEnabled &&
                storage.CanSupplyTo(destinationStore) && storage.Inventory.GetCount(item) > 0 ? storage : null;
            foreach (var source in sources)
                if (source != null && source.isActiveAndEnabled && source.CanSupplyTo(destinationStore) &&
                    source.Inventory.GetCount(item) > 0) return source;
            return null;
        }

        private int AvailableForSale(ItemDefinition item, InventoryStore destination)
        {
            int total = 0;
            if (sources.Length == 0) total = storage.Inventory.GetCount(item);
            else foreach (var source in sources)
                if (source != null && source.isActiveAndEnabled && source.CanSupplyTo(destination))
                    total += source.Inventory.GetCount(item);
            bool producedItem = false;
            foreach (var producer in productionConsumers)
                if (producer != null && producer.Recipe != null && producer.Recipe.Output.Id == item.Id) producedItem = true;
            // Nguyên liệu thô chờ một mẻ; bột chỉ giữ khi còn sữa/trứng để tránh khóa hàng retail.
            foreach (var consumer in productionConsumers)
            {
                if (consumer == null || !consumer.isActiveAndEnabled || consumer.Recipe == null ||
                    (producedItem && !HasCompanionInputs(consumer, item))) continue;
                foreach (var ingredient in consumer.Recipe.Ingredients)
                    if (ingredient.item.Id == item.Id)
                        total -= Mathf.Max(0, ingredient.quantity - consumer.Input.Inventory.GetCount(item));
            }
            return Mathf.Max(0, total);
        }

        private bool HasCompanionInputs(ProductionMachine consumer, ItemDefinition reservedItem)
        {
            foreach (var ingredient in consumer.Recipe.Ingredients)
            {
                if (ingredient.item.Id == reservedItem.Id) continue;
                int available = consumer.Input.Inventory.GetCount(ingredient.item);
                foreach (var source in sources)
                    if (source != null && source.isActiveAndEnabled && source.CanSupplyTo(consumer.Input))
                        available += source.Inventory.GetCount(ingredient.item);
                // Tính cả hàng processor đang mang để retail không lấy mất bột trong lúc giao sữa.
                if (workers == null) workers = FindObjectsByType<WorkerAgent>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var worker in workers)
                    if (worker != null && worker.isActiveAndEnabled && worker.gameObject.scene == gameObject.scene &&
                        worker.Job is ProcessorJob) available += worker.Inventory.GetCount(ingredient.item);
                if (available < ingredient.quantity) return false;
            }
            return true;
        }

        public override void CancelWork() { base.CancelWork(); target = null; collectingSource = null; }
    }
}
