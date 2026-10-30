using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
#endif

namespace UnityWorkshop.Tests
{
    /// <summary>
    /// Third-person template: orbit, wall collision, camera-relative movement through the real input
    /// actions, coherent facing, the animation adapter, and the shared mechanics on the Sky Steps route.
    /// </summary>
    public sealed class ThirdPersonTests : WorkshopPlayTestBase
    {
        const string ThirdPersonExamplePath = "Assets/WorkshopKit/Examples/ThirdPerson_Example.unity";
        const string TempControllerPath = "Assets/WorkshopKitDeveloper/Tests/ZZ_AdapterTest.controller";

        ThirdPersonCameraRig Rig => (ThirdPersonCameraRig)m_Context.CameraRig;

        [TearDown]
        public void DeleteTempController()
        {
#if UNITY_EDITOR
            AssetDatabase.DeleteAsset(TempControllerPath);
#endif
        }

        /// <summary>Turns the orbit by mouse movement, in steps below the per-frame cap.</summary>
        void OrbitRight(float degrees)
        {
            var pixels = degrees / FirstPersonCameraRig.DegreesPerPixel(Rig.Sensitivity);
            while (pixels > 0.001f)
            {
                var step = Mathf.Min(pixels, 200f);
                Rig.ApplyLook(new Vector2(step, 0f));
                pixels -= step;
            }
        }

        bool CameraInsideGeometry()
        {
            var hits = Physics.OverlapSphere(Rig.Camera.transform.position, 0.15f, ~0, QueryTriggerInteraction.Ignore);
            return hits.Any(h => !h.transform.IsChildOf(m_Context.Player.transform));
        }

        [UnityTest]
        public IEnumerator StartsBehindThePlayerAtFullDistance()
        {
            yield return StartScene(ThirdPersonExamplePath);
            var spawn = Object.FindObjectsByType<PlayerSpawn>().Single();
            Assert.That(Mathf.DeltaAngle(Rig.Yaw, spawn.SpawnPose.rotation.eulerAngles.y), Is.EqualTo(0f).Within(0.1f));
            Assert.That(Rig.Pitch, Is.EqualTo(ThirdPersonCameraRig.DefaultPitch));
            Assert.That(Rig.CurrentDistance, Is.EqualTo(Rig.FullDistance).Within(0.05f));
            Assert.That(m_Context.CursorShouldLock, Is.True);
            var toPlayer = m_Context.Player.transform.position - Rig.Camera.transform.position;
            Assert.That(Vector3.Dot(toPlayer.normalized, Rig.Camera.transform.forward), Is.GreaterThan(0.9f), "The camera looks at the player.");
        }

        [UnityTest]
        public IEnumerator OrbitIsClampedAndNeverFlips()
        {
            yield return StartScene(ThirdPersonExamplePath);
            for (var i = 0; i < 40; i++)
                Rig.ApplyLook(new Vector2(25f, -400f));
            Assert.That(Rig.Pitch, Is.EqualTo(ThirdPersonCameraRig.MaxPitch));
            Assert.That(Rig.Camera.transform.up.y, Is.GreaterThan(0f));
            for (var i = 0; i < 40; i++)
                Rig.ApplyLook(new Vector2(0f, 400f));
            Assert.That(Rig.Pitch, Is.EqualTo(ThirdPersonCameraRig.MinPitch));
            Assert.That(Rig.Camera.transform.up.y, Is.GreaterThan(0f));
            Rig.GetMoveBasis(out var forward, out _);
            Assert.That(forward.y, Is.EqualTo(0f));
        }

        [UnityTest]
        public IEnumerator KeyboardMovesRelativeToTheCameraAndTheCharacterTurns()
        {
            yield return StartScene(ThirdPersonExamplePath);
            yield return MovePlayerTo(new Vector3(0f, 0f, -10f));
            // Orbit the camera 90° to the right: W now walks toward +X.
            OrbitRight(90f);
            Assert.That(Rig.Yaw, Is.EqualTo(90f).Within(0.1f));

            var start = m_Context.Player.transform.position;
            yield return Hold(0.4f, Key.W);
            var moved = m_Context.Player.transform.position - start;
            Assert.That(moved.x, Is.GreaterThan(1f), "W walks away from the camera.");
            Assert.That(Mathf.Abs(moved.z), Is.LessThan(0.2f));
            Assert.That(Mathf.DeltaAngle(m_Context.Player.transform.eulerAngles.y, 90f), Is.EqualTo(0f).Within(5f),
                "The character turns to face where it walks.");

            start = m_Context.Player.transform.position;
            yield return Hold(0.4f, Key.A);
            moved = m_Context.Player.transform.position - start;
            Assert.That(moved.z, Is.GreaterThan(1f), "A strafes to the camera's left (+Z when facing +X).");
            Assert.That(Mathf.DeltaAngle(m_Context.Player.transform.eulerAngles.y, 0f), Is.EqualTo(0f).Within(5f));
            Assert.That(Rig.Yaw, Is.EqualTo(90f).Within(0.1f), "Walking does not turn the camera.");
        }

