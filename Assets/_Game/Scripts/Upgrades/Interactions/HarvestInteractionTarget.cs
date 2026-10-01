using UnityEngine;

namespace TYCOON
{
    public sealed class HarvestInteractionTarget : InteractionTarget
    {
        [SerializeField] private HarvestNode node;
        public override string Prompt => node == null || node.Product == null ? "Harvest" :
            "Harvest " + node.Product.DisplayName + (node.IsReady ? " (" + node.AvailableCount + ")" : " • Growing");
        public void Configure(HarvestNode source, float radius = 1.8f) { node = source; ConfigureRadius(radius); }
        public override bool TryInteract(PlayerInteractor actor) => actor != null && node != null && node.TryHarvest(actor.Carry.Inventory);
    }
}
