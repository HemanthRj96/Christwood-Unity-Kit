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
    /// Shared helpers for play tests: load a generated scene, wait for the game to start, and move the
    /// player by teleporting so results do not depend on keyboard simulation.
    /// </summary>
    public abstract class WorkshopPlayTestBase
    {
        protected const string ExamplePath = "Assets/WorkshopKit/Examples/TopDown_Example.unity";
        protected const string StarterPath = "Assets/WorkshopKit/Content/Templates/TopDown/TopDown_Starter.unity";

        protected WorkshopGameContext m_Context;

        protected static IEnumerator Load(string path, LoadSceneMode mode = LoadSceneMode.Single)
        {
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(mode));
#else
            Assert.Ignore("Gameplay tests load scenes by path and run in the Editor only.");
            yield break;
#endif
        }

        protected IEnumerator StartScene(string path)
        {
            yield return Load(path);
            yield return WaitUntilPlaying(null);
        }

        /// <summary>Waits for a Game Setup other than <paramref name="previous"/> to reach Playing.</summary>
        protected IEnumerator WaitUntilPlaying(WorkshopGameContext previous)
        {
            for (var frame = 0; frame < 300; frame++)
            {
                var context = Object.FindObjectsByType<WorkshopGameContext>().FirstOrDefault();
                if (context != null && context != previous && context.Session != null && context.Session.State == GameState.Playing)
                {
                    m_Context = context;
                    yield break;
                }
                yield return null;
            }
            Assert.Fail("The game did not start playing.");
        }

        protected IEnumerator MovePlayerTo(Vector3 position)
        {
            m_Context.Player.PlaceAt(new Pose(position + Vector3.up * 0.05f, Quaternion.identity));
            for (var i = 0; i < 4; i++)
                yield return new WaitForFixedUpdate();
            yield return null;
        }

        protected WorkshopGameSession Session => m_Context.Session;

        /// <summary>A spike trap that does not move, nearest the start, so tests are repeatable.</summary>
        protected static Hazard StaticHazard() =>
            Object.FindObjectsByType<Hazard>().Where(h => h.GetComponent<HazardMover>() == null)
                .OrderBy(h => h.transform.position.x).ThenBy(h => h.transform.position.z).First();

        protected static Collectible[] Collectibles() =>
            Object.FindObjectsByType<Collectible>().OrderBy(c => c.transform.position.x).ThenBy(c => c.transform.position.z).ToArray();

        protected static IEnumerator Steps(int steps = 4)
        {
            for (var i = 0; i < steps; i++)
                yield return new WaitForFixedUpdate();
            yield return null;
        }

        /// <summary>Test keyboard read through the game's real input actions.</summary>
        protected VirtualKeyboard Keys;

        [SetUp]
        public void CreateKeyboard() => Keys = new VirtualKeyboard();

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            Keys.Dispose();
        }

        /// <summary>Holds keys for a time of game time, then releases them.</summary>
        protected IEnumerator Hold(float seconds, params UnityEngine.InputSystem.Key[] keys)
        {
            Keys.Press(keys);
            yield return WaitSeconds(seconds);
            Keys.ReleaseAll();
            yield return null;
        }

        /// <summary>Waits on game time; batch mode runs frames too fast for frame counts.</summary>
        protected static IEnumerator WaitSeconds(float seconds)
        {
            var until = Time.time + seconds;
            while (Time.time < until)
                yield return null;
        }

        protected IEnumerator WaitWhileRespawning()
        {
            var until = Time.time + 2f;
            while (Session.State == GameState.Respawning && Time.time < until)
                yield return null;
            Assert.That(Session.State, Is.EqualTo(GameState.Playing));
        }
    }
}
