using System;
using UnityEngine;

namespace TYCOON
{
    public enum ItemCategory { Crop, AnimalProduct, Ingredient, ProcessedProduct, PreparedFood }

    [CreateAssetMenu(menuName = "TYCOON/Item")]
    public sealed class ItemDefinition : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private Sprite icon;
        [SerializeField] private GameObject prefab;
        [SerializeField, Min(0)] private int buyPrice;
        [SerializeField, Min(0)] private int sellPrice;
        [SerializeField, Min(1)] private int stackSize = 20;
        [SerializeField] private ItemCategory category;

        public string Id => id;
        public string DisplayName => displayName;
        public Sprite Icon => icon;
        public GameObject Prefab => prefab;
        public int BuyPrice => buyPrice;
        public int SellPrice => sellPrice;
        public int StackSize => stackSize;
        public ItemCategory Category => category;

        public void Configure(string id, string displayName, int sellPrice, int stackSize = 20,
            ItemCategory category = ItemCategory.Crop, GameObject prefab = null, Sprite icon = null, int buyPrice = 0)
        {
            if (!StableId.IsValid(id)) throw new ArgumentException("Item requires a stable ID.", nameof(id));
            if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Item requires a display name.", nameof(displayName));
            if (sellPrice < 0) throw new ArgumentOutOfRangeException(nameof(sellPrice));
            if (buyPrice < 0) throw new ArgumentOutOfRangeException(nameof(buyPrice));
            if (stackSize < 1) throw new ArgumentOutOfRangeException(nameof(stackSize));
            if (!Enum.IsDefined(typeof(ItemCategory), category)) throw new ArgumentOutOfRangeException(nameof(category));
            this.id = id;
            this.displayName = displayName;
            this.sellPrice = sellPrice;
            this.stackSize = stackSize;
            this.category = category;
            this.prefab = prefab;
            this.icon = icon;
            this.buyPrice = buyPrice;
        }
    }

    internal static class StableId
    {
        internal static bool IsValid(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;
            foreach (char character in id)
                if (char.IsWhiteSpace(character) || char.IsControl(character)) return false;
            return true;
        }
    }
}
