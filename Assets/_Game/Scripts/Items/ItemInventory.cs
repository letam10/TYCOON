using System;
using System.Collections.Generic;

namespace TYCOON
{
    public readonly struct ItemStack
    {
        public ItemDefinition Item { get; }
        public int Quantity { get; }
        internal ItemStack(ItemDefinition item, int quantity) { Item = item; Quantity = quantity; }
    }

    public sealed class ItemInventory
    {
        private sealed class Entry
        {
            internal ItemDefinition item;
            internal int quantity;
            internal Entry(ItemDefinition item, int quantity) { this.item = item; this.quantity = quantity; }
        }

        private readonly Dictionary<string, Entry> items = new Dictionary<string, Entry>(StringComparer.Ordinal);
        public int Capacity { get; private set; }
        public int TotalCount { get; private set; }
        public int RemainingCapacity => Capacity - TotalCount;
        public event Action Changed;

        // Tách stack khi đọc, tránh tạo hàng loạt đối tượng chỉ để lưu số lượng trong kho.
        public IEnumerable<ItemStack> Stacks
        {
            get
            {
                foreach (var entry in items.Values)
                {
                    int remaining = entry.quantity;
                    while (remaining > 0)
                    {
                        int quantity = Math.Min(remaining, entry.item.StackSize);
                        yield return new ItemStack(entry.item, quantity);
                        remaining -= quantity;
                    }
                }
            }
        }

        public ItemInventory(int capacity)
        {
            if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            Capacity = capacity;
        }

        public int GetCount(ItemDefinition item) => item == null ? 0 : GetCount(item.Id);
        public int GetCount(string itemId) => itemId != null && items.TryGetValue(itemId, out var entry) ? entry.quantity : 0;

        public bool TryAdd(ItemDefinition item, int quantity)
        {
            if (!IsValid(item) || quantity < 1 || quantity > RemainingCapacity) return false;
            AddUnchecked(item, quantity);
            Changed?.Invoke();
            return true;
        }

        public bool TryRemove(ItemDefinition item, int quantity)
        {
            if (!IsValid(item) || quantity < 1 || GetCount(item.Id) < quantity) return false;
            RemoveUnchecked(item.Id, quantity);
            Changed?.Invoke();
            return true;
        }

        public bool TryTransferTo(ItemInventory destination, ItemDefinition item, int quantity)
        {
            if (destination == null || ReferenceEquals(this, destination) || !IsValid(item) || quantity < 1 ||
                GetCount(item.Id) < quantity || destination.RemainingCapacity < quantity) return false;
            // Cập nhật cả hai kho trước khi phát sự kiện, người nghe luôn thấy trạng thái đã chuyển xong.
            ItemDefinition storedItem = items[item.Id].item;
            RemoveUnchecked(item.Id, quantity);
            destination.AddUnchecked(storedItem, quantity);
            Changed?.Invoke();
            destination.Changed?.Invoke();
            return true;
        }

        public bool TryConsume(IReadOnlyList<RecipeIngredient> ingredients)
        {
            if (ingredients == null || ingredients.Count == 0) return false;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var ingredient in ingredients)
                if (!IsValid(ingredient.item) || ingredient.quantity < 1 || !seen.Add(ingredient.item.Id) ||
                    GetCount(ingredient.item.Id) < ingredient.quantity) return false;
            foreach (var ingredient in ingredients) RemoveUnchecked(ingredient.item.Id, ingredient.quantity);
            Changed?.Invoke();
            return true;
        }

        public bool TrySetCapacity(int capacity)
        {
            if (capacity < TotalCount) return false;
            if (capacity == Capacity) return true;
            Capacity = capacity;
            Changed?.Invoke();
            return true;
        }

        public InventorySnapshot CreateSnapshot(string inventoryId)
        {
            if (!StableId.IsValid(inventoryId)) throw new ArgumentException("Inventory requires a stable ID.", nameof(inventoryId));
            var savedItems = new ItemQuantitySnapshot[items.Count];
            int index = 0;
            foreach (var pair in items)
                savedItems[index++] = new ItemQuantitySnapshot { itemId = pair.Key, quantity = pair.Value.quantity };
            Array.Sort(savedItems, (left, right) => string.CompareOrdinal(left.itemId, right.itemId));
            return new InventorySnapshot { inventoryId = inventoryId, capacity = Capacity, items = savedItems };
        }

        public bool TryLoadSnapshot(InventorySnapshot snapshot, IReadOnlyDictionary<string, ItemDefinition> definitions)
        {
            if (snapshot == null || definitions == null || !JsonSaveStore.TryValidateInventory(snapshot, out _)) return false;
            var replacement = new Dictionary<string, Entry>(StringComparer.Ordinal);
            int count = 0;
            foreach (var savedItem in snapshot.items)
            {
                if (!definitions.TryGetValue(savedItem.itemId, out var definition) || !IsValid(definition) ||
                    !string.Equals(definition.Id, savedItem.itemId, StringComparison.Ordinal)) return false;
                replacement.Add(savedItem.itemId, new Entry(definition, savedItem.quantity));
                count += savedItem.quantity;
            }
            items.Clear();
            foreach (var pair in replacement) items.Add(pair.Key, pair.Value);
            Capacity = snapshot.capacity;
            TotalCount = count;
            Changed?.Invoke();
            return true;
        }

        private static bool IsValid(ItemDefinition item) => item != null && StableId.IsValid(item.Id) && item.StackSize > 0;

        private void AddUnchecked(ItemDefinition item, int quantity)
        {
            if (items.TryGetValue(item.Id, out var entry)) entry.quantity += quantity;
            else items.Add(item.Id, new Entry(item, quantity));
            TotalCount += quantity;
        }

        private void RemoveUnchecked(string itemId, int quantity)
        {
            var entry = items[itemId];
            entry.quantity -= quantity;
            if (entry.quantity == 0) items.Remove(itemId);
            TotalCount -= quantity;
        }
    }
}
