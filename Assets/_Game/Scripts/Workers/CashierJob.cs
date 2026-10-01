using System;
using UnityEngine;

namespace TYCOON
{
    public sealed class CashierJob : WorkerJob
    {
        [SerializeField] private CheckoutStation checkout;

        public void Configure(CheckoutStation station)
        {
            if (station == null) throw new ArgumentNullException(nameof(station));
            CancelWork();
            checkout = station;
        }

        public override bool TryGetDestination(out Vector3 destination)
        {
            destination = transform.position;
            if (Owner == null || checkout == null || !checkout.isActiveAndEnabled) { BlockedReason = "Checkout is closed."; return false; }
            if (!checkout.TryAssignCashier(Owner)) { BlockedReason = "Checkout already has a cashier."; return false; }
            destination = checkout.ServicePoint.position;
            if (checkout.QueueCount == 0) { BlockedReason = "Waiting for customers."; return false; }
            return true;
        }

        public override bool Perform()
        {
            if (checkout == null || checkout.Cashier != Owner || !checkout.TryServeNext())
            { BlockedReason = "Waiting for queue head to arrive."; return false; }
            return true;
        }

        public override void CancelWork()
        {
            base.CancelWork();
            if (checkout != null) checkout.ReleaseCashier(Owner);
        }
    }
}
