using System;
using System.Collections.Generic;

namespace TYCOON
{
    public sealed class Wallet
    {
        private readonly HashSet<string> completedSaleIds = new HashSet<string>(StringComparer.Ordinal);
        public int Balance { get; private set; }
        public event Action Changed;

        public bool HasCompletedSale(string transactionId) => transactionId != null && completedSaleIds.Contains(transactionId);

        public Wallet(int startingMoney = 0)
        {
            if (startingMoney < 0) throw new ArgumentOutOfRangeException(nameof(startingMoney));
            Balance = startingMoney;
        }

        public bool TrySpend(int amount)
        {
            if (amount < 0 || amount > Balance) return false;
            if (amount == 0) return true;
            Balance -= amount;
            Changed?.Invoke();
            return true;
        }

        public bool TryCreditCompletedSale(string transactionId, int amount)
        {
            if (!StableId.IsValid(transactionId) || amount < 0 || amount > int.MaxValue - Balance ||
                completedSaleIds.Contains(transactionId)) return false;
            completedSaleIds.Add(transactionId);
            Balance += amount;
            Changed?.Invoke();
            return true;
        }

        public WalletSnapshot CreateSnapshot()
        {
            var receipts = new string[completedSaleIds.Count];
            completedSaleIds.CopyTo(receipts);
            Array.Sort(receipts, StringComparer.Ordinal);
            return new WalletSnapshot { balance = Balance, completedSaleIds = receipts };
        }

        public bool TryLoadSnapshot(WalletSnapshot snapshot)
        {
            if (!JsonSaveStore.TryValidateWallet(snapshot, out _)) return false;
            completedSaleIds.Clear();
            foreach (string receipt in snapshot.completedSaleIds) completedSaleIds.Add(receipt);
            Balance = snapshot.balance;
            Changed?.Invoke();
            return true;
        }
    }
}
