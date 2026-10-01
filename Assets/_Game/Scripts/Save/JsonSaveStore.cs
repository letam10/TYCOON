using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace TYCOON
{
    public static class JsonSaveStore
    {
        public static void Save(string path, GameSaveData data)
        {
            if (!TryValidate(data, out string error)) throw new ArgumentException(error, nameof(data));
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Save path is empty.", nameof(path));
            string destination = Path.GetFullPath(path);
            string folder = Path.GetDirectoryName(destination);
            Directory.CreateDirectory(folder);
            string temporary = Path.Combine(folder, ".tycoon-save-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, true))
                        writer.Write(JsonUtility.ToJson(data, true));
                    stream.Flush(true);
                }
                // Thay thế nguyên tử cùng thư mục, không tạo bản backup hoặc chạm file nguồn.
                if (File.Exists(destination)) File.Replace(temporary, destination, null);
                else File.Move(temporary, destination);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        public static bool TryLoad(string path, out GameSaveData data, out string error)
        {
            data = null;
            error = null;
            try
            {
                if (string.IsNullOrWhiteSpace(path)) { error = "Save path is empty."; return false; }
                string json = File.ReadAllText(path, Encoding.UTF8).Trim();
                if (json.Length < 2 || json[0] != '{' || json[json.Length - 1] != '}')
                { error = "Save must be a JSON object."; return false; }
                // Giá trị sentinel phát hiện trường bắt buộc bị thiếu thay vì tự dùng mặc định.
                var candidate = new GameSaveData
                {
                    version = 0, wallet = null, inventories = null, upgrades = null, unlocks = null,
                    businessStageId = null, employeeUnlocks = null, production = null, checkouts = null
                };
                JsonUtility.FromJsonOverwrite(json, candidate);
                if (!TryValidate(candidate, out error)) return false;
                data = candidate;
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException ||
                exception is ArgumentException || exception is NotSupportedException)
            {
                error = exception.Message;
                return false;
            }
        }

        public static bool TryValidate(GameSaveData data, out string error)
        {
            error = null;
            if (data == null) { error = "Save is null."; return false; }
            if (data.version != GameSaveData.CurrentVersion) { error = "Unsupported save version: " + data.version; return false; }
            if (!TryValidateWallet(data.wallet, out error)) return false;
            if (data.inventories == null || data.upgrades == null || data.production == null || data.checkouts == null ||
                !StableId.IsValid(data.businessStageId)) { error = "Save is missing required state."; return false; }
            var inventoryIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var inventory in data.inventories)
            {
                if (!TryValidateInventory(inventory, out error)) return false;
                if (!inventoryIds.Add(inventory.inventoryId)) { error = "Duplicate inventory ID."; return false; }
            }
            var upgradeIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var upgrade in data.upgrades)
                if (upgrade == null || !StableId.IsValid(upgrade.upgradeId) || upgrade.level < 0 || !upgradeIds.Add(upgrade.upgradeId))
                { error = "Invalid or duplicate upgrade."; return false; }
            if (!TryValidateIds(data.unlocks, out error) || !TryValidateIds(data.employeeUnlocks, out error)) return false;
            var machineIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var machine in data.production)
                if (!StableId.IsValid(machine.machineId) || !StableId.IsValid(machine.recipeId) ||
                    !machineIds.Add(machine.machineId) || machine.level < 0 || machine.elapsedSeconds < 0f ||
                    float.IsNaN(machine.elapsedSeconds) || float.IsInfinity(machine.elapsedSeconds) ||
                    (!machine.isProcessing && machine.elapsedSeconds != 0f))
                { error = "Invalid or duplicate production state."; return false; }
            var checkoutIds = new HashSet<string>(StringComparer.Ordinal);
            var saleIds = new HashSet<string>(StringComparer.Ordinal);
            var creditedIds = new HashSet<string>(data.wallet.completedSaleIds, StringComparer.Ordinal);
            foreach (var checkout in data.checkouts)
            {
                if (checkout == null || !StableId.IsValid(checkout.checkoutId) || !checkoutIds.Add(checkout.checkoutId) ||
                    !CheckoutLedger.TryValidateSnapshot(checkout.ledger))
                { error = "Invalid or duplicate checkout state."; return false; }
                foreach (string transactionId in checkout.ledger.completedSaleIds)
                    if (!saleIds.Add(transactionId)) { error = "Transaction belongs to multiple checkouts."; return false; }
                foreach (var receipt in checkout.ledger.pendingReceipts)
                    if (creditedIds.Contains(receipt.transactionId))
                    { error = "Pending transaction was already credited to wallet."; return false; }
            }
            return true;
        }

        internal static bool TryValidateWallet(WalletSnapshot wallet, out string error)
        {
            error = null;
            if (wallet == null || wallet.balance < 0) { error = "Wallet balance is invalid."; return false; }
            return TryValidateIds(wallet.completedSaleIds, out error);
        }

        internal static bool TryValidateInventory(InventorySnapshot inventory, out string error)
        {
            error = null;
            if (inventory == null || !StableId.IsValid(inventory.inventoryId) || inventory.capacity < 0 || inventory.items == null)
            { error = "Inventory state is invalid."; return false; }
            long total = 0;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in inventory.items)
            {
                if (item == null || !StableId.IsValid(item.itemId) || item.quantity < 1 || !ids.Add(item.itemId))
                { error = "Invalid or duplicate inventory item."; return false; }
                total += item.quantity;
            }
            if (total > inventory.capacity) { error = "Inventory exceeds capacity."; return false; }
            return true;
        }

        private static bool TryValidateIds(string[] ids, out string error)
        {
            error = null;
            if (ids == null) { error = "Required ID list is missing."; return false; }
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in ids)
                if (!StableId.IsValid(id) || !seen.Add(id)) { error = "Invalid or duplicate stable ID."; return false; }
            return true;
        }
    }
}
