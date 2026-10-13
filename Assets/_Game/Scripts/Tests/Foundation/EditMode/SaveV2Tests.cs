using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TYCOON.Tests
{
    public sealed class SaveV2Tests
    {
        private readonly List<ItemDefinition> assets = new List<ItemDefinition>();
        private ItemDefinition wheat;
        private string folder;
        private string v1Path;
        private string v2Path;

        [SetUp]
        public void SetUp()
        {
            wheat = ScriptableObject.CreateInstance<ItemDefinition>();
            wheat.Configure("wheat", "Wheat", 10);
            assets.Add(wheat);
            folder = Path.Combine(Application.dataPath, "..", "work", "save-v2-tests-" + Guid.NewGuid().ToString("N"));
            v1Path = Path.Combine(folder, "main.json");
            v2Path = Path.Combine(folder, "main.v2.json");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in assets) Object.DestroyImmediate(asset);
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }

        [Test]
        public void SaveV2RoundTripPreservesCustomerCartAndDeliveredOrder()
        {
            var order = new OrderState("order.1", "customer.1", "checkout.main",
                new[] { new OrderLineSpec(wheat, 2) });
            Assert.IsTrue(order.TryDeliver(wheat, 1));
            var cart = new ItemInventory(6);
            Assert.IsTrue(cart.TryAdd(wheat, 1));
            var data = new GameSaveDataV2
            {
                world = new GameSaveData(),
                orders = new[]
                {
                    new CustomerOrderSnapshot
                    {
                        customerId = "customer.1",
                        queueIndex = 0,
                        order = order.Capture(),
                        cart = cart.CreateSnapshot("customer.1.cart")
                    }
                }
            };
            var definitions = new Dictionary<string, ItemDefinition> { [wheat.Id] = wheat };

            JsonSaveV2Store.Save(v2Path, data, definitions);
            Assert.IsTrue(JsonSaveV2Store.TryLoad(v2Path, definitions, out var loaded, out var error), error);
            Assert.AreEqual(1, loaded.orders.Length);
            Assert.AreEqual("customer.1", loaded.orders[0].customerId);
            Assert.AreEqual(1, loaded.orders[0].order.lines[0].delivered);
            Assert.AreEqual(1, loaded.orders[0].cart.items[0].quantity);
        }

        [Test]
        public void SaveV2RejectsDuplicateCustomerOrOrderIdentity()
        {
            var order = new OrderState("order.1", "customer.1", "checkout.main",
                new[] { new OrderLineSpec(wheat, 1) });
            var snapshot = new CustomerOrderSnapshot
            {
                customerId = "customer.1",
                order = order.Capture(),
                cart = new ItemInventory(2).CreateSnapshot("customer.1.cart")
            };
            var data = new GameSaveDataV2
            {
                world = new GameSaveData(),
                orders = new[] { snapshot, snapshot }
            };
            var definitions = new Dictionary<string, ItemDefinition> { [wheat.Id] = wheat };

            Assert.IsFalse(GameSaveDataV2Validation.TryValidate(data, definitions, out var error));
            StringAssert.Contains("duplicate", error.ToLowerInvariant());
        }

        [Test]
        public void SaveV2UsesSeparatePathAndLeavesPrototypeFileUntouched()
        {
            Directory.CreateDirectory(folder);
            File.WriteAllText(v1Path, "prototype save remains unchanged");
            var data = new GameSaveDataV2 { world = new GameSaveData(), orders = Array.Empty<CustomerOrderSnapshot>() };
            var definitions = new Dictionary<string, ItemDefinition> { [wheat.Id] = wheat };

            JsonSaveV2Store.Save(v2Path, data, definitions);
            Assert.AreEqual("prototype save remains unchanged", File.ReadAllText(v1Path));
            Assert.IsTrue(File.Exists(v2Path));
        }
    }
}
