using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UnityWorkshop.Tests
{
    /// <summary>Side-scroller template content, play-plane placement and off-plane checks.</summary>
    public sealed class SideScrollerAuthoringTests
    {
        const string TemplatePath = "Assets/WorkshopKit/Content/Templates/SideScroller/Side-Scroller Template.asset";
        const string StarterPath = "Assets/WorkshopKit/Content/Templates/SideScroller/SideScroller_Starter.unity";
        const string ExamplePath = "Assets/WorkshopKit/Examples/SideScroller_Example.unity";
        const string TopDownStarterPath = "Assets/WorkshopKit/Content/Templates/TopDown/TopDown_Starter.unity";
        const string GemPath = "Assets/WorkshopKit/Content/Prefabs/Gameplay/Gem.prefab";
        const string DoorPath = "Assets/WorkshopKit/Content/Prefabs/Gameplay/Door.prefab";
        const string TestTeam = "ZZ_AutomatedSideScroller";
        const string TestTeamFolder = WorkshopPaths.TeamGames + "/" + TestTeam;

        EditorBuildSettingsScene[] m_BuildScenes;

        static GameTemplateDefinition Template => AssetDatabase.LoadAssetAtPath<GameTemplateDefinition>(TemplatePath);

        [SetUp]
        public void SetUp() => m_BuildScenes = EditorBuildSettings.scenes;

        [TearDown]
        public void TearDown()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (AssetDatabase.IsValidFolder(TestTeamFolder))
                AssetDatabase.DeleteAsset(TestTeamFolder);
            EditorBuildSettings.scenes = m_BuildScenes;
        }

        [Test]
        public void TemplateIsComplete()
        {
            var template = Template;
            Assert.That(template, Is.Not.Null);
            Assert.That(template.Perspective, Is.EqualTo(TemplatePerspective.SideScroller));
            Assert.That(template.CanJump, Is.True);
            Assert.That(template.Controls, Does.Contain("A/D").And.Contain("Space"));
            Assert.That(template.Preview, Is.Not.Null);
            Assert.That(AssetDatabase.GUIDToAssetPath(template.StarterSceneGuid), Is.EqualTo(StarterPath));
            var rig = template.CameraRigPrefab.GetComponent<SideScrollerCameraRig>();
            Assert.That(rig, Is.Not.Null);
            Assert.That(rig.LocksCursor, Is.False, "No mouse look in a side view.");
            Assert.That(rig.TryGetMovementPlane(out _), Is.True);
        }

        [Test]
        public void EveryGameplayPieceInTheGeneratedScenesIsOnThePlane()
        {
            foreach (var path in new[] { StarterPath, ExamplePath })
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                Assert.That(WorkshopSceneQuery.TryGetMovementPlane(scene, out var depth), Is.True);
                foreach (var piece in WorkshopSceneQuery.FindActive<WorkshopSceneBehaviour>(scene).Where(p => !(p is WorkshopCameraRig) && !(p is WorkshopHUD)))
                    Assert.That(piece.transform.position.z, Is.EqualTo(depth).Within(0.001f), $"{path}: {piece.name}");
            }
        }

        [Test]
        public void BuilderPlacementSnapsToThePlaneAndTurnsDoors()
        {
            var scene = EditorSceneManager.OpenScene(StarterPath, OpenSceneMode.Single);
            WorkshopSceneQuery.TryGetMovementPlane(scene, out var depth);
            var gem = WorkshopPlacement.Place(AssetDatabase.LoadAssetAtPath<GameObject>(GemPath), scene, new Vector3(3f, 0f, 1.7f), Quaternion.identity, "Gameplay", 1f);
            Assert.That(gem.transform.position.z, Is.EqualTo(depth));
            var door = WorkshopPlacement.Place(AssetDatabase.LoadAssetAtPath<GameObject>(DoorPath), scene, new Vector3(-3f, 0f, -1.2f), Quaternion.identity, "Gameplay", 1f);
            Assert.That(door.transform.position.z, Is.EqualTo(depth));
            Assert.That(Mathf.DeltaAngle(door.transform.eulerAngles.y, 90f), Is.EqualTo(0f).Within(0.01f), "Doors block left/right travel.");

            // Placement in other templates keeps the depth the user chose.
            var topDown = EditorSceneManager.OpenScene(TopDownStarterPath, OpenSceneMode.Single);
            Assert.That(WorkshopSceneQuery.TryGetMovementPlane(topDown, out _), Is.False);
            var free = WorkshopPlacement.Place(AssetDatabase.LoadAssetAtPath<GameObject>(GemPath), topDown, new Vector3(3f, 0f, 2f), Quaternion.identity, "Gameplay", 1f);
            Assert.That(free.transform.position.z, Is.EqualTo(2f));
        }

        [Test]
        public void OffPlanePiecesAndPathsAreReportedAndFixed()
        {
            var scene = EditorSceneManager.OpenScene(ExamplePath, OpenSceneMode.Single);
            var gem = WorkshopSceneQuery.FindActive<Collectible>(scene).First();
            gem.transform.position += new Vector3(0f, 0f, 1.5f);
            var mover = WorkshopSceneQuery.FindActive<HazardMover>(scene).Single();
            var serialized = new SerializedObject(mover);
            serialized.FindProperty("m_Travel").vector3Value = new Vector3(3.5f, 0f, 2f);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var tree = GameObject.Find("Tree");
            Assert.That(tree, Is.Not.Null);

            var issues = WorkshopPreflight.Run(scene);
            var gemIssue = issues.Single(i => i.Target == gem.gameObject);
            Assert.That(gemIssue.Message, Does.Contain("off the play plane"));
            var pathIssue = issues.Single(i => i.Target == mover && i.Message.Contains("leaves the play plane"));
            Assert.That(issues.Any(i => i.Target == tree), Is.False, "Scenery may sit behind the plane.");

            gemIssue.Fix();
            gemIssue.Fix();
            pathIssue.Fix();
            Assert.That(gem.transform.position.z, Is.EqualTo(0f));
            Assert.That(mover.Travel.z, Is.EqualTo(0f));
            Assert.That(WorkshopPreflight.Run(scene).Any(i => i.Message.Contains("play plane")), Is.False);
        }

        [Test]
        public void TeamSceneFromSideScrollerTemplateIsReady()
        {
            var path = TeamSceneFactory.Create(TestTeam, "Ninja Dash", "Reach the flag.", Template, out var error);
            Assert.That(path, Is.Not.Null, error);
            Assert.That(path, Does.EndWith("_SideScroller.unity"));
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            Assert.That(WorkshopPreflight.Run(scene).Count(i => i.Severity == IssueSeverity.Error), Is.EqualTo(0));
        }
    }
}
