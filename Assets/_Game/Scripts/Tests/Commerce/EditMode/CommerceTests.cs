using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace TYCOON.Tests
{
    public sealed class CommerceTests
    {
        private readonly List<Object> created = new List<Object>();
        private ItemDefinition carrot;

        [SetUp]
        public void SetUp()
        {
            carrot = ScriptableObject.CreateInstance<ItemDefinition>();
            carrot.Configure("carrot", "Carrot", 5);
            created.Add(carrot);
        }

        [TearDown]
        public void TearDown()
        {
            for (var index = created.Count - 1; index >= 0; index--) if (created[index] != null) Object.DestroyImmediate(created[index]);
            created.Clear();
        }

        private T Component<T>(string name) where T : Component
        {
            var owner = new GameObject(name);
            created.Add(owner);
            return owner.AddComponent<T>();
        }

        private InventoryStore Store(string id, int capacity, int quantity = 0)
        {
            var store = Component<InventoryStore>(id);
            store.Configure(id, capacity, carrot, quantity);
            return store;
        }

        private Shelf Shelf(int capacity, int quantity = 0)
        {
            var shelf = Component<Shelf>("Shelf");
            shelf.Store.Configure("shelf.inventory", capacity, carrot, quantity);
            shelf.Configure(carrot, shelf.Store);
            return shelf;
        }

        private ItemInventory Cart(int quantity)
        {
            var cart = new ItemInventory(10);
            if (quantity > 0) Assert.IsTrue(cart.TryAdd(carrot, quantity));
            return cart;
        }

        [Test]
        public void RealShelfSaleConsumesGoodsAndCollectsExactlyOnce()
        {
            var shelf = Shelf(3, 3);
            var cart = Cart(0);
            var ledger = new CheckoutLedger();
            var wallet = new Wallet();
            Assert.IsTrue(shelf.TryTake(cart, 2));
            Assert.AreEqual(3, shelf.AvailableCount + cart.TotalCount);
            Assert.IsTrue(ledger.TryCompleteSale("sale.first", cart, out var revenue));
            Assert.AreEqual(10, revenue);
            Assert.AreEqual(1, shelf.AvailableCount);
            Assert.AreEqual(0, cart.TotalCount);
            Assert.AreEqual(0, wallet.Balance);
            Assert.AreEqual(10, ledger.PendingRevenue);
            Assert.AreEqual(10, ledger.CollectMoney(wallet));
            Assert.AreEqual(10, wallet.Balance);
            Assert.AreEqual(0, ledger.CollectMoney(wallet));
            Assert.AreEqual(0, ledger.PendingRevenue);
        }

        [Test]
        public void StockoutDoesNotCreateCartGoodsOrRevenue()
        {
            var shelf = Shelf(2);
            var cart = Cart(0);
            var ledger = new CheckoutLedger();
            Assert.IsFalse(shelf.TryTake(cart));
            Assert.IsFalse(ledger.TryCompleteSale("sale.empty", cart, out var amount));
            Assert.AreEqual(0, amount);
            Assert.AreEqual(0, ledger.PendingRevenue);
            Assert.AreEqual(0, ledger.CompletedSaleCount);
        }

        [Test]
        public void DuplicateSaleDoesNotConsumeAnotherCart()
        {
            var ledger = new CheckoutLedger();
            Assert.IsTrue(ledger.TryCompleteSale("sale.same", Cart(1), out _));
            var duplicateCart = Cart(1);
            Assert.IsFalse(ledger.TryCompleteSale("sale.same", duplicateCart, out var amount));
            Assert.AreEqual(0, amount);
            Assert.AreEqual(1, duplicateCart.TotalCount);
            Assert.AreEqual(5, ledger.PendingRevenue);
        }

        [Test]
        public void InvalidSaleIdLeavesGoodsUntouched()
        {
            var ledger = new CheckoutLedger();
            var cart = Cart(1);
            Assert.IsFalse(ledger.TryCompleteSale("sale with space", cart, out _));
            Assert.AreEqual(1, cart.TotalCount);
        }

        [Test]
        public void SaleConsumesSplitStacksBeforeOneInventoryChangedEvent()
        {
            carrot.Configure("carrot", "Carrot", 5, 1);
            var cart = Cart(2);
            var changes = new List<int>();
            cart.Changed += () => changes.Add(cart.TotalCount);
            var ledger = new CheckoutLedger();
            Assert.IsTrue(ledger.TryCompleteSale("sale.atomic", cart, out var amount));
            CollectionAssert.AreEqual(new[] { 0 }, changes);
            Assert.AreEqual(10, amount);
            Assert.AreEqual(10, ledger.PendingRevenue);
        }

        [Test]
        public void ReservationReturnsCartAfterQueueCancellationWithoutLosingGoods()
        {
            var shelf = Shelf(2, 2);
            var customer = Component<CustomerAgent>("Customer");
            var checkout = Component<CheckoutStation>("Checkout");
            checkout.Configure(null, null);
            Assert.IsTrue(shelf.TryTakeForCustomer(customer));
            Assert.IsTrue(checkout.TryJoin(customer));
            Assert.AreEqual(0, shelf.RestockCapacity);
            Assert.IsFalse(shelf.TryStock(Cart(1)));
            checkout.Leave(customer);
            Assert.IsTrue(shelf.TryReturnFromCustomer(customer));
            Assert.AreEqual(2, shelf.AvailableCount);
            Assert.AreEqual(0, customer.Cart.TotalCount);
            Assert.AreEqual(0, checkout.QueueCount);
            Assert.AreEqual(0, checkout.PendingRevenue);
        }

        [Test]
        public void QueueLimitsCapacityAndReordersAfterHeadLeaves()
        {
            var checkout = Component<CheckoutStation>("Checkout");
            checkout.Configure(null, null, 2);
            var first = Component<CustomerAgent>("First");
            var second = Component<CustomerAgent>("Second");
            var third = Component<CustomerAgent>("Third");
            foreach (var customer in new[] { first, second, third }) customer.Cart.TryAdd(carrot, 1);
            Assert.IsTrue(checkout.TryJoin(first));
            Assert.IsTrue(checkout.TryJoin(first));
            Assert.IsTrue(checkout.TryJoin(second));
            Assert.IsFalse(checkout.TryJoin(third));
            Assert.AreEqual(2, checkout.QueueCount);
            checkout.Leave(first);
            Assert.AreEqual(0, checkout.QueueIndex(second));
            Assert.IsTrue(checkout.TryJoin(third));
            Assert.AreEqual(1, checkout.QueueIndex(third));
            Assert.IsFalse(checkout.TryServeNext());
            Assert.AreEqual(0, checkout.PendingRevenue);
        }

        [Test]
        public void CashierReservationAllowsOneWorkerAndCanBeReleased()
        {
            var checkout = Component<CheckoutStation>("Checkout");
            var first = Component<WorkerAgent>("First");
            var second = Component<WorkerAgent>("Second");
            Assert.IsTrue(checkout.TryAssignCashier(first));
            Assert.IsFalse(checkout.TryAssignCashier(second));
            checkout.ReleaseCashier(second);
            Assert.AreSame(first, checkout.Cashier);
            checkout.ReleaseCashier(first);
            Assert.IsTrue(checkout.TryAssignCashier(second));
        }

        [Test]
        public void FarmersReserveHarvestAndTransferGoodsWithoutDuplication()
        {
            var harvest = Component<HarvestNode>("Harvest");
            harvest.Configure("harvest.carrot", carrot, 20f, 3, 3);
            var storage = Store("storage", 10);
            var first = Component<WorkerAgent>("FarmerOne");
            var second = Component<WorkerAgent>("FarmerTwo");
            var firstJob = first.gameObject.AddComponent<FarmerJob>();
            var secondJob = second.gameObject.AddComponent<FarmerJob>();
            firstJob.Configure(new[] { harvest }, storage);
            secondJob.Configure(new[] { harvest }, storage);
            first.Configure(firstJob, 3, "worker.first");
            second.Configure(secondJob, 3, "worker.second");
            Assert.IsTrue(firstJob.TryGetDestination(out _));
            Assert.IsFalse(secondJob.TryGetDestination(out _));
            firstJob.CancelWork();
            Assert.IsTrue(secondJob.TryGetDestination(out _));
            Assert.IsTrue(secondJob.Perform());
            Assert.AreEqual(0, harvest.AvailableCount);
            Assert.AreEqual(3, second.Inventory.TotalCount);
            Assert.IsTrue(secondJob.TryGetDestination(out _));
            Assert.IsTrue(secondJob.Perform());
            Assert.AreEqual(3, storage.Inventory.TotalCount);
            Assert.AreEqual(0, second.Inventory.TotalCount);
        }

        [Test]
        public void FarmerRetainsGoodsWhenStorageFullAndRecoversAfterSpaceOpens()
        {
            var harvest = Component<HarvestNode>("Harvest");
            harvest.Configure("harvest", carrot, 20f, 2, 2);
            var storage = Store("storage", 2);
            var worker = Component<WorkerAgent>("Farmer");
            var job = worker.gameObject.AddComponent<FarmerJob>();
            job.Configure(new[] { harvest }, storage);
            worker.Configure(job, 2);
            Assert.IsTrue(job.TryGetDestination(out _));
            Assert.IsTrue(job.Perform());
            storage.Inventory.TryAdd(carrot, 2);
            Assert.IsFalse(job.TryGetDestination(out _));
            Assert.AreEqual(2, worker.Inventory.TotalCount);
            Assert.AreEqual(4, worker.Inventory.TotalCount + storage.Inventory.TotalCount);
            storage.Inventory.TryRemove(carrot, 2);
            Assert.IsTrue(job.TryGetDestination(out _));
            Assert.IsTrue(job.Perform());
            Assert.AreEqual(0, worker.Inventory.TotalCount);
            Assert.AreEqual(2, storage.Inventory.TotalCount);
        }

        [Test]
        public void RestockerMovesOnlyShelfCapacityAndConservesGoods()
        {
            var storage = Store("storage", 8, 5);
            var shelf = Shelf(2);
            var worker = Component<WorkerAgent>("Restocker");
            var job = worker.gameObject.AddComponent<RestockerJob>();
            job.Configure(storage, new[] { shelf });
            worker.Configure(job, 6);
            Assert.IsTrue(job.TryGetDestination(out _));
            Assert.IsTrue(job.Perform());
            Assert.AreEqual(3, storage.Inventory.TotalCount);
            Assert.AreEqual(2, worker.Inventory.TotalCount);
            Assert.IsTrue(job.TryGetDestination(out _));
            Assert.IsTrue(job.Perform());
            Assert.AreEqual(2, shelf.AvailableCount);
            Assert.AreEqual(0, worker.Inventory.TotalCount);
            Assert.AreEqual(5, storage.Inventory.TotalCount + shelf.AvailableCount);
        }

        [Test]
        public void RestockerRestoredCarryResumesDeliveryWithoutTakingNewStock()
        {
            var storage = Store("storage", 10, 4);
            var shelf = Shelf(4);
            var worker = Component<WorkerAgent>("Restocker");
            var job = worker.gameObject.AddComponent<RestockerJob>();
            job.Configure(storage, new[] { shelf });
            worker.Configure(job, 6, "worker.saved");
            var saved = Cart(2).CreateSnapshot("worker.saved");
            Assert.IsTrue(worker.TryRestoreInventory(saved, new Dictionary<string, ItemDefinition> { [carrot.Id] = carrot }));
            Assert.IsTrue(job.TryGetDestination(out _));
            Assert.IsTrue(job.Perform());
            Assert.AreEqual(4, storage.Inventory.TotalCount);
            Assert.AreEqual(2, shelf.AvailableCount);
            Assert.AreEqual(0, worker.Inventory.TotalCount);
        }

        [Test]
        public void PendingRevenueSurvivesJsonRoundtripAndIsCollectedOnce()
        {
            var ledger = new CheckoutLedger();
            ledger.TryCompleteSale("sale.saved", Cart(2), out _);
            var json = JsonUtility.ToJson(ledger.CreateSnapshot());
            var restored = new CheckoutLedger();
            Assert.IsTrue(restored.TryLoadSnapshot(JsonUtility.FromJson<CheckoutLedgerSnapshot>(json)));
            Assert.AreEqual(10, restored.PendingRevenue);
            var wallet = new Wallet();
            Assert.AreEqual(10, restored.CollectMoney(wallet));
            Assert.AreEqual(0, restored.CollectMoney(wallet));
            Assert.AreEqual(10, wallet.Balance);
            Assert.IsFalse(restored.TryCompleteSale("sale.saved", Cart(1), out _));
        }

        [Test]
        public void ProcessorWorkerFeedsRecipeAndDeliversRealOutput()
        {
            var flour = ScriptableObject.CreateInstance<ItemDefinition>();
            flour.Configure("flour", "Flour", 8, category: ItemCategory.ProcessedProduct);
            created.Add(flour);
            var recipe = ScriptableObject.CreateInstance<RecipeDefinition>();
            recipe.Configure("recipe.flour", new[] { new RecipeIngredient(carrot, 2) }, flour, 1, 1f);
            created.Add(recipe);
            var storage = Store("storage", 10, 4);
            var input = Store("mill.input", 3);
            var output = Store("mill.output", 3);
            var machine = Component<ProductionMachine>("Mill");
            machine.Configure("mill", recipe, input, output);
            var worker = Component<WorkerAgent>("Processor");
            var job = worker.gameObject.AddComponent<ProcessorJob>();
            job.Configure(machine, storage);
            worker.Configure(job, 6);
            Assert.IsTrue(job.TryGetDestination(out _));
            Assert.IsTrue(job.Perform());
            Assert.AreEqual(2, worker.Inventory.GetCount(carrot));
            Assert.IsTrue(job.TryGetDestination(out _));
            Assert.IsTrue(job.Perform());
            Assert.AreEqual(2, input.Inventory.GetCount(carrot));
            machine.Tick(1f);
            Assert.AreEqual(1, output.Inventory.GetCount(flour));
            Assert.IsTrue(job.TryGetDestination(out _));
            Assert.IsTrue(job.Perform());
            Assert.AreEqual(1, worker.Inventory.GetCount(flour));
            Assert.IsTrue(job.TryGetDestination(out _));
            Assert.IsTrue(job.Perform());
            Assert.AreEqual(2, storage.Inventory.GetCount(carrot));
            Assert.AreEqual(1, storage.Inventory.GetCount(flour));
            Assert.AreEqual(0, worker.Inventory.TotalCount);
            Assert.AreEqual(0, output.Inventory.TotalCount);
        }

        [Test]
        public void AlreadyCreditedReceiptCannotCreditWalletAgain()
        {
            var ledger = new CheckoutLedger();
            ledger.TryCompleteSale("sale.already", Cart(1), out _);
            var wallet = new Wallet();
            wallet.TryCreditCompletedSale("sale.already", 5);
            Assert.AreEqual(0, ledger.CollectMoney(wallet));
            Assert.AreEqual(5, wallet.Balance);
            Assert.AreEqual(0, ledger.PendingRevenue);
        }

        [Test]
        public void WalletOverflowKeepsDrawerReceiptUntilCollectionCanSucceed()
        {
            var ledger = new CheckoutLedger();
            ledger.TryCompleteSale("sale.pending", Cart(1), out _);
            var wallet = new Wallet(int.MaxValue - 1);
            Assert.AreEqual(0, ledger.CollectMoney(wallet));
            Assert.AreEqual(5, ledger.PendingRevenue);
            Assert.IsTrue(wallet.TrySpend(5));
            Assert.AreEqual(5, ledger.CollectMoney(wallet));
            Assert.AreEqual(0, ledger.PendingRevenue);
            Assert.AreEqual(int.MaxValue - 1, wallet.Balance);
        }

        [Test]
        public void InvalidSnapshotDoesNotReplaceCurrentDrawer()
        {
            var ledger = new CheckoutLedger();
            ledger.TryCompleteSale("sale.current", Cart(1), out _);
            var invalid = new CheckoutLedgerSnapshot
            {
                completedSaleIds = new[] { "sale.current" },
                pendingReceipts = new[] { new CheckoutReceiptSnapshot { transactionId = "sale.unknown", amount = 5 } }
            };
            Assert.IsFalse(ledger.TryLoadSnapshot(invalid));
            Assert.AreEqual(5, ledger.PendingRevenue);
            Assert.AreEqual(1, ledger.CompletedSaleCount);
        }

        [Test]
        public void DuplicateSnapshotReceiptIsRejected()
        {
            var snapshot = new CheckoutLedgerSnapshot
            {
                completedSaleIds = new[] { "sale.duplicate" },
                pendingReceipts = new[]
                {
                    new CheckoutReceiptSnapshot { transactionId = "sale.duplicate", amount = 5 },
                    new CheckoutReceiptSnapshot { transactionId = "sale.duplicate", amount = 5 }
                }
            };
            Assert.IsFalse(CheckoutLedger.TryValidateSnapshot(snapshot));
        }
    }
}
