using UnityEngine;

namespace TYCOON
{
    public sealed class MachineInteractionTarget : InteractionTarget
    {
        [SerializeField] private ProductionMachine machine;
        public override string Prompt => machine == null || machine.Recipe == null ? "Use machine" :
            "Load ingredients / collect " + machine.Recipe.Output.DisplayName;
        public void Configure(ProductionMachine station, float radius = 1.8f) { machine = station; ConfigureRadius(radius); }
        public override bool TryInteract(PlayerInteractor actor)
        {
            if (actor == null || machine == null || machine.Recipe == null || machine.Input == null || machine.Output == null) return false;
            ItemInventory carried = actor.Carry.Inventory;
            foreach (RecipeIngredient ingredient in machine.Recipe.Ingredients)
                if (carried.GetCount(ingredient.item) > 0)
                    return carried.TryTransferTo(machine.Input.Inventory, ingredient.item, 1);
            return machine.Output.Inventory.TryTransferTo(carried, machine.Recipe.Output, 1);
        }
    }
}
