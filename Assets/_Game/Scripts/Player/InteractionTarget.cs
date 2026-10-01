using System.Collections.Generic;
using UnityEngine;

namespace TYCOON
{
    public abstract class InteractionTarget : MonoBehaviour
    {
        private static readonly HashSet<InteractionTarget> activeTargets = new HashSet<InteractionTarget>();
        [SerializeField, Min(0.25f)] private float interactionRadius = 1.8f;
        public static IEnumerable<InteractionTarget> ActiveTargets => activeTargets;
        public float InteractionRadius => interactionRadius;
        public abstract string Prompt { get; }
        public virtual string GetPrompt(PlayerInteractor actor) => Prompt;
        public virtual bool CanInteract(PlayerInteractor actor) => actor != null && isActiveAndEnabled;
        public abstract bool TryInteract(PlayerInteractor actor);

        protected virtual void OnEnable() => activeTargets.Add(this);
        protected virtual void OnDisable() => activeTargets.Remove(this);
        protected void ConfigureRadius(float radius) => interactionRadius = Mathf.Max(0.25f, radius);

        public float PlanarDistanceSquared(Vector3 position)
        {
            Vector3 difference = transform.position - position;
            difference.y = 0f;
            return difference.sqrMagnitude;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearRegistry() => activeTargets.Clear();
    }
}
