using System;
using System.Collections.Generic;

namespace TYCOON
{
    [Serializable]
    public sealed class CheckoutReceiptSnapshot
    {
        public string transactionId;
        public long amount;
    }

    [Serializable]
    public sealed class CheckoutLedgerSnapshot
    {
        public string[] completedSaleIds = Array.Empty<string>();
        public CheckoutReceiptSnapshot[] pendingReceipts = Array.Empty<CheckoutReceiptSnapshot>();
    }

    [Serializable]
    public sealed class CheckoutSnapshot
    {
        public string checkoutId;
        public CheckoutLedgerSnapshot ledger = new CheckoutLedgerSnapshot();
    }

    public sealed class CheckoutLedger
    {
        private readonly HashSet<string> completedSales = new HashSet<string>();
        private readonly Dictionary<string, long> drawer = new Dictionary<string, long>();
        public long PendingRevenue { get; private set; }
        public int CompletedSaleCount => completedSales.Count;

        public bool TryCompleteSale(string transactionId, ItemInventory cart, out long amount)
        {
            amount = 0;
            if (!StableId.IsValid(transactionId) || cart == null || cart.TotalCount == 0 ||
                completedSales.Contains(transactionId)) return false;
            var goods = new List<ItemStack>(cart.Stacks);
            var ingredients = new Dictionary<string, RecipeIngredient>(StringComparer.Ordinal);
            long total = 0;
            try
            {
                foreach (var stack in goods)
                {
                    if (stack.Item == null || stack.Quantity < 1 || stack.Item.SellPrice < 1) return false;
                    total = checked(total + checked((long)stack.Item.SellPrice * stack.Quantity));
                    if (ingredients.TryGetValue(stack.Item.Id, out var ingredient))
                    {
                        ingredient.quantity += stack.Quantity;
                        ingredients[stack.Item.Id] = ingredient;
                    }
                    else ingredients.Add(stack.Item.Id, new RecipeIngredient(stack.Item, stack.Quantity));
                }
                checked { _ = PendingRevenue + total; }
            }
            catch (OverflowException) { amount = 0; return false; }

            // Tiền chỉ được ghi sau khi hàng thật trong giỏ đã được bán.
            if (!cart.TryConsume(new List<RecipeIngredient>(ingredients.Values))) return false;
            amount = total;
            completedSales.Add(transactionId);
            drawer.Add(transactionId, amount);
            PendingRevenue += amount;
            return true;
        }

        public long CollectMoney(Wallet wallet)
        {
            if (wallet == null || drawer.Count == 0) return 0;
            var receipts = new List<KeyValuePair<string, long>>(drawer);
            long collected = 0;
            foreach (var receipt in receipts)
            {
                if (wallet.HasCompletedSale(receipt.Key))
                {
                    drawer.Remove(receipt.Key);
                    PendingRevenue -= receipt.Value;
                    continue;
                }
                if (receipt.Value > int.MaxValue || !wallet.TryCreditCompletedSale(receipt.Key, (int)receipt.Value)) continue;
                drawer.Remove(receipt.Key);
                PendingRevenue -= receipt.Value;
                collected += receipt.Value;
            }
            return collected;
        }

        public CheckoutLedgerSnapshot CreateSnapshot()
        {
            var completed = new string[completedSales.Count];
            completedSales.CopyTo(completed);
            Array.Sort(completed, StringComparer.Ordinal);
            var receipts = new CheckoutReceiptSnapshot[drawer.Count];
            var index = 0;
            foreach (var receipt in drawer)
                receipts[index++] = new CheckoutReceiptSnapshot { transactionId = receipt.Key, amount = receipt.Value };
            Array.Sort(receipts, (left, right) => string.CompareOrdinal(left.transactionId, right.transactionId));
            return new CheckoutLedgerSnapshot { completedSaleIds = completed, pendingReceipts = receipts };
        }

        public static bool TryValidateSnapshot(CheckoutLedgerSnapshot snapshot)
        {
            if (snapshot == null || snapshot.completedSaleIds == null || snapshot.pendingReceipts == null) return false;
            var completed = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in snapshot.completedSaleIds)
                if (!StableId.IsValid(id) || !completed.Add(id)) return false;
            var pending = new HashSet<string>(StringComparer.Ordinal);
            long total = 0;
            foreach (var receipt in snapshot.pendingReceipts)
            {
                if (receipt == null || !completed.Contains(receipt.transactionId) || receipt.amount < 1 ||
                    !pending.Add(receipt.transactionId)) return false;
                try { total = checked(total + receipt.amount); }
                catch (OverflowException) { return false; }
            }
            return true;
        }

        public bool TryLoadSnapshot(CheckoutLedgerSnapshot snapshot)
        {
            if (!TryValidateSnapshot(snapshot)) return false;
            completedSales.Clear();
            drawer.Clear();
            PendingRevenue = 0;
            foreach (var id in snapshot.completedSaleIds) completedSales.Add(id);
            foreach (var receipt in snapshot.pendingReceipts)
            {
                drawer.Add(receipt.transactionId, receipt.amount);
                PendingRevenue += receipt.amount;
            }
            return true;
        }
    }
}
