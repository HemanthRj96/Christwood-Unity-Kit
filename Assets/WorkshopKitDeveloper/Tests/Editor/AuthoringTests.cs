using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityWorkshop.Tests
{
    /// <summary>Generated content, team scene creation and preflight fixes (A01, A02, A08, A09).</summary>
    public sealed class AuthoringTests
    {
        const string StarterPath = "Assets/WorkshopKit/Content/Templates/TopDown/TopDown_Starter.unity";
        const string ExamplePath = "Assets/WorkshopKit/Examples/TopDown_Example.unity";
        const string FirstPersonStarterPath = "Assets/WorkshopKit/Content/Templates/FirstPerson/FirstPerson_Starter.unity";
        const string FirstPersonExamplePath = "Assets/WorkshopKit/Examples/FirstPerson_Example.unity";
        const string FirstPersonTemplatePath = "Assets/WorkshopKit/Content/Templates/FirstPerson/First-Person Template.asset";
        const string TestTeam = "ZZ_AutomatedTest";
        const string TestTeamFolder = WorkshopPaths.TeamGames + "/" + TestTeam;

        EditorBuildSettingsScene[] m_BuildScenes;

        [SetUp]
        public void SetUp()
        {
            m_BuildScenes = EditorBuildSettings.scenes;
            Assert.That(AssetDatabase.IsValidFolder(TestTeamFolder), Is.False, "A previous run left test files behind.");
        }

        [TearDown]
        public void TearDown()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (AssetDatabase.IsValidFolder(TestTeamFolder))
                AssetDatabase.DeleteAsset(TestTeamFolder);
            EditorBuildSettings.scenes = m_BuildScenes;
        }

        static GameTemplateDefinition TopDown =>
            AssetDatabase.LoadAssetAtPath<GameTemplateDefinition>("Assets/WorkshopKit/Content/Templates/TopDown/Top-Down Template.asset");

        static int Errors(Scene scene) => WorkshopPreflight.Run(scene).Count(i => i.Severity == IssueSeverity.Error);

        [TestCase(StarterPath)]
        [TestCase(ExamplePath)]
        [TestCase(FirstPersonStarterPath)]
        [TestCase(FirstPersonExamplePath)]
        [TestCase("Assets/WorkshopKit/Content/Templates/ThirdPerson/ThirdPerson_Starter.unity")]
        [TestCase("Assets/WorkshopKit/Examples/ThirdPerson_Example.unity")]
        [TestCase("Assets/WorkshopKit/Content/Templates/SideScroller/SideScroller_Starter.unity")]
        [TestCase("Assets/WorkshopKit/Examples/SideScroller_Example.unity")]
        public void GeneratedScenesPassPreflight(string path)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var errors = WorkshopPreflight.Run(scene).Where(i => i.Severity == IssueSeverity.Error).Select(i => i.Message).ToList();
            Assert.That(errors, Is.Empty);
        }

        [Test]
        public void TemplateIsComplete()
        {
            var template = TopDown;
            Assert.That(template, Is.Not.Null);
            Assert.That(template.PlayerPrefab.GetComponent<WorkshopPlayer>(), Is.Not.Null);
            Assert.That(template.CameraRigPrefab.GetComponent<WorkshopCameraRig>(), Is.Not.Null);
            Assert.That(template.DefaultMovement, Is.Not.Null);
            Assert.That(template.CanJump, Is.False);
            Assert.That(AssetDatabase.GUIDToAssetPath(template.StarterSceneGuid), Is.EqualTo(StarterPath));
        }

        [Test]
        public void CatalogPrefabsAreAssignedAndCompatible()
        {
            var catalog = WorkshopCatalog.Load();
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.entries, Is.Not.Empty);
            foreach (var entry in catalog.entries)
            {
                Assert.That(entry.prefab, Is.Not.Null, entry.displayName);
            }
            var hiddenInTopDown = catalog.entries.Where(e => !e.IsCompatible(TopDown)).Select(e => e.displayName).OrderBy(n => n).ToArray();
            Assert.That(hiddenInTopDown, Is.EqualTo(new[] { "Floating Platform", "Raised Platform", "Tall Wall 2 m", "Tall Wall 4 m" }),
                "Top-Down hides only jump-only and view-blocking pieces.");
            var firstPerson = AssetDatabase.LoadAssetAtPath<GameTemplateDefinition>(FirstPersonTemplatePath);
            Assert.That(catalog.entries.All(e => e.IsCompatible(firstPerson)), Is.True, "First-person can use every piece.");
            var thirdPerson = AssetDatabase.LoadAssetAtPath<GameTemplateDefinition>("Assets/WorkshopKit/Content/Templates/ThirdPerson/Third-Person Template.asset");
            Assert.That(catalog.entries.All(e => e.IsCompatible(thirdPerson)), Is.True, "Third-person can use every piece.");
            var sideScroller = AssetDatabase.LoadAssetAtPath<GameTemplateDefinition>("Assets/WorkshopKit/Content/Templates/SideScroller/Side-Scroller Template.asset");
            Assert.That(catalog.entries.All(e => e.IsCompatible(sideScroller)), Is.True, "Side-scroller can use every piece.");
        }

        [Test]
        public void CreatingTeamScenesNeverOverwrites()
        {
            var first = TeamSceneFactory.Create(TestTeam, "Lava Run", "Grab the crowns.", TopDown, out var error);
            Assert.That(first, Is.Not.Null, error);
            var firstGuid = AssetDatabase.AssetPathToGUID(first);

            var second = TeamSceneFactory.Create(TestTeam, "Second", "Another.", TopDown, out error);
            Assert.That(second, Is.Not.Null, error);
            Assert.That(second, Is.Not.EqualTo(first));
            Assert.That(AssetDatabase.AssetPathToGUID(first), Is.EqualTo(firstGuid), "The first scene is untouched.");

            var scene = EditorSceneManager.OpenScene(first, OpenSceneMode.Single);
            var context = WorkshopSceneQuery.FindContext(scene);
            Assert.That(context.Rules.title, Is.EqualTo("Lava Run"));
            Assert.That(context.Rules.objective, Is.EqualTo("Grab the crowns."));
            Assert.That(context.Template, Is.EqualTo(TopDown));
            Assert.That(Errors(scene), Is.EqualTo(0));
            Assert.That(EditorBuildSettings.scenes.Any(s => s.path == first && s.enabled), Is.True);

            var starter = EditorSceneManager.OpenScene(StarterPath, OpenSceneMode.Single);
            Assert.That(WorkshopSceneQuery.FindContext(starter).Rules.title, Is.EqualTo("My Top-Down Game"), "The shared starter is unchanged.");
        }

        [Test]
        public void TeamNameIsSanitized()
        {
            Assert.That(TeamSceneFactory.SanitizeTeamId("  Team 7 / Rockets!  "), Is.EqualTo("Team_7_Rockets"));
            Assert.That(TeamSceneFactory.SanitizeTeamId("../.."), Is.EqualTo(""));
            Assert.That(TeamSceneFactory.Create("???", "t", "o", TopDown, out var error), Is.Null);
            Assert.That(error, Is.Not.Empty);
        }

        [Test]
        public void PreflightReportsAuthoringErrors()
        {
            var scene = EditorSceneManager.OpenScene(StarterPath, OpenSceneMode.Single);
            var spawn = Object.FindObjectsByType<PlayerSpawn>()[0];
            Object.Instantiate(spawn.gameObject, spawn.transform.position + Vector3.right * 2f, Quaternion.identity);
            foreach (var goal in Object.FindObjectsByType<GoalZone>())
                Object.DestroyImmediate(goal.gameObject);

            var messages = WorkshopPreflight.Run(scene).Where(i => i.Severity == IssueSeverity.Error).Select(i => i.Message).ToList();
            Assert.That(messages.Any(m => m.Contains("2 Start Points")), Is.True, string.Join("\n", messages));
            Assert.That(messages.Any(m => m.Contains("no Exit Goal")), Is.True, string.Join("\n", messages));
        }

        [Test]
        public void CollectAllWithoutCollectiblesIsAnErrorWithSafeFix()
        {
            var scene = EditorSceneManager.OpenScene(StarterPath, OpenSceneMode.Single);
            foreach (var item in Object.FindObjectsByType<Collectible>())
                Object.DestroyImmediate(item.gameObject);

            var issue = WorkshopPreflight.Run(scene).Single(i => i.Message.Contains("nothing to collect"));
            Assert.That(issue.Severity, Is.EqualTo(IssueSeverity.Error));
            issue.Fix();
            issue.Fix();
            Assert.That(WorkshopSceneQuery.FindContext(scene).Rules.goalRule, Is.EqualTo(GoalRule.ReachExit));
            Assert.That(Errors(scene), Is.EqualTo(0));
        }

        [Test]
        public void MissingSystemFixesAreIdempotent()
        {
            var scene = EditorSceneManager.OpenScene(StarterPath, OpenSceneMode.Single);
            Object.DestroyImmediate(Object.FindObjectsByType<WorkshopHUD>()[0].gameObject);
            Object.DestroyImmediate(Object.FindObjectsByType<PlayerSpawn>()[0].gameObject);

            var issues = WorkshopPreflight.Run(scene);
            var addHud = issues.Single(i => i.FixLabel == "Add HUD");
            var addStart = issues.Single(i => i.FixLabel == "Add Start Point");
            addHud.Fix();
            addHud.Fix();
            addStart.Fix();
            addStart.Fix();

            Assert.That(WorkshopSceneQuery.FindActive<WorkshopHUD>(scene).Count, Is.EqualTo(1));
            Assert.That(WorkshopSceneQuery.FindActive<PlayerSpawn>(scene).Count, Is.EqualTo(1));
            var spawn = WorkshopSceneQuery.FindActive<PlayerSpawn>(scene)[0];
            var assigned = new SerializedObject(spawn).FindProperty(WorkshopSceneBehaviour.ContextPropertyName).objectReferenceValue;
            Assert.That(assigned, Is.EqualTo(WorkshopSceneQuery.FindContext(scene)), "Placed pieces are wired to the Game Setup automatically.");
        }

        [Test]
        public void SolidHazardIsReportedAndFixable()
        {
            var scene = EditorSceneManager.OpenScene(ExamplePath, OpenSceneMode.Single);
            var hazard = Object.FindObjectsByType<Hazard>()[0];
            hazard.GetComponent<Collider>().isTrigger = false;

            var issue = WorkshopPreflight.Run(scene).Single(i => i.FixLabel == "Make Trigger");
            issue.Fix();
            Assert.That(hazard.GetComponent<Collider>().isTrigger, Is.True);
            Assert.That(Errors(scene), Is.EqualTo(0));
        }

        [Test]
        public void ExtraCameraAndListenerAreReported()
        {
            var scene = EditorSceneManager.OpenScene(StarterPath, OpenSceneMode.Single);
            var extra = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            var issues = WorkshopPreflight.Run(scene);
            Assert.That(issues.Count(i => i.Target == extra && i.Severity == IssueSeverity.Error), Is.EqualTo(2));
            foreach (var issue in issues.Where(i => i.Target == extra))
                issue.Fix();
            Assert.That(Errors(scene), Is.EqualTo(0));
        }

        [Test]
        public void RuntimeAssemblyHasNoEditorReferences()
        {
            var runtime = typeof(WorkshopGameContext).Assembly;
            Assert.That(runtime.GetReferencedAssemblies().Any(a => a.Name.StartsWith("UnityEditor")), Is.False);
        }

        // ---- Phase 2 -------------------------------------------------------------------------------

        static System.Collections.Generic.List<PreflightIssue> Issues(Scene scene) => WorkshopPreflight.Run(scene);

        [Test]
        public void EveryKitAssetLoadsWithItsScript()
        {
            // A ScriptableObject whose class is not in a file of the same name saves with no script.
            foreach (var guid in AssetDatabase.FindAssets("t:Object", new[] { WorkshopPaths.Content }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".asset"))
                    continue;
                Assert.That(AssetDatabase.LoadMainAssetAtPath(path), Is.Not.Null, path);
            }
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { WorkshopPaths.Content }))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                foreach (var t in prefab.GetComponentsInChildren<Transform>(true))
                    Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject), Is.EqualTo(0), prefab.name);
                foreach (var piece in prefab.GetComponentsInChildren<ChannelPiece>(true))
                    Assert.That(piece.Style, Is.Not.Null, $"{prefab.name} has no channel style");
            }
        }

        [Test]
        public void DoorWithoutKeyOrSwitchIsReported()
        {
            var scene = EditorSceneManager.OpenScene(ExamplePath, OpenSceneMode.Single);
            foreach (var key in Object.FindObjectsByType<KeyPickup>())
                Object.DestroyImmediate(key.gameObject);
            var issues = Issues(scene);
            Assert.That(issues.Any(i => i.Severity == IssueSeverity.Warning && i.Message.Contains("can never open") && i.Message.Contains("Red")), Is.True);
            Assert.That(issues.Any(i => i.Message.Contains("can never open") && i.Message.Contains("Blue")), Is.False, "Blue still has its switch.");
        }

        [Test]
        public void StartBelowFallHeightIsAnErrorWithFix()
        {
            var scene = EditorSceneManager.OpenScene(ExamplePath, OpenSceneMode.Single);
            var context = WorkshopSceneQuery.FindContext(scene);
            var serialized = new SerializedObject(context);
            serialized.FindProperty("m_FallHeight").floatValue = 1f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var issue = Issues(scene).First(i => i.Severity == IssueSeverity.Error && i.Message.Contains("below the Fall Height"));
            issue.Fix();
            issue.Fix();
            Assert.That(context.FallHeight, Is.LessThan(-0.2f - 4.9f));
            Assert.That(Errors(scene), Is.EqualTo(0));
        }

        [Test]
        public void MoverWithoutPathAndCheckpointInHazardAreReported()
        {
            var scene = EditorSceneManager.OpenScene(ExamplePath, OpenSceneMode.Single);
            var mover = Object.FindObjectsByType<HazardMover>()[0];
            var serialized = new SerializedObject(mover);
            serialized.FindProperty("m_Travel").vector3Value = Vector3.zero;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var checkpoint = Object.FindObjectsByType<Checkpoint>()[0];
            var spikes = Object.FindObjectsByType<Hazard>().First(h => h.GetComponent<HazardMover>() == null);
            checkpoint.transform.position = spikes.transform.position;

            var issues = Issues(scene);
            Assert.That(issues.Any(i => i.Severity == IssueSeverity.Error && i.Message.Contains("has no path")), Is.True);
            Assert.That(issues.Any(i => i.Severity == IssueSeverity.Warning && i.Message.Contains("is inside hazard")), Is.True);
        }

        [Test]
        public void ChannelChangeUpdatesLookAndIsUndoable()
        {
            var scene = EditorSceneManager.OpenScene(ExamplePath, OpenSceneMode.Single);
            var door = Object.FindObjectsByType<Door>().First(d => d.Channel == ChannelColor.Red);
            var slab = door.GetComponentsInChildren<MeshRenderer>().First(r => r.name == "Slab");
            Assert.That(slab.sharedMaterial, Is.EqualTo(door.Style.MaterialFor(ChannelColor.Red)));

            Undo.IncrementCurrentGroup();
            Undo.RecordObjects(door.LookTargets(), "Change channel");
            var serialized = new SerializedObject(door);
            serialized.FindProperty(ChannelPiece.ChannelPropertyName).enumValueIndex = (int)ChannelColor.Green;
            serialized.ApplyModifiedProperties();
            door.ApplyChannelLook();
            Assert.That(slab.sharedMaterial, Is.EqualTo(door.Style.MaterialFor(ChannelColor.Green)));
            Assert.That(door.GetComponentsInChildren<MeshFilter>().First(f => f.name == "Symbol Front").sharedMesh,
                Is.EqualTo(door.Style.SymbolFor(ChannelColor.Green)));

            Undo.PerformUndo();
            Assert.That(door.Channel, Is.EqualTo(ChannelColor.Red));
            Assert.That(slab.sharedMaterial, Is.EqualTo(door.Style.MaterialFor(ChannelColor.Red)));
        }

        [Test]
        public void SharedPresetsAreNotTeamSettings()
        {
            var template = TopDown;
            var before = EditorJsonUtility.ToJson(template) + EditorJsonUtility.ToJson(template.DefaultMovement);
            var path = TeamSceneFactory.Create(TestTeam, "Timer Team", "Fast!", template, out var error);
            Assert.That(path, Is.Not.Null, error);
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var context = WorkshopSceneQuery.FindContext(scene);
            var serialized = new SerializedObject(context);
            serialized.FindProperty("m_Rules.timer").enumValueIndex = (int)TimerPreset.Challenge;
            serialized.FindProperty("m_Movement").objectReferenceValue = template.MovementPresets[2];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene);
            Assert.That(EditorJsonUtility.ToJson(template) + EditorJsonUtility.ToJson(template.DefaultMovement), Is.EqualTo(before));
        }
    }
}
