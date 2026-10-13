using System;
using UnityEngine;

namespace TYCOON
{
    [DisallowMultipleComponent]
    public sealed class InventoryStore : MonoBehaviour
    {
        [SerializeField] private string stableId;
        [SerializeField] private string ownerScopeId;
        [SerializeField, Min(1)] private int capacity = 20;
        [SerializeField] private ItemDefinition startingItem;
        [SerializeField, Min(0)] private int startingQuantity;
        private ItemInventory inventory;

        public string StableId => stableId;
        public string OwnerScopeId => string.IsNullOrWhiteSpace(ownerScopeId) ? InferOwnerScope() : ownerScopeId;
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

        public void Configure(string id, int size, ItemDefinition initialItem = null, int initialQuantity = 0,
            string scopeId = null)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new System.ArgumentException("Inventory requires a stable ID.");
            if (size < 1) throw new System.ArgumentOutOfRangeException(nameof(size));
            stableId = id;
            ownerScopeId = string.IsNullOrWhiteSpace(scopeId) ? null : scopeId.Trim().ToLowerInvariant();
            capacity = size;
            startingItem = initialItem;
            startingQuantity = Mathf.Clamp(initialQuantity, 0, size);
            inventory = null;
        }

        public bool CanSupplyTo(InventoryStore destination)
        {
            if (destination == null) return false;
            string source = OwnerScopeId;
            string target = destination.OwnerScopeId;
            if (source == target) return true;
            if (source == "unscoped" || target == "unscoped") return true;
            return source == "farm" && target == "farmshop" ||
                source == "farm" && (target == "supermarket" || target == "bakery") ||
                source == "processing" && (target == "supermarket" || target == "bakery") ||
                source == "supermarket" && target == "bakery";
        }

        private string InferOwnerScope()
        {
            if (string.Equals(stableId, "inventory.raw", StringComparison.OrdinalIgnoreCase)) return "farm";
            Transform current = transform;
            while (current != null)
            {
                string name = current.name.ToLowerInvariant();
                if (name.Contains("bakery")) return "bakery";
                if (name.Contains("supermarket") || name.Contains("market")) return "supermarket";
                if (name.Contains("processing")) return "processing";
                if (name.Contains("farmshop") || name.Contains("farm shop")) return "farmshop";
                if (name.Contains("farm")) return "farm";
                current = current.parent;
            }
            return "unscoped";
        }
    }
}
