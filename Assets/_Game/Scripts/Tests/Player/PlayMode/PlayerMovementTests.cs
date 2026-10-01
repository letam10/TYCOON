using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace TYCOON.Tests
{
    public sealed class PlayerMovementTests
    {
        private const float StepTime = 1f / 60f;
        private readonly List<GameObject> ownedObjects = new List<GameObject>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            GameObject floor = CreateObject("Test floor");
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            BoxCollider collider = floor.AddComponent<BoxCollider>();
            collider.size = new Vector3(50f, 1f, 50f);
            Physics.SyncTransforms();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (GameObject owner in ownedObjects)
                if (owner != null)
                    Object.Destroy(owner);
            ownedObjects.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator DiagonalTravelMatchesCardinalTravel()
        {
            PlayerMotor straight = CreatePlayer(new Vector3(-4f, 0f, 0f));
            PlayerMotor diagonal = CreatePlayer(new Vector3(4f, 0f, 0f));
            yield return null;
            Vector3 straightStart = straight.transform.position;
            Vector3 diagonalStart = diagonal.transform.position;

            for (int step = 0; step < 30; step++)
            {
                straight.Simulate(Vector2.up, false, StepTime);
                diagonal.Simulate(Vector2.one, false, StepTime);
            }

            float straightDistance = FlatDistance(straightStart, straight.transform.position);
            float diagonalDistance = FlatDistance(diagonalStart, diagonal.transform.position);
            Assert.That(straightDistance, Is.GreaterThan(2f));
            Assert.That(diagonalDistance, Is.EqualTo(straightDistance).Within(0.02f));
        }

        [UnityTest]
        public IEnumerator ReleasingInputStopsWithoutHorizontalDrift()
        {
            PlayerMotor motor = CreatePlayer(Vector3.zero);
            yield return null;
            for (int step = 0; step < 15; step++)
                motor.Simulate(Vector2.up, true, StepTime);
            Vector3 releasePosition = motor.transform.position;

            for (int step = 0; step < 30; step++)
                motor.Simulate(Vector2.zero, false, StepTime);

            Assert.That(FlatDistance(releasePosition, motor.transform.position), Is.LessThan(0.01f));
            Assert.That(motor.PlanarSpeed, Is.LessThan(0.01f));
            Assert.That(motor.transform.position.y, Is.InRange(-0.1f, 0.1f));
        }

        [UnityTest]
        public IEnumerator WallBlocksPlayerAndReportsActualStoppedSpeed()
        {
            PlayerMotor motor = CreatePlayer(Vector3.zero);
            GameObject wall = CreateObject("Test wall");
            wall.transform.position = new Vector3(0f, 1f, 2f);
            wall.AddComponent<BoxCollider>().size = new Vector3(4f, 2f, 0.2f);
            Physics.SyncTransforms();
            yield return null;

            for (int step = 0; step < 90; step++)
                motor.Simulate(Vector2.up, true, StepTime);

            Assert.That(motor.transform.position.z, Is.InRange(1f, 1.75f));
            Assert.That(motor.PlanarSpeed, Is.LessThan(0.05f));
            Assert.That((motor.LastCollisionFlags & CollisionFlags.Sides) != 0, Is.True);
        }

        [UnityTest]
        public IEnumerator MovementUsesCameraHeadingAndSprintIncreasesTravel()
        {
            Transform heading = CreateObject("Movement heading").transform;
            heading.rotation = Quaternion.Euler(52f, 90f, 0f);
            PlayerMotor walking = CreatePlayer(new Vector3(0f, 0f, -4f));
            PlayerMotor running = CreatePlayer(new Vector3(0f, 0f, 4f));
            walking.Configure(heading);
            running.Configure(heading);
            yield return null;
            Vector3 walkStart = walking.transform.position;
            Vector3 runStart = running.transform.position;

            for (int step = 0; step < 30; step++)
            {
                walking.Simulate(Vector2.up, false, StepTime);
                running.Simulate(Vector2.up, true, StepTime);
            }

            Assert.That(walking.transform.position.x - walkStart.x, Is.GreaterThan(2f));
            Assert.That(Mathf.Abs(walking.transform.position.z - walkStart.z), Is.LessThan(0.01f));
            Assert.That(FlatDistance(runStart, running.transform.position),
                Is.GreaterThan(FlatDistance(walkStart, walking.transform.position) * 1.4f));
        }

        [UnityTest]
        public IEnumerator CameraConvergesWithoutOvershootAndKeepsPitch()
        {
            Transform target = CreateObject("Camera target").transform;
            SmoothFollowCamera camera = CreateObject("Follow camera").AddComponent<SmoothFollowCamera>();
            camera.enabled = false;
            camera.Configure(target);
            Vector3 initialOffset = camera.transform.position - target.position;
            target.position = new Vector3(6f, 0f, 0f);
            Vector3 destination = target.position + initialOffset;
            float previousDistance = Vector3.Distance(camera.transform.position, destination);
            yield return null;

            for (int step = 0; step < 120; step++)
            {
                camera.Follow(StepTime);
                float remaining = Vector3.Distance(camera.transform.position, destination);
                Assert.That(remaining, Is.LessThanOrEqualTo(previousDistance + 0.001f));
                previousDistance = remaining;
            }

            Assert.That(previousDistance, Is.LessThan(0.02f));
            Assert.That(camera.transform.eulerAngles.x, Is.EqualTo(52f).Within(0.01f));
            Assert.That(camera.transform.eulerAngles.z, Is.EqualTo(0f).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator CameraMovesInFrontOfObstructionInsteadOfThroughIt()
        {
            Transform target = CreateObject("Camera obstruction target").transform;
            SmoothFollowCamera camera = CreateObject("Obstructed camera").AddComponent<SmoothFollowCamera>();
            camera.enabled = false;
            camera.Configure(target);
            Vector3 pivot = target.position + new Vector3(0f, 0.9f, 0f);
            Vector3 sightLine = (camera.transform.position - pivot).normalized;
            GameObject obstacle = CreateObject("Camera obstruction");
            obstacle.transform.position = pivot + sightLine * 5f;
            obstacle.AddComponent<BoxCollider>().size = Vector3.one * 2f;
            Physics.SyncTransforms();
            yield return null;

            camera.Follow(StepTime);
            Assert.That(Vector3.Distance(pivot, camera.transform.position), Is.LessThan(4f));
            Assert.That(Physics.Linecast(pivot, camera.transform.position, ~0,
                QueryTriggerInteraction.Ignore), Is.False);
        }

        private GameObject CreateObject(string name)
        {
            GameObject owner = new GameObject(name);
            ownedObjects.Add(owner);
            return owner;
        }

        private PlayerMotor CreatePlayer(Vector3 position)
        {
            GameObject owner = CreateObject("Test player");
            owner.transform.position = position;
            CharacterController controller = owner.AddComponent<CharacterController>();
            controller.center = Vector3.up;
            controller.height = 2f;
            controller.radius = 0.3f;
            controller.skinWidth = 0.03f;
            controller.minMoveDistance = 0f;
            PlayerMotor motor = owner.AddComponent<PlayerMotor>();
            motor.enabled = false;
            motor.Configure(null);
            Physics.SyncTransforms();
            return motor;
        }

        private static float FlatDistance(Vector3 start, Vector3 end)
        {
            return Vector3.ProjectOnPlane(end - start, Vector3.up).magnitude;
        }
    }
}
