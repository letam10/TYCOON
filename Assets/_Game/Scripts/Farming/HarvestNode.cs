using System;
using UnityEngine;

namespace TYCOON
{
    [RequireComponent(typeof(InventoryStore))]
    public sealed class HarvestNode : MonoBehaviour
    {
        [SerializeField] private string stableId;
        [SerializeField] private ItemDefinition product;
        [SerializeField, Min(0.1f)] private float productionSeconds = 6f;
        [SerializeField] private InventoryStore output;
        private float elapsed;
        public event Action Produced;
        public string StableId => stableId;
        public ItemDefinition Product => product;
        public InventoryStore Output => output != null ? output : output = GetComponent<InventoryStore>();
        public int AvailableCount => product == null ? 0 : Output.Inventory.GetCount(product);
        public bool IsReady => AvailableCount > 0;
        public float Progress => Mathf.Clamp01(elapsed / productionSeconds);

        public void Configure(string id, ItemDefinition item, float seconds, int capacity = 5, int initialQuantity = 1)
        {
            if (string.IsNullOrWhiteSpace(id) || item == null || seconds <= 0f)
                throw new ArgumentException("Harvest requires ID, product and positive production duration.");
            stableId = id;
            product = item;
            productionSeconds = seconds;
            Output.Configure(id + ".output", capacity, item, initialQuantity);
            elapsed = 0f;
        }

        private void Update() => Tick(Time.deltaTime);
        public void Tick(float seconds)
        {
            if (product == null || seconds <= 0f || Output.Inventory.RemainingCapacity == 0) return;
            elapsed += seconds;
            while (elapsed >= productionSeconds && Output.Inventory.RemainingCapacity > 0)
            {
                if (!Output.Inventory.TryAdd(product, 1)) break;
                elapsed -= productionSeconds;
                Produced?.Invoke();
            }
            if (Output.Inventory.RemainingCapacity == 0) elapsed = 0f;
        }

        public bool TryHarvest(ItemInventory destination, int quantity = 1)
        {
            return product != null && destination != null && Output.Inventory.TryTransferTo(destination, product, quantity);
        }

        public void RestoreProgress(float seconds) => elapsed = Mathf.Clamp(seconds, 0f, productionSeconds);
    }
}
