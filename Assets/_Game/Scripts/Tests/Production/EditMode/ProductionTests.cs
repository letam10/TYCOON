using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace TYCOON.Tests
{
    public sealed class ProductionTests
    {
        private readonly List<Object> owned = new List<Object>();
        private ItemDefinition wheat, egg, flour;
        private InventoryStore input, output;
        private RecipeDefinition recipe;
        private ProductionMachine machine;

        private T Own<T>(T obj) where T : Object { owned.Add(obj); return obj; }
        private ItemDefinition Item(string id)
        {
            var item = Own(ScriptableObject.CreateInstance<ItemDefinition>());
            item.Configure(id, id, 5);
            return item;
        }
        private InventoryStore Store(string id, int size)
        {
            var store = Own(new GameObject(id)).AddComponent<InventoryStore>();
            store.Configure(id, size);
            return store;
        }

        [SetUp]
        public void Setup()
        {
            wheat = Item("wheat"); egg = Item("egg"); flour = Item("flour");
            input = Store("input", 20); output = Store("output", 5);
            recipe = Own(ScriptableObject.CreateInstance<RecipeDefinition>());
            recipe.Configure("test_recipe", new[] {new RecipeIngredient(wheat, 2), new RecipeIngredient(egg, 1)}, flour, 1, 4f);
            machine = Own(new GameObject("machine")).AddComponent<ProductionMachine>();
            machine.Configure("mill", recipe, input, output);
        }
        [TearDown]
        public void Cleanup()
        {
            for (int i = owned.Count - 1; i >= 0; i--) Object.DestroyImmediate(owned[i]);
            owned.Clear();
        }

        [Test]
        public void MissingIngredientDoesNotConsumeAnotherIngredient()
        {
            input.Inventory.TryAdd(wheat, 4);
            machine.Tick(10f);
            Assert.That(input.Inventory.GetCount(wheat), Is.EqualTo(4));
            Assert.That(output.Inventory.TotalCount, Is.Zero);
            Assert.That(machine.IsProcessing, Is.False);
        }
        [Test]
        public void FullOutputDoesNotConsumeInput()
        {
            input.Inventory.TryAdd(wheat, 2); input.Inventory.TryAdd(egg, 1);
            output.Inventory.TryAdd(flour, 5);
            machine.Tick(10f);
            Assert.That(input.Inventory.TotalCount, Is.EqualTo(3));
            Assert.That(machine.IsProcessing, Is.False);
        }
        [Test]
        public void OutputThatFillsMidCyclePreservesPendingBatchUntilSpaceReturns()
        {
            input.Inventory.TryAdd(wheat, 2); input.Inventory.TryAdd(egg, 1);
            machine.Tick(1f);
            Assert.That(input.Inventory.TotalCount, Is.Zero);
            output.Inventory.TryAdd(flour, 5);
            machine.Tick(10f);
            Assert.That(machine.OutputBlocked, Is.True);
            Assert.That(output.Inventory.GetCount(flour), Is.EqualTo(5));
            output.Inventory.TryRemove(flour, 1);
            machine.Tick(0f);
            Assert.That(output.Inventory.GetCount(flour), Is.EqualTo(5));
            Assert.That(machine.IsProcessing, Is.False);
            machine.Tick(10f);
            Assert.That(output.Inventory.GetCount(flour), Is.EqualTo(5));
        }
        [Test]
        public void RestoredMidCycleCompletesWithoutConsumingIngredientsTwice()
        {
            input.Inventory.TryAdd(wheat, 4); input.Inventory.TryAdd(egg, 2);
            machine.Tick(2f);
            var state = machine.CaptureState();
            var resumed = Own(new GameObject("resumed")).AddComponent<ProductionMachine>();
            resumed.Configure("mill", recipe, input, output);
            Assert.That(resumed.TryRestoreState(state), Is.True);
            resumed.Tick(2f);
            Assert.That(input.Inventory.GetCount(wheat), Is.EqualTo(2));
            Assert.That(input.Inventory.GetCount(egg), Is.EqualTo(1));
            Assert.That(output.Inventory.GetCount(flour), Is.EqualTo(1));
        }
        [Test]
        public void HarvestDoesNotBankProductionWhileStorageIsFull()
        {
            var node = Own(new GameObject("plot")).AddComponent<HarvestNode>();
            node.Configure("plot", wheat, 6f, 2, 2);
            node.Tick(60f);
            Assert.That(node.TryHarvest(input.Inventory), Is.True);
            node.Tick(0.01f);
            Assert.That(node.AvailableCount, Is.EqualTo(1));
            node.Tick(5.99f);
            Assert.That(node.AvailableCount, Is.EqualTo(2));
        }
        [Test]
        public void FullCarryCannotDuplicateHarvest()
        {
            var node = Own(new GameObject("plot")).AddComponent<HarvestNode>();
            node.Configure("plot", wheat, 6f, 2, 2);
            var carry = new ItemInventory(1);
            Assert.That(node.TryHarvest(carry), Is.True);
            Assert.That(node.TryHarvest(carry), Is.False);
            Assert.That(carry.TotalCount + node.AvailableCount, Is.EqualTo(2));
        }
        [Test]
        public void VisualStacksClearAndReuseObjectsAcrossQuantityChanges()
        {
            var visualPrefab = Own(new GameObject("item visual"));
            var visibleItem = Own(ScriptableObject.CreateInstance<ItemDefinition>());
            visibleItem.Configure("visual", "visual", 5, prefab: visualPrefab);
            var store = Store("visual storage", 20);
            var root = Own(new GameObject("stack root"));
            var view = root.AddComponent<InventoryStackView>();
            view.Configure(store);
            Assert.That(view.VisibleCount, Is.Zero);
            store.Inventory.TryAdd(visibleItem, 5);
            Assert.That(view.VisibleCount, Is.EqualTo(5));
            store.Inventory.TryAdd(visibleItem, 15);
            Assert.That(view.VisibleCount, Is.EqualTo(12));
            int pooledCount = root.transform.childCount;
            for (int i = 0; i < 20; i++)
            {
                store.Inventory.TryRemove(visibleItem, 20);
                Assert.That(view.VisibleCount, Is.Zero);
                store.Inventory.TryAdd(visibleItem, 20);
            }
            Assert.That(root.transform.childCount, Is.EqualTo(pooledCount));
        }
    }
}
