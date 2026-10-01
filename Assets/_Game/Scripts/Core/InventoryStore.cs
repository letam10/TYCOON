using UnityEngine;

namespace TYCOON
{
    [DisallowMultipleComponent]
    public sealed class InventoryStore : MonoBehaviour
    {
        [SerializeField] private string stableId;
        [SerializeField, Min(1)] private int capacity = 20;
        [SerializeField] private ItemDefinition startingItem;
        [SerializeField, Min(0)] private int startingQuantity;
        private ItemInventory inventory;

        public string StableId => stableId;
        public ItemInventory Inventory
        {
            get
            {
                if (inventory == null)
                {
                    inventory = new ItemInventory(capacity);
                    if (startingItem != null && startingQuantity > 0)
                        inventory.TryAdd(startingItem, Mathf.Min(startingQuantity, capacity));
                }
                return inventory;
            }
        }

        public void Configure(string id, int size, ItemDefinition initialItem = null, int initialQuantity = 0)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new System.ArgumentException("Inventory requires a stable ID.");
            if (size < 1) throw new System.ArgumentOutOfRangeException(nameof(size));
            stableId = id;
            capacity = size;
            startingItem = initialItem;
            startingQuantity = Mathf.Clamp(initialQuantity, 0, size);
            inventory = null;
        }
    }
}
