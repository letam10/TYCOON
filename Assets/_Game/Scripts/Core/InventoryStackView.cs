using System.Collections.Generic;
using UnityEngine;

namespace TYCOON
{
    public sealed class InventoryStackView : MonoBehaviour
    {
        [SerializeField] private InventoryStore store;
        [SerializeField, Min(1)] private int visibleLimit = 12;
        [SerializeField, Min(1)] private int columns = 3;
        [SerializeField] private Vector3 spacing = new Vector3(0.25f, 0.22f, 0.25f);
        [SerializeField, Min(0.01f)] private float itemScale = 0.25f;
        private ItemInventory observed;
        private readonly List<GameObject> active = new List<GameObject>();
        private readonly Dictionary<GameObject, Stack<GameObject>> pool = new Dictionary<GameObject, Stack<GameObject>>();
        private readonly Dictionary<GameObject, GameObject> prototypes = new Dictionary<GameObject, GameObject>();
        public int VisibleCount => active.Count;

        public void Configure(InventoryStore source, int limit = 12, float scale = 0.25f)
        {
            Unbind();
            store = source;
            visibleLimit = Mathf.Max(1, limit);
            itemScale = Mathf.Max(0.01f, scale);
            if (isActiveAndEnabled) Bind();
        }

        private void OnEnable() => Bind();
        private void OnDisable() => Unbind();
        private void Bind()
        {
            if (store == null) return;
            observed = store.Inventory;
            observed.Changed += Refresh;
            Refresh();
        }
        private void Unbind()
        {
            if (observed != null) observed.Changed -= Refresh;
            observed = null;
        }
        public void Refresh()
        {
            foreach (var obj in active)
            {
                obj.SetActive(false);
                pool[prototypes[obj]].Push(obj);
            }
            active.Clear();
            if (observed == null) return;
            foreach (var stack in observed.Stacks)
            {
                var prefab = stack.Item.Prefab;
                if (prefab == null) continue;
                if (!pool.TryGetValue(prefab, out var available))
                {
                    available = new Stack<GameObject>();
                    pool.Add(prefab, available);
                }
                for (int i = 0; i < stack.Quantity && active.Count < visibleLimit; i++)
                {
                    var obj = available.Count > 0 ? available.Pop() : Instantiate(prefab, transform);
                    foreach (var collider in obj.GetComponentsInChildren<Collider>()) collider.enabled = false;
                    prototypes[obj] = prefab;
                    int slot = active.Count;
                    obj.transform.localPosition = new Vector3((slot % columns - (columns - 1) * 0.5f) * spacing.x,
                        (slot / (columns * columns)) * spacing.y, (slot / columns % columns) * spacing.z);
                    obj.transform.localRotation = Quaternion.identity;
                    obj.transform.localScale = Vector3.one * itemScale;
                    obj.SetActive(true);
                    active.Add(obj);
                }
            }
        }
    }
}
