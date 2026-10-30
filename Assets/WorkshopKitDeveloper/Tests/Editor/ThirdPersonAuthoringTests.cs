using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityWorkshop.Tests
{
    /// <summary>Third-person template content, the editor camera preview and competing-motion checks.</summary>
    public sealed class ThirdPersonAuthoringTests
    {
        const string TemplatePath = "Assets/WorkshopKit/Content/Templates/ThirdPerson/Third-Person Template.asset";
        const string StarterPath = "Assets/WorkshopKit/Content/Templates/ThirdPerson/ThirdPerson_Starter.unity";
        const string ExamplePath = "Assets/WorkshopKit/Examples/ThirdPerson_Example.unity";
        const string TestTeam = "ZZ_AutomatedThirdPerson";
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
            Assert.That(template.Perspective, Is.EqualTo(TemplatePerspective.ThirdPerson));
            Assert.That(template.CanJump, Is.True);
            Assert.That(template.Controls, Does.Contain("Orbit"));
            Assert.That(template.Preview, Is.Not.Null);
            Assert.That(AssetDatabase.GUIDToAssetPath(template.StarterSceneGuid), Is.EqualTo(StarterPath));

            var player = template.PlayerPrefab.GetComponent<WorkshopPlayer>();
            Assert.That(player.Facing, Is.EqualTo(PlayerFacing.MovementDirection), "The character turns toward where it walks.");
            var adapter = template.PlayerPrefab.GetComponent<PlayerVisualAdapter>();
            Assert.That(adapter, Is.Not.Null);
            Assert.That(adapter.Visual, Is.Not.Null);
            Assert.That(template.PlayerPrefab.GetComponentsInChildren<Renderer>().All(r => r.shadowCastingMode == ShadowCastingMode.On),
                Is.True, "The character is visible.");
            Assert.That(template.CameraRigPrefab.GetComponent<ThirdPersonCameraRig>(), Is.Not.Null);
            Assert.That(WorkshopPreflight.CheckCompetingMotion(player).Any(), Is.False, "The owned character has nothing competing with the motor.");
        }

        [Test]
        public void EditorPreviewPullsTheCameraInFrontOfWalls()
        {
            var scene = EditorSceneManager.OpenScene(ExamplePath, OpenSceneMode.Single);
            var rig = WorkshopSceneQuery.FindActive<ThirdPersonCameraRig>(scene).Single();

            rig.PlaceBehind(new Pose(new Vector3(0f, 0f, -8f), Quaternion.identity));
            Assert.That(rig.CurrentDistance, Is.EqualTo(rig.FullDistance).Within(0.01f), "Open space: full distance.");

            rig.PlaceBehind(new Pose(new Vector3(0f, 0f, -13f), Quaternion.identity));
            Assert.That(rig.CurrentDistance, Is.LessThan(1.5f), "A wall right behind the start pulls the camera in.");
            Assert.That(rig.Camera.transform.position.z, Is.GreaterThan(-14f), "The camera stays on the player's side of the wall.");
        }

        [Test]
        public void CompetingMotionInAModelIsReported()
        {
            var scene = EditorSceneManager.OpenScene(StarterPath, OpenSceneMode.Single);
            var player = WorkshopSceneQuery.FindActive<WorkshopPlayer>(scene).Single();
            var model = new GameObject("Vendor Model");
            model.transform.SetParent(player.GetComponent<PlayerVisualAdapter>().Visual, false);
            var body = model.AddComponent<Rigidbody>();
            var animator = model.AddComponent<Animator>();
            animator.applyRootMotion = true;
            var extra = new GameObject("Vendor Controller").AddComponent<CharacterController>();
            extra.transform.SetParent(model.transform, false);

            var issues = WorkshopPreflight.Run(scene);
            Assert.That(issues.Any(i => i.Severity == IssueSeverity.Error && i.Target == extra.gameObject && i.Message.Contains("Character Controller")), Is.True);
            Assert.That(issues.Single(i => i.Target == extra.gameObject).Fix, Is.Null, "Vendor parts are not removed automatically.");
            var bodyIssue = issues.Single(i => i.Target == model && i.Message.Contains("physics body"));
            var rootMotionIssue = issues.Single(i => i.Target == model && i.Message.Contains("root motion"));
            Assert.That(rootMotionIssue.Severity, Is.EqualTo(IssueSeverity.Warning));

            bodyIssue.Fix();
            bodyIssue.Fix();
            rootMotionIssue.Fix();
            Assert.That(body.isKinematic, Is.True);
            Assert.That(animator.applyRootMotion, Is.False);
            Object.DestroyImmediate(extra.gameObject);
            Assert.That(WorkshopPreflight.Run(scene).Count(i => i.Severity == IssueSeverity.Error), Is.EqualTo(0));
        }

        [Test]
        public void TeamSceneFromThirdPersonTemplateIsReady()
        {
            var path = TeamSceneFactory.Create(TestTeam, "Cliff Run", "Reach the summit.", Template, out var error);
            Assert.That(path, Is.Not.Null, error);
            Assert.That(path, Does.EndWith("_ThirdPerson.unity"));
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            Assert.That(WorkshopSceneQuery.FindContext(scene).Template, Is.EqualTo(Template));
            Assert.That(WorkshopPreflight.Run(scene).Count(i => i.Severity == IssueSeverity.Error), Is.EqualTo(0));
        }
    }
}
