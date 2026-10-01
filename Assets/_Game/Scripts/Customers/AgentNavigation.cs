using UnityEngine;
using UnityEngine.AI;

namespace TYCOON
{
    internal sealed class AgentNavigation
    {
        private readonly NavMeshAgent agent;
        private Vector3 destination;
        private bool hasDestination;

        public AgentNavigation(NavMeshAgent navAgent) { agent = navAgent; }

        public bool TryMoveTo(Vector3 target, out bool arrived)
        {
            arrived = false;
            if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh ||
                !NavMesh.SamplePosition(target, out var hit, 1.5f, agent.areaMask)) return false;
            if (!hasDestination || (destination - hit.position).sqrMagnitude > 0.04f)
            {
                if (!agent.SetDestination(hit.position)) return false;
                destination = hit.position;
                hasDestination = true;
                agent.isStopped = false;
            }
            if (agent.pathPending) return true;
            if (agent.pathStatus != NavMeshPathStatus.PathComplete) { hasDestination = false; return false; }
            arrived = agent.remainingDistance <= agent.stoppingDistance + 0.15f;
            return true;
        }

        public void Stop()
        {
            hasDestination = false;
            if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh) return;
            agent.isStopped = true;
            agent.ResetPath();
        }
    }
}
