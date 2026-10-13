using System;
using System.Collections.Generic;
using UnityEngine;

namespace TYCOON
{
    public enum BusinessStage { Farm, FarmShop, Processing, Supermarket, Bakery }
    public enum PurchaseFailure { None, InvalidUpgrade, AlreadyPurchased, PrerequisiteMissing, StageLocked, InsufficientMoney, InvalidActor, Busy }

    [Serializable]
    public struct UpgradeLevelState
    {
        public string upgradeId;
        public int level;
        public UpgradeLevelState(string id, int value) { upgradeId = id; level = value; }
    }

    [Serializable]
    public struct PurchaseProgressState
    {
        public string upgradeId;
        public int totalCost;
        public int contributed;
        public bool completed;

        public PurchaseProgressState(string id, int total, int paid, bool done)
        {
            upgradeId = id; totalCost = total; contributed = paid; completed = done;
        }
    }

    [Serializable]
    public sealed class ProgressionState
    {
        public BusinessStage stage;
        public string[] unlockedIds = Array.Empty<string>();
        public string[] employeeIds = Array.Empty<string>();
        public UpgradeLevelState[] upgradeLevels = Array.Empty<UpgradeLevelState>();
        public PurchaseProgressState[] purchaseProgress = Array.Empty<PurchaseProgressState>();
    }

    [DisallowMultipleComponent]
    public sealed class GameSession : MonoBehaviour
    {
        [SerializeField, Min(0)] private int startingMoney;
        private Wallet wallet;
        private BusinessStage stage;
        private bool purchasing;
        private readonly HashSet<string> unlocks = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> employees = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> levels = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> purchaseProgress = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> purchaseTotals = new Dictionary<string, int>(StringComparer.Ordinal);

        public event Action Changed;
        public Wallet Wallet => wallet ?? (wallet = new Wallet(startingMoney));
        public BusinessStage Stage => stage;
        public string Objective
        {
            get
            {
                switch (stage)
                {
                    case BusinessStage.Farm: return "Harvest goods, stock the shop and serve customers to earn your first upgrade.";
                    case BusinessStage.FarmShop: return "Hire workers for the farm and shop, then open the processing area.";
                    case BusinessStage.Processing: return "Turn farm produce into higher value goods and expand to a supermarket.";
                    case BusinessStage.Supermarket: return "Stock processed goods, automate checkout and open the bakery.";
                    default: return "Supply flour, eggs and milk to the bakery, then sell the finished food.";
                }
            }
        }

        public void Configure(int initialMoney = 0)
        {
            if (initialMoney < 0) throw new ArgumentOutOfRangeException(nameof(initialMoney));
            startingMoney = initialMoney;
            wallet = new Wallet(initialMoney);
            stage = BusinessStage.Farm;
            unlocks.Clear(); employees.Clear(); levels.Clear(); purchaseProgress.Clear(); purchaseTotals.Clear();
            Changed?.Invoke();
        }

        public bool HasUnlock(string id) => id != null && unlocks.Contains(id);
        public bool HasEmployee(string id) => id != null && employees.Contains(id);
        public int GetUpgradeLevel(string id)
        {
            if (id == null) return 0;
            if (levels.TryGetValue(id, out int value)) return value;
            return HasUnlock(id) || HasEmployee(id) ? 1 : 0;
        }

        public int GetPurchaseContribution(string id) => id != null && purchaseProgress.TryGetValue(id, out int value) ? value : 0;

