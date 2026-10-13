using System;
using System.Collections.Generic;

namespace TYCOON
{
    [Serializable]
    public sealed class GameSaveDataV2
    {
        public const int CurrentVersion = 2;
        public int version = CurrentVersion;
        public GameSaveData world = new GameSaveData();
        public CustomerOrderSnapshot[] orders = Array.Empty<CustomerOrderSnapshot>();
    }

    [Serializable]
    public sealed class CustomerOrderSnapshot
    {
        public string customerId;
        public int queueIndex = -1;
        public float x, y, z, yaw;
        public OrderStateSnapshot order;
        public InventorySnapshot cart;
    }

    public static class GameSaveDataV2Validation
    {
        public static bool TryValidate(GameSaveDataV2 data,
            IReadOnlyDictionary<string, ItemDefinition> definitions, out string error)
        {
            error = null;
            if (data == null || data.version != GameSaveDataV2.CurrentVersion || data.world == null ||
                data.orders == null || definitions == null)
                return Fail("Save v2 is missing required state.", out error);
            if (!JsonSaveStore.TryValidate(data.world, out error)) return false;

            var customerIds = new HashSet<string>(StringComparer.Ordinal);
            var orderIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var saved in data.orders)
            {
                if (saved == null || !StableId.IsValid(saved.customerId) || !customerIds.Add(saved.customerId) ||
                    saved.queueIndex < -1 || !Finite(saved.x) || !Finite(saved.y) ||
                    !Finite(saved.z) || !Finite(saved.yaw) || saved.order == null || saved.cart == null)
                    return Fail("Invalid or duplicate customer order state.", out error);
                if (!string.Equals(saved.order.customerId, saved.customerId, StringComparison.Ordinal) ||
                    !orderIds.Add(saved.order.orderId) || !JsonSaveStore.TryValidateInventory(saved.cart, out error))
                    return false;
                if (!string.Equals(saved.cart.inventoryId, saved.customerId + ".cart", StringComparison.Ordinal))
                    return Fail("Customer cart ownership does not match its customer.", out error);
                if (!OrderState.TryRestore(saved.order, definitions, out _)) return Fail("Invalid order snapshot.", out error);
            }
            return true;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool Fail(string message, out string error)
        {
            error = message;
            return false;
        }
    }
}
