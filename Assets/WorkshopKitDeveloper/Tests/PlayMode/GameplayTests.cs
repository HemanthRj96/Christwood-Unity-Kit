using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace UnityWorkshop.Tests
{
    /// <summary>
    /// Plays the generated scenes and drives the real components through physics triggers
    /// (A03 to A07 for the top-down template).
    /// </summary>
    public sealed class GameplayTests : WorkshopPlayTestBase
    {
        [UnityTest]
        public IEnumerator StartsAtStartPointAndPlaying()
        {
            yield return StartScene(ExamplePath);
            var spawn = Object.FindObjectsByType<PlayerSpawn>().Single();
            Assert.That(Vector3.Distance(m_Context.Player.transform.position, spawn.SpawnPose.position), Is.LessThan(0.5f));
            Assert.That(Session.RequiredCount, Is.EqualTo(6));
            Assert.That(m_Context.CameraRig, Is.Not.Null);
            Assert.That(Object.FindObjectsByType<AudioListener>().Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<Camera>().Length, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator CollectAllThenExitWinsOnce()
        {
            yield return StartScene(ExamplePath);
            var goal = Object.FindObjectsByType<GoalZone>().Single();
            var blocked = 0;
            var wins = 0;
            Session.GoalBlocked += _ => blocked++;
            Session.StateChanged += (_, next) => { if (next == GameState.Won) wins++; };

            yield return MovePlayerTo(goal.transform.position);
            Assert.That(Session.State, Is.EqualTo(GameState.Playing));
            Assert.That(blocked, Is.EqualTo(1), "Entering a locked exit explains what is missing once.");

            foreach (var collectible in Collectibles())
            {
                var before = Session.CollectedCount;
                yield return MovePlayerTo(collectible.transform.position);
                Assert.That(collectible.IsCollected, Is.True, collectible.name);
                Assert.That(Session.CollectedCount, Is.EqualTo(before + 1));
                yield return MovePlayerTo(collectible.transform.position + Vector3.right * 0.01f);
                Assert.That(Session.CollectedCount, Is.EqualTo(before + 1), "Touching again does not count twice.");
            }

            yield return MovePlayerTo(goal.transform.position);
            Assert.That(Session.State, Is.EqualTo(GameState.Won));
            yield return MovePlayerTo(goal.transform.position + Vector3.forward * 0.2f);
            Assert.That(wins, Is.EqualTo(1));
            Assert.That(Session.AcceptsPlayerInput, Is.False);
        }

        [UnityTest]
        public IEnumerator HazardRespawnsAndKeepsProgress()
        {
            yield return StartScene(ExamplePath);
            var spawn = Object.FindObjectsByType<PlayerSpawn>().Single();
            var hazard = StaticHazard();
            var respawns = 0;
            Session.RespawnStarted += () => respawns++;

            yield return MovePlayerTo(Collectibles()[0].transform.position);
            Assert.That(Session.CollectedCount, Is.EqualTo(1));
            yield return new WaitForSeconds(0.2f);
            var clock = Session.ElapsedTime;

            yield return MovePlayerTo(hazard.transform.position);
            Assert.That(respawns, Is.EqualTo(1), "Overlapping trigger callbacks start one respawn.");
            Assert.That(Vector3.Distance(m_Context.Player.transform.position, spawn.SpawnPose.position), Is.LessThan(0.5f));
            Assert.That(Session.State, Is.EqualTo(GameState.Respawning));

            yield return new WaitForSeconds(WorkshopGameSession.RespawnDuration + 0.1f);
            Assert.That(Session.State, Is.EqualTo(GameState.Playing));
            Assert.That(Session.CollectedCount, Is.EqualTo(1));
            Assert.That(Session.ElapsedTime, Is.GreaterThan(clock));
            Assert.That(Collectibles()[0].IsCollected, Is.True);

            yield return MovePlayerTo(hazard.transform.position);
            Assert.That(respawns, Is.EqualTo(1), "Protected right after a respawn.");
            yield return new WaitForSeconds(WorkshopGameSession.HazardProtectionDuration + 0.1f);
            Assert.That(respawns, Is.EqualTo(2), "Still standing in the hazard after protection ends respawns again.");
        }

        [UnityTest]
        public IEnumerator PauseFreezesAndResumes()
        {
            yield return StartScene(ExamplePath);
            m_Context.TogglePause();
            Assert.That(Session.State, Is.EqualTo(GameState.Paused));
            Assert.That(Time.timeScale, Is.EqualTo(0f));
            var clock = Session.ElapsedTime;
            yield return null;
            yield return null;
            Assert.That(Session.ElapsedTime, Is.EqualTo(clock));

            var hazard = StaticHazard();
            m_Context.Player.PlaceAt(new Pose(hazard.transform.position, Quaternion.identity));
            yield return null;
            Assert.That(Session.State, Is.EqualTo(GameState.Paused), "No physics callbacks while paused.");

            m_Context.TogglePause();
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            for (var i = 0; i < 4; i++)
                yield return new WaitForFixedUpdate();
            Assert.That(Session.State, Is.EqualTo(GameState.Respawning));
        }

        [UnityTest]
        public IEnumerator RestartRestoresEverythingTenTimes()
        {
            yield return StartScene(ExamplePath);
            for (var cycle = 0; cycle < 10; cycle++)
            {
                yield return MovePlayerTo(Collectibles()[cycle % 6].transform.position);
                Assert.That(Session.CollectedCount, Is.EqualTo(1), $"cycle {cycle}");
                if (cycle % 3 == 1)
                    m_Context.TogglePause();

                var previous = m_Context;
                var previousSession = previous.Session;
                var events = 0;
                previousSession.ProgressChanged += () => events++;
                previous.RestartGame();
                previous.RestartGame();
                yield return WaitUntilPlaying(previous);

                Assert.That(Object.FindObjectsByType<WorkshopGameContext>().Length, Is.EqualTo(1));
                Assert.That(previousSession.IsShutDown, Is.True);
                Assert.That(events, Is.EqualTo(0), "The old session raises nothing after restart.");
                Assert.That(Session.CollectedCount, Is.EqualTo(0));
                Assert.That(Session.RequiredCount, Is.EqualTo(6));
                Assert.That(Session.ElapsedTime, Is.LessThan(0.5f));
                Assert.That(Collectibles().All(c => !c.IsCollected), Is.True);
                Assert.That(Time.timeScale, Is.EqualTo(1f));
            }
        }

        [UnityTest]
        public IEnumerator StarterSceneIsWinnable()
        {
            yield return StartScene(StarterPath);
            foreach (var collectible in Collectibles())
                yield return MovePlayerTo(collectible.transform.position);
            yield return MovePlayerTo(Object.FindObjectsByType<GoalZone>().Single().transform.position);
            Assert.That(Session.State, Is.EqualTo(GameState.Won));
        }

        [UnityTest]
        public IEnumerator MotorWalksOnFloorAndStopsAtWalls()
        {
            yield return StartScene(ExamplePath);
            var player = m_Context.Player;
            var motor = player.Motor;
            player.enabled = false; // the test drives the motor directly instead of keyboard input
            player.PlaceAt(new Pose(new Vector3(-8f, 0.05f, -8f), Quaternion.identity));

            for (var i = 0; i < 60; i++)
            {
                motor.Step(Vector3.right, false, false, 1f / 60f);
                yield return null;
            }
            var x = player.transform.position.x;
            Assert.That(motor.IsGrounded, Is.True);
            Assert.That(x, Is.GreaterThan(-8f + 2f), "Moved right.");
            Assert.That(x, Is.LessThan(-4.2f + 0.01f), "Stopped by the maze wall at x = -4.");
            Assert.That(player.transform.position.y, Is.EqualTo(0f).Within(0.1f), "Stayed on the floor.");
        }
    }
}