        public bool CanPurchase(UpgradeDefinition definition, out PurchaseFailure failure)
        {
            failure = PurchaseFailure.None;
            if (purchasing) failure = PurchaseFailure.Busy;
            else if (definition == null || !definition.IsValid) failure = PurchaseFailure.InvalidUpgrade;
            else if (GetUpgradeLevel(definition.StableId) >= definition.MaxLevel) failure = PurchaseFailure.AlreadyPurchased;
            else if (stage < definition.RequiredStage) failure = PurchaseFailure.StageLocked;
            else
            {
                foreach (string id in definition.PrerequisiteIds)
                    if (!HasUnlock(id) && !HasEmployee(id))
                    {
                        failure = PurchaseFailure.PrerequisiteMissing;
                        break;
                    }
                int level = GetUpgradeLevel(definition.StableId);
                int remaining = Mathf.Max(0, definition.CostAtLevel(level) - GetPurchaseContribution(definition.StableId));
                if (failure == PurchaseFailure.None && Wallet.Balance < remaining)
                    failure = PurchaseFailure.InsufficientMoney;
            }
            return failure == PurchaseFailure.None;
        }

        public bool TryPurchase(UpgradeDefinition definition, out PurchaseFailure failure) =>
            TryContributePurchase(definition, int.MaxValue, out failure, false);

        public bool TryContributePurchase(UpgradeDefinition definition, int requestedAmount, out PurchaseFailure failure) =>
            TryContributePurchase(definition, requestedAmount, out failure, true);

        private bool TryContributePurchase(UpgradeDefinition definition, int requestedAmount,
            out PurchaseFailure failure, bool allowPartial)
        {
            if (!CanPurchase(definition, allowPartial, out failure)) return false;
            if (requestedAmount < 1) requestedAmount = 1;
            int level = GetUpgradeLevel(definition.StableId);
            int totalCost = definition.CostAtLevel(level);
            int paid = GetPurchaseContribution(definition.StableId);
            int remaining = Mathf.Max(0, totalCost - paid);
            int debit = Mathf.Min(requestedAmount, remaining, Wallet.Balance);
            if (debit < 1)
            {
                failure = PurchaseFailure.InsufficientMoney;
                return false;
            }
            purchasing = true;
            try
            {
                if (!Wallet.TrySpend(debit))
                {
                    failure = PurchaseFailure.InsufficientMoney;
                    return false;
                }
                paid += debit;
                if (paid < totalCost)
                {
                    purchaseProgress[definition.StableId] = paid;
                    purchaseTotals[definition.StableId] = totalCost;
                    failure = PurchaseFailure.None;
                    Changed?.Invoke();
                    return true;
                }
                purchaseProgress.Remove(definition.StableId);
                purchaseTotals.Remove(definition.StableId);
                levels[definition.StableId] = level + 1;
                if (definition.Kind == UpgradeKind.Employee) employees.Add(definition.StableId);
                else unlocks.Add(definition.StableId);
                if (definition.UnlockedStage > stage) stage = definition.UnlockedStage;
            }
            finally { purchasing = false; }
            failure = PurchaseFailure.None;
            Changed?.Invoke();
            return true;
        }

        private bool CanPurchase(UpgradeDefinition definition, bool allowPartial, out PurchaseFailure failure)
        {
            failure = PurchaseFailure.None;
            if (purchasing) failure = PurchaseFailure.Busy;
            else if (definition == null || !definition.IsValid) failure = PurchaseFailure.InvalidUpgrade;
            else if (GetUpgradeLevel(definition.StableId) >= definition.MaxLevel) failure = PurchaseFailure.AlreadyPurchased;
            else if (stage < definition.RequiredStage) failure = PurchaseFailure.StageLocked;
            else
            {
                foreach (string id in definition.PrerequisiteIds)
                    if (!HasUnlock(id) && !HasEmployee(id))
                    {
                        failure = PurchaseFailure.PrerequisiteMissing;
                        break;
                    }
                int level = GetUpgradeLevel(definition.StableId);
                int remaining = Mathf.Max(0, definition.CostAtLevel(level) - GetPurchaseContribution(definition.StableId));
                int minimum = allowPartial ? (remaining > 0 ? 1 : 0) : remaining;
                if (failure == PurchaseFailure.None && Wallet.Balance < minimum)
                    failure = PurchaseFailure.InsufficientMoney;
            }
            return failure == PurchaseFailure.None;
        }

