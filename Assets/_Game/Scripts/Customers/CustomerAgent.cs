using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace TYCOON
{
    public enum CustomerState { Spawn, Enter, SelectProduct, GoToShelf, TakeProduct, FindCheckout, Queue, Pay, Exit, Blocked }

    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class CustomerAgent : MonoBehaviour
    {
        [SerializeField] private Shelf[] shelves = Array.Empty<Shelf>();
        [SerializeField] private CheckoutStation checkout;
        [SerializeField] private Transform entrance;
        [SerializeField] private Transform exit;
        private ItemInventory cart;
        private Shelf selectedShelf;
        private AgentNavigation movement;
        private Action<CustomerAgent> returnToPool;
        private CustomerState retryState;
        private float retrySeconds;
        private bool queueReady;
        private bool visiting;
        public event Action<CustomerState> StateChanged;
        public event Action Paid;
        public event Action<CustomerAgent, ItemDefinition, int> GoodsLost;
        public ItemInventory Cart => cart ?? (cart = new ItemInventory(1));
        public Shelf SourceShelf => selectedShelf;
        public CustomerState State { get; private set; } = CustomerState.Spawn;
        public string VisitId { get; private set; }
        public bool PaymentComplete { get; private set; }
        public bool HasUnsettledGoods => Cart.TotalCount > 0;
        public bool IsReadyForCheckout => visiting && State == CustomerState.Queue && queueReady && checkout != null &&
            checkout.QueueIndex(this) == 0 && (transform.position - checkout.GetQueuePosition(this)).sqrMagnitude <= 0.25f;

        public void Configure(Shelf[] shopShelves, CheckoutStation station, Transform shopEntrance, Transform shopExit)
        {
            if (station == null || shopEntrance == null || shopExit == null) throw new ArgumentException("Customer needs checkout, entrance and exit.");
            if (visiting) throw new InvalidOperationException("Cannot configure a customer during a visit.");
            shelves = shopShelves == null ? Array.Empty<Shelf>() : (Shelf[])shopShelves.Clone();
            checkout = station;
            entrance = shopEntrance;
            exit = shopExit;
        }

        public void BeginVisit(Action<CustomerAgent> onReturnToPool)
        {
            if (checkout == null || entrance == null || exit == null) throw new InvalidOperationException("Configure customer before starting a visit.");
            if (Cart.TotalCount > 0)
            {
                AbandonHeldGoods();
                selectedShelf?.AbandonCustomer(this);
            }
            checkout.Leave(this);
            movement = new AgentNavigation(GetComponent<NavMeshAgent>());
            returnToPool = onReturnToPool;
            selectedShelf = null;
            PaymentComplete = false;
            queueReady = false;
            visiting = true;
            VisitId = Guid.NewGuid().ToString("N");
            SetState(CustomerState.Spawn);
        }

        private void Update() => Tick(Time.deltaTime);

        public void Tick(float seconds)
        {
            if (!visiting || seconds < 0f) return;
            if (State == CustomerState.Blocked)
            {
                retrySeconds -= seconds;
                if (retrySeconds > 0f) return;
                SetState(retryState);
            }
            switch (State)
            {
                case CustomerState.Spawn: SetState(CustomerState.Enter); break;
                case CustomerState.Enter:
                    if (MoveTo(entrance.position)) SetState(CustomerState.SelectProduct);
                    break;
                case CustomerState.SelectProduct:
                    SelectProduct();
                    break;
                case CustomerState.GoToShelf:
                    if (selectedShelf == null) SetState(CustomerState.SelectProduct);
                    else if (MoveTo(selectedShelf.ApproachPoint.position)) SetState(CustomerState.TakeProduct);
                    break;
                case CustomerState.TakeProduct:
                    if (selectedShelf != null && selectedShelf.TryTakeForCustomer(this)) SetState(CustomerState.FindCheckout);
                    else SetState(CustomerState.SelectProduct);
                    break;
                case CustomerState.FindCheckout:
                    if (checkout != null && checkout.isActiveAndEnabled && checkout.TryJoin(this)) SetState(CustomerState.Queue);
                    else if (checkout == null || !checkout.isActiveAndEnabled) CancelVisit();
                    break;
                case CustomerState.Queue:
                    if (checkout == null || checkout.QueueIndex(this) < 0) { queueReady = false; SetState(CustomerState.FindCheckout); }
                    else queueReady = MoveTo(checkout.GetQueuePosition(this));
                    break;
                case CustomerState.Pay: SetState(CustomerState.Exit); break;
                case CustomerState.Exit:
                    if (MoveTo(exit.position)) EndVisit();
                    break;
            }
        }

        private void SelectProduct()
        {
            if (Cart.TotalCount > 0) { SetState(CustomerState.FindCheckout); return; }
            var first = shelves.Length == 0 ? 0 : UnityEngine.Random.Range(0, shelves.Length);
            for (var offset = 0; offset < shelves.Length; offset++)
            {
                var shelf = shelves[(first + offset) % shelves.Length];
                if (shelf == null || !shelf.isActiveAndEnabled || shelf.AvailableCount == 0) continue;
                selectedShelf = shelf;
                SetState(CustomerState.GoToShelf);
                return;
            }
            SetState(CustomerState.Exit);
        }

        private bool MoveTo(Vector3 target)
        {
            if (movement.TryMoveTo(target, out var arrived)) return arrived;
            retryState = State;
            retrySeconds = 1f;
            queueReady = false;
            SetState(CustomerState.Blocked);
            return false;
        }

        internal void CompletePayment()
        {
            selectedShelf?.CompletePurchase(this);
            PaymentComplete = true;
            queueReady = false;
            movement.Stop();
            SetState(CustomerState.Pay);
            Paid?.Invoke();
        }

        public bool CancelVisit()
        {
            checkout?.Leave(this);
            queueReady = false;
            AbandonHeldGoods();
            selectedShelf?.AbandonCustomer(this);
            SetState(CustomerState.Exit);
            return true;
        }

        public void AbandonHeldGoods()
        {
            if (Cart.TotalCount == 0) return;
            var stacks = new List<ItemStack>(Cart.Stacks);
            foreach (var stack in stacks) GoodsLost?.Invoke(this, stack.Item, stack.Quantity);
            if (checkout != null) checkout.RecordCustomerLoss(Cart);
            else Cart.Clear();
        }

        private void EndVisit()
        {
            if (!PaymentComplete)
            {
                AbandonHeldGoods();
                selectedShelf?.AbandonCustomer(this);
            }
            visiting = false;
            movement.Stop();
            var callback = returnToPool;
            returnToPool = null;
            callback?.Invoke(this);
        }

        private void SetState(CustomerState state)
        {
            if (State == state) return;
            State = state;
            StateChanged?.Invoke(state);
        }

        private void OnDisable()
        {
            checkout?.Leave(this);
            queueReady = false;
            if (!PaymentComplete)
            {
                AbandonHeldGoods();
                selectedShelf?.AbandonCustomer(this);
            }
            visiting = false;
            movement?.Stop();
        }
    }
}
