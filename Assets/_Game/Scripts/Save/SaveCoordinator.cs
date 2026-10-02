using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TYCOON
{
    [DefaultExecutionOrder(-1000)]
    public sealed class SaveCoordinator : MonoBehaviour
    {
        [SerializeField] private GameSession session;
        [SerializeField] private ItemDefinition[] catalog = Array.Empty<ItemDefinition>();
        [SerializeField] private Transform player;
        [SerializeField] private string pathOverride;
        [SerializeField] private bool automaticSaving = true;
        private GameSession subscribedSession;
        private bool restoring, queued, writesBlocked, initialized;
        public string LastError { get; private set; }
        public string LastStatus { get; private set; } = "Not saved yet";
        public bool CanSave => session != null && !writesBlocked;
        public string SavePath => string.IsNullOrWhiteSpace(pathOverride)
            ? Path.Combine(Application.persistentDataPath, "profiles", "main.json") : Path.GetFullPath(pathOverride);

        public void Configure(GameSession gameSession, ItemDefinition[] items, Transform actor = null,
            string path = null, bool automatic = true)
        {
            if (gameSession == null || items == null) throw new ArgumentException("Save requires session and item catalog.");
            Unbind();
            session = gameSession; catalog = (ItemDefinition[])items.Clone(); player = actor;
            pathOverride = path; automaticSaving = automatic;
            writesBlocked = false;
            if (isActiveAndEnabled) Bind();
        }

        private void OnEnable() => Bind();
        private void OnDisable() => Unbind();
        private void Bind()
        {
            if (session == null || subscribedSession == session) return;
            subscribedSession = session;
            subscribedSession.Changed += QueueSave;
        }
        private void Unbind()
        {
            if (subscribedSession != null) subscribedSession.Changed -= QueueSave;
            subscribedSession = null;
        }
        private void Start()
        {
            initialized = true;
            if (automaticSaving && File.Exists(SavePath)) TryLoad(out _);
        }
        private void QueueSave() { if (automaticSaving && !restoring) queued = true; }
        private void Update()
        {
            if (Keyboard.current == null) return;
            if (Keyboard.current.f5Key.wasPressedThisFrame) queued = true;
            if (Keyboard.current.f9Key.wasPressedThisFrame) TryLoad(out _);
        }
        private void LateUpdate()
        {
            if (!queued) return;
            queued = false;
            TrySave(out _);
        }
        private void OnApplicationQuit()
        {
            if (automaticSaving && initialized && CanSave) TrySave(out _);
        }

        public bool TryCapture(out GameSaveData data, out string error)
        {
            data = null; error = null;
            try
            {
                var definitions = Definitions();
                var inventories = Inventories();
                // Hàng khách chưa trả tiền thuộc bản save của kệ; không thay đổi giỏ/queue đang chạy.
                var projected = inventories.ToDictionary(pair => pair.Key, pair =>
                {
                    var inventory = new ItemInventory(pair.Value.Capacity);
                    if (!inventory.TryLoadSnapshot(pair.Value.CreateSnapshot(pair.Key), definitions))
                        throw new InvalidOperationException("Inventory snapshot is invalid: " + pair.Key);
                    return inventory;
                }, StringComparer.Ordinal);
                foreach (var customer in World<CustomerAgent>())
                {
                    if (customer.Cart.TotalCount == 0) continue;
                    if (customer.SourceShelf == null || !projected.TryGetValue(customer.SourceShelf.Store.StableId, out var shelf))
                        throw new InvalidOperationException("Customer goods have no source shelf.");
                    foreach (var stack in customer.Cart.Stacks)
                        if (!shelf.TryAdd(stack.Item, stack.Quantity))
                            throw new InvalidOperationException("Customer return reservation exceeds shelf capacity.");
                }
                var progress = session.CaptureState();
                data = new GameSaveData
                {
                    wallet = session.Wallet.CreateSnapshot(), businessStageId = progress.stage.ToString().ToLowerInvariant(),
                    unlocks = progress.unlockedIds, employeeUnlocks = progress.employeeIds,
                    upgrades = progress.upgradeLevels.Select(value => new UpgradeSnapshot { upgradeId = value.upgradeId, level = value.level }).ToArray(),
                    inventories = projected.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Value.CreateSnapshot(pair.Key)).ToArray(),
                    production = World<ProductionMachine>().Select(machine => machine.CaptureState()).ToArray(),
                    checkouts = World<CheckoutStation>().Select(checkout => checkout.CaptureLedgerState()).ToArray(),
                    harvests = World<HarvestNode>().Select(node => new HarvestSnapshot { nodeId = node.StableId, elapsedSeconds = node.ElapsedSeconds }).ToArray(),
                    player = player == null ? new PlayerSnapshot() : new PlayerSnapshot
                    { hasPosition = true, x = player.position.x, y = player.position.y, z = player.position.z, yaw = player.eulerAngles.y }
                };
                return JsonSaveStore.TryValidate(data, out error);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            { error = exception.Message; data = null; return false; }
        }

        public bool TrySave(out string error)
        {
            error = null;
            if (!CanSave) return Fail("Saving is blocked until the existing save is loaded successfully.", out error);
            if (!TryCapture(out var data, out error)) return Fail(error, out error);
            try { JsonSaveStore.Save(SavePath, data); }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException)
            { return Fail(exception.Message, out error); }
            LastError = null; LastStatus = "Saved";
            return true;
        }

        public bool TryLoad(out string error)
        {
            if (!JsonSaveStore.TryLoad(SavePath, out var data, out error))
            {
                if (File.Exists(SavePath)) writesBlocked = true;
                return Fail(error, out error);
            }
            if (!ValidateWorld(data, out var progress, out error))
            { writesBlocked = true; return Fail(error, out error); }
            var pools = World<CustomerPool>();
            foreach (var pool in pools)
                if (!pool.SuspendAndReturnCarts())
                {
                    foreach (var resume in pools) resume.ResumeSpawning();
                    return Fail("Customers could not return all unsold goods.", out error);
                }
            foreach (var customer in World<CustomerAgent>())
                if (!customer.CancelVisit())
                {
                    foreach (var resume in pools) resume.ResumeSpawning();
                    return Fail("A customer still holds unreturned goods.", out error);
                }
            restoring = true;
            try
            {
                var definitions = Definitions();
                var inventoryStates = data.inventories.ToDictionary(value => value.inventoryId, StringComparer.Ordinal);
                session.Wallet.TryLoadSnapshot(data.wallet);
                session.TryRestoreState(progress);
                foreach (var store in World<InventoryStore>()) store.Inventory.TryLoadSnapshot(inventoryStates[store.StableId], definitions);
                foreach (var worker in World<WorkerAgent>()) worker.TryRestoreInventory(inventoryStates[worker.StableId], definitions);
                var machines = data.production.ToDictionary(value => value.machineId, StringComparer.Ordinal);
                foreach (var machine in World<ProductionMachine>()) machine.TryRestoreState(machines[machine.StableId]);
                var checkouts = data.checkouts.ToDictionary(value => value.checkoutId, StringComparer.Ordinal);
                foreach (var checkout in World<CheckoutStation>()) checkout.TryRestoreState(checkouts[checkout.StableId]);
                var harvests = data.harvests.ToDictionary(value => value.nodeId, StringComparer.Ordinal);
                foreach (var node in World<HarvestNode>()) node.RestoreProgress(harvests[node.StableId].elapsedSeconds);
                if (player != null && data.player.hasPosition)
                {
                    var controller = player.GetComponent<CharacterController>();
                    bool enabledBefore = controller != null && controller.enabled;
                    if (enabledBefore) controller.enabled = false;
                    player.SetPositionAndRotation(new Vector3(data.player.x, data.player.y, data.player.z), Quaternion.Euler(0, data.player.yaw, 0));
                    if (enabledBefore) controller.enabled = true;
                    Physics.SyncTransforms();
                    foreach (var camera in World<SmoothFollowCamera>()) camera.SnapToTarget();
                }
                writesBlocked = false; queued = false; LastError = null; LastStatus = "Loaded";
                return true;
            }
            finally
            {
                restoring = false;
                foreach (var pool in pools) pool.ResumeSpawning();
            }
        }

        private bool ValidateWorld(GameSaveData data, out ProgressionState progress, out string error)
        {
            progress = null; error = null;
            try
            {
                if (session == null || !JsonSaveStore.TryValidate(data, out error)) return false;
                if (!Enum.TryParse(data.businessStageId, true, out BusinessStage stage))
                    return Fail("Unknown business stage.", out error);
                progress = new ProgressionState { stage = stage, unlockedIds = data.unlocks, employeeIds = data.employeeUnlocks,
                    upgradeLevels = data.upgrades.Select(value => new UpgradeLevelState(value.upgradeId, value.level)).ToArray() };
                if (!GameSession.IsValidState(progress)) return Fail("Invalid progression state.", out error);
                var definitions = Definitions();
                var inventories = Inventories();
                if (inventories.Count != data.inventories.Length) return Fail("Save inventory set does not match this world.", out error);
                foreach (var saved in data.inventories)
                    if (!inventories.ContainsKey(saved.inventoryId) || saved.capacity < 1 ||
                        !new ItemInventory(saved.capacity).TryLoadSnapshot(saved, definitions))
                        return Fail("Save inventory cannot be restored: " + saved.inventoryId, out error);
                var machines = World<ProductionMachine>().ToDictionary(value => value.StableId, StringComparer.Ordinal);
                if (machines.Count != data.production.Length) return Fail("Save production set does not match this world.", out error);
                foreach (var saved in data.production)
                    if (!machines.TryGetValue(saved.machineId, out var machine) || machine.Recipe == null || machine.Recipe.Id != saved.recipeId)
                        return Fail("Save recipe does not match this world.", out error);
                var checkouts = new HashSet<string>(World<CheckoutStation>().Select(value => value.StableId), StringComparer.Ordinal);
                if (checkouts.Count != data.checkouts.Length || data.checkouts.Any(value => !checkouts.Contains(value.checkoutId)))
                    return Fail("Save checkout set does not match this world.", out error);
                var harvests = new HashSet<string>(World<HarvestNode>().Select(value => value.StableId), StringComparer.Ordinal);
                if (harvests.Count != data.harvests.Length || data.harvests.Any(value => !harvests.Contains(value.nodeId)))
                    return Fail("Save harvest set does not match this world.", out error);
                return true;
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            { return Fail(exception.Message, out error); }
        }

        private Dictionary<string, ItemDefinition> Definitions()
        {
            if (session == null) throw new InvalidOperationException("Save has no game session.");
            var result = new Dictionary<string, ItemDefinition>(StringComparer.Ordinal);
            foreach (var item in catalog)
            {
                if (item == null || !StableId.IsValid(item.Id) || result.ContainsKey(item.Id))
                    throw new InvalidOperationException("Item catalog contains invalid or duplicate IDs.");
                result.Add(item.Id, item);
            }
            return result;
        }
        private Dictionary<string, ItemInventory> Inventories()
        {
            var result = new Dictionary<string, ItemInventory>(StringComparer.Ordinal);
            foreach (var store in World<InventoryStore>()) Add(store.StableId, store.Inventory);
            foreach (var worker in World<WorkerAgent>()) Add(worker.StableId, worker.Inventory);
            return result;
            void Add(string id, ItemInventory inventory)
            {
                if (!StableId.IsValid(id) || result.ContainsKey(id)) throw new InvalidOperationException("World inventory IDs must be unique.");
                result.Add(id, inventory);
            }
        }
        private T[] World<T>() where T : Component => FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(value => value.gameObject.scene == gameObject.scene).ToArray();
        private bool Fail(string message, out string error)
        { error = message; LastError = message; LastStatus = "Save needs attention"; return false; }
    }
}