        public ProgressionState CaptureState()
        {
            var savedLevels = new List<UpgradeLevelState>();
            foreach (var pair in levels) savedLevels.Add(new UpgradeLevelState(pair.Key, pair.Value));
            savedLevels.Sort((a, b) => StringComparer.Ordinal.Compare(a.upgradeId, b.upgradeId));
            var savedPurchases = new List<PurchaseProgressState>();
            foreach (var pair in purchaseProgress)
            {
                int total = purchaseTotals.TryGetValue(pair.Key, out int storedTotal) ? storedTotal : 0;
                savedPurchases.Add(new PurchaseProgressState(pair.Key, total, pair.Value, false));
            }
            savedPurchases.Sort((a, b) => StringComparer.Ordinal.Compare(a.upgradeId, b.upgradeId));
            return new ProgressionState
            {
                stage = stage,
                unlockedIds = SortedIds(unlocks),
                employeeIds = SortedIds(employees),
                upgradeLevels = savedLevels.ToArray(),
                purchaseProgress = savedPurchases.ToArray()
            };
        }

        public static bool IsValidState(ProgressionState state)
        {
            if (state == null || !Enum.IsDefined(typeof(BusinessStage), state.stage) ||
                !TryReadIds(state.unlockedIds, out HashSet<string> restoredUnlocks) ||
                !TryReadIds(state.employeeIds, out HashSet<string> restoredEmployees) || state.upgradeLevels == null)
                return false;
            var restoredLevels = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (UpgradeLevelState value in state.upgradeLevels)
                if (!StableId.IsValid(value.upgradeId) || value.level < 1 || restoredLevels.ContainsKey(value.upgradeId) ||
                    (!restoredUnlocks.Contains(value.upgradeId) && !restoredEmployees.Contains(value.upgradeId))) return false;
                else restoredLevels.Add(value.upgradeId, value.level);
            foreach (string id in restoredEmployees)
                if (restoredUnlocks.Contains(id)) return false;
            var restoredPurchases = new HashSet<string>(StringComparer.Ordinal);
            if (state.purchaseProgress != null)
                foreach (PurchaseProgressState value in state.purchaseProgress)
                    if (!StableId.IsValid(value.upgradeId) || !restoredPurchases.Add(value.upgradeId) ||
                        value.totalCost < 0 || value.contributed < 1 || value.contributed > value.totalCost ||
                        restoredUnlocks.Contains(value.upgradeId) || restoredEmployees.Contains(value.upgradeId)) return false;

            return true;
        }

        public bool TryRestoreState(ProgressionState state)
        {
            if (!IsValidState(state)) return false;
            // Kiểm tra toàn bộ snapshot trước khi thay đổi phiên chơi hiện tại.
            stage = state.stage;
            unlocks.Clear(); employees.Clear(); levels.Clear(); purchaseProgress.Clear(); purchaseTotals.Clear();
            foreach (string id in state.unlockedIds) unlocks.Add(id);
            foreach (string id in state.employeeIds) employees.Add(id);
            foreach (var value in state.upgradeLevels) levels.Add(value.upgradeId, value.level);
            if (state.purchaseProgress != null)
                foreach (PurchaseProgressState value in state.purchaseProgress)
                {
                    purchaseProgress.Add(value.upgradeId, value.contributed);
                    purchaseTotals.Add(value.upgradeId, value.totalCost);
                }
            Changed?.Invoke();
            return true;
        }

        private static string[] SortedIds(HashSet<string> ids)
        {
            var result = new List<string>(ids);
            result.Sort(StringComparer.Ordinal);
            return result.ToArray();
        }

        private static bool TryReadIds(string[] ids, out HashSet<string> result)
        {
            result = new HashSet<string>(StringComparer.Ordinal);
            if (ids == null) return false;
            foreach (string id in ids)
                if (!StableId.IsValid(id) || !result.Add(id)) return false;
            return true;
        }
    }
}
