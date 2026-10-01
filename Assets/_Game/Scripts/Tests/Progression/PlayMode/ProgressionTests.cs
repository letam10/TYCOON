using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TYCOON.Tests
{
    public sealed class ProgressionTests
    {
        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void CleanUp()
        {
            for (int i = created.Count - 1; i >= 0; i--)
                if (created[i] != null) Object.DestroyImmediate(created[i]);
            created.Clear();
        }

        [Test]
        public void NewSessionStartsAtFarmWithoutFreeMoney()
        {
            GameSession session = Session();
            Assert.That(session.Wallet.Balance, Is.Zero);
            Assert.That(session.Stage, Is.EqualTo(BusinessStage.Farm));
            Assert.That(session.CaptureState().unlockedIds, Is.Empty);
        }

        [Test]
        public void InsufficientMoneyDoesNotSpendOrUnlock()
        {
            GameSession session = Session(99);
            UpgradeDefinition upgrade = Upgrade("worker.farmer", 100, UpgradeKind.Employee);
            Assert.That(session.TryPurchase(upgrade, out PurchaseFailure failure), Is.False);
            Assert.That(failure, Is.EqualTo(PurchaseFailure.InsufficientMoney));
            Assert.That(session.Wallet.Balance, Is.EqualTo(99));
            Assert.That(session.HasEmployee(upgrade.StableId), Is.False);
            Assert.That(session.GetUpgradeLevel(upgrade.StableId), Is.Zero);
        }

        [Test]
        public void ExactBalanceBuysOnceAndDuplicateDoesNotChargeAgain()
        {
            GameSession session = Session(100);
            UpgradeDefinition upgrade = Upgrade("area.shop", 100);
            Assert.That(session.TryPurchase(upgrade, out _), Is.True);
            Assert.That(session.Wallet.Balance, Is.Zero);
            Assert.That(session.HasUnlock(upgrade.StableId), Is.True);
            Assert.That(session.TryPurchase(upgrade, out PurchaseFailure failure), Is.False);
            Assert.That(failure, Is.EqualTo(PurchaseFailure.AlreadyPurchased));
            Assert.That(session.Wallet.Balance, Is.Zero);
            Assert.That(session.GetUpgradeLevel(upgrade.StableId), Is.EqualTo(1));
        }

        [Test]
        public void WalletCallbackCannotReenterAndChargePurchaseTwice()
        {
            GameSession session = Session(30);
            UpgradeDefinition upgrade = Upgrade("area.field", 10);
            PurchaseFailure repeatedFailure = PurchaseFailure.None;
            session.Wallet.Changed += () => Assert.That(session.TryPurchase(upgrade, out repeatedFailure), Is.False);
            Assert.That(session.TryPurchase(upgrade, out _), Is.True);
            Assert.That(repeatedFailure, Is.EqualTo(PurchaseFailure.Busy));
            Assert.That(session.Wallet.Balance, Is.EqualTo(20));
            Assert.That(session.GetUpgradeLevel(upgrade.StableId), Is.EqualTo(1));
        }

        [Test]
        public void StageAndPrerequisiteGuardsRequirePreviousPurchase()
        {
            GameSession session = Session(30);
            UpgradeDefinition shop = Upgrade("area.shop", 10);
            shop.Configure("area.shop", "Farm shop", 10, unlockedStage: BusinessStage.FarmShop);
            UpgradeDefinition processor = Upgrade("area.processing", 20);
            processor.Configure("area.processing", "Processing", 20, BusinessStage.FarmShop, BusinessStage.Processing,
                prerequisiteIds: new[] { "area.shop" });
            Assert.That(session.TryPurchase(processor, out PurchaseFailure stageFailure), Is.False);
            Assert.That(stageFailure, Is.EqualTo(PurchaseFailure.StageLocked));
            Assert.That(session.Wallet.Balance, Is.EqualTo(30));
            Assert.That(session.TryPurchase(shop, out _), Is.True);
            Assert.That(session.TryPurchase(processor, out _), Is.True);
            Assert.That(session.Stage, Is.EqualTo(BusinessStage.Processing));
            Assert.That(session.Wallet.Balance, Is.Zero);
        }

        [Test]
        public void MissingPrerequisiteBlocksEvenAtRequiredStage()
        {
            GameSession session = Session(100);
            UpgradeDefinition upgrade = Upgrade("worker.restocker", 20, UpgradeKind.Employee);
            upgrade.Configure("worker.restocker", "Restocker", 20, prerequisiteIds: new[] { "area.shop" });
            Assert.That(session.TryPurchase(upgrade, out PurchaseFailure failure), Is.False);
            Assert.That(failure, Is.EqualTo(PurchaseFailure.PrerequisiteMissing));
            Assert.That(session.Wallet.Balance, Is.EqualTo(100));
        }

        [Test]
        public void EmployeeAndMachineLevelsAreSavedSeparatelyAndCostsIncrease()
        {
            GameSession session = Session(65);
            UpgradeDefinition employee = Upgrade("worker.farmer", 5, UpgradeKind.Employee);
            UpgradeDefinition machine = Upgrade("machine.mill.speed", 10, UpgradeKind.MachineUpgrade);
            machine.Configure("machine.mill.speed", "Mill speed", 10, kind: UpgradeKind.MachineUpgrade, maxLevel: 3);
            Assert.That(session.TryPurchase(employee, out _), Is.True);
            Assert.That(session.HasEmployee(employee.StableId), Is.True);
            Assert.That(session.HasUnlock(employee.StableId), Is.False);
            for (int i = 0; i < 3; i++) Assert.That(session.TryPurchase(machine, out _), Is.True);
            Assert.That(session.GetUpgradeLevel(machine.StableId), Is.EqualTo(3));
            Assert.That(session.Wallet.Balance, Is.Zero);
            Assert.That(session.TryPurchase(machine, out PurchaseFailure failure), Is.False);
            Assert.That(failure, Is.EqualTo(PurchaseFailure.AlreadyPurchased));
        }

        [Test]
        public void JsonProgressionRoundTripPreservesStageEmployeesAndLevels()
        {
            GameSession session = Session(30);
            UpgradeDefinition shop = Upgrade("area.shop", 10);
            shop.Configure("area.shop", "Shop", 10, unlockedStage: BusinessStage.FarmShop);
            UpgradeDefinition worker = Upgrade("worker.farmer", 20, UpgradeKind.Employee);
            Assert.That(session.TryPurchase(shop, out _), Is.True);
            Assert.That(session.TryPurchase(worker, out _), Is.True);
            ProgressionState state = JsonUtility.FromJson<ProgressionState>(JsonUtility.ToJson(session.CaptureState()));
            GameSession restored = Session();
            Assert.That(restored.TryRestoreState(state), Is.True);
            Assert.That(restored.Stage, Is.EqualTo(BusinessStage.FarmShop));
            Assert.That(restored.HasUnlock("area.shop"), Is.True);
            Assert.That(restored.HasEmployee("worker.farmer"), Is.True);
            Assert.That(restored.GetUpgradeLevel("worker.farmer"), Is.EqualTo(1));
            Assert.That(restored.TryPurchase(worker, out PurchaseFailure failure), Is.False);
            Assert.That(failure, Is.EqualTo(PurchaseFailure.AlreadyPurchased));
        }

        [Test]
        public void ProgressionRestorePreservesWalletAndCompletedSaleIds()
        {
            GameSession session = Session(100);
            Assert.That(session.Wallet.TryCreditCompletedSale("sale.test.1", 50), Is.True);
            Assert.That(session.TryRestoreState(session.CaptureState()), Is.True);
            Assert.That(session.Wallet.Balance, Is.EqualTo(150));
            Assert.That(session.Wallet.TryCreditCompletedSale("sale.test.1", 50), Is.False);
        }

        [Test]
        public void InvalidRestoreDoesNotPartiallyMutateExistingProgression()
        {
            GameSession session = Session(10);
            Assert.That(session.TryPurchase(Upgrade("area.shop", 10), out _), Is.True);
            var invalid = new ProgressionState
            {
                stage = BusinessStage.Bakery,
                unlockedIds = new[] { "area.processing", "area.processing" }
            };
            Assert.That(session.TryRestoreState(invalid), Is.False);
            Assert.That(session.Stage, Is.EqualTo(BusinessStage.Farm));
            Assert.That(session.HasUnlock("area.shop"), Is.True);
            Assert.That(session.HasUnlock("area.processing"), Is.False);
        }

        [Test]
        public void InvalidUpgradeIdsAndOverflowingLevelPricesAreRejected()
        {
            UpgradeDefinition upgrade = Upgrade("area.shop", 10);
            Assert.Throws<ArgumentException>(() => upgrade.Configure("area invalid", "Invalid", 10));
            Assert.Throws<ArgumentException>(() => upgrade.Configure("area.huge", "Huge", int.MaxValue, maxLevel: 2));
            Assert.Throws<ArgumentException>(() => upgrade.Configure("area.self", "Self", 10, prerequisiteIds: new[] { "area.self" }));
        }

        private GameSession Session(int money = 0)
        {
            var owner = new GameObject("Progression test session");
            created.Add(owner);
            GameSession session = owner.AddComponent<GameSession>();
            session.Configure(money);
            return session;
        }

        private UpgradeDefinition Upgrade(string id, int cost, UpgradeKind kind = UpgradeKind.Unlock)
        {
            UpgradeDefinition upgrade = ScriptableObject.CreateInstance<UpgradeDefinition>();
            created.Add(upgrade);
            upgrade.Configure(id, id, cost, kind: kind);
            return upgrade;
        }
    }
}
