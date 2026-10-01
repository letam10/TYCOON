using UnityEngine;

namespace TYCOON
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    [DefaultExecutionOrder(100)]
    public sealed class SmoothFollowCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 targetOffset = new Vector3(0f, 0.9f, 0f);
        [SerializeField, Range(45f, 60f)] private float pitch = 52f;
        [SerializeField] private float yaw;
        [SerializeField] private float distance = 11f;
        [SerializeField] private float smoothTime = 0.12f;
        [SerializeField] private LayerMask obstructionLayers = ~0;
        [SerializeField] private float collisionRadius = 0.25f;

        private readonly RaycastHit[] obstructionHits = new RaycastHit[16];
        private Vector3 followVelocity;

        public void Configure(Transform playerRoot, bool snapImmediately = true)
        {
            target = playerRoot;
            followVelocity = Vector3.zero;
            if (snapImmediately)
                SnapToTarget();
        }

        public void SetObstructionMask(LayerMask layerMask) => obstructionLayers = layerMask;

        private void LateUpdate() => Follow(Time.deltaTime);

        public void SnapToTarget()
        {
            if (target == null)
                return;
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 pivot = target.position + targetOffset;
            transform.position = ResolveObstruction(pivot, pivot - transform.forward * distance);
            followVelocity = Vector3.zero;
        }

        public void Follow(float deltaTime)
        {
            if (target == null || deltaTime <= 0f)
                return;

            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 pivot = target.position + targetOffset;
            Vector3 desiredPosition = ResolveObstruction(pivot, pivot - transform.forward * distance);
            Vector3 candidate = Vector3.SmoothDamp(transform.position, desiredPosition,
                ref followVelocity, smoothTime, Mathf.Infinity, deltaTime);

            // Chặn cả vị trí sau damping để camera không xuyên vật cản vừa đi vào đường nhìn.
            Vector3 constrained = ResolveObstruction(pivot, candidate);
            if ((candidate - constrained).sqrMagnitude > 0.0001f)
                followVelocity = Vector3.zero;
            transform.position = constrained;
        }

        private Vector3 ResolveObstruction(Vector3 pivot, Vector3 candidate)
        {
            Vector3 offset = candidate - pivot;
            float travelDistance = offset.magnitude;
            if (travelDistance <= 0.001f)
                return candidate;

            Vector3 direction = offset / travelDistance;
            int hitCount = Physics.SphereCastNonAlloc(pivot, collisionRadius, direction,
                obstructionHits, travelDistance, obstructionLayers, QueryTriggerInteraction.Ignore);
            float clearDistance = travelDistance;
            for (int index = 0; index < hitCount; index++)
            {
                Transform hitTransform = obstructionHits[index].transform;
                if (hitTransform == target || hitTransform.IsChildOf(target))
                    continue;
                clearDistance = Mathf.Min(clearDistance, Mathf.Max(0.01f, obstructionHits[index].distance - 0.05f));
            }
            return pivot + direction * clearDistance;
        }

        private void OnValidate()
        {
            pitch = Mathf.Clamp(pitch, 45f, 60f);
            distance = Mathf.Max(2f, distance);
            smoothTime = Mathf.Max(0.01f, smoothTime);
            collisionRadius = Mathf.Max(0.05f, collisionRadius);
        }
    }
}
