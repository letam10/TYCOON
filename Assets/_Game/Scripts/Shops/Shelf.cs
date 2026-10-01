using System;
using System.Collections.Generic;
using UnityEngine;

namespace TYCOON
{
    [RequireComponent(typeof(InventoryStore))]
    public sealed class Shelf : MonoBehaviour
    {
        [SerializeField] private ItemDefinition product;
        [SerializeField] private InventoryStore store;
        [SerializeField] private Transform approachPoint;
        private readonly Dictionary<CustomerAgent, int> returnReservations = new Dictionary<CustomerAgent, int>();
        private int reservedReturns;

        public ItemDefinition Product => product;
        public InventoryStore Store => store != null ? store : store = GetComponent<InventoryStore>();
        public Transform ApproachPoint => approachPoint != null ? approachPoint : transform;
        public int AvailableCount => product == null ? 0 : Store.Inventory.GetCount(product);
        public int RestockCapacity => Mathf.Max(0, Store.Inventory.RemainingCapacity - reservedReturns);

        public void Configure(ItemDefinition item, InventoryStore inventoryStore)
        {
            if (item == null || inventoryStore == null) throw new ArgumentException("Shelf requires product and store.");
            if (reservedReturns != 0) throw new InvalidOperationException("Cannot change shelf while customers hold goods.");
            product = item;
            store = inventoryStore;
        }

        public void SetApproachPoint(Transform point) => approachPoint = point;

        public bool TryTake(ItemInventory cart, int quantity = 1)
        {
            return product != null && cart != null && Store.Inventory.TryTransferTo(cart, product, quantity);
        }

        public bool TryStock(ItemInventory source, int quantity = 1)
        {
            return product != null && source != null && quantity > 0 && quantity <= RestockCapacity &&
                source.TryTransferTo(Store.Inventory, product, quantity);
        }

        public bool TryTakeForCustomer(CustomerAgent customer, int quantity = 1)
        {
            if (customer == null || quantity < 1 || !TryTake(customer.Cart, quantity)) return false;
            // Giữ chỗ trống để khách hủy mua luôn có thể trả lại đúng vật phẩm.
            returnReservations.TryGetValue(customer, out var previous);
            returnReservations[customer] = previous + quantity;
            reservedReturns += quantity;
            return true;
        }

        public bool TryReturnFromCustomer(CustomerAgent customer)
        {
            if (customer == null || !returnReservations.TryGetValue(customer, out var quantity)) return false;
            if (!customer.Cart.TryTransferTo(Store.Inventory, product, quantity)) return false;
            CompletePurchase(customer);
            return true;
        }

        public void CompletePurchase(CustomerAgent customer)
        {
            if (customer == null || !returnReservations.TryGetValue(customer, out var quantity)) return;
            reservedReturns -= quantity;
            returnReservations.Remove(customer);
        }
    }
}
