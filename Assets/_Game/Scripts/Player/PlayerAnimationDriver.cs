using UnityEngine;

namespace TYCOON
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerMotor))]
    public sealed class PlayerAnimationDriver : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private float speedDamping = 0.08f;

        private static readonly int MoveSpeedId = Animator.StringToHash("MoveSpeed");
        private static readonly int CarryingId = Animator.StringToHash("Carrying");
        private static readonly int InteractId = Animator.StringToHash("Interact");

        private PlayerMotor motor;
        private RuntimeAnimatorController cachedController;
        private bool carrying;
        private bool hasMoveSpeed;
        private bool hasCarrying;
        private bool hasInteract;

        private void Awake()
        {
            motor = GetComponent<PlayerMotor>();
            if (animator == null)
                animator = GetComponentInChildren<Animator>();
            RefreshParameters();
        }

        public void Configure(Animator modelAnimator)
        {
            animator = modelAnimator;
            RefreshParameters();
        }

        public void SetCarrying(bool value) => carrying = value;

        public void PlayInteraction()
        {
            if (!RefreshIfChanged())
                return;
            if (hasInteract)
                animator.SetTrigger(InteractId);
        }

        private void Update()
        {
            if (!RefreshIfChanged())
                return;
            if (hasMoveSpeed)
                animator.SetFloat(MoveSpeedId, motor.PlanarSpeed, speedDamping, Time.deltaTime);
            if (hasCarrying)
                animator.SetBool(CarryingId, carrying);
        }

        private bool RefreshIfChanged()
        {
            if (animator == null || animator.runtimeAnimatorController == null)
                return false;
            if (cachedController != animator.runtimeAnimatorController)
                RefreshParameters();
            return true;
        }

        private void RefreshParameters()
        {
            hasMoveSpeed = false;
            hasCarrying = false;
            hasInteract = false;
            cachedController = animator != null ? animator.runtimeAnimatorController : null;
            if (cachedController == null)
                return;

            // Clip/rig có thể đến sau; chỉ gửi parameter thực sự có để tránh Console warning.
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                hasMoveSpeed |= parameter.nameHash == MoveSpeedId && parameter.type == AnimatorControllerParameterType.Float;
                hasCarrying |= parameter.nameHash == CarryingId && parameter.type == AnimatorControllerParameterType.Bool;
                hasInteract |= parameter.nameHash == InteractId && parameter.type == AnimatorControllerParameterType.Trigger;
            }
        }
    }
}
