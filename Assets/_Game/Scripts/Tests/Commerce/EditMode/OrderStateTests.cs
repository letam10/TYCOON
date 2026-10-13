using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace TYCOON.Tests
{
    public sealed class OrderStateTests
    {
        private readonly List<ItemDefinition> assets = new List<ItemDefinition>();
        private ItemDefinition wheat;
        private ItemDefinition flour;

        [SetUp]
        public void SetUp()
        {
            wheat = Item("wheat", 10);
            flour = Item("flour", 14);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in assets) UnityEngine.Object.DestroyImmediate(asset);
            assets.Clear();
        }

        [Test]
        public void PartialDeliveryMustCompleteBeforePaymentAndCannotBeDuplicated()
        {
            var order = NewOrder(2, 1);
            Assert.IsTrue(order.TryDeliver(wheat, 1));
            Assert.AreEqual(1, order.GetRemaining("wheat"));
            Assert.IsFalse(order.TryCompletePayment());
            Assert.IsFalse(order.TryDeliver(wheat, 2));
            Assert.IsTrue(order.TryDeliver(wheat, 1));
            Assert.IsTrue(order.TryDeliver(flour, 1));
            Assert.IsTrue(order.TryCompletePayment());
            Assert.IsFalse(order.TryCompletePayment());
            Assert.IsFalse(order.TryDeliver(wheat, 1));
            Assert.AreEqual(OrderStatus.Paid, order.Status);
        }

        [Test]
        public void PatienceFailureIsTerminalAndDoesNotCreatePayment()
        {
            var order = NewOrder(1, 0);
            Assert.IsFalse(order.Tick(89f));
            Assert.IsTrue(order.Tick(1f));
            Assert.AreEqual(OrderStatus.Failed, order.Status);
            Assert.IsFalse(order.Fail());
            Assert.IsFalse(order.TryDeliver(wheat, 1));
            Assert.IsFalse(order.TryCompletePayment());
        }

        [Test]
        public void SnapshotRestoresDeliveredLinesAndLockedPrice()
        {
            var original = NewOrder(2, 1);
            Assert.IsTrue(original.TryDeliver(wheat, 1));
            var snapshot = original.Capture();
            var definitions = new Dictionary<string, ItemDefinition>
            {
                [wheat.Id] = wheat,
                [flour.Id] = flour
            };

            Assert.IsTrue(OrderState.TryRestore(snapshot, definitions, out var restored));
            Assert.AreEqual(snapshot.lockedTotalPrice, restored.LockedTotalPrice);
            Assert.AreEqual(1, restored.GetDelivered(wheat.Id));
            Assert.AreEqual(1, restored.GetRemaining(flour.Id));
            Assert.AreEqual(OrderStatus.Open, restored.Status);
        }

        private OrderState NewOrder(int wheatQuantity, int flourQuantity)
        {
            var lines = new List<OrderLineSpec> { new OrderLineSpec(wheat, wheatQuantity) };
            if (flourQuantity > 0) lines.Add(new OrderLineSpec(flour, flourQuantity));
            return new OrderState("order.1", "customer.1", "checkout.main", lines);
        }

        private ItemDefinition Item(string id, int price)
        {
            var asset = ScriptableObject.CreateInstance<ItemDefinition>();
            asset.Configure(id, id, price);
            assets.Add(asset);
            return asset;
        }
    }
}
