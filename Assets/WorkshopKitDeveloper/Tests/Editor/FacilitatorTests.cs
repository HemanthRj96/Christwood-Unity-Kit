using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

namespace UnityWorkshop.Tests
{
    /// <summary>Restore rig, template replacement, recovery milestones, team-owned movement, overlay help and drag-and-drop placement.</summary>
    public sealed class FacilitatorTests
    {
        const string Templates = "Assets/WorkshopKit/Content/Templates";
        const string TopDownExamplePath = "Assets/WorkshopKit/Examples/TopDown_Example.unity";
        const string ThirdPersonStarterPath = Templates + "/ThirdPerson/ThirdPerson_Starter.unity";
        const string TestTeam = "ZZ_AutomatedFacilitator";

        EditorBuildSettingsScene[] m_BuildScenes;

        static GameTemplateDefinition Template(string folder, string name) =>
            AssetDatabase.LoadAssetAtPath<GameTemplateDefinition>($"{Templates}/{folder}/{name} Template.asset");

        [SetUp]
        public void SetUp() => m_BuildScenes = EditorBuildSettings.scenes;

        [TearDown]
        public void TearDown()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var team = WorkshopPaths.TeamGames + "/" + TestTeam;
            if (AssetDatabase.IsValidFolder(team))
                AssetDatabase.DeleteAsset(team);
            EditorBuildSettings.scenes = m_BuildScenes;
        }

        static int LevelPieceCount(UnityEngine.SceneManagement.Scene scene) =>
            WorkshopSceneQuery.FindActive<Collider>(scene).Count(c => c.GetComponentInParent<WorkshopPlayer>() == null);

        [Test]
        public void RestoreRigReAddsMissingSystemsAndKeepsTheLevel()
        {
            var scene = EditorSceneManager.OpenScene(ThirdPersonStarterPath, OpenSceneMode.Single);
            var context = WorkshopSceneQuery.FindContext(scene);
            var title = context.Rules.title;
            Object.DestroyImmediate(WorkshopSceneQuery.FindActive<WorkshopPlayer>(scene).Single().gameObject);
            Object.DestroyImmediate(WorkshopSceneQuery.FindActive<WorkshopCameraRig>(scene).Single().gameObject);
            Object.DestroyImmediate(WorkshopSceneQuery.FindActive<WorkshopHUD>(scene).Single().gameObject);
            var level = LevelPieceCount(scene);

            var plan = WorkshopRigTools.Plan(scene, null);
            Assert.That(plan.CanApply, Is.True);
            Assert.That(plan.Actions.Count, Is.EqualTo(3), string.Join("\n", plan.Actions));
            WorkshopRigTools.Apply(scene, null);

            var template = context.Template;
            Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(WorkshopSceneQuery.FindActive<WorkshopPlayer>(scene).Single().gameObject), Is.EqualTo(template.PlayerPrefab));
            Assert.That(WorkshopSceneQuery.FindActive<WorkshopCameraRig>(scene).Count, Is.EqualTo(1));
            Assert.That(WorkshopSceneQuery.FindActive<WorkshopHUD>(scene).Count, Is.EqualTo(1));
            Assert.That(WorkshopSceneQuery.FindActive<EventSystem>(scene).Count, Is.EqualTo(1));
            Assert.That(context.Rules.title, Is.EqualTo(title), "Game Setup configuration is kept.");
            Assert.That(LevelPieceCount(scene), Is.EqualTo(level), "The level is kept.");
            Assert.That(WorkshopPreflight.Run(scene).Count(i => i.Severity == IssueSeverity.Error), Is.EqualTo(0));

