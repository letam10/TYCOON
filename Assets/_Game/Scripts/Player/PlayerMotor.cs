using UnityEngine;

namespace TYCOON
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController), typeof(PlayerInputReader))]
    public sealed class PlayerMotor : MonoBehaviour
    {
        [SerializeField] private Transform movementCamera;
        [SerializeField] private float walkSpeed = 4.5f;
        [SerializeField] private float runSpeed = 7f;
        [SerializeField] private float turnSpeed = 720f;
        [SerializeField] private float gravity = -24f;

        private CharacterController controller;
        private PlayerInputReader input;
        private float verticalSpeed;

        public Vector3 PlanarVelocity { get; private set; }
        public float PlanarSpeed => PlanarVelocity.magnitude;
        public CollisionFlags LastCollisionFlags { get; private set; }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            input = GetComponent<PlayerInputReader>();
        }

        public void Configure(Transform cameraTransform, float walkingSpeed = 4.5f, float runningSpeed = 7f)
        {
            movementCamera = cameraTransform;
            walkSpeed = Mathf.Max(0f, walkingSpeed);
            runSpeed = Mathf.Max(walkSpeed, runningSpeed);
        }

        private void Update() => Simulate(input.Move, input.Sprint, Time.deltaTime);

        private void OnDisable()
        {
            PlanarVelocity = Vector3.zero;
            verticalSpeed = 0f;
        }

        public void Simulate(Vector2 moveInput, bool sprint, float deltaTime)
        {
            if (deltaTime <= 0f || controller == null || !controller.enabled)
            {
                PlanarVelocity = Vector3.zero;
                return;
            }

            Vector2 boundedInput = Vector2.ClampMagnitude(moveInput, 1f);
            Vector3 forward = movementCamera != null ? movementCamera.forward : Vector3.forward;
            forward = Vector3.ProjectOnPlane(forward, Vector3.up);
            forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 direction = right * boundedInput.x + forward * boundedInput.y;
            Vector3 planarMotion = direction * (sprint ? runSpeed : walkSpeed);

            if (direction.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(direction, Vector3.up), turnSpeed * deltaTime);
            }

            // Giữ tiếp xúc mặt đất; CharacterController.Move không tự áp dụng trọng lực.
            if (controller.isGrounded && verticalSpeed < 0f)
                verticalSpeed = -2f;
            verticalSpeed += gravity * deltaTime;

            Vector3 previousPosition = transform.position;
            LastCollisionFlags = controller.Move((planarMotion + Vector3.up * verticalSpeed) * deltaTime);
            if ((LastCollisionFlags & CollisionFlags.Below) != 0 && verticalSpeed < 0f)
                verticalSpeed = -2f;
            PlanarVelocity = Vector3.ProjectOnPlane(transform.position - previousPosition, Vector3.up) / deltaTime;
        }

        private void OnValidate()
        {
            walkSpeed = Mathf.Max(0f, walkSpeed);
            runSpeed = Mathf.Max(walkSpeed, runSpeed);
            turnSpeed = Mathf.Max(0f, turnSpeed);
            gravity = Mathf.Min(-0.1f, gravity);
        }
    }
}
