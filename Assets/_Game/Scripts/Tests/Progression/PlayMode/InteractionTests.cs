using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TYCOON.Tests
{
    public sealed class InteractionTests
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
        public void HarvestCannotOverflowPlayerCarryOrLoseProduce()
        {
            ItemDefinition carrot = Item("carrot");
            PlayerInteractor player = Player(1);
            HarvestNode node = Component<HarvestNode>();
            node.Configure("farm.carrot", carrot, 5f, 2, 2);
            HarvestInteractionTarget target = node.gameObject.AddComponent<HarvestInteractionTarget>();
            target.Configure(node);
            Assert.That(target.TryInteract(player), Is.True);
            Assert.That(target.TryInteract(player), Is.False);
            Assert.That(player.Carry.Inventory.TotalCount, Is.EqualTo(1));
            Assert.That(node.AvailableCount, Is.EqualTo(1));
        }

        [Test]
        public void FullStorageDoesNotRemoveCarriedItemAndEmptyCarryCanWithdraw()
        {
            ItemDefinition carrot = Item("carrot");
            PlayerInteractor player = Player(2);
            InventoryStore store = Component<InventoryStore>();
            store.Configure("store.test", 1, carrot, 1);
            StorageInteractionTarget target = store.gameObject.AddComponent<StorageInteractionTarget>();
            target.Configure(store, carrot);
            Assert.That(player.Carry.Inventory.TryAdd(carrot, 1), Is.True);
            Assert.That(target.TryInteract(player), Is.False);
            Assert.That(player.Carry.Inventory.GetCount(carrot), Is.EqualTo(1));
            Assert.That(store.Inventory.GetCount(carrot), Is.EqualTo(1));
            player.Carry.Inventory.TryRemove(carrot, 1);
            Assert.That(target.TryInteract(player), Is.True);
            Assert.That(store.Inventory.TotalCount, Is.Zero);
            Assert.That(player.Carry.Inventory.TotalCount, Is.EqualTo(1));
        }

        [Test]
        public void StorageDepositMovesExactlyOneItem()
        {
            ItemDefinition carrot = Item("carrot");
            PlayerInteractor player = Player(3);
            player.Carry.Inventory.TryAdd(carrot, 2);
            InventoryStore store = Component<InventoryStore>();
            store.Configure("store.deposit", 5);
            StorageInteractionTarget target = store.gameObject.AddComponent<StorageInteractionTarget>();
            target.Configure(store);
            Assert.That(target.TryInteract(player), Is.True);
            Assert.That(store.Inventory.GetCount(carrot), Is.EqualTo(1));
            Assert.That(player.Carry.Inventory.GetCount(carrot), Is.EqualTo(1));
        }

        [Test]
        public void FullShelfRejectsRestockWithoutLosingCarriedGoods()
        {
            ItemDefinition carrot = Item("carrot");
            PlayerInteractor player = Player(2);
            player.Carry.Inventory.TryAdd(carrot, 1);
            Shelf shelf = Component<Shelf>();
            shelf.Store.Configure("shelf.test", 1, carrot, 1);
            shelf.Configure(carrot, shelf.Store);
            ShelfInteractionTarget target = shelf.gameObject.AddComponent<ShelfInteractionTarget>();
            target.Configure(shelf);
            Assert.That(target.TryInteract(player), Is.False);
            Assert.That(player.Carry.Inventory.GetCount(carrot), Is.EqualTo(1));
            Assert.That(shelf.AvailableCount, Is.EqualTo(1));
        }

        [Test]
        public void MachineInteractionLoadsInputThenTakesOutputWithoutOverflow()
        {
            ItemDefinition wheat = Item("wheat");
            ItemDefinition flour = Item("flour");
            PlayerInteractor player = Player(1);
            player.Carry.Inventory.TryAdd(wheat, 1);
            InventoryStore input = Component<InventoryStore>();
            input.Configure("machine.input", 2);
            InventoryStore output = Component<InventoryStore>();
            output.Configure("machine.output", 2, flour, 1);
            RecipeDefinition recipe = ScriptableObject.CreateInstance<RecipeDefinition>();
            created.Add(recipe);
            recipe.Configure("recipe.flour", new[] { new RecipeIngredient(wheat, 1) }, flour, 1, 2f);
            ProductionMachine machine = Component<ProductionMachine>();
            machine.Configure("machine.mill", recipe, input, output);
            MachineInteractionTarget target = machine.gameObject.AddComponent<MachineInteractionTarget>();
            target.Configure(machine);
            Assert.That(target.TryInteract(player), Is.True);
            Assert.That(input.Inventory.GetCount(wheat), Is.EqualTo(1));
            Assert.That(target.TryInteract(player), Is.True);
            output.Inventory.TryAdd(flour, 1);
            Assert.That(target.TryInteract(player), Is.False);
            Assert.That(player.Carry.Inventory.GetCount(flour), Is.EqualTo(1));
            Assert.That(output.Inventory.GetCount(flour), Is.EqualTo(1));
        }

        [Test]
        public void PlayerSelectsNearestEnabledZoneWithinRadius()
        {
            PlayerInteractor player = Player(1);
            player.transform.position = new Vector3(1000f, 0f, 1000f);
            InventoryStore nearStore = Component<InventoryStore>(); nearStore.Configure("near.store", 1);
            StorageInteractionTarget near = nearStore.gameObject.AddComponent<StorageInteractionTarget>();
            near.Configure(nearStore); near.transform.position = player.transform.position + Vector3.right;
            InventoryStore farStore = Component<InventoryStore>(); farStore.Configure("far.store", 1);
            StorageInteractionTarget far = farStore.gameObject.AddComponent<StorageInteractionTarget>();
            far.Configure(farStore); far.transform.position = player.transform.position + Vector3.right * 1.5f;
            player.RefreshTarget();
            Assert.That(player.CurrentTarget, Is.SameAs(near));
            near.enabled = false;
            player.RefreshTarget();
            Assert.That(player.CurrentTarget, Is.SameAs(far));
            far.transform.position += Vector3.right * 4f;
            player.RefreshTarget();
            Assert.That(player.CurrentTarget, Is.Null);
        }

        [Test]
        public void InteractionActionSupportsKeyboardAndGamepad()
        {
            PlayerInteractor player = Player(1);
            var bindings = new List<string>();
            foreach (var binding in player.InteractAction.bindings) bindings.Add(binding.path);
            Assert.That(bindings, Does.Contain("<Keyboard>/e"));
            Assert.That(bindings, Does.Contain("<Gamepad>/buttonSouth"));
        }

        [Test]
        public void PadUnlocksObjectOnlyForItsSessionAndRestoredOwnership()
        {
            PlayerInteractor player = Player(1);
            player.Session.Wallet.TryCreditCompletedSale("sale.pad.1", 10);
            UpgradeDefinition upgrade = ScriptableObject.CreateInstance<UpgradeDefinition>(); created.Add(upgrade);
            upgrade.Configure("area.shop", "Shop", 10);
            GameObject targetObject = new GameObject("Shop unlock target"); created.Add(targetObject);
            PurchasePad pad = Component<PurchasePad>();
            pad.Configure(upgrade, player.Session, new[] { targetObject });
            ProgressionState emptyState = player.Session.CaptureState();
            Assert.That(targetObject.activeSelf, Is.False);
            PlayerInteractor otherPlayer = Player(1);
            Assert.That(pad.TryPurchase(otherPlayer), Is.False);
            Assert.That(player.Session.Wallet.Balance, Is.EqualTo(10));
            Assert.That(pad.TryPurchase(player), Is.True);
            Assert.That(targetObject.activeSelf, Is.True);
            Assert.That(pad.TryPurchase(player), Is.False);
            Assert.That(player.Session.TryRestoreState(emptyState), Is.True);
            Assert.That(targetObject.activeSelf, Is.False);
        }

        [Test]
        public void HudReflectsActualBalanceCarriedItemsAndCapacity()
        {
            PlayerInteractor player = Player(3);
            ItemDefinition carrot = Item("carrot");
            player.Carry.Inventory.TryAdd(carrot, 2);
            player.Session.Wallet.TryCreditCompletedSale("sale.hud.1", 40);
            TycoonHud hud = Component<TycoonHud>();
            hud.Configure(player.Session, player);
            hud.RefreshView();
            Assert.That(hud.MoneyText, Is.EqualTo("$40"));
            Assert.That(hud.CarryText, Does.Contain("2 carrot"));
            Assert.That(hud.CapacityText, Does.StartWith("2 / 3"));
            player.Carry.Inventory.TryRemove(carrot, 2);
            hud.RefreshView();
            Assert.That(hud.CarryText, Is.EqualTo("Hands free"));
            Assert.That(hud.MoneyText, Is.EqualTo("$40"));
        }

        private T Component<T>() where T : Component
        {
            var owner = new GameObject(typeof(T).Name + " test"); created.Add(owner);
            return owner.AddComponent<T>();
        }

        private PlayerInteractor Player(int capacity)
        {
            GameSession session = Component<GameSession>(); session.Configure();
            PlayerInteractor player = Component<PlayerInteractor>();
            player.Carry.Configure("player." + created.Count, capacity);
            player.Configure(player.Carry, session);
            return player;
        }

        private ItemDefinition Item(string id)
        {
            ItemDefinition item = ScriptableObject.CreateInstance<ItemDefinition>(); created.Add(item);
            item.Configure(id, id, 10);
            return item;
        }
    }
}
