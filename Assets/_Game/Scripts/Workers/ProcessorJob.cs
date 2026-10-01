using System;
using UnityEngine;

namespace TYCOON
{
    public sealed class ProcessorJob : WorkerJob
    {
        [SerializeField] private ProductionMachine machine;
        [SerializeField] private InventoryStore storage;
        private ItemDefinition carriedProduct;
        private bool takingOutput;

        public void Configure(ProductionMachine productionMachine, InventoryStore sharedStorage)
        {
            if (productionMachine == null || sharedStorage == null) throw new ArgumentException("Processor requires machine and storage.");
            CancelWork();
            machine = productionMachine;
            storage = sharedStorage;
        }

        public override bool TryGetDestination(out Vector3 destination)
        {
            destination = transform.position;
            if (Owner == null || machine == null || storage == null || machine.Recipe == null)
            { BlockedReason = "Processor is not configured."; return false; }
            if (!TryReserve(machine)) { BlockedReason = "Machine already has a processor worker."; return false; }
            if (Owner.Inventory.TotalCount > 0)
            {
                foreach (var stack in Owner.Inventory.Stacks) { carriedProduct = stack.Item; break; }
                var destinationStore = carriedProduct.Id == machine.Recipe.Output.Id ? storage : machine.Input;
                if (destinationStore.Inventory.RemainingCapacity == 0)
                { BlockedReason = "Processor destination is full; carried goods retained."; return false; }
                destination = destinationStore.transform.position;
                return true;
            }
            takingOutput = machine.Output.Inventory.GetCount(machine.Recipe.Output) > 0;
            if (takingOutput)
            {
                if (storage.Inventory.RemainingCapacity == 0) { BlockedReason = "Storage is full."; ReleaseReservation(); return false; }
                carriedProduct = machine.Recipe.Output;
                destination = machine.Output.transform.position;
                return true;
            }
            foreach (var ingredient in machine.Recipe.Ingredients)
            {
                if (machine.Input.Inventory.GetCount(ingredient.item) >= ingredient.quantity ||
                    storage.Inventory.GetCount(ingredient.item) == 0 || machine.Input.Inventory.RemainingCapacity == 0) continue;
                carriedProduct = ingredient.item;
                destination = storage.transform.position;
                return true;
            }
            ReleaseReservation();
            BlockedReason = "Waiting for input goods or machine output.";
            return false;
        }

        public override bool Perform()
        {
            if (Owner == null || carriedProduct == null || machine == null || storage == null) return false;
            if (Owner.Inventory.TotalCount > 0)
            {
                var destination = carriedProduct.Id == machine.Recipe.Output.Id ? storage : machine.Input;
                var amount = Mathf.Min(Owner.Inventory.GetCount(carriedProduct), destination.Inventory.RemainingCapacity);
                var delivered = amount > 0 && Owner.Inventory.TryTransferTo(destination.Inventory, carriedProduct, amount);
                CancelWork();
                return delivered;
            }
            var source = takingOutput ? machine.Output : storage;
            var quantity = Mathf.Min(source.Inventory.GetCount(carriedProduct), Owner.Inventory.RemainingCapacity);
            if (!takingOutput)
            {
                foreach (var ingredient in machine.Recipe.Ingredients)
                    if (ingredient.item.Id == carriedProduct.Id)
                        quantity = Mathf.Min(quantity, Mathf.Max(0, ingredient.quantity - machine.Input.Inventory.GetCount(carriedProduct)), machine.Input.Inventory.RemainingCapacity);
            }
            return quantity > 0 && source.Inventory.TryTransferTo(Owner.Inventory, carriedProduct, quantity);
        }

        public override void CancelWork() { base.CancelWork(); carriedProduct = null; }
    }
}
