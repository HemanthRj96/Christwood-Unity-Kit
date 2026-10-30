using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace UnityWorkshop.Tests
{
    /// <summary>
    /// Repeated Play sessions with domain and scene reload disabled (A07): no stale static
    /// registrations, duplicate contexts or duplicate events carry over between sessions.
    /// </summary>
    public sealed class PlaySessionTests
    {
        const string ExamplePath = "Assets/WorkshopKit/Examples/TopDown_Example.unity";

        bool m_OptionsEnabled;
        EnterPlayModeOptions m_Options;

        [SetUp]
        public void SetUp()
        {
            m_OptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            m_Options = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;
        }

        [TearDown]
        public void TearDown()
        {
            EditorSettings.enterPlayModeOptionsEnabled = m_OptionsEnabled;
            EditorSettings.enterPlayModeOptions = m_Options;
        }

        [UnityTest]
        public IEnumerator RepeatedPlaySessionsStartClean()
        {
            EditorSceneManager.OpenScene(ExamplePath, OpenSceneMode.Single);
            for (var session = 0; session < 3; session++)
            {
                yield return new EnterPlayMode();

                WorkshopGameContext context = null;
                for (var frame = 0; frame < 300 && (context == null || context.Session.State != GameState.Playing); frame++)
                {
                    context = Object.FindObjectsByType<WorkshopGameContext>().FirstOrDefault();
                    yield return null;
                }
                Assert.That(context, Is.Not.Null, $"session {session}");
                Assert.That(context.Session.State, Is.EqualTo(GameState.Playing), $"session {session}");
                Assert.That(Object.FindObjectsByType<WorkshopGameContext>().Length, Is.EqualTo(1));
                Assert.That(context.Session.RequiredCount, Is.EqualTo(6), "Collectibles registered exactly once.");
                Assert.That(context.Session.CollectedCount, Is.EqualTo(0), "No progress carried over.");

                var events = 0;
                context.Session.ProgressChanged += () => events++;
                var gem = Object.FindObjectsByType<Collectible>().First();
                context.Player.PlaceAt(new Pose(gem.transform.position, Quaternion.identity));
                // EditMode tests advance by editor frames, so wait on game time rather than WaitForFixedUpdate.
                var until = Time.time + 0.3f;
                while (Time.time < until)
                    yield return null;
                Assert.That(context.Session.CollectedCount, Is.EqualTo(1));
                Assert.That(events, Is.EqualTo(1), "One pickup raises one event.");

                yield return new ExitPlayMode();
                Assert.That(Time.timeScale, Is.EqualTo(1f));
            }
        }
    }
}
