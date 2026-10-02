using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TYCOON.Tests
{
    public sealed class SaveCoordinatorTests
    {
        private GameObject root;
        private readonly List<Object> assets = new List<Object>();
        private GameSession session;
        private SaveCoordinator saves;
        private ItemDefinition wheat, flour;
        private InventoryStore storage, input, output;
        private ProductionMachine machine;
        private HarvestNode plot;
        private CheckoutStation checkout;
        private WorkerAgent worker;
        private Transform player;
        private string folder, savePath, scratchRoot;

        private GameObject Owner(string name, bool active = true)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(root.transform);
            obj.SetActive(active);
            return obj;
        }
        private T Asset<T>() where T : ScriptableObject
        { var value = ScriptableObject.CreateInstance<T>(); assets.Add(value); return value; }
        private InventoryStore Store(string id)
        { var value = Owner(id).AddComponent<InventoryStore>(); value.Configure(id, 20); return value; }

        [UnitySetUp]
        public IEnumerator Setup()
        {
            scratchRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "work", "save-tests"));
            folder = Path.Combine(scratchRoot, Guid.NewGuid().ToString("N"));
            savePath = Path.Combine(folder, "slot.json");
            root = new GameObject("SaveCoordinatorTestWorld");
            session = root.AddComponent<GameSession>(); session.Configure(100);
            wheat = Asset<ItemDefinition>(); wheat.Configure("wheat", "Wheat", 5);
            flour = Asset<ItemDefinition>(); flour.Configure("flour", "Flour", 12);
            storage = Store("storage"); input = Store("machine.input"); output = Store("machine.output");
            var recipe = Asset<RecipeDefinition>();
            recipe.Configure("flour.recipe", new[] {new RecipeIngredient(wheat, 2)}, flour, 1, 4f);
            machine = Owner("Machine").AddComponent<ProductionMachine>();
            machine.Configure("mill", recipe, input, output); machine.enabled = false;
            plot = Owner("Plot").AddComponent<HarvestNode>();
            plot.Configure("wheat.plot", wheat, 6f, 5, 0); plot.enabled = false;
            checkout = Owner("Checkout").AddComponent<CheckoutStation>();
            checkout.Configure(checkout.transform, Array.Empty<Transform>(), id: "checkout.main");
            var workerObject = Owner("Worker", false);
            var job = workerObject.AddComponent<FarmerJob>(); job.Configure(new[] {plot}, storage);
            worker = workerObject.AddComponent<WorkerAgent>(); worker.Configure(job, 6, "worker.farmer");
            player = Owner("PlayerRoot").transform;
            saves = root.AddComponent<SaveCoordinator>();
            saves.Configure(session, new[] {wheat, flour}, player, savePath, automatic: false);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Object.Destroy(root);
            foreach (var asset in assets) Object.Destroy(asset);
            assets.Clear();
            yield return null;
            if (Directory.Exists(folder))
            {
                var checkedFolder = Path.GetFullPath(folder);
                Assert.IsTrue(checkedFolder.StartsWith(scratchRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
                foreach (var file in Directory.GetFiles(checkedFolder)) File.Delete(file);
                Directory.Delete(checkedFolder);
            }
        }

        [Test]
        public void RestoreWholeWorldPreservesConsumedBatchCarryProgressAndPlayer()
        {
            var upgrade = Asset<UpgradeDefinition>();
            upgrade.Configure("hire.farmer", "Farmer", 20, kind: UpgradeKind.Employee);
            Assert.IsTrue(session.TryPurchase(upgrade, out _));
            input.Inventory.TryAdd(wheat, 4);
            machine.Tick(1.5f);
            plot.Tick(4f);
            worker.Inventory.TryAdd(wheat, 3);
            player.SetPositionAndRotation(new Vector3(2, 0.2f, 3), Quaternion.Euler(0, 45, 0));
            Assert.IsTrue(saves.TrySave(out var error), error);

            session.Wallet.TrySpend(10);
            machine.Tick(4f);
            plot.Tick(6f);
            worker.Inventory.TryRemove(wheat, 3);
            player.position = Vector3.one * 9;
            Assert.IsTrue(saves.TryLoad(out error), error);
            Assert.AreEqual(80, session.Wallet.Balance);
            Assert.IsTrue(session.HasEmployee("hire.farmer"));
            Assert.AreEqual(3, worker.Inventory.GetCount(wheat));
            Assert.AreEqual(2, input.Inventory.GetCount(wheat));
            Assert.AreEqual(0, output.Inventory.TotalCount);
            Assert.AreEqual(1.5f, machine.CaptureState().elapsedSeconds, 0.001f);
            Assert.AreEqual(4f, plot.ElapsedSeconds, 0.001f);
            Assert.That(Vector3.Distance(player.position, new Vector3(2, 0.2f, 3)), Is.LessThan(0.001f));
            machine.Tick(2.5f);
            Assert.AreEqual(1, output.Inventory.GetCount(flour));
            Assert.AreEqual(2, input.Inventory.GetCount(wheat));
        }

        [Test]
        public void PendingCashSurvivesSaveAndCanOnlyBeCollectedOnce()
        {
            var ledger = new CheckoutLedger();
            var cart = new ItemInventory(2); cart.TryAdd(wheat, 2);
            Assert.IsTrue(ledger.TryCompleteSale("sale.before.save", cart, out _));
            Assert.IsTrue(checkout.TryRestoreState(new CheckoutSnapshot {checkoutId = checkout.StableId, ledger = ledger.CreateSnapshot()}));
            Assert.IsTrue(saves.TrySave(out var error), error);
            Assert.AreEqual(10, checkout.CollectMoney(session.Wallet));
            Assert.IsTrue(saves.TryLoad(out error), error);
            Assert.AreEqual(100, session.Wallet.Balance);
            Assert.AreEqual(10, checkout.PendingRevenue);
            Assert.AreEqual(10, checkout.CollectMoney(session.Wallet));
            Assert.AreEqual(0, checkout.CollectMoney(session.Wallet));
            Assert.AreEqual(110, session.Wallet.Balance);
        }

        [Test]
        public void SaveFromWrongWorldIsRejectedBeforeAnyStateMutation()
        {
            storage.Inventory.TryAdd(wheat, 4);
            Assert.IsTrue(saves.TryCapture(out var data, out var error), error);
            data.inventories.First(value => value.inventoryId == storage.StableId).inventoryId = "unknown.store";
            JsonSaveStore.Save(savePath, data);
            var originalFile = File.ReadAllText(savePath);
            Assert.IsFalse(saves.TryLoad(out _));
            Assert.AreEqual(100, session.Wallet.Balance);
            Assert.AreEqual(4, storage.Inventory.GetCount(wheat));
            Assert.IsFalse(saves.CanSave);
            Assert.IsFalse(saves.TrySave(out _));
            Assert.AreEqual(originalFile, File.ReadAllText(savePath));
        }

        [Test]
        public void InvalidProgressionCannotPartiallyRestoreMoneyOrStock()
        {
            storage.Inventory.TryAdd(wheat, 4);
            Assert.IsTrue(saves.TryCapture(out var data, out var error), error);
            data.wallet.balance = 999;
            data.businessStageId = "missing_stage";
            JsonSaveStore.Save(savePath, data);
            Assert.IsFalse(saves.TryLoad(out _));
            Assert.AreEqual(100, session.Wallet.Balance);
            Assert.AreEqual(4, storage.Inventory.GetCount(wheat));
            Assert.AreEqual(BusinessStage.Farm, session.Stage);
        }

        [Test]
        public void DuplicateWorldInventoryIdsPreventWritingASave()
        {
            Store(storage.StableId);
            Assert.IsFalse(saves.TrySave(out _));
            Assert.IsFalse(File.Exists(savePath));
        }

        [Test]
        public void SnapshotRejectsNonFinitePlayerOrGrowthValues()
        {
            Assert.IsTrue(saves.TryCapture(out var data, out var error), error);
            data.player.x = float.NaN;
            Assert.IsFalse(JsonSaveStore.TryValidate(data, out _));
            data.player.x = 0;
            data.harvests[0].elapsedSeconds = float.PositiveInfinity;
            Assert.IsFalse(JsonSaveStore.TryValidate(data, out _));
        }
    }
}
