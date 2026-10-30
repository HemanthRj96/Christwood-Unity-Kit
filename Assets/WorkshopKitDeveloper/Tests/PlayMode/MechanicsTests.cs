using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace UnityWorkshop.Tests
{
    /// <summary>Phase 2 mechanics in the example scene: checkpoints, falling, moving hazards, channels and scene isolation.</summary>
    public sealed class MechanicsTests : WorkshopPlayTestBase
    {
        static Door DoorOf(ChannelColor channel) => Object.FindObjectsByType<Door>().Single(d => d.Channel == channel);

        static float Distance(Vector3 a, Vector3 b) => Vector3.Distance(a, b);

        [UnityTest]
        public IEnumerator CheckpointBecomesTheRespawnPoint()
        {
            yield return StartScene(ExamplePath);
            var checkpoint = Object.FindObjectsByType<Checkpoint>().OrderBy(c => c.transform.position.x).First();
            var reached = 0;
            m_Context.CheckpointActivated += _ => reached++;

            yield return MovePlayerTo(Collectibles()[0].transform.position);
            yield return MovePlayerTo(checkpoint.transform.position);
            Assert.That(checkpoint.IsActive, Is.True);
            Assert.That(m_Context.ActiveCheckpoint, Is.EqualTo(checkpoint));
            yield return MovePlayerTo(checkpoint.transform.position + Vector3.right * 0.1f);
            Assert.That(reached, Is.EqualTo(1), "Standing on an active checkpoint does not re-trigger it.");

            yield return MovePlayerTo(StaticHazard().transform.position);
            Assert.That(Session.State, Is.EqualTo(GameState.Respawning));
            Assert.That(Distance(m_Context.Player.transform.position, checkpoint.RespawnPose.position), Is.LessThan(0.5f));
            Assert.That(Quaternion.Angle(m_Context.Player.transform.rotation, checkpoint.RespawnPose.rotation), Is.LessThan(1f));
            Assert.That(Session.CollectedCount, Is.EqualTo(1), "Respawning keeps collected items.");

            var other = Object.FindObjectsByType<Checkpoint>().Single(c => c != checkpoint);
            yield return WaitWhileRespawning();
            yield return MovePlayerTo(other.transform.position);
            Assert.That(other.IsActive, Is.True);
            Assert.That(checkpoint.IsActive, Is.False, "Only the latest checkpoint is active.");
        }

        [UnityTest]
        public IEnumerator FallingBelowTheLevelReturnsThePlayer()
        {
            yield return StartScene(ExamplePath);
            var respawns = 0;
            Session.RespawnStarted += () => respawns++;

            m_Context.Player.PlaceAt(new Pose(new Vector3(0f, m_Context.FallHeight - 1f, 0f), Quaternion.identity));
            yield return null;
            yield return null;
            Assert.That(respawns, Is.EqualTo(1));
            Assert.That(Distance(m_Context.Player.transform.position, m_Context.StartPose.position), Is.LessThan(0.5f));

            yield return WaitWhileRespawning();
            Assert.That(Session.IsHazardProtected, Is.True);
            m_Context.Player.PlaceAt(new Pose(new Vector3(0f, m_Context.FallHeight - 1f, 0f), Quaternion.identity));
            yield return null;
            yield return null;
            Assert.That(respawns, Is.EqualTo(2), "Falling is caught even during hazard protection.");
        }

        [UnityTest]
        public IEnumerator RepeatedFallsDropTheCheckpointThenStopWithAnExplanation()
        {
            yield return StartScene(ExamplePath);
            var checkpoint = Object.FindObjectsByType<Checkpoint>().First();
            yield return MovePlayerTo(checkpoint.transform.position);
            Assert.That(m_Context.ActiveCheckpoint, Is.EqualTo(checkpoint));

            for (var fall = 0; fall < 3; fall++)
            {
                m_Context.Player.PlaceAt(new Pose(new Vector3(0f, m_Context.FallHeight - 1f, 0f), Quaternion.identity));
                yield return null;
                yield return WaitWhileRespawning();
            }
            Assert.That(m_Context.ActiveCheckpoint, Is.Null, "After three immediate falls the checkpoint is abandoned.");
            Assert.That(checkpoint.IsActive, Is.False);
            Assert.That(Distance(m_Context.Player.transform.position, m_Context.StartPose.position), Is.LessThan(0.5f));

            LogAssert.Expect(LogType.Error, new Regex("keeps falling"));
            for (var fall = 0; fall < 3 && m_Context.SetupError == null; fall++)
            {
                m_Context.Player.PlaceAt(new Pose(new Vector3(0f, m_Context.FallHeight - 1f, 0f), Quaternion.identity));
                yield return null;
                if (m_Context.SetupError == null)
                    yield return WaitWhileRespawning();
            }
            Assert.That(m_Context.SetupError, Does.Contain("keeps falling"), "The game stops instead of looping forever.");
            var position = m_Context.Player.transform.position;
            yield return null;
            Assert.That(m_Context.Player.transform.position, Is.EqualTo(position), "The stopped player no longer moves.");
        }

        [UnityTest]
        public IEnumerator FallZoneCatchesThePlayer()
        {
            yield return StartScene(ExamplePath);
            var zone = new GameObject("Test Fall Zone", typeof(BoxCollider));
            zone.transform.position = new Vector3(-7f, 0.5f, -6f);
            zone.GetComponent<BoxCollider>().size = new Vector3(2f, 2f, 2f);
            zone.AddComponent<FallZone>();
            yield return null;

            yield return MovePlayerTo(new Vector3(-7f, 0f, -6f));
            Assert.That(Session.State, Is.EqualTo(GameState.Respawning));
        }

        [UnityTest]
        public IEnumerator MovingHazardMovesFreezesAndResetsOnRestart()
        {
            yield return StartScene(ExamplePath);
            var mover = Object.FindObjectsByType<HazardMover>().Single();
            var start = new Vector3(-3f, 0f, 2f);
            yield return new WaitForSeconds(0.6f);
            Assert.That(Distance(mover.transform.position, start), Is.GreaterThan(0.2f), "It moves.");

            m_Context.TogglePause();
            var paused = mover.transform.position;
            for (var i = 0; i < 10; i++)
                yield return null;
            Assert.That(mover.transform.position, Is.EqualTo(paused), "Pause freezes it.");
            m_Context.TogglePause();

            yield return MovePlayerTo(mover.transform.position);
            Assert.That(Session.State, Is.EqualTo(GameState.Respawning), "Touching it sends the player back.");

            var previous = m_Context;
            previous.RestartGame();
            yield return WaitUntilPlaying(previous);
            mover = Object.FindObjectsByType<HazardMover>().Single();
            Assert.That(Distance(mover.transform.position, start), Is.LessThan(0.3f), "Restart puts it back at its start.");
        }

        [UnityTest]
        public IEnumerator KeyOpensOnlyItsOwnDoor()
        {
            yield return StartScene(ExamplePath);
            var red = DoorOf(ChannelColor.Red);
            var blue = DoorOf(ChannelColor.Blue);
            Assert.That(red.IsOpen || blue.IsOpen, Is.False);
            Assert.That(red.Blocker.enabled && blue.Blocker.enabled, Is.True);

            var key = Object.FindObjectsByType<KeyPickup>().Single();
            yield return MovePlayerTo(key.transform.position);
            Assert.That(key.IsCollected, Is.True);
            Assert.That(Session.HasKey(ChannelColor.Red), Is.True);
            yield return null;
            Assert.That(red.IsOpen, Is.True);
            Assert.That(red.Blocker.enabled, Is.False, "An open door has no collision.");
            Assert.That(blue.IsOpen, Is.False, "Other channels stay closed.");
            Assert.That(blue.Blocker.enabled, Is.True);

            yield return MovePlayerTo(new Vector3(-8f, 0f, -6f));
            yield return null;
            Assert.That(red.IsOpen, Is.True, "A key keeps its doors open.");
        }

        [UnityTest]
        public IEnumerator LatchingSwitchKeepsDoorOpen()
        {
            yield return StartScene(ExamplePath);
            var plate = Object.FindObjectsByType<TriggerSwitch>().Single();
            Assert.That(plate.Latch, Is.True);
            var blue = DoorOf(ChannelColor.Blue);

            yield return MovePlayerTo(plate.transform.position);
            yield return Steps();
            Assert.That(blue.IsOpen, Is.True);
            yield return MovePlayerTo(new Vector3(6f, 0f, -6f));
            yield return Steps();
            Assert.That(blue.IsOpen, Is.True, "Latched switches keep doors open.");
            Assert.That(DoorOf(ChannelColor.Red).IsOpen, Is.False);
        }

        [UnityTest]
        public IEnumerator DoorWaitsForAnEmptyDoorwayBeforeClosing()
        {
            yield return StartScene(ExamplePath);
            var plate = Object.FindObjectsByType<TriggerSwitch>().Single();
            plate.Configure(latch: false);
            var blue = DoorOf(ChannelColor.Blue);

            yield return MovePlayerTo(plate.transform.position);
            yield return Steps();
            Assert.That(blue.IsOpen, Is.True);

            // Stepping off the switch into the doorway: the door must wait.
            yield return MovePlayerTo(blue.transform.position);
            yield return Steps(6);
            Assert.That(Session.IsChannelOpen(ChannelColor.Blue), Is.False, "The switch is released.");
            Assert.That(blue.IsWaitingToClose, Is.True);
            Assert.That(blue.Blocker.enabled, Is.False, "The player is never trapped inside a closing door.");

            yield return MovePlayerTo(new Vector3(6f, 0f, -6f));
            yield return Steps();
            Assert.That(blue.IsOpen, Is.False);
            Assert.That(blue.Blocker.enabled, Is.True);
        }

        [UnityTest]
        public IEnumerator TwoGamesInSeparateScenesDoNotCrossConnect()
        {
            yield return StartScene(ExamplePath);
            var example = m_Context;
            yield return Load(StarterPath, LoadSceneMode.Additive);
            WorkshopGameContext starter = null;
            for (var i = 0; i < 300 && (starter == null || starter.Session.State != GameState.Playing); i++)
            {
                starter = Object.FindObjectsByType<WorkshopGameContext>().FirstOrDefault(c => c != example);
                yield return null;
            }
            Assert.That(starter, Is.Not.Null);
            Assert.That(starter.Session.State, Is.EqualTo(GameState.Playing));
            Assert.That(example.Session.RequiredCount, Is.EqualTo(6));
            Assert.That(starter.Session.RequiredCount, Is.EqualTo(3));
            Assert.That(example.Player.gameObject.scene, Is.EqualTo(example.gameObject.scene));
            Assert.That(starter.Player.gameObject.scene, Is.EqualTo(starter.gameObject.scene));
            Assert.That(starter.Player, Is.Not.EqualTo(example.Player));

            // Each player walks onto the other scene's gems; neither game counts it.
            var starterGem = Object.FindObjectsByType<Collectible>().First(c => c.gameObject.scene == starter.gameObject.scene);
            example.Player.PlaceAt(new Pose(starterGem.transform.position, Quaternion.identity));
            var exampleGem = Object.FindObjectsByType<Collectible>().First(c => c.gameObject.scene == example.gameObject.scene);
            starter.Player.PlaceAt(new Pose(exampleGem.transform.position, Quaternion.identity));
            yield return Steps();
            Assert.That(starterGem.IsCollected, Is.False);
            Assert.That(exampleGem.IsCollected, Is.False);
            Assert.That(starter.Session.CollectedCount + example.Session.CollectedCount, Is.EqualTo(0));

            // Each player collects its own scene's gem.
            example.Player.PlaceAt(new Pose(exampleGem.transform.position, Quaternion.identity));
            yield return Steps();
            Assert.That(example.Session.CollectedCount, Is.EqualTo(1));
            Assert.That(starter.Session.CollectedCount, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator PlayDoesNotChangeSharedPresets()
        {
            yield return StartScene(ExamplePath);
            var movement = m_Context.Movement;
            var style = DoorOf(ChannelColor.Red).Style;
            var before = JsonUtility.ToJson(movement) + JsonUtility.ToJson(style);

            yield return MovePlayerTo(Object.FindObjectsByType<KeyPickup>().Single().transform.position);
            yield return MovePlayerTo(Object.FindObjectsByType<TriggerSwitch>().Single().transform.position);
            yield return MovePlayerTo(StaticHazard().transform.position);
            yield return WaitWhileRespawning();

            Assert.That(JsonUtility.ToJson(movement) + JsonUtility.ToJson(style), Is.EqualTo(before));
        }

        [UnityTest]
        public IEnumerator HudShowsCountdownAndKeys()
        {
            yield return StartScene(ExamplePath);
            Assert.That(Session.HasTimeLimit, Is.True);
            Assert.That(Session.TimeLimit, Is.EqualTo(WorkshopGameRules.RelaxedSeconds));
            var texts = Object.FindObjectsByType<UnityEngine.UI.Text>();
            Assert.That(texts.Any(t => t.isActiveAndEnabled && (t.text == "Time left: 3:00" || t.text == "Time left: 2:59")), Is.True);

            yield return MovePlayerTo(Object.FindObjectsByType<KeyPickup>().Single().transform.position);
            texts = Object.FindObjectsByType<UnityEngine.UI.Text>();
            Assert.That(texts.Any(t => t.isActiveAndEnabled && t.text.Contains("Red (Circle) Key")), Is.True);
        }
    }
}
