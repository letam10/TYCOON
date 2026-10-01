using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace TYCOON
{
    public enum WorkerState { Idle, MovingToWork, Working, Carrying, Blocked }

    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class WorkerAgent : MonoBehaviour
    {
        [SerializeField] private string stableId;
        [SerializeField] private WorkerJob job;
        [SerializeField, Min(1)] private int capacity = 6;
        [SerializeField, Min(0.05f)] private float actionSeconds = 0.5f;
        private ItemInventory inventory;
        private AgentNavigation movement;
        private bool hasTask;
        private Vector3 destination;
        private float remainingSeconds;
        public event Action<WorkerState> StateChanged;
        public ItemInventory Inventory => inventory ?? (inventory = new ItemInventory(capacity));
        public WorkerState State { get; private set; } = WorkerState.Idle;
        public WorkerJob Job => job;
        public string StableId => stableId;
        public string BlockedReason { get; private set; }

        public void Configure(WorkerJob workerJob, int carryCapacity = 6, string id = null)
        {
            if (workerJob == null || carryCapacity < 1) throw new ArgumentException("Worker requires job and positive carry capacity.");
            if (Inventory.TotalCount > 0) throw new InvalidOperationException("Unload worker before changing job.");
            var configuredId = id ?? (TYCOON.StableId.IsValid(stableId) ? stableId : "worker." + workerJob.GetType().Name.ToLowerInvariant());
            if (!TYCOON.StableId.IsValid(configuredId)) throw new ArgumentException("Worker requires a stable ID.");
            if (!workerJob.TryBind(this)) throw new InvalidOperationException("Job is already assigned to another worker.");
            if (job != null && job != workerJob) job.Unbind(this);
            job = workerJob;
            stableId = configuredId;
            capacity = carryCapacity;
            inventory = new ItemInventory(capacity);
            movement = new AgentNavigation(GetComponent<NavMeshAgent>());
            hasTask = false;
            remainingSeconds = 0f;
            BlockedReason = null;
            SetState(WorkerState.Idle);
        }

        public bool TryRestoreInventory(InventorySnapshot snapshot, IReadOnlyDictionary<string, ItemDefinition> definitions)
        {
            if (snapshot == null || snapshot.inventoryId != stableId || !Inventory.TryLoadSnapshot(snapshot, definitions)) return false;
            capacity = Inventory.Capacity;
            job?.CancelWork();
            movement?.Stop();
            hasTask = false;
            remainingSeconds = 0f;
            SetState(WorkerState.Idle);
            return true;
        }

        private void OnEnable()
        {
            movement = new AgentNavigation(GetComponent<NavMeshAgent>());
            if (job != null) job.TryBind(this);
            hasTask = false;
            remainingSeconds = 0f;
        }

        private void Update() => Tick(Time.deltaTime);

        public void Tick(float seconds)
        {
            if (seconds < 0f || job == null || !job.isActiveAndEnabled) return;
            if (job.Owner != this) { Block("Job is assigned to another worker."); return; }
            if (State == WorkerState.Blocked)
            {
                remainingSeconds -= seconds;
                if (remainingSeconds > 0f) return;
                hasTask = false;
            }
            if (!hasTask)
            {
                if (!job.TryGetDestination(out destination)) { Block(job.BlockedReason); return; }
                hasTask = true;
                BlockedReason = null;
                SetState(Inventory.TotalCount > 0 ? WorkerState.Carrying : WorkerState.MovingToWork);
            }
            if (State != WorkerState.Working)
            {
                if (!movement.TryMoveTo(destination, out var arrived))
                {
                    job.CancelWork();
                    Block("No complete NavMesh path.");
                    return;
                }
                if (!arrived) return;
                movement.Stop();
                remainingSeconds = actionSeconds;
                SetState(WorkerState.Working);
            }
            remainingSeconds -= seconds;
            if (remainingSeconds > 0f) return;
            hasTask = false;
            if (!job.Perform()) { job.CancelWork(); Block(job.BlockedReason); return; }
            SetState(WorkerState.Idle);
        }

        private void Block(string reason)
        {
            hasTask = false;
            BlockedReason = reason;
            remainingSeconds = 1f;
            movement?.Stop();
            SetState(WorkerState.Blocked);
        }

        private void SetState(WorkerState state)
        {
            if (State == state) return;
            State = state;
            StateChanged?.Invoke(state);
        }

        private void OnDisable()
        {
            movement?.Stop();
            job?.CancelWork();
            hasTask = false;
        }

        private void OnDestroy() { if (job != null) job.Unbind(this); }
    }
}
