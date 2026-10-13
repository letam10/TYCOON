using System;
using UnityEngine;

namespace TYCOON
{
    public sealed class ProcessorJob : WorkerJob
    {
        [SerializeField] private ProductionMachine machine;
        [SerializeField] private ProductionMachine[] machines = Array.Empty<ProductionMachine>();
        [SerializeField] private InventoryStore storage;
        private ItemDefinition carriedProduct;
        private bool takingOutput;
        private bool operating;
        private int nextMachine;

        public void Configure(ProductionMachine productionMachine, InventoryStore sharedStorage)
        {
            if (productionMachine == null || sharedStorage == null) throw new ArgumentException("Processor requires machine and storage.");
            Configure(new[] { productionMachine }, sharedStorage);
        }

        public void Configure(ProductionMachine[] productionMachines, InventoryStore sharedStorage)
        {
            if (productionMachines == null || productionMachines.Length == 0 || sharedStorage == null)
                throw new ArgumentException("Processor requires machines and storage.");
            foreach (var candidate in productionMachines)
                if (candidate == null) throw new ArgumentException("Processor machines cannot contain null.");
            CancelWork();
            machines = (ProductionMachine[])productionMachines.Clone();
            machine = machines[0]; storage = sharedStorage; nextMachine = 0;
        }

        public override bool TryGetDestination(out Vector3 destination)
        {
            destination = transform.position;
            if (Owner == null || storage == null || (machine == null && machines.Length == 0))
            { BlockedReason = "Processor is not configured."; return false; }
            if (Owner.Inventory.TotalCount == 0)
            {
                int operationCount = machines.Length == 0 ? 1 : machines.Length;
                for (int offset = 0; offset < operationCount; offset++)
                {
                    int index = (nextMachine + offset) % operationCount;
                    var candidate = machines.Length == 0 ? machine : machines[index];
                    if (candidate == null || !candidate.isActiveAndEnabled || !candidate.CanStartOperation || !TryReserve(candidate)) continue;
                    machine = candidate;
                    operating = true;
                    nextMachine = (index + 1) % operationCount;
                    destination = candidate.transform.position;
                    return true;
                }
            }
            if (Owner.Inventory.TotalCount > 0)
            {
                foreach (var stack in Owner.Inventory.Stacks) { carriedProduct = stack.Item; break; }
                var destinationStore = !takingOutput && CanReceive(machine, carriedProduct) ? machine.Input : storage;
                if (destinationStore == storage && !takingOutput)
                    foreach (var candidate in machines)
                        if (CanReceive(candidate, carriedProduct) && TryReserve(candidate))
                        { machine = candidate; destinationStore = candidate.Input; break; }
                if (destinationStore != storage && !TryReserve(machine))
                { BlockedReason = "Machine already has a processor worker."; return false; }
                if (destinationStore.Inventory.RemainingCapacity == 0)
                { BlockedReason = "Processor destination is full; carried goods retained."; return false; }
                destination = destinationStore.transform.position;
                return true;
            }
            int count = machines.Length == 0 ? 1 : machines.Length;
            // Mỗi lượt chuyển xong sẽ đổi máy, tránh mill chiếm toàn bộ thời gian công nhân.
            for (int offset = 0; offset < count; offset++)
            {
                int index = (nextMachine + offset) % count;
                var candidate = machines.Length == 0 ? machine : machines[index];
                if (candidate == null || !candidate.isActiveAndEnabled || candidate.Recipe == null || !TryReserve(candidate)) continue;
                takingOutput = candidate.Output.Inventory.GetCount(candidate.Recipe.Output) > 0 && storage.Inventory.RemainingCapacity > 0;
                if (takingOutput)
                {
                    machine = candidate; nextMachine = (index + 1) % count; carriedProduct = candidate.Recipe.Output;
                    destination = candidate.Output.transform.position; return true;
                }
                foreach (var ingredient in candidate.Recipe.Ingredients)
                {
                    if (!CanReceive(candidate, ingredient.item) || AvailableInput(candidate, ingredient.item) == 0) continue;
                    machine = candidate; nextMachine = (index + 1) % count; carriedProduct = ingredient.item;
                    destination = storage.transform.position; return true;
                }
            }
            ReleaseReservation();
            BlockedReason = "Waiting for input goods or machine output.";
            return false;
        }

