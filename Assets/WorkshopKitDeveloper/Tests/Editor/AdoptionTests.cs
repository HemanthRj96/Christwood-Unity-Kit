using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace UnityWorkshop.Tests
{
    /// <summary>
    /// Third-party asset adoption (A11): owned wrappers, untouched vendor files, conflict handling,
    /// material conversion, repeatable and transactional adoption, catalog use and player looks.
    /// </summary>
    public sealed class AdoptionTests
    {
        const string TopDownStarterPath = "Assets/WorkshopKit/Content/Templates/TopDown/TopDown_Starter.unity";
        const string ThirdPersonStarterPath = "Assets/WorkshopKit/Content/Templates/ThirdPerson/ThirdPerson_Starter.unity";
        const string FirstPersonStarterPath = "Assets/WorkshopKit/Content/Templates/FirstPerson/FirstPerson_Starter.unity";
        const string TempClips = "Assets/ZZ_TestVendor/Clips";
        const string TestTeam = "ZZ_AutomatedAdoption";

        VendorFixture m_Vendor;
        EditorBuildSettingsScene[] m_BuildScenes;

        [SetUp]
        public void SetUp()
        {
            m_BuildScenes = EditorBuildSettings.scenes;
            m_Vendor = new VendorFixture();
        }

        [TearDown]
        public void TearDown()
        {
            if (!Application.isPlaying)
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            m_Vendor.Dispose();
            var team = WorkshopPaths.TeamGames + "/" + TestTeam;
            if (AssetDatabase.IsValidFolder(team))
                AssetDatabase.DeleteAsset(team);
            EditorBuildSettings.scenes = m_BuildScenes;
        }

        static AdoptionResult Adopt(GameObject source, AdoptedRole role, AdoptionSize size = AdoptionSize.Keep)
        {
            var result = WorkshopAssetAdoption.Adopt(new AdoptionRequest { Source = source, Role = role, Size = size, DisplayName = source.name });
            Assert.That(result.Success, Is.True, result.Error);
            return result;
        }

        [Test]
        public void AnalysisDetectsConflictsAndMaterials()
        {
            var analysis = WorkshopAssetAdoption.Analyze(m_Vendor.Robot);
            Assert.That(analysis.Supported, Is.True);
            Assert.That(analysis.Kind, Is.EqualTo("Prefab"));
            foreach (var expected in new[] { "Camera", "Audio Listener", "Character Controller", "Physics body", "Root-motion Animator", "VendorSpinner" })
                Assert.That(analysis.Conflicts.Any(c => c.Contains(expected)), Is.True, expected);
            Assert.That(analysis.ConvertibleMaterials.Select(m => m.name), Is.EquivalentTo(new[] { "Vendor Unlit" }));
            Assert.That(analysis.UnsupportedMaterials.Select(m => m.name), Is.EquivalentTo(new[] { "Vendor Skybox" }));

            Assert.That(WorkshopAssetAdoption.Analyze(m_Vendor.CrateMaterial).Supported, Is.False, "Materials alone are not adopted.");
            var kitPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WorkshopPaths.ExitGoalPrefab);
            Assert.That(WorkshopAssetAdoption.Analyze(kitPrefab).Supported, Is.False, "Workshop assets are not adopted again.");
        }

        [Test]
        public void AdoptedPropIsAnOwnedWrapperAndVendorFilesAreUntouched()
        {
            var before = VendorFixture.Hashes();
            var result = Adopt(m_Vendor.Crate, AdoptedRole.Prop, AdoptionSize.Person);
            Assert.That(VendorFixture.Hashes(), Is.EqualTo(before), "Vendor bytes and GUIDs are unchanged.");

            Assert.That(result.PrefabPath, Does.StartWith(WorkshopPaths.AdoptedAssets + "/Vendor Crate/"));
            var wrapper = AssetDatabase.LoadAssetAtPath<GameObject>(result.PrefabPath);
            Assert.That(wrapper.transform.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(wrapper.transform.localScale, Is.EqualTo(Vector3.one));
            var adopted = wrapper.GetComponent<AdoptedAsset>();
            Assert.That(adopted.Role, Is.EqualTo(AdoptedRole.Prop));
            Assert.That(adopted.SourceGuid, Is.EqualTo(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(m_Vendor.Crate))));
            Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(adopted.Visual.gameObject), Is.EqualTo(m_Vendor.Crate),
                "The model is a reference to the vendor prefab, not a copy.");

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(wrapper);
            try
            {
                var bounds = instance.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
                Assert.That(bounds.min.y, Is.EqualTo(0f).Within(0.01f), "Stands on the ground.");
                Assert.That(new Vector2(bounds.center.x, bounds.center.z).magnitude, Is.LessThan(0.01f), "Centred on the pivot.");
                Assert.That(bounds.size.y, Is.EqualTo(1.8f).Within(0.01f), "Person size preset.");
                var box = instance.GetComponent<BoxCollider>();
                Assert.That(box, Is.Not.Null, "A mesh collider is replaced by a simple owned box.");
                Assert.That(box.bounds.size.y, Is.EqualTo(1.8f).Within(0.05f));
                Assert.That(instance.GetComponentsInChildren<MeshCollider>(true).All(c => !c.enabled), Is.True);

                var material = instance.GetComponentInChildren<MeshRenderer>().sharedMaterial;
                Assert.That(material, Is.Not.EqualTo(m_Vendor.CrateMaterial));
                Assert.That(material.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"));
                Assert.That(AssetDatabase.GetAssetPath(material), Does.StartWith(WorkshopPaths.AdoptedAssets + "/Vendor Crate/Materials/"));
                Assert.That(material.GetColor("_BaseColor"), Is.EqualTo(new Color(0.8f, 0.2f, 0.1f)));
                Assert.That(material.GetFloat("_Smoothness"), Is.EqualTo(0.7f).Within(0.001f));
                Assert.That(material.GetFloat("_AlphaClip"), Is.EqualTo(1f), "Cut-out is carried over.");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
            Assert.That(m_Vendor.Crate.GetComponentInChildren<MeshRenderer>().sharedMaterial.shader.name, Is.EqualTo("Standard"), "The vendor prefab still uses its own material.");
        }

        [Test]
        public void RepeatedAdoptionNeverCollides()
        {
            var first = Adopt(m_Vendor.Crate, AdoptedRole.Prop);
            var firstGuid = AssetDatabase.AssetPathToGUID(first.PrefabPath);
            var second = Adopt(m_Vendor.Crate, AdoptedRole.Prop);
            Assert.That(second.PrefabPath, Is.Not.EqualTo(first.PrefabPath));
            Assert.That(AssetDatabase.AssetPathToGUID(first.PrefabPath), Is.EqualTo(firstGuid), "The first adoption is untouched.");
        }

        [Test]
        public void ConflictingPartsAreTurnedOffOnlyInTheOwnedCopy()
        {
            var before = VendorFixture.Hashes();
            var result = Adopt(m_Vendor.Robot, AdoptedRole.Prop);
            Assert.That(VendorFixture.Hashes(), Is.EqualTo(before));

            var wrapper = AssetDatabase.LoadAssetAtPath<GameObject>(result.PrefabPath);
            Assert.That(wrapper.GetComponentsInChildren<Camera>(true).All(c => !c.enabled), Is.True);
            Assert.That(wrapper.GetComponentsInChildren<AudioListener>(true).All(c => !c.enabled), Is.True);
            Assert.That(wrapper.GetComponentsInChildren<CharacterController>(true).All(c => !c.enabled), Is.True);
            Assert.That(wrapper.GetComponentsInChildren<Rigidbody>(true).All(b => b.isKinematic), Is.True);
            Assert.That(wrapper.GetComponentsInChildren<VendorSpinner>(true).All(s => !s.enabled), Is.True);
            Assert.That(wrapper.GetComponentsInChildren<Animator>(true).All(a => !a.applyRootMotion && !a.enabled), Is.True);
            Assert.That(result.Limitations.Any(l => l.Contains("Skybox/Procedural")), Is.True, "The unsupported shader is reported, not guessed.");

            // The vendor original keeps everything it had.
            Assert.That(m_Vendor.Robot.GetComponentInChildren<Camera>().enabled, Is.True);
            Assert.That(m_Vendor.Robot.GetComponent<Rigidbody>().isKinematic, Is.False);
            Assert.That(m_Vendor.Robot.GetComponent<VendorSpinner>().enabled, Is.True);
        }

        [Test]
        public void FailedAdoptionRemovesOnlyItsOwnFiles()
        {
            var kept = Adopt(m_Vendor.Crate, AdoptedRole.Prop);
            var foldersBefore = VendorFixture.AdoptedFolders();
            var vendorBefore = VendorFixture.Hashes();

            WorkshopAssetAdoption.FailurePoint = step =>
            {
                if (step == "materials")
                    throw new System.InvalidOperationException("simulated failure");
            };
            var failed = WorkshopAssetAdoption.Adopt(new AdoptionRequest { Source = m_Vendor.Crate, Role = AdoptedRole.Prop });
            WorkshopAssetAdoption.FailurePoint = null;

            Assert.That(failed.Success, Is.False);
            Assert.That(failed.Error, Does.Contain("simulated failure"));
            Assert.That(VendorFixture.AdoptedFolders(), Is.EquivalentTo(foldersBefore), "The half-made folder and its material copies are gone.");
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(kept.PrefabPath), Is.Not.Null, "Earlier adoptions are untouched.");
            Assert.That(VendorFixture.Hashes(), Is.EqualTo(vendorBefore));
        }

        [Test]
        public void AdoptedPropIsInTheCatalogAndPlacesCleanly()
        {
            var result = Adopt(m_Vendor.Crate, AdoptedRole.Prop);
            var entry = WorkshopCatalog.Load().AllEntries().Single(e => AssetDatabase.GetAssetPath(e.prefab) == result.PrefabPath);
            Assert.That(entry.category, Is.EqualTo(CatalogCategory.Decoration));

            var scene = EditorSceneManager.OpenScene(TopDownStarterPath, OpenSceneMode.Single);
            var placed = WorkshopPlacement.Place(entry.prefab, scene, new Vector3(-2f, 0f, 3f), Quaternion.identity, entry.groupName, entry.gridSize);
            Assert.That(placed, Is.Not.Null);
            Assert.That(WorkshopPreflight.Run(scene).Count(i => i.Severity == IssueSeverity.Error), Is.EqualTo(0));
        }

        [Test]
        public void PlayerLookReplacesTheModelWithoutCompetingMotion()
        {
            var result = Adopt(m_Vendor.Robot, AdoptedRole.PlayerLook);
            Assert.That(result.Limitations.Any(l => l.Contains("Not a Humanoid rig")), Is.True, "A generic rig is honestly reported as a still model.");
            var look = AssetDatabase.LoadAssetAtPath<GameObject>(result.PrefabPath).GetComponent<AdoptedAsset>();
            Assert.That(WorkshopCatalog.Load().AllEntries().Any(e => e.playerLook == look && e.category == CatalogCategory.PlayerAppearance), Is.True);
            Assert.That(look.GetComponentsInChildren<Collider>(true).All(c => !c.enabled), Is.True, "A look never collides; the player capsule does.");

            var scene = EditorSceneManager.OpenScene(ThirdPersonStarterPath, OpenSceneMode.Single);
            var player = WorkshopSceneQuery.FindActive<WorkshopPlayer>(scene).Single();
            var original = player.transform.Find("Visual");
            var instance = WorkshopAssetAdoption.ApplyPlayerLook(player, look);

            Assert.That(original.gameObject.activeSelf, Is.False);
            Assert.That(player.GetComponent<PlayerVisualAdapter>().Visual, Is.EqualTo(instance.transform));
            Assert.That(WorkshopPreflight.CheckCompetingMotion(player).Where(i => i.Severity == IssueSeverity.Error), Is.Empty);
            Assert.That(WorkshopPreflight.Run(scene).Count(i => i.Severity == IssueSeverity.Error), Is.EqualTo(0));

            WorkshopAssetAdoption.ApplyPlayerLook(player, look);
            Assert.That(player.GetComponentsInChildren<AdoptedAsset>(true).Length, Is.EqualTo(1), "Using a look twice does not stack models.");

            WorkshopAssetAdoption.RestoreDefaultLook(player);
            Assert.That(WorkshopAssetAdoption.HasAdoptedLook(player), Is.False);
            Assert.That(original.gameObject.activeSelf, Is.True);
            Assert.That(player.GetComponent<PlayerVisualAdapter>().Visual, Is.EqualTo(original));
        }

        [Test]
        public void PlayerLookIsUndoableAndHiddenFromAFirstPersonCamera()
        {
            var look = AssetDatabase.LoadAssetAtPath<GameObject>(Adopt(m_Vendor.Robot, AdoptedRole.PlayerLook).PrefabPath).GetComponent<AdoptedAsset>();
            var scene = EditorSceneManager.OpenScene(FirstPersonStarterPath, OpenSceneMode.Single);
            var player = WorkshopSceneQuery.FindActive<WorkshopPlayer>(scene).Single();
            var instance = WorkshopAssetAdoption.ApplyPlayerLook(player, look);
            Assert.That(instance.GetComponentsInChildren<Renderer>().All(r => r.shadowCastingMode == ShadowCastingMode.ShadowsOnly), Is.True);

            Undo.PerformUndo();
            Assert.That(WorkshopAssetAdoption.HasAdoptedLook(player), Is.False);
            Assert.That(player.transform.Find("Visual").gameObject.activeSelf, Is.True);
        }

        [Test]
        public void AdoptedLookSurvivesAChangeOfTemplate()
        {
            var look = AssetDatabase.LoadAssetAtPath<GameObject>(Adopt(m_Vendor.Robot, AdoptedRole.PlayerLook).PrefabPath).GetComponent<AdoptedAsset>();
            var scene = EditorSceneManager.OpenScene(ThirdPersonStarterPath, OpenSceneMode.Single);
            WorkshopAssetAdoption.ApplyPlayerLook(WorkshopSceneQuery.FindActive<WorkshopPlayer>(scene).Single(), look);

            var sideScroller = AssetDatabase.LoadAssetAtPath<GameTemplateDefinition>("Assets/WorkshopKit/Content/Templates/SideScroller/Side-Scroller Template.asset");
            WorkshopRigTools.Apply(scene, sideScroller);
            var player = WorkshopSceneQuery.FindActive<WorkshopPlayer>(scene).Single();
            Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(player.gameObject), Is.EqualTo(sideScroller.PlayerPrefab));
            Assert.That(WorkshopAssetAdoption.HasAdoptedLook(player), Is.True);
        }

        [Test]
        public void LocomotionControllerMappingIsComplete()
        {
            AssetDatabase.CreateFolder(VendorFixture.Folder, "Clips");
            var idle = new AnimationClip { name = "Idle" };
            var run = new AnimationClip { name = "Run" };
            AssetDatabase.CreateAsset(idle, $"{TempClips}/Idle.anim");
            AssetDatabase.CreateAsset(run, $"{TempClips}/Run.anim");
            Assert.That(WorkshopAssetAdoption.ValidateHumanoidClip(idle), Does.Contain("not a Humanoid"), "Generic clips are refused for Humanoid mapping.");

            var controller = WorkshopAssetAdoption.CreateLocomotionController($"{TempClips}/Test.controller", idle, run);
            Assert.That(controller.parameters.Select(p => p.name), Is.EquivalentTo(new[] { "Speed", "Grounded", "VerticalSpeed" }));
            var tree = (BlendTree)controller.layers[0].stateMachine.defaultState.motion;
            Assert.That(tree.blendParameter, Is.EqualTo("Speed"));
            Assert.That(tree.children.Select(c => c.threshold), Is.EqualTo(new[] { 0f, 1f }));
            Assert.That(tree.children.Select(c => c.motion), Is.EqualTo(new Motion[] { idle, run }));
        }

        [UnityTest]
        public IEnumerator AdoptedLookPlaysWithTheWorkshopMotor()
        {
            var look = AssetDatabase.LoadAssetAtPath<GameObject>(Adopt(m_Vendor.Robot, AdoptedRole.PlayerLook).PrefabPath).GetComponent<AdoptedAsset>();
            var template = AssetDatabase.LoadAssetAtPath<GameTemplateDefinition>("Assets/WorkshopKit/Content/Templates/ThirdPerson/Third-Person Template.asset");
            var path = TeamSceneFactory.Create(TestTeam, "Robot Run", "Run!", template, out var error);
            Assert.That(path, Is.Not.Null, error);
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            WorkshopAssetAdoption.ApplyPlayerLook(WorkshopSceneQuery.FindActive<WorkshopPlayer>(scene).Single(), look);
            EditorSceneManager.SaveScene(scene);

            // Keep this test's fixture alive across Play mode: no domain reload while it runs.
            var optionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            var options = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            try
            {
                yield return PlayAndCheck();
            }
            finally
            {
                EditorSettings.enterPlayModeOptionsEnabled = optionsEnabled;
                EditorSettings.enterPlayModeOptions = options;
            }
        }

        static IEnumerator PlayAndCheck()
        {
            yield return new EnterPlayMode();
            WorkshopGameContext context = null;
            var until = Time.realtimeSinceStartup + 10f;
            while ((context == null || context.Session.State != GameState.Playing) && Time.realtimeSinceStartup < until)
            {
                context = Object.FindObjectsByType<WorkshopGameContext>().FirstOrDefault();
                yield return null;
            }
            Assert.That(context, Is.Not.Null);
            Assert.That(context.Session.State, Is.EqualTo(GameState.Playing));
            var settle = Time.time + 0.5f;
            while (Time.time < settle)
                yield return null;

            var player = context.Player;
            Assert.That(player.GetComponentsInChildren<Collider>().Where(c => c.enabled), Is.EquivalentTo(new Collider[] { player.Motor.Controller }),
                "Only the workshop capsule collides.");
            Assert.That(player.Motor.IsGrounded, Is.True, "Stands on the floor; the model's physics body does not interfere.");
            Assert.That(Vector3.Distance(player.transform.position, context.StartPose.position), Is.LessThan(0.2f), "Nothing in the model moves the player.");
            yield return new ExitPlayMode();
        }
    }
}
