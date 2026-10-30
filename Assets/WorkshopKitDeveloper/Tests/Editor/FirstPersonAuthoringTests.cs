using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityWorkshop.Tests
{
    /// <summary>First-person template content, team scenes and template-suitability checks.</summary>
    public sealed class FirstPersonAuthoringTests
    {
        const string TemplatePath = "Assets/WorkshopKit/Content/Templates/FirstPerson/First-Person Template.asset";
        const string StarterPath = "Assets/WorkshopKit/Content/Templates/FirstPerson/FirstPerson_Starter.unity";
        const string TopDownStarterPath = "Assets/WorkshopKit/Content/Templates/TopDown/TopDown_Starter.unity";
        const string PlatformPath = "Assets/WorkshopKit/Content/Prefabs/Environment/Raised Platform.prefab";
        const string TallWallPath = "Assets/WorkshopKit/Content/Prefabs/Environment/Tall Wall 4m.prefab";
        const string TestTeam = "ZZ_AutomatedFirstPerson";
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
            Assert.That(template.Perspective, Is.EqualTo(TemplatePerspective.FirstPerson));
            Assert.That(template.CanJump, Is.True);
            Assert.That(template.Controls, Does.Contain("Mouse").And.Contain("Space"));
            Assert.That(template.Preview, Is.Not.Null);
            Assert.That(AssetDatabase.GUIDToAssetPath(template.StarterSceneGuid), Is.EqualTo(StarterPath));

            var player = template.PlayerPrefab.GetComponent<WorkshopPlayer>();
            Assert.That(player.CanJump, Is.True);
            Assert.That(player.Facing, Is.EqualTo(PlayerFacing.CameraDirection));
            Assert.That(template.CameraRigPrefab.GetComponent<FirstPersonCameraRig>(), Is.Not.Null);
            Assert.That(template.CameraRigPrefab.GetComponent<WorkshopCameraRig>().LocksCursor, Is.True);
        }

        [Test]
        public void OwnBodyIsHiddenFromTheCameraButCastsAShadow()
        {
            var renderers = Template.PlayerPrefab.GetComponentsInChildren<Renderer>(true);
            Assert.That(renderers, Is.Not.Empty);
            Assert.That(renderers.All(r => r.shadowCastingMode == ShadowCastingMode.ShadowsOnly), Is.True);
            var camera = Template.CameraRigPrefab.GetComponentInChildren<Camera>();
            Assert.That(camera.nearClipPlane, Is.LessThanOrEqualTo(0.1f), "Close walls are not clipped away.");
        }

        [Test]
        public void TeamSceneFromFirstPersonTemplateIsReady()
        {
            var path = TeamSceneFactory.Create(TestTeam, "Dark Halls", "Escape the halls.", Template, out var error);
            Assert.That(path, Is.Not.Null, error);
            Assert.That(path, Does.EndWith("_FirstPerson.unity"));
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            Assert.That(WorkshopSceneQuery.FindContext(scene).Template, Is.EqualTo(Template));
            Assert.That(WorkshopPreflight.Run(scene).Count(i => i.Severity == IssueSeverity.Error), Is.EqualTo(0));
        }

        [Test]
        public void JumpOnlyAndTallPiecesAreFlaggedInTopDown()
        {
            var scene = EditorSceneManager.OpenScene(TopDownStarterPath, OpenSceneMode.Single);
            var platform = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PlatformPath), scene);
            platform.transform.position = new Vector3(-4f, 0f, 2f);
            var wall = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(TallWallPath), scene);
            wall.transform.position = new Vector3(2f, 0f, 2f);

            var issues = WorkshopPreflight.Run(scene);
            var platformIssue = issues.Single(i => i.Target == platform);
            Assert.That(platformIssue.Severity, Is.EqualTo(IssueSeverity.Warning));
            Assert.That(platformIssue.Message, Does.Contain("needs jumping"));
            Assert.That(platformIssue.Fix, Is.Null, "Content is never deleted automatically.");
            Assert.That(issues.Single(i => i.Target == wall).Message, Does.Contain("hide the player"));
        }

        [Test]
        public void TheSamePiecesSuitFirstPerson()
        {
            var scene = EditorSceneManager.OpenScene(StarterPath, OpenSceneMode.Single);
            Assert.That(WorkshopSceneQuery.FindActive<PieceTraits>(scene), Is.Not.Empty, "The starter has a raised platform.");
            Assert.That(WorkshopPreflight.Run(scene).Any(i => i.Message.Contains("needs jumping")), Is.False);
        }
    }
}
