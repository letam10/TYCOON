using System.Collections.Generic;
using UnityEngine;

namespace TYCOON
{
    public abstract class WorkerJob : MonoBehaviour
    {
        private static readonly Dictionary<Object, WorkerAgent> reservations = new Dictionary<Object, WorkerAgent>();
        private Object heldResource;
        public WorkerAgent Owner { get; private set; }
        public string BlockedReason { get; protected set; }

        public bool TryBind(WorkerAgent worker)
        {
            if (worker == null || (Owner != null && Owner != worker)) return false;
            Owner = worker;
            return true;
        }

        public void Unbind(WorkerAgent worker)
        {
            if (Owner != worker) return;
            CancelWork();
            Owner = null;
        }

        public abstract bool TryGetDestination(out Vector3 destination);
        public abstract bool Perform();

        protected bool TryReserve(Object resource)
        {
            // Chỉ một công nhân giữ đích công việc trong lúc đang di chuyển và thao tác.
            if (resource == null || Owner == null) return false;
            if (heldResource != resource) ReleaseReservation();
            if (reservations.TryGetValue(resource, out var existing) && existing != null && existing != Owner && existing.isActiveAndEnabled) return false;
            reservations[resource] = Owner;
            heldResource = resource;
            return true;
        }

        protected void ReleaseReservation()
        {
            if (!ReferenceEquals(heldResource, null) && reservations.TryGetValue(heldResource, out var holder) && holder == Owner)
                reservations.Remove(heldResource);
            heldResource = null;
        }

        public virtual void CancelWork() => ReleaseReservation();
        protected virtual void OnDisable() => CancelWork();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetReservations() => reservations.Clear();
    }
}