        [UnityTest]
        public IEnumerator WallsPullTheCameraInAndItEasesBackOut()
        {
            yield return StartScene(ThirdPersonExamplePath);
            // Close to the south wall, looking north: the camera would be behind the wall.
            yield return MovePlayerTo(new Vector3(0f, 0f, -13f));
            yield return null;
            Assert.That(Rig.CurrentDistance, Is.LessThan(1.2f));
            Assert.That(CameraInsideGeometry(), Is.False, "The camera never ends up inside the wall.");

            yield return MovePlayerTo(new Vector3(0f, 0f, -6f));
            Assert.That(Rig.CurrentDistance, Is.LessThan(Rig.FullDistance - 0.5f), "It does not pop straight back.");
            var previous = Rig.CurrentDistance;
            yield return null;
            Assert.That(Rig.CurrentDistance, Is.GreaterThanOrEqualTo(previous), "It moves out steadily.");
            yield return WaitSeconds(1.2f);
            Assert.That(Rig.CurrentDistance, Is.EqualTo(Rig.FullDistance).Within(0.01f), "Full distance once clear.");

            // Orbiting so the camera swings toward the west canyon wall pulls it in again.
            yield return MovePlayerTo(new Vector3(-2.8f, 0f, -2.5f));
            OrbitRight(90f);
            yield return null;
            yield return null;
            Assert.That(Rig.CurrentDistance, Is.LessThan(Rig.FullDistance - 1f));
            Assert.That(CameraInsideGeometry(), Is.False);
        }

        [UnityTest]
        public IEnumerator RespawnResetsTheOrbitBehindThePlayer()
        {
            yield return StartScene(ThirdPersonExamplePath);
            var spawn = Object.FindObjectsByType<PlayerSpawn>().Single();
            Rig.ApplyLook(new Vector2(240f, 200f));
            yield return MovePlayerTo(StaticHazard().transform.position);
            Assert.That(Session.State, Is.EqualTo(GameState.Respawning));
            Assert.That(Mathf.DeltaAngle(Rig.Yaw, spawn.SpawnPose.rotation.eulerAngles.y), Is.EqualTo(0f).Within(0.1f));
            Assert.That(Rig.Pitch, Is.EqualTo(ThirdPersonCameraRig.DefaultPitch));
        }

        [UnityTest]
        public IEnumerator SkyStepsRouteWorks()
        {
            yield return StartScene(ThirdPersonExamplePath);
            foreach (var gem in Collectibles().Where(c => c.transform.position.z < 6f))
                yield return MovePlayerTo(gem.transform.position);
            Assert.That(Session.CollectedCount, Is.EqualTo(3), "Ground gems and the platform gem.");

            var checkpoint = Object.FindObjectsByType<Checkpoint>().Single();
            yield return MovePlayerTo(checkpoint.transform.position);
            Assert.That(m_Context.ActiveCheckpoint, Is.EqualTo(checkpoint));

            // Missing the floating stone: the pit's fall zone returns the player to the checkpoint.
            yield return MovePlayerTo(new Vector3(0f, -2.4f, 6.5f));
            yield return null;
            Assert.That(Session.State, Is.EqualTo(GameState.Respawning));
            Assert.That(Vector3.Distance(m_Context.Player.transform.position, checkpoint.RespawnPose.position), Is.LessThan(0.5f));
            yield return WaitWhileRespawning();

            foreach (var gem in Collectibles().Where(c => c.transform.position.z >= 6f))
                yield return MovePlayerTo(gem.transform.position);
            Assert.That(Session.CollectedCount, Is.EqualTo(5));
            yield return MovePlayerTo(Object.FindObjectsByType<GoalZone>().Single().transform.position);
            Assert.That(Session.State, Is.EqualTo(GameState.Won));
            Assert.That(m_Context.CursorShouldLock, Is.False);
        }

        [UnityTest]
        public IEnumerator RestartRestoresTheThirdPersonGameTenTimes()
        {
            yield return StartScene(ThirdPersonExamplePath);
            for (var cycle = 0; cycle < 10; cycle++)
            {
                yield return MovePlayerTo(Collectibles()[cycle % 5].transform.position);
                Rig.ApplyLook(new Vector2(200f, 100f));
                if (cycle % 3 == 1)
                    m_Context.TogglePause();
                var previous = m_Context;
                previous.RestartGame();
                yield return WaitUntilPlaying(previous);
                Assert.That(Session.CollectedCount, Is.EqualTo(0), $"cycle {cycle}");
                Assert.That(Collectibles().All(c => !c.IsCollected), Is.True);
                Assert.That(Rig.Pitch, Is.EqualTo(ThirdPersonCameraRig.DefaultPitch));
                Assert.That(Time.timeScale, Is.EqualTo(1f));
                Assert.That(m_Context.CursorShouldLock, Is.True);
            }
        }

#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator AnimationAdapterDrivesOnlyTheParametersThatExist()
        {
            yield return StartScene(ThirdPersonExamplePath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(TempControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);

            var adapter = m_Context.Player.GetComponent<PlayerVisualAdapter>();
            var animator = adapter.Visual.gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = true;
            adapter.UseAnimator(animator);
            Assert.That(animator.applyRootMotion, Is.False, "The workshop motor moves the player, never root motion.");

            yield return MovePlayerTo(new Vector3(0f, 0f, -12f));
            Keys.Press(Key.W);
            yield return WaitSeconds(0.4f);
            Assert.That(animator.GetFloat(PlayerVisualAdapter.SpeedParameter), Is.GreaterThan(0.9f));
            Assert.That(animator.GetBool(PlayerVisualAdapter.GroundedParameter), Is.True);
            Keys.ReleaseAll();
            yield return WaitSeconds(0.4f);
            Assert.That(animator.GetFloat(PlayerVisualAdapter.SpeedParameter), Is.LessThan(0.05f), "Speed falls to zero when stopped.");
            Assert.That(adapter.Visual.localPosition, Is.EqualTo(Vector3.zero), "The model stays where the adapter placed it.");
        }
#endif
    }
}
