using UnityEngine;

namespace TYCOON
{
    public sealed class StorageInteractionTarget : InteractionTarget
    {
        [SerializeField] private InventoryStore store;
        [SerializeField] private ItemDefinition preferredItem;
        public InventoryStore Store => store;
        public override string Prompt => "Use storage";
        public override string GetPrompt(PlayerInteractor actor) => actor != null && actor.Carry.Inventory.TotalCount > 0 ? "Deposit item" : "Take item from storage";
        public void Configure(InventoryStore storage, ItemDefinition desiredItem = null, float radius = 1.8f)
        { store = storage; preferredItem = desiredItem; ConfigureRadius(radius); }

        public override bool TryInteract(PlayerInteractor actor)
        {
            if (actor == null || store == null || actor.Carry == store) return false;
            ItemInventory carried = actor.Carry.Inventory;
            if (carried.TotalCount > 0)
            {
                foreach (var stack in carried.Stacks)
                    return carried.TryTransferTo(store.Inventory, stack.Item, 1);
                return false;
            }
            if (preferredItem != null) return store.Inventory.TryTransferTo(carried, preferredItem, 1);
            foreach (var stack in store.Inventory.Stacks)
                return store.Inventory.TryTransferTo(carried, stack.Item, 1);
            return false;
        }
    }
}
