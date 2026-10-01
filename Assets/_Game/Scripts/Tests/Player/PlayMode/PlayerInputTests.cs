using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace TYCOON.Tests
{
    // Input System 1.20 chỉ hỗ trợ InputTestFixture trong PlayMode; EditMode dùng luồng input Editor.
    public sealed class PlayerInputTests : InputTestFixture
    {
        private GameObject owner;
        private PlayerInputReader reader;
        private Keyboard keyboard;
        private Gamepad gamepad;

        public override void Setup()
        {
            Assert.That(Application.isPlaying, Is.True, "Input lifecycle tests must run in PlayMode.");
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            gamepad = InputSystem.AddDevice<Gamepad>();
            owner = new GameObject("Player input test");
            reader = owner.AddComponent<PlayerInputReader>();
            Assert.That(reader.MoveAction.enabled, Is.True, "OnEnable must enable the runtime input map.");
        }

        public override void TearDown()
        {
            try
            {
                if (owner != null)
                    Object.DestroyImmediate(owner);
                owner = null;
            }
            finally
            {
                base.TearDown();
            }
        }

        [Test]
        public void WasdDiagonalDoesNotExceedFullStickSpeedAndReleaseStopsInput()
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.D));
            InputSystem.Update();
            Assert.That(reader.Move.x, Is.GreaterThan(0.69f));
            Assert.That(reader.Move.y, Is.GreaterThan(0.69f));
            Assert.That(reader.Move.magnitude, Is.EqualTo(1f).Within(0.001f));

            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            Assert.That(reader.Move, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void ArrowKeysAndShiftDriveTheSamePlayerActions()
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftArrow, Key.LeftShift));
            InputSystem.Update();
            Assert.That(reader.Move, Is.EqualTo(Vector2.left));
            Assert.That(reader.Sprint, Is.True);

            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            Assert.That(reader.Sprint, Is.False);
        }

        [Test]
        public void GamepadRetainsAnalogMovementAndTriggerSprint()
        {
            InputSystem.QueueStateEvent(gamepad, new GamepadState
            {
                leftStick = new Vector2(0.5f, 0f),
                rightTrigger = 1f
            });
            InputSystem.Update();
            Assert.That(reader.Move.x, Is.GreaterThan(0f).And.LessThan(1f));
            Assert.That(reader.Move.y, Is.EqualTo(0f).Within(0.001f));
            Assert.That(reader.Sprint, Is.True);

            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            InputSystem.Update();
            Assert.That(reader.Move, Is.EqualTo(Vector2.zero));
            Assert.That(reader.Sprint, Is.False);
        }

        [Test]
        public void DisabledReaderDoesNotMovePlayerWithHeldInput()
        {
            InputSystem.QueueStateEvent(gamepad, new GamepadState { leftStick = Vector2.up });
            InputSystem.Update();
            Assert.That(reader.Move.y, Is.GreaterThan(0.9f));
            reader.enabled = false;
            Assert.That(reader.MoveAction.enabled, Is.False);
            Assert.That(reader.Move, Is.EqualTo(Vector2.zero));
            Assert.That(reader.Sprint, Is.False);
        }
    }
}
