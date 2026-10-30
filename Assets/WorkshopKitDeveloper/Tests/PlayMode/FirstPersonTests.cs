using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace UnityWorkshop.Tests
{
    /// <summary>First-person template: look, cursor lifecycle, respawn view, jumping and the shared mechanics.</summary>
    public sealed class FirstPersonTests : WorkshopPlayTestBase
    {
        const string FirstPersonExamplePath = "Assets/WorkshopKit/Examples/FirstPerson_Example.unity";

        FirstPersonCameraRig Rig => (FirstPersonCameraRig)m_Context.CameraRig;

        static Door DoorOf(ChannelColor channel) => Object.FindObjectsByType<Door>().Single(d => d.Channel == channel);

        static KeyPickup KeyOf(ChannelColor channel) => Object.FindObjectsByType<KeyPickup>().Single(k => k.Channel == channel);

        [UnityTest]
        public IEnumerator StartsLevelFacingTheStartWithCursorCaptured()
        {
            yield return StartScene(FirstPersonExamplePath);
            var spawn = Object.FindObjectsByType<PlayerSpawn>().Single();
            Assert.That(m_Context.CursorShouldLock, Is.True);
            Assert.That(Rig.Pitch, Is.EqualTo(0f));
            Assert.That(Mathf.DeltaAngle(Rig.Yaw, spawn.SpawnPose.rotation.eulerAngles.y), Is.EqualTo(0f).Within(0.1f));
            Assert.That(Vector3.Distance(Rig.transform.position, m_Context.Player.transform.position + Vector3.up * FirstPersonCameraRig.EyeHeight),
                Is.LessThan(0.05f), "The camera sits at eye height on the player.");
            Assert.That(Object.FindObjectsByType<Camera>().Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<AudioListener>().Length, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator PauseFocusAndResultsReleaseTheCursor()
        {
            yield return StartScene(FirstPersonExamplePath);
            m_Context.TogglePause();
            Assert.That(m_Context.CursorShouldLock, Is.False, "Paused: the mouse is free for the menu.");
            m_Context.TogglePause();
            Assert.That(m_Context.CursorShouldLock, Is.True, "Resumed: captured again.");

            m_Context.HandleFocusChanged(false);
            Assert.That(Session.State, Is.EqualTo(GameState.Paused), "Losing focus pauses.");
            Assert.That(m_Context.CursorShouldLock, Is.False);
            m_Context.HandleFocusChanged(true);
            Assert.That(Session.State, Is.EqualTo(GameState.Paused), "Regaining focus waits for the player to resume.");
            m_Context.TogglePause();
            Assert.That(m_Context.CursorShouldLock, Is.True);

            yield return MovePlayerTo(Object.FindObjectsByType<GoalZone>().Single().transform.position);
            Assert.That(Session.State, Is.EqualTo(GameState.Won));
            Assert.That(m_Context.CursorShouldLock, Is.False, "The results screen frees the mouse.");
        }

        [UnityTest]
        public IEnumerator LookIsClampedAndNeverFlips()
        {
            yield return StartScene(FirstPersonExamplePath);
            for (var i = 0; i < 50; i++)
            {
                Rig.ApplyLook(new Vector2(37f, -400f));
                Assert.That(Rig.Camera.transform.up.y, Is.GreaterThan(0f), "The view never turns upside down.");
            }
            Assert.That(Rig.Pitch, Is.EqualTo(FirstPersonCameraRig.MaxPitch));
            for (var i = 0; i < 50; i++)
                Rig.ApplyLook(new Vector2(0f, 400f));
            Assert.That(Rig.Pitch, Is.EqualTo(-FirstPersonCameraRig.MaxPitch));
            Assert.That(Rig.Camera.transform.up.y, Is.GreaterThan(0f));

            Rig.ApplyLook(new Vector2(10000f, 0f));
            Assert.That(Rig.Yaw, Is.InRange(0f, 360f), "A large mouse jump is limited and wraps.");

            var before = Rig.Yaw;
            Rig.ApplyLook(new Vector2(100f, 0f));
            var expected = Mathf.Repeat(before + 100f * FirstPersonCameraRig.DegreesPerPixel(Rig.Sensitivity), 360f);
            Assert.That(Mathf.DeltaAngle(Rig.Yaw, expected), Is.EqualTo(0f).Within(0.01f));

            Rig.GetMoveBasis(out var forward, out _);
            Assert.That(forward.y, Is.EqualTo(0f), "Looking up or down never makes W climb or dig.");
            yield return null;
            Assert.That(Mathf.DeltaAngle(m_Context.Player.transform.eulerAngles.y, Rig.Yaw), Is.EqualTo(0f).Within(0.5f),
                "The body faces where the camera looks.");
        }

        [UnityTest]
        public IEnumerator RespawnRestoresPoseAndView()
        {
            yield return StartScene(FirstPersonExamplePath);
            var spawn = Object.FindObjectsByType<PlayerSpawn>().Single();
            Rig.ApplyLook(new Vector2(600f, -300f));
            Assert.That(Rig.Pitch, Is.Not.EqualTo(0f));

            yield return MovePlayerTo(StaticHazard().transform.position);
            Assert.That(Session.State, Is.EqualTo(GameState.Respawning));
            Assert.That(Rig.Pitch, Is.EqualTo(0f));
            Assert.That(Mathf.DeltaAngle(Rig.Yaw, spawn.SpawnPose.rotation.eulerAngles.y), Is.EqualTo(0f).Within(0.1f));
            Assert.That(m_Context.Player.Motor.Velocity.magnitude, Is.LessThan(2.1f), "Only the ground-stick speed remains.");
            yield return WaitWhileRespawning();
            Assert.That(Mathf.DeltaAngle(m_Context.Player.transform.eulerAngles.y, spawn.SpawnPose.rotation.eulerAngles.y),
                Is.EqualTo(0f).Within(0.5f));
        }

        [UnityTest]
        public IEnumerator JumpReachesThePresetHeightAndLands()
        {
            yield return StartScene(FirstPersonExamplePath);
            var player = m_Context.Player;
            var motor = player.Motor;
            player.enabled = false; // drive the motor directly
            player.PlaceAt(new Pose(new Vector3(-8f, 0.05f, -8f), Quaternion.identity));
            for (var i = 0; i < 10; i++)
            {
                motor.Step(Vector3.zero, false, true, 1f / 60f);
                yield return null;
            }
            Assert.That(motor.IsGrounded, Is.True);

            var peak = 0f;
            for (var i = 0; i < 120; i++)
            {
                motor.Step(Vector3.zero, i == 0, true, 1f / 60f);
                peak = Mathf.Max(peak, player.transform.position.y);
                yield return null;
            }
            Assert.That(peak, Is.EqualTo(motor.Settings.JumpHeight).Within(0.15f));
            Assert.That(motor.IsGrounded, Is.True, "Lands again.");
            Assert.That(player.transform.position.y, Is.EqualTo(0f).Within(0.1f));
        }

        [UnityTest]
        public IEnumerator JumpingOntoThePlatformWorksAndWalkingDoesNot()
        {
            yield return StartScene(FirstPersonExamplePath);
            var player = m_Context.Player;
            var motor = player.Motor;
            player.enabled = false;

            // Walking into the 1 m platform is blocked.
            player.PlaceAt(new Pose(new Vector3(-8f, 0.05f, -5f), Quaternion.identity));
            for (var i = 0; i < 90; i++)
            {
                motor.Step(Vector3.forward, false, true, 1f / 60f);
                yield return null;
            }
            Assert.That(player.transform.position.y, Is.LessThan(0.2f));
            Assert.That(player.transform.position.z, Is.LessThan(-3f + 0.01f));

            // A jump while walking forward lands on top.
            player.PlaceAt(new Pose(new Vector3(-8f, 0.05f, -5f), Quaternion.identity));
            for (var i = 0; i < 120; i++)
            {
                motor.Step(i < 30 ? Vector3.forward : Vector3.zero, i == 8, true, 1f / 60f);
                yield return null;
            }
            Assert.That(player.transform.position.y, Is.EqualTo(1f).Within(0.1f), "Standing on the platform.");
            Assert.That(motor.IsGrounded, Is.True);
        }

        [UnityTest]
        public IEnumerator KeysCheckpointsAndDoorsWorkInFirstPerson()
        {
            yield return StartScene(FirstPersonExamplePath);
            yield return MovePlayerTo(KeyOf(ChannelColor.Red).transform.position);
            Assert.That(KeyOf(ChannelColor.Red).IsCollected, Is.True, "The key on the platform can be collected.");
            yield return null;
            Assert.That(DoorOf(ChannelColor.Red).IsOpen, Is.True);
            Assert.That(DoorOf(ChannelColor.Blue).IsOpen || DoorOf(ChannelColor.Green).IsOpen, Is.False);

            var checkpoint = Object.FindObjectsByType<Checkpoint>().OrderBy(c => c.transform.position.x).First();
            yield return MovePlayerTo(checkpoint.transform.position);
            Assert.That(m_Context.ActiveCheckpoint, Is.EqualTo(checkpoint));
            Rig.ApplyLook(new Vector2(-500f, 200f));
            yield return MovePlayerTo(StaticHazard().transform.position);
            Assert.That(Vector3.Distance(m_Context.Player.transform.position, checkpoint.RespawnPose.position), Is.LessThan(0.5f));
            Assert.That(Mathf.DeltaAngle(Rig.Yaw, checkpoint.RespawnPose.rotation.eulerAngles.y), Is.EqualTo(0f).Within(0.1f),
                "Respawning at a checkpoint faces its direction.");
            yield return WaitWhileRespawning();

            yield return MovePlayerTo(KeyOf(ChannelColor.Blue).transform.position);
            yield return MovePlayerTo(KeyOf(ChannelColor.Green).transform.position);
            yield return null;
            Assert.That(DoorOf(ChannelColor.Blue).IsOpen && DoorOf(ChannelColor.Green).IsOpen, Is.True);

            yield return MovePlayerTo(Object.FindObjectsByType<GoalZone>().Single().transform.position);
            Assert.That(Session.State, Is.EqualTo(GameState.Won));
        }

        [UnityTest]
        public IEnumerator RestartRestoresTheFirstPersonGame()
        {
            yield return StartScene(FirstPersonExamplePath);
            for (var cycle = 0; cycle < 10; cycle++)
            {
                yield return MovePlayerTo(KeyOf(ChannelColor.Red).transform.position);
                Rig.ApplyLook(new Vector2(300f, 300f));
                if (cycle == 1)
                    m_Context.TogglePause();
                var previous = m_Context;
                previous.RestartGame();
                yield return WaitUntilPlaying(previous);

                Assert.That(KeyOf(ChannelColor.Red).IsCollected, Is.False, $"cycle {cycle}");
                Assert.That(DoorOf(ChannelColor.Red).IsOpen, Is.False);
                Assert.That(Rig.Pitch, Is.EqualTo(0f));
                Assert.That(m_Context.CursorShouldLock, Is.True);
                Assert.That(Time.timeScale, Is.EqualTo(1f));
            }
        }
    }
}
