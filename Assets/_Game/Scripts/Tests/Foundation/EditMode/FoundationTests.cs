using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TYCOON.Tests
{
    public sealed class FoundationTests
    {
        private readonly List<Object> created = new List<Object>();
        private string testFolder;
        private ItemDefinition wheat;
        private ItemDefinition egg;
        private ItemDefinition flour;

        [SetUp]
        public void SetUp()
        {
            testFolder = Path.Combine(Path.GetTempPath(), "TYCOON.FoundationTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testFolder);
            wheat = Item("wheat", 2, 3);
            egg = Item("egg", 4);
            flour = Item("flour", 6);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in created) if (obj != null) Object.DestroyImmediate(obj);
            created.Clear();
            // Chỉ xóa thư mục tạm riêng do test này tạo, không đụng cache hoặc dữ liệu người dùng.
            string resolved = Path.GetFullPath(testFolder);
            string tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!resolved.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(resolved).StartsWith("TYCOON.FoundationTests-", StringComparison.Ordinal))
                throw new InvalidOperationException("Test cleanup path escaped its temporary folder.");
            if (Directory.Exists(resolved)) Directory.Delete(resolved, true);
        }

        [Test]
        public void ItemDefinitionRejectsInvalidIdsAndValues()
        {
            Assert.Throws<ArgumentException>(() => wheat.Configure("bad id", "Wheat", 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => wheat.Configure("wheat", "Wheat", -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => wheat.Configure("wheat", "Wheat", 2, 0));
            Assert.That(wheat.Id, Is.EqualTo("wheat"));
            Assert.That(wheat.SellPrice, Is.EqualTo(2));
        }

        [Test]
        public void InventoryCapacityCountsUnitsAndStacksRespectItemLimit()
        {
            var inventory = new ItemInventory(8);
            Assert.That(inventory.TryAdd(wheat, 7), Is.True);
            Assert.That(inventory.Stacks.Select(stack => stack.Quantity), Is.EqualTo(new[] { 3, 3, 1 }));
            Assert.That(inventory.TotalCount, Is.EqualTo(7));
            Assert.That(inventory.RemainingCapacity, Is.EqualTo(1));
            Assert.That(inventory.TryAdd(egg, 2), Is.False);
            Assert.That(inventory.TryAdd(egg, 1), Is.True);
            Assert.That(inventory.TotalCount, Is.EqualTo(8));
            Assert.That(inventory.GetCount("wheat"), Is.EqualTo(7));
            Assert.That(inventory.GetCount(egg), Is.EqualTo(1));
            Assert.That(inventory.Stacks.Sum(stack => stack.Quantity), Is.EqualTo(inventory.TotalCount));
        }

        [Test]
        public void InvalidInventoryOperationsPreserveQuantitiesAndEvents()
        {
            var inventory = new ItemInventory(5);
            inventory.TryAdd(wheat, 3);
            int events = 0;
            inventory.Changed += () => events++;
            Assert.That(inventory.TryAdd(wheat, 0), Is.False);
            Assert.That(inventory.TryAdd(wheat, -1), Is.False);
            Assert.That(inventory.TryAdd(wheat, int.MaxValue), Is.False);
            Assert.That(inventory.TryRemove(wheat, 4), Is.False);
            Assert.That(inventory.TryRemove(wheat, -1), Is.False);
            Assert.That(inventory.TryAdd(null, 1), Is.False);
            Assert.That(inventory.TotalCount, Is.EqualTo(3));
            Assert.That(events, Is.Zero);
        }

        [Test]
        public void CapacityCannotShrinkBelowContentsAndSupportsEmptyInventory()
        {
            var inventory = new ItemInventory(5);
            inventory.TryAdd(wheat, 3);
            Assert.That(inventory.TrySetCapacity(2), Is.False);
            Assert.That(inventory.TrySetCapacity(-1), Is.False);
            Assert.That(inventory.Capacity, Is.EqualTo(5));
            Assert.That(inventory.TrySetCapacity(3), Is.True);
            Assert.That(inventory.TryRemove(wheat, 3), Is.True);
            Assert.That(inventory.TrySetCapacity(0), Is.True);
            Assert.That(inventory.Stacks, Is.Empty);
            Assert.That(inventory.TryAdd(wheat, 1), Is.False);
        }

        [Test]
        public void TransferFailureIsAtomicAndSuccessPublishesCompletedState()
        {
            var source = new ItemInventory(10);
            var destination = new ItemInventory(4);
            source.TryAdd(wheat, 5);
            destination.TryAdd(egg, 2);
            int sourceEvents = 0;
            int destinationEvents = 0;
            source.Changed += () =>
            {
                sourceEvents++;
                Assert.That(source.GetCount(wheat), Is.EqualTo(3));
                Assert.That(destination.GetCount(wheat), Is.EqualTo(2));
            };
            destination.Changed += () => destinationEvents++;
            Assert.That(source.TryTransferTo(destination, wheat, 3), Is.False);
            Assert.That(source.GetCount(wheat), Is.EqualTo(5));
            Assert.That(destination.GetCount(wheat), Is.Zero);
            Assert.That(sourceEvents + destinationEvents, Is.Zero);
            Assert.That(source.TryTransferTo(destination, wheat, 2), Is.True);
            Assert.That(source.TotalCount + destination.TotalCount, Is.EqualTo(7));
            Assert.That(sourceEvents, Is.EqualTo(1));
            Assert.That(destinationEvents, Is.EqualTo(1));
        }

        [Test]
        public void TransferRejectsSelfAndInsufficientSource()
        {
            var source = new ItemInventory(5);
            var destination = new ItemInventory(5);
            source.TryAdd(wheat, 1);
            Assert.That(source.TryTransferTo(source, wheat, 1), Is.False);
            Assert.That(source.TryTransferTo(destination, wheat, 2), Is.False);
            Assert.That(source.GetCount(wheat), Is.EqualTo(1));
            Assert.That(destination.TotalCount, Is.Zero);
        }

        [Test]
        public void RecipeConsumptionChecksAllIngredientsBeforeMutating()
        {
            var inventory = new ItemInventory(10);
            inventory.TryAdd(wheat, 3);
            inventory.TryAdd(egg, 1);
            var ingredients = new[] { new RecipeIngredient(wheat, 2), new RecipeIngredient(egg, 2) };
            int events = 0;
            inventory.Changed += () => events++;
            Assert.That(inventory.TryConsume(ingredients), Is.False);
            Assert.That(inventory.GetCount(wheat), Is.EqualTo(3));
            Assert.That(inventory.GetCount(egg), Is.EqualTo(1));
            ingredients[1] = new RecipeIngredient(egg, 1);
            Assert.That(inventory.TryConsume(ingredients), Is.True);
            Assert.That(inventory.GetCount(wheat), Is.EqualTo(1));
            Assert.That(inventory.GetCount(egg), Is.Zero);
            Assert.That(events, Is.EqualTo(1));
        }

        [Test]
        public void RecipeConsumptionRejectsDuplicateIdsWithoutRemovingAnything()
        {
            var inventory = new ItemInventory(5);
            inventory.TryAdd(wheat, 3);
            Assert.That(inventory.TryConsume(new[] { new RecipeIngredient(wheat, 2), new RecipeIngredient(wheat, 2) }), Is.False);
            Assert.That(inventory.GetCount(wheat), Is.EqualTo(3));
        }

        [Test]
        public void SnapshotLoadRejectsUnknownItemWithoutPartialReplacement()
        {
            var inventory = new ItemInventory(7);
            inventory.TryAdd(egg, 2);
            var snapshot = new InventorySnapshot
            {
                inventoryId = "storage", capacity = 20,
                items = new[]
                {
                    new ItemQuantitySnapshot { itemId = "wheat", quantity = 3 },
                    new ItemQuantitySnapshot { itemId = "unknown", quantity = 2 }
                }
            };
            Assert.That(inventory.TryLoadSnapshot(snapshot, Catalog()), Is.False);
            Assert.That(inventory.Capacity, Is.EqualTo(7));
            Assert.That(inventory.GetCount(egg), Is.EqualTo(2));
            Assert.That(inventory.GetCount(wheat), Is.Zero);
        }

        [Test]
        public void SnapshotLoadRejectsOverflowAndDuplicateItems()
        {
            var inventory = new ItemInventory(5);
            var snapshot = new InventorySnapshot
            {
                inventoryId = "storage", capacity = int.MaxValue,
                items = new[]
                {
                    new ItemQuantitySnapshot { itemId = "wheat", quantity = int.MaxValue },
                    new ItemQuantitySnapshot { itemId = "egg", quantity = int.MaxValue }
                }
            };
            Assert.That(inventory.TryLoadSnapshot(snapshot, Catalog()), Is.False);
            snapshot.items = new[]
            {
                new ItemQuantitySnapshot { itemId = "wheat", quantity = 1 },
                new ItemQuantitySnapshot { itemId = "wheat", quantity = 1 }
            };
            Assert.That(inventory.TryLoadSnapshot(snapshot, Catalog()), Is.False);
            Assert.That(inventory.TotalCount, Is.Zero);
        }

        [Test]
        public void SnapshotLoadRequiresCatalogKeyToMatchDefinitionId()
        {
            var inventory = new ItemInventory(5);
            var snapshot = new InventorySnapshot
            {
                inventoryId = "storage", capacity = 5,
                items = new[] { new ItemQuantitySnapshot { itemId = "wheat", quantity = 1 } }
            };
            Assert.That(inventory.TryLoadSnapshot(snapshot, new Dictionary<string, ItemDefinition> { { "wheat", egg } }), Is.False);
            Assert.That(inventory.TotalCount, Is.Zero);
        }

        [Test]
        public void WalletRejectsInsufficientFundsAndOverflowWithoutMutation()
        {
            var wallet = new Wallet(10);
            int events = 0;
            wallet.Changed += () => events++;
            Assert.That(wallet.TrySpend(11), Is.False);
            Assert.That(wallet.TrySpend(-1), Is.False);
            Assert.That(wallet.TryCreditCompletedSale("sale-overflow", int.MaxValue), Is.False);
            Assert.That(wallet.TryCreditCompletedSale("bad id", 5), Is.False);
            Assert.That(wallet.Balance, Is.EqualTo(10));
            Assert.That(events, Is.Zero);
            Assert.That(wallet.TrySpend(10), Is.True);
            Assert.That(wallet.Balance, Is.Zero);
            Assert.That(wallet.TryCreditCompletedSale("sale-overflow", int.MaxValue), Is.True);
            Assert.That(wallet.Balance, Is.EqualTo(int.MaxValue));
        }

        [Test]
        public void CompletedSaleIsIdempotentBeforeAndAfterLoad()
        {
            var wallet = new Wallet(5);
            Assert.That(wallet.TryCreditCompletedSale("checkout-001", 8), Is.True);
            Assert.That(wallet.TryCreditCompletedSale("checkout-001", 80), Is.False);
            var restored = new Wallet();
            Assert.That(restored.TryLoadSnapshot(wallet.CreateSnapshot()), Is.True);
            Assert.That(restored.TryCreditCompletedSale("checkout-001", 8), Is.False);
            Assert.That(restored.Balance, Is.EqualTo(13));
            Assert.That(restored.TrySpend(3), Is.True);
            Assert.That(restored.Balance, Is.EqualTo(10));
        }

        [Test]
        public void InvalidWalletSnapshotDoesNotReplaceCurrentBalanceOrReceipts()
        {
            var wallet = new Wallet(5);
            wallet.TryCreditCompletedSale("sale-1", 3);
            Assert.That(wallet.TryLoadSnapshot(new WalletSnapshot { balance = 99, completedSaleIds = new[] { "sale-2", "sale-2" } }), Is.False);
            Assert.That(wallet.Balance, Is.EqualTo(8));
            Assert.That(wallet.TryCreditCompletedSale("sale-1", 3), Is.False);
        }

        [Test]
        public void SaveRoundTripPreservesAllRequiredStableIdState()
        {
            string path = Path.Combine(testFolder, "game.json");
            var inventory = new ItemInventory(20);
            inventory.TryAdd(wheat, 5);
            var wallet = new Wallet(50);
            wallet.TryCreditCompletedSale("sale-1", 8);
            var data = new GameSaveData
            {
                wallet = wallet.CreateSnapshot(), inventories = new[] { inventory.CreateSnapshot("storage-farm") },
                upgrades = new[] { new UpgradeSnapshot { upgradeId = "capacity", level = 2 } },
                unlocks = new[] { "farm-shop", "mill" }, businessStageId = "processing",
                employeeUnlocks = new[] { "farmer", "cashier" },
                production = new[] { new MachineState { machineId = "mill-1", recipeId = "wheat-flour", isProcessing = true, elapsedSeconds = 2.5f, level = 1 } }
            };
            JsonSaveStore.Save(path, data);
            Assert.That(JsonSaveStore.TryLoad(path, out var loaded, out var error), Is.True, error);
            Assert.That(loaded.wallet.balance, Is.EqualTo(58));
            Assert.That(loaded.wallet.completedSaleIds, Is.EqualTo(new[] { "sale-1" }));
            Assert.That(loaded.businessStageId, Is.EqualTo("processing"));
            Assert.That(loaded.employeeUnlocks, Is.EqualTo(data.employeeUnlocks));
            Assert.That(loaded.unlocks, Is.EqualTo(data.unlocks));
            Assert.That(loaded.upgrades[0].level, Is.EqualTo(2));
            Assert.That(loaded.production[0].elapsedSeconds, Is.EqualTo(2.5f));
            var restored = new ItemInventory(1);
            Assert.That(restored.TryLoadSnapshot(loaded.inventories[0], Catalog()), Is.True);
            Assert.That(restored.Capacity, Is.EqualTo(20));
            Assert.That(restored.GetCount(wheat), Is.EqualTo(5));
            Assert.That(File.ReadAllText(path), Does.Not.Contain("instanceID"));
        }

        [Test]
        public void SaveOverwriteLeavesOnlyDestinationAndInvalidSaveKeepsOldFile()
        {
            string path = Path.Combine(testFolder, "game.json");
            var data = new GameSaveData();
            JsonSaveStore.Save(path, data);
            data.wallet.balance = 10;
            JsonSaveStore.Save(path, data);
            Assert.That(Directory.GetFiles(testFolder), Is.EqualTo(new[] { path }));
            string validJson = File.ReadAllText(path);
            data.version = 999;
            Assert.Throws<ArgumentException>(() => JsonSaveStore.Save(path, data));
            Assert.That(File.ReadAllText(path), Is.EqualTo(validJson));
            Assert.That(Directory.GetFiles(testFolder), Is.EqualTo(new[] { path }));
        }

        [TestCase("{}")]
        [TestCase("not json")]
        [TestCase("{\"version\":999}")]
        [TestCase("{\"version\":1,\"wallet\":{\"balance\":-1}}")]
        public void InvalidSaveFileReturnsErrorAndNoData(string json)
        {
            string path = Path.Combine(testFolder, "bad.json");
            File.WriteAllText(path, json);
            Assert.That(JsonSaveStore.TryLoad(path, out var loaded, out var error), Is.False);
            Assert.That(loaded, Is.Null);
            Assert.That(error, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void FailedFileReplacementRemovesItsTemporaryFileAndPreservesOtherFiles()
        {
            string destination = Path.Combine(testFolder, "existing-directory");
            Directory.CreateDirectory(destination);
            string unrelated = Path.Combine(testFolder, "user-data.txt");
            File.WriteAllText(unrelated, "keep");
            Assert.Throws<IOException>(() => JsonSaveStore.Save(destination, new GameSaveData()));
            Assert.That(Directory.Exists(destination), Is.True);
            Assert.That(File.ReadAllText(unrelated), Is.EqualTo("keep"));
            Assert.That(Directory.GetFiles(testFolder), Is.EqualTo(new[] { unrelated }));
        }

        [Test]
        public void PendingCheckoutReceiptSurvivesFileLoadAndCanBeCollectedOnce()
        {
            var cart = new ItemInventory(3);
            cart.TryAdd(wheat, 3);
            var ledger = new CheckoutLedger();
            Assert.That(ledger.TryCompleteSale("sale-pending", cart, out var revenue), Is.True);
            Assert.That(revenue, Is.EqualTo(6));
            string path = Path.Combine(testFolder, "checkout.json");
            JsonSaveStore.Save(path, new GameSaveData
            {
                checkouts = new[] { new CheckoutSnapshot { checkoutId = "checkout-1", ledger = ledger.CreateSnapshot() } }
            });
            Assert.That(JsonSaveStore.TryLoad(path, out var data, out var error), Is.True, error);
            var restoredLedger = new CheckoutLedger();
            Assert.That(restoredLedger.TryLoadSnapshot(data.checkouts[0].ledger), Is.True);
            var wallet = new Wallet();
            Assert.That(wallet.TryLoadSnapshot(data.wallet), Is.True);
            Assert.That(restoredLedger.CollectMoney(wallet), Is.EqualTo(6));
            Assert.That(wallet.HasCompletedSale("sale-pending"), Is.True);
            Assert.That(restoredLedger.CollectMoney(wallet), Is.Zero);
            Assert.That(wallet.Balance, Is.EqualTo(6));
        }

        [Test]
        public void SaveRejectsTransactionAppearingAtMultipleCheckouts()
        {
            var ledger = new CheckoutLedger();
            var cart = new ItemInventory(1);
            cart.TryAdd(wheat, 1);
            ledger.TryCompleteSale("sale-duplicate", cart, out _);
            var data = new GameSaveData
            {
                checkouts = new[]
                {
                    new CheckoutSnapshot { checkoutId = "checkout-1", ledger = ledger.CreateSnapshot() },
                    new CheckoutSnapshot { checkoutId = "checkout-2", ledger = ledger.CreateSnapshot() }
                }
            };
            Assert.That(JsonSaveStore.TryValidate(data, out var error), Is.False);
            Assert.That(error, Does.Contain("multiple checkouts"));
        }

        [Test]
        public void SaveRejectsPendingReceiptAlreadyCreditedButAllowsCollectedReceiptHistory()
        {
            var ledger = new CheckoutLedger();
            var cart = new ItemInventory(1);
            cart.TryAdd(wheat, 1);
            ledger.TryCompleteSale("sale-paid", cart, out _);
            var wallet = new Wallet();
            wallet.TryCreditCompletedSale("sale-paid", 2);
            var data = new GameSaveData
            {
                wallet = wallet.CreateSnapshot(),
                checkouts = new[] { new CheckoutSnapshot { checkoutId = "checkout-1", ledger = ledger.CreateSnapshot() } }
            };
            Assert.That(JsonSaveStore.TryValidate(data, out var error), Is.False);
            Assert.That(error, Does.Contain("already credited"));
            ledger.CollectMoney(wallet);
            data.checkouts[0].ledger = ledger.CreateSnapshot();
            Assert.That(JsonSaveStore.TryValidate(data, out error), Is.True, error);
        }

        [Test]
        public void MissingSaveReturnsErrorAndNoData()
        {
            Assert.That(JsonSaveStore.TryLoad(Path.Combine(testFolder, "missing.json"), out var data, out var error), Is.False);
            Assert.That(data, Is.Null);
            Assert.That(error, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void SaveValidationRejectsInvalidNumbersDuplicateIdsAndMissingState()
        {
            var data = new GameSaveData { unlocks = new[] { "mill", "mill" } };
            Assert.That(JsonSaveStore.TryValidate(data, out _), Is.False);
            data.unlocks = Array.Empty<string>();
            data.production = new[] { new MachineState { machineId = "mill", recipeId = "flour", isProcessing = true, elapsedSeconds = float.NaN } };
            Assert.That(JsonSaveStore.TryValidate(data, out _), Is.False);
            data.production[0].elapsedSeconds = float.PositiveInfinity;
            Assert.That(JsonSaveStore.TryValidate(data, out _), Is.False);
            data.production[0].elapsedSeconds = 1f;
            data.production[0].isProcessing = false;
            Assert.That(JsonSaveStore.TryValidate(data, out _), Is.False);
            data.production = Array.Empty<MachineState>();
            data.wallet.completedSaleIds = null;
            Assert.That(JsonSaveStore.TryValidate(data, out _), Is.False);
        }

        [Test]
        public void MidProductionFileRoundTripDoesNotConsumeInputsAgain()
        {
            var input = Store("mill-input", 10, wheat, 3);
            var output = Store("mill-output", 10);
            var machine = Machine("mill", input, output);
            machine.Tick(2f);
            Assert.That(input.Inventory.GetCount(wheat), Is.EqualTo(2));
            Assert.That(machine.IsProcessing, Is.True);
            string path = Path.Combine(testFolder, "production.json");
            JsonSaveStore.Save(path, new GameSaveData
            {
                inventories = new[] { input.Inventory.CreateSnapshot(input.StableId), output.Inventory.CreateSnapshot(output.StableId) },
                production = new[] { machine.CaptureState() }
            });
            Assert.That(JsonSaveStore.TryLoad(path, out var loaded, out var error), Is.True, error);
            var restoredInput = Store("mill-input", 1);
            var restoredOutput = Store("mill-output", 1);
            Assert.That(restoredInput.Inventory.TryLoadSnapshot(loaded.inventories[0], Catalog()), Is.True);
            Assert.That(restoredOutput.Inventory.TryLoadSnapshot(loaded.inventories[1], Catalog()), Is.True);
            var restored = Machine("mill", restoredInput, restoredOutput);
            Assert.That(restored.TryRestoreState(loaded.production[0]), Is.True);
            restored.Tick(3f);
            Assert.That(restoredInput.Inventory.GetCount(wheat), Is.EqualTo(2));
            Assert.That(restoredOutput.Inventory.GetCount(flour), Is.EqualTo(2));
            Assert.That(restored.IsProcessing, Is.False);
        }

        [Test]
        public void BlockedProductionSurvivesLoadAndCompletesOnlyWhenSpaceIsAvailable()
        {
            var input = Store("mill-input", 10, wheat, 2);
            var output = Store("mill-output", 2);
            var machine = Machine("mill", input, output);
            machine.Tick(2f);
            output.Inventory.TryAdd(flour, 2);
            machine.Tick(3f);
            Assert.That(machine.OutputBlocked, Is.True);
            string path = Path.Combine(testFolder, "blocked.json");
            JsonSaveStore.Save(path, new GameSaveData
            {
                inventories = new[] { input.Inventory.CreateSnapshot(input.StableId), output.Inventory.CreateSnapshot(output.StableId) },
                production = new[] { machine.CaptureState() }
            });
            Assert.That(JsonSaveStore.TryLoad(path, out var loaded, out var error), Is.True, error);
            var restoredInput = Store("mill-input", 10);
            var restoredOutput = Store("mill-output", 2);
            restoredInput.Inventory.TryLoadSnapshot(loaded.inventories[0], Catalog());
            restoredOutput.Inventory.TryLoadSnapshot(loaded.inventories[1], Catalog());
            var restored = Machine("mill", restoredInput, restoredOutput);
            Assert.That(restored.TryRestoreState(loaded.production[0]), Is.True);
            restored.Tick(0f);
            Assert.That(restored.OutputBlocked, Is.True);
            Assert.That(restoredInput.Inventory.GetCount(wheat), Is.EqualTo(1));
            Assert.That(restoredOutput.Inventory.GetCount(flour), Is.EqualTo(2));
            restoredOutput.Inventory.TryRemove(flour, 2);
            restored.Tick(0f);
            Assert.That(restored.IsProcessing, Is.False);
            Assert.That(restoredOutput.Inventory.GetCount(flour), Is.EqualTo(2));
            Assert.That(restoredInput.Inventory.GetCount(wheat), Is.EqualTo(1));
        }

        private ItemDefinition Item(string id, int price, int stackSize = 20)
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            created.Add(item);
            item.Configure(id, id, price, stackSize);
            return item;
        }

        private IReadOnlyDictionary<string, ItemDefinition> Catalog() => new Dictionary<string, ItemDefinition>
        { { wheat.Id, wheat }, { egg.Id, egg }, { flour.Id, flour } };

        private InventoryStore Store(string id, int capacity, ItemDefinition item = null, int quantity = 0)
        {
            var obj = new GameObject(id);
            created.Add(obj);
            var store = obj.AddComponent<InventoryStore>();
            store.Configure(id, capacity, item, quantity);
            return store;
        }

        private ProductionMachine Machine(string id, InventoryStore input, InventoryStore output)
        {
            var recipe = ScriptableObject.CreateInstance<RecipeDefinition>();
            created.Add(recipe);
            recipe.Configure("wheat-flour", new[] { new RecipeIngredient(wheat, 1) }, flour, 2, 5f);
            var obj = new GameObject(id);
            created.Add(obj);
            var machine = obj.AddComponent<ProductionMachine>();
            machine.Configure(id, recipe, input, output);
            return machine;
        }
    }
}
