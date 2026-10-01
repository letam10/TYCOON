using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TYCOON
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(InventoryStore))]
    public sealed class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] private InventoryStore carry;
        [SerializeField] private GameSession session;
        [SerializeField] private PlayerAnimationDriver animationDriver;
        private InputAction interactAction;
        private float feedbackUntil;
        private string feedback;
        public event Action<InteractionTarget, bool> InteractionCompleted;
        public InventoryStore Carry => carry != null ? carry : carry = GetComponent<InventoryStore>();
        public GameSession Session => session;
        public InteractionTarget CurrentTarget { get; private set; }
        public string StatusMessage => Time.unscaledTime < feedbackUntil ? feedback : string.Empty;
        public InputAction InteractAction
        {
            get
            {
                if (interactAction == null)
                {
                    interactAction = new InputAction("TYCOON Interact", InputActionType.Button);
                    interactAction.AddBinding("<Keyboard>/e");
                    interactAction.AddBinding("<Gamepad>/buttonSouth");
                }
                return interactAction;
            }
        }

        public void Configure(InventoryStore carriedItems, GameSession gameSession, PlayerAnimationDriver driver = null)
        {
            if (carriedItems == null || gameSession == null) throw new ArgumentException("Player interaction requires carry storage and a game session.");
            carry = carriedItems; session = gameSession; animationDriver = driver;
            RefreshTarget();
        }

        private void Awake()
        {
            if (carry == null) carry = GetComponent<InventoryStore>();
            if (animationDriver == null) animationDriver = GetComponent<PlayerAnimationDriver>();
        }
        private void OnEnable() => InteractAction.Enable();
        private void OnDisable() { interactAction?.Disable(); CurrentTarget = null; }
        private void OnDestroy() { interactAction?.Dispose(); interactAction = null; }

        private void Update()
        {
            RefreshTarget();
            if (InteractAction.WasPressedThisFrame()) TryInteract();
            if (animationDriver != null) animationDriver.SetCarrying(Carry.Inventory.TotalCount > 0);
        }

        public void RefreshTarget()
        {
            CurrentTarget = null;
            float closest = float.PositiveInfinity;
            foreach (InteractionTarget target in InteractionTarget.ActiveTargets)
            {
                if (target == null || !target.CanInteract(this)) continue;
                float distance = target.PlanarDistanceSquared(transform.position);
                if (distance > target.InteractionRadius * target.InteractionRadius || distance >= closest) continue;
                closest = distance;
                CurrentTarget = target;
            }
        }

        public bool TryInteract()
        {
            RefreshTarget();
            if (CurrentTarget == null) return false;
            InteractionTarget target = CurrentTarget;
            bool succeeded = target.TryInteract(this);
            if (succeeded && animationDriver != null) animationDriver.PlayInteraction();
            if (!(target is PurchasePad)) ShowFeedback(succeeded ? "Done!" : "Not ready, no matching item, or storage is full.");
            InteractionCompleted?.Invoke(target, succeeded);
            return succeeded;
        }

        public void ShowFeedback(string message, float seconds = 2.5f)
        {
            feedback = message;
            feedbackUntil = Time.unscaledTime + Mathf.Max(0f, seconds);
        }
    }
}
