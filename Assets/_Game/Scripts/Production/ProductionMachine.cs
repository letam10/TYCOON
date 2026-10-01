using System;
using UnityEngine;

namespace TYCOON
{
    [Serializable]
    public struct MachineState
    {
        public string machineId, recipeId;
        public float elapsedSeconds;
        public bool isProcessing;
        public int level;
    }

    public sealed class ProductionMachine : MonoBehaviour
    {
        [SerializeField] private string stableId;
        [SerializeField] private RecipeDefinition recipe;
        [SerializeField] private InventoryStore input;
        [SerializeField] private InventoryStore output;
        [SerializeField, Min(0)] private int level;
        private float elapsed;
        private bool processing;
        public event Action Completed;
        public string StableId => stableId;
        public RecipeDefinition Recipe => recipe;
        public InventoryStore Input => input;
        public InventoryStore Output => output;
        public bool IsProcessing => processing;
        public int Level => level;
        public float Duration => recipe == null ? 0f : recipe.Seconds / (1f + 0.25f * level);
        public float Progress => Duration > 0f ? Mathf.Clamp01(elapsed / Duration) : 0f;
        public bool OutputBlocked => processing && elapsed >= Duration && output.Inventory.RemainingCapacity < recipe.OutputQuantity;

        public void Configure(string id, RecipeDefinition definition, InventoryStore source, InventoryStore destination)
        {
            if (string.IsNullOrWhiteSpace(id) || definition == null || source == null || destination == null || source == destination)
                throw new ArgumentException("Machine requires unique ID, recipe and separate input/output stores.");
            stableId = id; recipe = definition; input = source; output = destination;
        }

        private void Update() => Tick(Time.deltaTime);
        public void Tick(float seconds)
        {
            if (seconds < 0f || recipe == null || input == null || output == null) return;
            if (!processing)
            {
                if (seconds == 0f) return;
                if (output.Inventory.RemainingCapacity < recipe.OutputQuantity) return;
                // Kiểm tra toàn bộ công thức trước khi trừ để không tiêu hao một phần nguyên liệu.
                if (!input.Inventory.TryConsume(recipe.Ingredients)) return;
                processing = true;
                elapsed = 0f;
            }
            elapsed = Mathf.Min(Duration, elapsed + seconds);
            if (elapsed < Duration || !output.Inventory.TryAdd(recipe.Output, recipe.OutputQuantity)) return;
            processing = false;
            elapsed = 0f;
            Completed?.Invoke();
        }

        public void ApplyLevel(int newLevel)
        {
            if (newLevel < 0) throw new ArgumentOutOfRangeException(nameof(newLevel));
            level = newLevel;
        }

        public MachineState CaptureState() => new MachineState
        { machineId = stableId, recipeId = recipe.Id, elapsedSeconds = elapsed, isProcessing = processing, level = level };

        public bool TryRestoreState(MachineState state)
        {
            if (recipe == null || state.machineId != stableId || state.recipeId != recipe.Id || state.level < 0 ||
                float.IsNaN(state.elapsedSeconds) || float.IsInfinity(state.elapsedSeconds) || state.elapsedSeconds < 0f) return false;
            level = state.level;
            elapsed = Mathf.Min(state.elapsedSeconds, Duration);
            processing = state.isProcessing;
            if (!processing) elapsed = 0f;
            return true;
        }
    }
}
