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
        [SerializeField] private string stableId;
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
        private OrderState order;
        public event Action<CustomerState> StateChanged;
        public event Action Paid;
        public event Action<CustomerAgent, ItemDefinition, int> GoodsLost;
        public ItemInventory Cart => cart ?? (cart = new ItemInventory(1));
        public Shelf SourceShelf => selectedShelf;
        public CustomerState State { get; private set; } = CustomerState.Spawn;
        public string StableId => stableId;
        public string GetEntityId() => stableId;
        public string VisitId { get; private set; }
        public bool PaymentComplete { get; private set; }
        public OrderState CurrentOrder => order;
        public float PatienceRemaining => order == null ? 0f : order.PatienceRemaining;
        public bool HasUnsettledGoods => Cart.TotalCount > 0;
        internal bool CanCompletePayment => order == null || order.IsComplete;
        public bool IsReadyForCheckout => visiting && State == CustomerState.Queue && queueReady && checkout != null &&
            checkout.QueueIndex(this) == 0 && (transform.position - checkout.GetQueuePosition(this)).sqrMagnitude <= 0.25f;

        public CustomerOrderSnapshot CaptureOrderSnapshot()
        {
            if (!visiting || order == null || !TYCOON.StableId.IsValid(stableId) || !TYCOON.StableId.IsValid(VisitId))
                throw new InvalidOperationException("Customer has no active order to save.");
            return new CustomerOrderSnapshot
            {
                customerId = stableId,
                visitId = VisitId,
                state = State,
                paymentComplete = PaymentComplete,
                sourceShelfId = selectedShelf == null ? null : selectedShelf.Store.StableId,
                queueIndex = checkout == null ? -1 : checkout.QueueIndex(this),
                x = transform.position.x,
                y = transform.position.y,
                z = transform.position.z,
                yaw = transform.eulerAngles.y,
                order = order.Capture(),
                cart = Cart.CreateSnapshot(stableId + ".cart")
            };
        }

        public bool TryRestoreOrderSnapshot(CustomerOrderSnapshot snapshot,
            IReadOnlyDictionary<string, ItemDefinition> definitions, Action<CustomerAgent> onReturnToPool)
        {
            if (snapshot == null || definitions == null || visiting || snapshot.customerId != stableId ||
                !TYCOON.StableId.IsValid(snapshot.visitId) || snapshot.cart == null ||
                snapshot.cart.inventoryId != stableId + ".cart" ||
                snapshot.order == null || snapshot.order.customerId != stableId ||
                checkout == null || snapshot.order.checkoutId != checkout.StableId ||
                !Enum.IsDefined(typeof(CustomerState), snapshot.state))
                return false;
            if (!OrderState.TryRestore(snapshot.order, definitions, out var restored) ||
                !Cart.TryLoadSnapshot(snapshot.cart, definitions))
                return false;
            selectedShelf = null;
            if (!string.IsNullOrWhiteSpace(snapshot.sourceShelfId))
            {
                foreach (var shelf in shelves)
                    if (shelf != null && shelf.Store.StableId == snapshot.sourceShelfId) { selectedShelf = shelf; break; }
                if (selectedShelf == null) return false;
                var held = selectedShelf.Product == null ? 0 : Cart.GetCount(selectedShelf.Product);
                if (held > 0 && !selectedShelf.RestoreCustomerReservation(this, held)) return false;
            }
            movement = new AgentNavigation(GetComponent<NavMeshAgent>());
            returnToPool = onReturnToPool;
            VisitId = snapshot.visitId;
            order = restored;
            PaymentComplete = snapshot.paymentComplete;
            queueReady = false;
            visiting = true;
            SetState(snapshot.state);
            return true;
        }

        public void Configure(Shelf[] shopShelves, CheckoutStation station, Transform shopEntrance, Transform shopExit,
            string id = null)
        {
            if (station == null || shopEntrance == null || shopExit == null) throw new ArgumentException("Customer needs checkout, entrance and exit.");
            if (visiting) throw new InvalidOperationException("Cannot configure a customer during a visit.");
            var configuredId = id ?? stableId;
            if (!TYCOON.StableId.IsValid(configuredId))
                configuredId = "customer." + gameObject.name.Replace(" ", ".").ToLowerInvariant();
            if (!TYCOON.StableId.IsValid(configuredId)) throw new ArgumentException("Customer requires a stable ID.");
            stableId = configuredId;
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
            order = null;
            queueReady = false;
            visiting = true;
            VisitId = Guid.NewGuid().ToString("N");
            SetState(CustomerState.Spawn);
        }

        private void Update() => Tick(Time.deltaTime);

        public void Tick(float seconds)
        {
            if (!visiting || seconds < 0f) return;
            if (State == CustomerState.Queue && order != null && order.Tick(seconds))
            {
                checkout?.Leave(this);
                queueReady = false;
                SetState(CustomerState.Exit);
                return;
            }
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
                    if (selectedShelf != null && selectedShelf.TryTakeForCustomer(this))
                    {
                        if (order == null || !order.TryDeliver(selectedShelf.Product, 1))
                        {
                            AbandonHeldGoods();
                            selectedShelf.AbandonCustomer(this);
                            SetState(CustomerState.Exit);
                        }
                        else SetState(CustomerState.FindCheckout);
                    }
                    else SetState(CustomerState.SelectProduct);
                    break;
                case CustomerState.FindCheckout:
                    if (checkout != null && checkout.isActiveAndEnabled && checkout.TryJoin(this))
                    {
                        SetState(CustomerState.Queue);
                    }
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
                order = new OrderState(VisitId + ".order", stableId, checkout.StableId,
                    new[] { new OrderLineSpec(shelf.Product, 1) });
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
            order?.TryCompletePayment();
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
            order?.Fail();
            if (Cart.TotalCount == 0) return;
            var stacks = new List<ItemStack>(Cart.Stacks);
            foreach (var stack in stacks) GoodsLost?.Invoke(this, stack.Item, stack.Quantity);
            if (checkout != null) checkout.RecordCustomerLoss(Cart);
            else Cart.Clear();
        }

        internal void PrepareForRestore()
        {
            checkout?.Leave(this);
            selectedShelf?.CompletePurchase(this);
            movement?.Stop();
            Cart.Clear();
            selectedShelf = null;
            order = null;
            PaymentComplete = false;
            queueReady = false;
            visiting = false;
            returnToPool = null;
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
