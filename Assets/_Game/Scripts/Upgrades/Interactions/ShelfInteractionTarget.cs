using UnityEngine;

namespace TYCOON
{
    public sealed class ShelfInteractionTarget : InteractionTarget
    {
        [SerializeField] private Shelf shelf;
        public override string Prompt => shelf == null || shelf.Product == null ? "Use shelf" : "Stock " + shelf.Product.DisplayName;
        public void Configure(Shelf destination, float radius = 1.8f) { shelf = destination; ConfigureRadius(radius); }
        public override bool TryInteract(PlayerInteractor actor) => actor != null && shelf != null && shelf.TryStock(actor.Carry.Inventory);
    }
}
