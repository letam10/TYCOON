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
    public sealed class ProgressionState
    {
        public BusinessStage stage;
        public string[] unlockedIds = Array.Empty<string>();
        public string[] employeeIds = Array.Empty<string>();
        public UpgradeLevelState[] upgradeLevels = Array.Empty<UpgradeLevelState>();
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
            unlocks.Clear(); employees.Clear(); levels.Clear();
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
                if (failure == PurchaseFailure.None && Wallet.Balance < definition.CostAtLevel(GetUpgradeLevel(definition.StableId)))
                    failure = PurchaseFailure.InsufficientMoney;
            }
            return failure == PurchaseFailure.None;
        }

        public bool TryPurchase(UpgradeDefinition definition, out PurchaseFailure failure)
        {
            if (!CanPurchase(definition, out failure)) return false;
            int level = GetUpgradeLevel(definition.StableId);
            purchasing = true;
            try
            {
                if (!Wallet.TrySpend(definition.CostAtLevel(level)))
                {
                    failure = PurchaseFailure.InsufficientMoney;
                    return false;
                }
                // Chỉ ghi quyền sở hữu sau khi giao dịch trừ tiền đã thành công.
                levels[definition.StableId] = level + 1;
                if (definition.Kind == UpgradeKind.Employee) employees.Add(definition.StableId);
                else unlocks.Add(definition.StableId);
                if (definition.UnlockedStage > stage) stage = definition.UnlockedStage;
            }
            finally { purchasing = false; }
            Changed?.Invoke();
            return true;
        }

        public ProgressionState CaptureState()
        {
            var savedLevels = new List<UpgradeLevelState>();
            foreach (var pair in levels) savedLevels.Add(new UpgradeLevelState(pair.Key, pair.Value));
            savedLevels.Sort((a, b) => StringComparer.Ordinal.Compare(a.upgradeId, b.upgradeId));
            return new ProgressionState
            {
                stage = stage,
                unlockedIds = SortedIds(unlocks),
                employeeIds = SortedIds(employees),
                upgradeLevels = savedLevels.ToArray()
            };
        }

        public bool TryRestoreState(ProgressionState state)
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

            // Kiểm tra toàn bộ snapshot trước khi thay đổi phiên chơi hiện tại.
            stage = state.stage;
            unlocks.Clear(); employees.Clear(); levels.Clear();
            foreach (string id in restoredUnlocks) unlocks.Add(id);
            foreach (string id in restoredEmployees) employees.Add(id);
            foreach (var pair in restoredLevels) levels.Add(pair.Key, pair.Value);
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
