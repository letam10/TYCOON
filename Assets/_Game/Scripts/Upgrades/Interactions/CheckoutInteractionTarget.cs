using UnityEngine;

namespace TYCOON
{
    public sealed class CheckoutInteractionTarget : InteractionTarget
    {
        [SerializeField] private CheckoutStation checkout;
        public override string Prompt => checkout == null ? "Checkout" :
            "Serve / collect $" + checkout.PendingRevenue + " • Queue " + checkout.QueueCount;
        public void Configure(CheckoutStation station, float radius = 1.8f) { checkout = station; ConfigureRadius(radius); }
        public override bool TryInteract(PlayerInteractor actor)
        {
            if (actor == null || actor.Session == null || checkout == null) return false;
            bool served = checkout.TryServeNext();
            int before = actor.Session.Wallet.Balance;
            checkout.CollectMoney(actor.Session.Wallet);
            return served || actor.Session.Wallet.Balance > before;
        }
    }
}