            var again = WorkshopRigTools.Apply(scene, null);
            Assert.That(again.Actions.Single(), Does.StartWith("Nothing to do"));
            Assert.That(WorkshopSceneQuery.FindActive<WorkshopPlayer>(scene).Count, Is.EqualTo(1), "Repeating creates no duplicates.");
        }

        [Test]
        public void RestoreRefusesAmbiguousGameSetups()
        {
            var scene = EditorSceneManager.OpenScene(ThirdPersonStarterPath, OpenSceneMode.Single);
            Object.Instantiate(WorkshopSceneQuery.FindContext(scene).gameObject);
            var plan = WorkshopRigTools.Apply(scene, null);
            Assert.That(plan.CanApply, Is.False);
            Assert.That(plan.Blocker, Does.Contain("2 Game Setup"));
        }

        [Test]
        public void ChangingTemplateKeepsTheLevelAndRulesAndIsUndoable()
        {
            var scene = EditorSceneManager.OpenScene(TopDownExamplePath, OpenSceneMode.Single);
            var context = WorkshopSceneQuery.FindContext(scene);
            var topDown = context.Template;
            var rules = JsonUtility.ToJson(context.Rules);
            var gems = WorkshopSceneQuery.FindActive<Collectible>(scene).Count;
            var doors = WorkshopSceneQuery.FindActive<Door>(scene).Count;
            var firstPerson = Template("FirstPerson", "First-Person");

            var plan = WorkshopRigTools.Plan(scene, firstPerson);
            Assert.That(plan.Actions.Any(a => a.Contains("Change the template")), Is.True);
            Undo.IncrementCurrentGroup();
            WorkshopRigTools.Apply(scene, firstPerson);

            Assert.That(context.Template, Is.EqualTo(firstPerson));
            Assert.That(JsonUtility.ToJson(context.Rules), Is.EqualTo(rules), "Title, objective, rule and timer are kept.");
            Assert.That(WorkshopSceneQuery.FindActive<FirstPersonCameraRig>(scene).Count, Is.EqualTo(1));
            Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(WorkshopSceneQuery.FindActive<WorkshopPlayer>(scene).Single().gameObject), Is.EqualTo(firstPerson.PlayerPrefab));
            Assert.That(WorkshopSceneQuery.FindActive<Collectible>(scene).Count, Is.EqualTo(gems));
            Assert.That(WorkshopSceneQuery.FindActive<Door>(scene).Count, Is.EqualTo(doors));
            Assert.That(WorkshopPreflight.Run(scene).Count(i => i.Severity == IssueSeverity.Error), Is.EqualTo(0));

            Undo.PerformUndo();
            Assert.That(context.Template, Is.EqualTo(topDown));
            Assert.That(WorkshopSceneQuery.FindActive<TopDownCameraRig>(scene).Count, Is.EqualTo(1));
            Assert.That(WorkshopSceneQuery.FindActive<FirstPersonCameraRig>(scene).Count, Is.EqualTo(0));
        }

        [Test]
        public void ChangingToSideScrollerPreviewsOffPlanePieces()
        {
            var scene = EditorSceneManager.OpenScene(TopDownExamplePath, OpenSceneMode.Single);
            var plan = WorkshopRigTools.Plan(scene, Template("SideScroller", "Side-Scroller"));
            Assert.That(plan.Warnings.Any(w => w.Contains("off the new play plane")), Is.True, string.Join("\n", plan.Warnings));
        }

        [Test]
        public void EveryTemplateHasFourPlayableRecoveryMilestones()
        {
            foreach (var (folder, name) in new[] { ("TopDown", "Top-Down"), ("FirstPerson", "First-Person"), ("ThirdPerson", "Third-Person"), ("SideScroller", "Side-Scroller") })
            {
                var milestones = WorkshopRigTools.Milestones(Template(folder, name));
                Assert.That(milestones.Select(System.IO.Path.GetFileNameWithoutExtension),
                    Is.EqualTo(new[] { "1 Movement Ready", "2 First Winnable", "3 Objective Ready", "4 Challenge Ready" }), folder);
                foreach (var path in milestones)
                {
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    Assert.That(WorkshopPreflight.Run(scene).Where(i => i.Severity == IssueSeverity.Error).Select(i => i.Message), Is.Empty, path);
                }
            }
        }

        [Test]
        public void CopyingAMilestoneMakesANewTeamScene()
        {
            var template = Template("ThirdPerson", "Third-Person");
            var source = WorkshopRigTools.Milestones(template)[2];
            var sourceGuid = AssetDatabase.AssetPathToGUID(source);
            var first = TeamSceneFactory.Create(TestTeam, null, null, template, out var error, source, "ThirdPerson_3_Objective_Ready");
            Assert.That(first, Is.Not.Null, error);
            var second = TeamSceneFactory.Create(TestTeam, null, null, template, out error, source, "ThirdPerson_3_Objective_Ready");
            Assert.That(second, Is.Not.EqualTo(first), "Copies never overwrite each other.");
            Assert.That(AssetDatabase.AssetPathToGUID(source), Is.EqualTo(sourceGuid));
            var scene = EditorSceneManager.OpenScene(second, OpenSceneMode.Single);
            Assert.That(WorkshopSceneQuery.FindContext(scene).Rules.title, Does.StartWith("Milestone 3"), "Milestone settings are kept when no title is given.");
        }

        [Test]
        public void CustomMovementIsATeamOwnedCopy()
        {
            var template = Template("ThirdPerson", "Third-Person");
            var shared = template.DefaultMovement;
            var sharedJson = JsonUtility.ToJson(shared);
            var path = TeamSceneFactory.Create(TestTeam, "Fast Feet", "Go!", template, out var error);
            Assert.That(path, Is.Not.Null, error);
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var context = WorkshopSceneQuery.FindContext(scene);

            var copy = WorkshopTeamAssets.CreateCustomMovement(context, context.Movement);
            Assert.That(AssetDatabase.GetAssetPath(copy), Does.StartWith(WorkshopPaths.TeamGames + "/" + TestTeam + "/"));
            Assert.That(context.Movement, Is.EqualTo(copy));
            var serialized = new SerializedObject(copy);
            serialized.FindProperty("m_MoveSpeed").floatValue = 9f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(JsonUtility.ToJson(shared), Is.EqualTo(sharedJson), "Editing the team copy never changes the shared preset.");
            Assert.That(WorkshopPreflight.Run(scene).Any(i => i.Message.Contains("Custom movement")), Is.False);

            var setup = new SerializedObject(context);
            setup.FindProperty("m_CustomMovement").objectReferenceValue = shared;
            setup.ApplyModifiedPropertiesWithoutUndo();
            var issue = WorkshopPreflight.Run(scene).Single(i => i.Message.Contains("Custom movement"));
            issue.Fix();
            Assert.That(context.Movement, Is.EqualTo(shared), "Back to the approved preset.");

            var example = EditorSceneManager.OpenScene(TopDownExamplePath, OpenSceneMode.Single);
            Assert.That(WorkshopTeamAssets.CreateCustomMovement(WorkshopSceneQuery.FindContext(example), shared), Is.Null, "Only team games get custom copies.");
        }

        [Test]
        public void OverlayDescribesSelectedPieces()
        {
            var scene = EditorSceneManager.OpenScene(TopDownExamplePath, OpenSceneMode.Single);
            Assert.That(WorkshopSceneOverlay.Describe(WorkshopSceneQuery.FindActive<Collectible>(scene)[0].gameObject), Does.StartWith("Collectible"));
            Assert.That(WorkshopSceneOverlay.Describe(WorkshopSceneQuery.FindActive<Door>(scene)[0].gameObject), Does.StartWith("Door"));
            Assert.That(WorkshopSceneOverlay.Describe(null), Is.Null);
        }

        [Test]
        public void DragAndDropLandsOnSurfacesOrThePlayPlane()
        {
            var scene = EditorSceneManager.OpenScene(TopDownExamplePath, OpenSceneMode.Single);
            var down = new Ray(new Vector3(-8f, 20f, -8f), Vector3.down);
            Assert.That(WorkshopCatalogDrop.DropPoint(down).y, Is.EqualTo(0f).Within(0.01f), "Lands on the floor.");

            EditorSceneManager.OpenScene(Templates + "/SideScroller/SideScroller_Starter.unity", OpenSceneMode.Single);
            var slanted = new Ray(new Vector3(0f, 2f, -14f), new Vector3(0.2f, -0.05f, 1f).normalized);
            Assert.That(WorkshopCatalogDrop.DropPoint(slanted).z, Is.EqualTo(0f).Within(0.001f), "Side-scroller drops land on the play plane.");
        }
    }
}
