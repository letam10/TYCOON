using System;
using System.Collections.Generic;

namespace TYCOON
{
    public enum OrderStatus
    {
        Open,
        Paid,
        Failed
    }

    public readonly struct OrderLineSpec
    {
        public ItemDefinition Item { get; }
        public int Quantity { get; }

        public OrderLineSpec(ItemDefinition item, int quantity)
        {
            Item = item;
            Quantity = quantity;
        }
    }

    [Serializable]
    public sealed class OrderLineSnapshot
    {
        public string itemId;
        public int requested;
        public int delivered;
        public int lockedUnitPrice;
    }

    [Serializable]
    public sealed class OrderStateSnapshot
    {
        public string orderId;
        public string customerId;
        public string checkoutId;
        public float patienceRemaining;
        public int lockedTotalPrice;
        public OrderStatus status;
        public OrderLineSnapshot[] lines = Array.Empty<OrderLineSnapshot>();
    }

    public sealed class OrderState
    {
        private sealed class Line
        {
            internal ItemDefinition item;
            internal int requested;
            internal int delivered;
            internal int lockedUnitPrice;
        }

        private readonly Dictionary<string, Line> lines;
        public string OrderId { get; }
        public string CustomerId { get; }
        public string CheckoutId { get; }
        public float PatienceRemaining { get; private set; }
        public int LockedTotalPrice { get; }
        public OrderStatus Status { get; private set; }
        public bool IsComplete => Status == OrderStatus.Open && AllLinesDelivered();
        public IReadOnlyCollection<ItemDefinition> Products
        {
            get
            {
                var result = new List<ItemDefinition>(lines.Count);
                foreach (var line in lines.Values) result.Add(line.item);
                return result;
            }
        }

        public OrderState(string orderId, string customerId, string checkoutId,
            IReadOnlyList<OrderLineSpec> requested, float patienceSeconds = 90f)
        {
            if (!StableId.IsValid(orderId) || !StableId.IsValid(customerId) || !StableId.IsValid(checkoutId))
                throw new ArgumentException("Order requires stable order, customer and checkout IDs.");
            if (requested == null || requested.Count == 0 || patienceSeconds <= 0f ||
                float.IsNaN(patienceSeconds) || float.IsInfinity(patienceSeconds))
                throw new ArgumentException("Order requires lines and positive patience.");

            OrderId = orderId;
            CustomerId = customerId;
            CheckoutId = checkoutId;
            PatienceRemaining = patienceSeconds;
            Status = OrderStatus.Open;
            lines = new Dictionary<string, Line>(StringComparer.Ordinal);
            var total = 0;
            foreach (var spec in requested)
            {
                if (spec.Item == null || !StableId.IsValid(spec.Item.Id) || spec.Quantity < 1 ||
                    lines.ContainsKey(spec.Item.Id))
                    throw new ArgumentException("Order lines must contain unique valid items.");
                total = checked(total + checked(spec.Item.SellPrice * spec.Quantity));
                lines.Add(spec.Item.Id, new Line
                {
                    item = spec.Item,
                    requested = spec.Quantity,
                    lockedUnitPrice = spec.Item.SellPrice
                });
            }
            LockedTotalPrice = total;
        }

        public bool Tick(float seconds)
        {
            if (Status != OrderStatus.Open || seconds < 0f) return false;
            PatienceRemaining = Math.Max(0f, PatienceRemaining - seconds);
            if (PatienceRemaining > 0f) return false;
            Status = OrderStatus.Failed;
            return true;
        }

        public int GetDelivered(string itemId) =>
            itemId != null && lines.TryGetValue(itemId, out var line) ? line.delivered : 0;

        public int GetRemaining(string itemId) =>
            itemId != null && lines.TryGetValue(itemId, out var line) ? line.requested - line.delivered : 0;

        public bool TryDeliver(ItemDefinition item, int quantity)
        {
            if (Status != OrderStatus.Open || item == null || quantity < 1 ||
                !lines.TryGetValue(item.Id, out var line) || line.delivered > line.requested - quantity)
                return false;
            line.delivered += quantity;
            return true;
        }

        public bool TryCompletePayment()
        {
            if (Status != OrderStatus.Open || !AllLinesDelivered()) return false;
            Status = OrderStatus.Paid;
            return true;
        }

        public bool Fail()
        {
            if (Status != OrderStatus.Open) return false;
            Status = OrderStatus.Failed;
            return true;
        }

        public OrderStateSnapshot Capture()
        {
            var saved = new OrderLineSnapshot[lines.Count];
            var index = 0;
            foreach (var pair in lines)
                saved[index++] = new OrderLineSnapshot
                {
                    itemId = pair.Key,
                    requested = pair.Value.requested,
                    delivered = pair.Value.delivered,
                    lockedUnitPrice = pair.Value.lockedUnitPrice
                };
            Array.Sort(saved, (left, right) => string.CompareOrdinal(left.itemId, right.itemId));
            return new OrderStateSnapshot
            {
                orderId = OrderId,
                customerId = CustomerId,
                checkoutId = CheckoutId,
                patienceRemaining = PatienceRemaining,
                lockedTotalPrice = LockedTotalPrice,
                status = Status,
                lines = saved
            };
        }

        public static bool TryRestore(OrderStateSnapshot snapshot,
            IReadOnlyDictionary<string, ItemDefinition> definitions, out OrderState order)
        {
            order = null;
            if (snapshot == null || definitions == null || snapshot.lines == null ||
                snapshot.patienceRemaining < 0f || float.IsNaN(snapshot.patienceRemaining) ||
                float.IsInfinity(snapshot.patienceRemaining) || !Enum.IsDefined(typeof(OrderStatus), snapshot.status))
                return false;
            var specs = new List<OrderLineSpec>(snapshot.lines.Length);
            var delivered = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var line in snapshot.lines)
            {
                if (line == null || !StableId.IsValid(line.itemId) || line.requested < 1 ||
                    line.delivered < 0 || line.delivered > line.requested || line.lockedUnitPrice < 0 ||
                    delivered.ContainsKey(line.itemId) || !definitions.TryGetValue(line.itemId, out var item) ||
                    item.SellPrice != line.lockedUnitPrice)
                    return false;
                specs.Add(new OrderLineSpec(item, line.requested));
                delivered.Add(line.itemId, line.delivered);
            }
            try
            {
                order = new OrderState(snapshot.orderId, snapshot.customerId, snapshot.checkoutId, specs,
                    Math.Max(0.001f, snapshot.patienceRemaining));
            }
            catch (ArgumentException) { return false; }
            if (order.LockedTotalPrice != snapshot.lockedTotalPrice) { order = null; return false; }
            foreach (var pair in delivered)
                for (var i = 0; i < pair.Value; i++)
                    if (!order.TryDeliver(definitions[pair.Key], 1)) { order = null; return false; }
            order.PatienceRemaining = snapshot.patienceRemaining;
            order.Status = snapshot.status;
            if (order.Status == OrderStatus.Paid && !order.AllLinesDelivered()) { order = null; return false; }
            return true;
        }

        private bool AllLinesDelivered()
        {
            foreach (var line in lines.Values)
                if (line.delivered != line.requested) return false;
            return true;
        }
    }
}