        public override bool Perform()
        {
            if (Owner == null || machine == null || storage == null) return false;
            if (operating)
            {
                bool result = machine.BeginOperation(Owner);
                if (result) machine.Tick(0.5f);
                machine.EndOperation(Owner);
                CancelWork();
                return result;
            }
            if (carriedProduct == null) return false;
            if (Owner.Inventory.TotalCount > 0)
            {
                var destination = !takingOutput && CanReceive(machine, carriedProduct) ? machine.Input : storage;
                var amount = Mathf.Min(Owner.Inventory.GetCount(carriedProduct), destination.Inventory.RemainingCapacity);
                if (destination == machine.Input)
                    foreach (var ingredient in machine.Recipe.Ingredients)
                        if (ingredient.item.Id == carriedProduct.Id)
                            amount = Mathf.Min(amount, Mathf.Max(0, ingredient.quantity - machine.Input.Inventory.GetCount(carriedProduct)));
                var delivered = amount > 0 && Owner.Inventory.TryTransferTo(destination.Inventory, carriedProduct, amount);
                CancelWork();
                return delivered;
            }
            var source = takingOutput ? machine.Output : storage;
            var quantity = Mathf.Min(source.Inventory.GetCount(carriedProduct), Owner.Inventory.RemainingCapacity);
            if (!takingOutput)
            {
                quantity = Mathf.Min(quantity, AvailableInput(machine, carriedProduct));
                foreach (var ingredient in machine.Recipe.Ingredients)
                    if (ingredient.item.Id == carriedProduct.Id)
                        quantity = Mathf.Min(quantity, Mathf.Max(0, ingredient.quantity - machine.Input.Inventory.GetCount(carriedProduct)), machine.Input.Inventory.RemainingCapacity);
            }
            return quantity > 0 && source.Inventory.TryTransferTo(Owner.Inventory, carriedProduct, quantity);
        }

        private int AvailableInput(ProductionMachine target, ItemDefinition product)
        {
            int quantity = storage.Inventory.GetCount(product);
            if (UsesProducedIngredient(target)) return quantity;
            // Dairy chỉ nhận phần dư: chừa một mẻ milk cho bread và pastry khi Bakery đã mở.
            foreach (var consumer in machines)
            {
                if (consumer == null || consumer == target || !consumer.isActiveAndEnabled ||
                    consumer.Recipe == null || !UsesProducedIngredient(consumer)) continue;
                foreach (var ingredient in consumer.Recipe.Ingredients)
                    if (ingredient.item.Id == product.Id) quantity -= ingredient.quantity;
            }
            return Mathf.Max(0, quantity);
        }

        private bool UsesProducedIngredient(ProductionMachine target)
        {
            if (target == null || target.Recipe == null) return false;
            foreach (var ingredient in target.Recipe.Ingredients)
            {
                if (ingredient.item.Category == ItemCategory.Ingredient ||
                    ingredient.item.Category == ItemCategory.ProcessedProduct ||
                    ingredient.item.Category == ItemCategory.PreparedFood) return true;
                foreach (var producer in machines)
                    if (producer != null && producer != target && producer.Recipe != null &&
                        producer.Recipe.Output.Id == ingredient.item.Id) return true;
            }
            return false;
        }

        private bool CanReceive(ProductionMachine target, ItemDefinition product)
        {
            if (target == null || !target.isActiveAndEnabled || target.Recipe == null || target.Input.Inventory.RemainingCapacity == 0) return false;
            // Không nhét hết bột vào mẻ bánh thiếu sữa/trứng, làm mất hàng bán ở Supermarket.
            if (UsesProducedIngredient(target))
                foreach (var ingredient in target.Recipe.Ingredients)
                {
                    if (ingredient.item.Id == product.Id) continue;
                    int available = target.Input.Inventory.GetCount(ingredient.item) +
                        storage.Inventory.GetCount(ingredient.item) + (Owner == null ? 0 : Owner.Inventory.GetCount(ingredient.item));
                    foreach (var source in machines)
                        if (source != null && source.isActiveAndEnabled) available += source.Output.Inventory.GetCount(ingredient.item);
                    if (available < ingredient.quantity) return false;
                }
            foreach (var ingredient in target.Recipe.Ingredients)
                if (ingredient.item.Id == product.Id && target.Input.Inventory.GetCount(product) < ingredient.quantity) return true;
            return false;
        }

        public override void CancelWork()
        {
            if (operating && machine != null) machine.EndOperation(Owner);
            operating = false;
            base.CancelWork();
            carriedProduct = null;
            takingOutput = false;
        }
    }
}
