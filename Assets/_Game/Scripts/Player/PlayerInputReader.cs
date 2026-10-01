using UnityEngine;
using UnityEngine.InputSystem;

namespace TYCOON
{
    [DisallowMultipleComponent]
    public sealed class PlayerInputReader : MonoBehaviour
    {
        private InputActionMap actions;
        private InputAction moveAction;
        private InputAction sprintAction;

        public Vector2 Move => isActiveAndEnabled
            ? Vector2.ClampMagnitude(MoveAction.ReadValue<Vector2>(), 1f)
            : Vector2.zero;

        public bool Sprint => isActiveAndEnabled && SprintAction.IsPressed();

        public InputAction MoveAction
        {
            get { EnsureActions(); return moveAction; }
        }

        public InputAction SprintAction
        {
            get { EnsureActions(); return sprintAction; }
        }

        private void Awake() => EnsureActions();

        private void OnEnable()
        {
            EnsureActions();
            actions.Enable();
        }

        private void OnDisable() => actions?.Disable();

        private void OnDestroy()
        {
            actions?.Dispose();
            actions = null;
        }

        private void EnsureActions()
        {
            if (actions != null)
                return;

            actions = new InputActionMap("Tycoon Player");
            moveAction = actions.AddAction("Move", InputActionType.Value);
            moveAction.expectedControlType = "Vector2";
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");
            moveAction.AddBinding("<Gamepad>/leftStick");

            sprintAction = actions.AddAction("Sprint", InputActionType.Button);
            sprintAction.AddBinding("<Keyboard>/leftShift");
            sprintAction.AddBinding("<Keyboard>/rightShift");
            sprintAction.AddBinding("<Gamepad>/rightTrigger");
        }
    }
}
