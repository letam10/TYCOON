using System;
using System.Collections.Generic;
using UnityEngine;

namespace TYCOON
{
    public enum UpgradeKind { Unlock, Employee, MachineUpgrade }

    [CreateAssetMenu(menuName = "TYCOON/Upgrade")]
    public sealed class UpgradeDefinition : ScriptableObject
    {
        [SerializeField] private string stableId;
        [SerializeField] private string displayName;
        [SerializeField, Min(0)] private int baseCost = 100;
        [SerializeField] private BusinessStage requiredStage;
        [SerializeField] private BusinessStage unlockedStage;
        [SerializeField] private UpgradeKind kind;
        [SerializeField, Min(1)] private int maxLevel = 1;
        [SerializeField] private string[] prerequisiteIds = Array.Empty<string>();

        public string StableId => stableId;
        public string DisplayName => displayName;
        public int BaseCost => baseCost;
        public BusinessStage RequiredStage => requiredStage;
        public BusinessStage UnlockedStage => unlockedStage;
        public UpgradeKind Kind => kind;
        public int MaxLevel => maxLevel;
        public IReadOnlyList<string> PrerequisiteIds => prerequisiteIds;
        public bool IsValid => TYCOON.StableId.IsValid(stableId) && !string.IsNullOrWhiteSpace(displayName) && baseCost >= 0 &&
            maxLevel > 0 && (long)baseCost * maxLevel <= int.MaxValue && prerequisiteIds != null &&
            Enum.IsDefined(typeof(BusinessStage), requiredStage) && Enum.IsDefined(typeof(BusinessStage), unlockedStage) &&
            Enum.IsDefined(typeof(UpgradeKind), kind);

        public void Configure(string id, string label, int cost, BusinessStage requiredStage = BusinessStage.Farm,
            BusinessStage unlockedStage = BusinessStage.Farm, UpgradeKind kind = UpgradeKind.Unlock, int maxLevel = 1,
            string[] prerequisiteIds = null)
        {
            if (!TYCOON.StableId.IsValid(id) || string.IsNullOrWhiteSpace(label) || cost < 0 || maxLevel < 1 ||
                (long)cost * maxLevel > int.MaxValue || !Enum.IsDefined(typeof(BusinessStage), requiredStage) ||
                !Enum.IsDefined(typeof(BusinessStage), unlockedStage) || !Enum.IsDefined(typeof(UpgradeKind), kind))
                throw new ArgumentException("Upgrade requires a stable ID, label, valid cost, stage and level limit.");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            string[] requirements = prerequisiteIds ?? Array.Empty<string>();
            foreach (string prerequisite in requirements)
                if (!TYCOON.StableId.IsValid(prerequisite) || prerequisite == id || !seen.Add(prerequisite))
                    throw new ArgumentException("Upgrade prerequisites must be unique stable IDs other than the upgrade itself.");
            stableId = id; displayName = label; baseCost = cost; this.requiredStage = requiredStage;
            this.unlockedStage = unlockedStage; this.kind = kind; this.maxLevel = maxLevel;
            this.prerequisiteIds = (string[])requirements.Clone();
        }

        public int CostAtLevel(int currentLevel)
        {
            if (currentLevel < 0 || currentLevel >= maxLevel) throw new ArgumentOutOfRangeException(nameof(currentLevel));
            return checked(baseCost * (currentLevel + 1));
        }
    }
}
