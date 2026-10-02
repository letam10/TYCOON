using System;

namespace TYCOON
{
    [Serializable]
    public sealed class GameSaveData
    {
        public const int CurrentVersion = 1;
        public int version = CurrentVersion;
        public WalletSnapshot wallet = new WalletSnapshot { completedSaleIds = Array.Empty<string>() };
        public InventorySnapshot[] inventories = Array.Empty<InventorySnapshot>();
        public UpgradeSnapshot[] upgrades = Array.Empty<UpgradeSnapshot>();
        public string[] unlocks = Array.Empty<string>();
        public string businessStageId = "farm";
        public string[] employeeUnlocks = Array.Empty<string>();
        // isProcessing=true nghĩa là nguyên liệu đã được tiêu hao trước khi lưu.
        public MachineState[] production = Array.Empty<MachineState>();
        public CheckoutSnapshot[] checkouts = Array.Empty<CheckoutSnapshot>();
        public HarvestSnapshot[] harvests = Array.Empty<HarvestSnapshot>();
        public PlayerSnapshot player = new PlayerSnapshot();
    }

    [Serializable]
    public sealed class HarvestSnapshot
    {
        public string nodeId;
        public float elapsedSeconds;
    }

    [Serializable]
    public sealed class PlayerSnapshot
    {
        public bool hasPosition;
        public float x, y, z, yaw;
    }

    [Serializable]
    public sealed class WalletSnapshot
    {
        public int balance;
        public string[] completedSaleIds;
    }

    [Serializable]
    public sealed class ItemQuantitySnapshot
    {
        public string itemId;
        public int quantity;
    }

    [Serializable]
    public sealed class InventorySnapshot
    {
        public string inventoryId;
        public int capacity;
        public ItemQuantitySnapshot[] items;
    }

    [Serializable]
    public sealed class UpgradeSnapshot
    {
        public string upgradeId;
        public int level;
    }
}
