using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityWorkshop
{
    public enum AdoptionSize
    {
        [InspectorName("Keep original size")] Keep,
        [InspectorName("Small (0.5 m tall)")] Small,
        [InspectorName("Person (1.8 m tall)")] Person,
        [InspectorName("Large (3 m tall)")] Large,
    }

    public enum ColliderPolicy
    {
        [InspectorName("Automatic (recommended)")] Automatic,
        [InspectorName("Keep the model's colliders")] KeepExisting,
        [InspectorName("Add a simple box")] AddBox,
        [InspectorName("No collision")] None,
    }

    /// <summary>What a facilitator asks adoption to make.</summary>
    public sealed class AdoptionRequest
    {
        public GameObject Source;
        public AdoptedRole Role = AdoptedRole.Prop;
        public string DisplayName = "";
        public AdoptionSize Size = AdoptionSize.Keep;
        [Tooltip("Degrees to turn the model so it faces forward.")]
        public float FacingTurn;
        public bool AlignToFloor = true;
        public bool CenterOnPivot = true;
        public ColliderPolicy Colliders = ColliderPolicy.Automatic;
        /// <summary>Optional Humanoid clips for a player look.</summary>
        public AnimationClip IdleClip, RunClip;
    }

    /// <summary>What adoption found, before anything is created.</summary>
    public sealed class AdoptionAnalysis
    {
        public bool Supported;
        public string Problem;
        public string Kind = "";
        public Bounds Bounds;
        public int RendererCount;
        public bool HasColliders;
        public bool HasMeshColliders;
        public bool IsHumanoid;
        public bool HasAnimator;
        public readonly List<string> Conflicts = new List<string>();
        public readonly List<Material> CompatibleMaterials = new List<Material>();
        public readonly List<Material> ConvertibleMaterials = new List<Material>();
        public readonly List<Material> UnsupportedMaterials = new List<Material>();
        public readonly List<string> Warnings = new List<string>();
    }

    /// <summary>What adoption did.</summary>
    public sealed class AdoptionResult
    {
        public bool Success;
        public string Error;
        public string PrefabPath;
        public readonly List<string> Changes = new List<string>();
        public readonly List<string> Limitations = new List<string>();
        public readonly List<string> CreatedAssets = new List<string>();
    }

    /// <summary>
    /// "Adopt for Workshop": wraps a third-party model or prefab in a workshop-owned prefab under
    /// AdoptedAssets. The vendor asset is referenced as a nested prefab and never modified; all fixes
    /// (placement, disabled components, converted materials, animation) are overrides in the owned
    /// wrapper. A failed adoption deletes only the folder it created.
    /// </summary>
    public static class WorkshopAssetAdoption
    {
        public const string MaterialsFolder = "Materials";

        /// <summary>Test hook: called with a step name during adoption so tests can simulate failure.</summary>
        internal static Action<string> FailurePoint;

        public static float TargetHeight(AdoptionSize size) =>
            size == AdoptionSize.Small ? 0.5f : size == AdoptionSize.Person ? 1.8f : size == AdoptionSize.Large ? 3f : 0f;

        // ---- Analysis ------------------------------------------------------------------------------

        public static AdoptionAnalysis Analyze(Object asset)
        {
            var analysis = new AdoptionAnalysis();
            var path = asset != null ? AssetDatabase.GetAssetPath(asset) : null;
            if (string.IsNullOrEmpty(path))
            {
                analysis.Problem = "Select a model or prefab in the Project window.";
                return analysis;
            }
            if (path.StartsWith(WorkshopPaths.Root + "/"))
            {
                analysis.Problem = "This is already a workshop asset. Adopt third-party assets from outside the WorkshopKit folder.";
                return analysis;
            }
            if (asset is Material)
            {
                analysis.Problem = "Materials are adopted together with the model that uses them. Select the model.";
                return analysis;
            }
            if (asset is AnimationClip || asset is RuntimeAnimatorController)
            {
                analysis.Problem = "Animations are adopted with a Humanoid character: select the character model and choose its Idle and Run clips.";
                return analysis;
            }
            if (!(asset is GameObject source))
            {
                analysis.Problem = $"'{asset.name}' is not a model or prefab, so it cannot be adopted.";
                return analysis;
            }

            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            analysis.Kind = importer != null ? "Model" : "Prefab";
            analysis.IsHumanoid = importer != null && importer.animationType == ModelImporterAnimationType.Human;

            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(source, preview);
                var renderers = instance.GetComponentsInChildren<Renderer>(true);
                analysis.RendererCount = renderers.Length;
                if (renderers.Length == 0)
                {
                    analysis.Problem = "It has nothing visible (no renderers).";
                    return analysis;
                }
                analysis.Bounds = MeasureBounds(instance);
                var colliders = instance.GetComponentsInChildren<Collider>(true);
                analysis.HasColliders = colliders.Length > 0;
                analysis.HasMeshColliders = colliders.Any(c => c is MeshCollider);
                analysis.HasAnimator = instance.GetComponentInChildren<Animator>(true) != null;
                foreach (var description in DescribeConflicts(instance))
                    analysis.Conflicts.Add(description);

                var seen = new HashSet<Material>();
                foreach (var material in renderers.SelectMany(r => r.sharedMaterials))
                {
                    if (material == null || !seen.Add(material))
                        continue;
                    switch (WorkshopMaterialConverter.Classify(material))
                    {
                        case MaterialSupport.Compatible: analysis.CompatibleMaterials.Add(material); break;
                        case MaterialSupport.Convertible: analysis.ConvertibleMaterials.Add(material); break;
                        default: analysis.UnsupportedMaterials.Add(material); break;
                    }
                }

                var missing = instance.GetComponentsInChildren<Transform>(true).Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
                if (missing > 0)
                    analysis.Warnings.Add($"{missing} missing script(s) in the model. They cannot run and are left as they are.");
                var size = analysis.Bounds.size;
                if (Mathf.Max(size.x, size.y, size.z) > 20f)
                    analysis.Warnings.Add($"It is very large ({size.x:0.#} × {size.y:0.#} × {size.z:0.#} m). Choose a size preset or ask a facilitator.");
                if (Mathf.Max(size.x, size.y, size.z) < 0.05f)
                    analysis.Warnings.Add("It is extremely small. Choose a size preset.");
                analysis.Supported = true;
                return analysis;
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        static Bounds MeasureBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
                bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        static bool IsVendorScript(Component component) =>
            component is MonoBehaviour behaviour && behaviour != null &&
            !(behaviour.GetType().Namespace ?? "").StartsWith("UnityEngine") &&
            !(behaviour is WorkshopSceneBehaviour) && !(behaviour is AdoptedAsset);

        static IEnumerable<string> DescribeConflicts(GameObject instance)
        {
            foreach (var component in instance.GetComponentsInChildren<Component>(true))
            {
                switch (component)
                {
                    case Camera _: yield return $"Camera on '{component.name}'"; break;
                    case AudioListener _: yield return $"Audio Listener on '{component.name}'"; break;
                    case CharacterController _: yield return $"Character Controller on '{component.name}'"; break;
                    case Rigidbody _: yield return $"Physics body on '{component.name}'"; break;
                    case Animator animator when animator.applyRootMotion: yield return $"Root-motion Animator on '{component.name}'"; break;
                    default:
                        if (IsVendorScript(component))
                            yield return $"Script '{component.GetType().Name}' on '{component.name}'";
                        break;
                }
            }
        }

        // ---- Adoption ------------------------------------------------------------------------------

        public static AdoptionResult Adopt(AdoptionRequest request)
        {
            var result = new AdoptionResult();
            var analysis = Analyze(request.Source);
            if (!analysis.Supported)
            {
                result.Error = analysis.Problem;
                return result;
            }

            var name = SafeName(string.IsNullOrWhiteSpace(request.DisplayName) ? request.Source.name : request.DisplayName);
            string folder = null;
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                EnsureAdoptedRoot();
                folder = AssetDatabase.GenerateUniqueAssetPath($"{WorkshopPaths.AdoptedAssets}/{name}");
                AssetDatabase.CreateFolder(WorkshopPaths.AdoptedAssets, Path.GetFileName(folder));
                result.CreatedAssets.Add(folder);

                var root = new GameObject(name);
                SceneManagerMove(root, preview);
                var marker = root.AddComponent<AdoptedAsset>();
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(request.Source, preview);
                visual.transform.SetParent(root.transform, false);

                PlaceVisual(request, visual, result);
                FailurePoint?.Invoke("placed");
                NeutraliseConflicts(request.Role, visual, result);
                ApplyColliderPolicy(request, root, visual, analysis, result);
                ConvertMaterials(visual, folder, result);
                FailurePoint?.Invoke("materials");
                var animator = SetUpAnimation(request, visual, folder, analysis, result);

                foreach (var material in analysis.UnsupportedMaterials)
                    result.Limitations.Add($"Material '{material.name}' uses shader '{material.shader.name}', which has no automatic URP conversion. It is kept as it is and may look wrong; a facilitator decides.");
                foreach (var warning in analysis.Warnings)
                    result.Limitations.Add(warning);

                var serialized = new SerializedObject(marker);
                serialized.FindProperty("m_DisplayName").stringValue = name;
                serialized.FindProperty("m_Role").enumValueIndex = (int)request.Role;
                serialized.FindProperty("m_SourceGuid").stringValue = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(request.Source));
                serialized.FindProperty("m_Visual").objectReferenceValue = visual.transform;
                serialized.FindProperty("m_Animator").objectReferenceValue = animator;
                serialized.FindProperty("m_Notes").stringValue = string.Join("\n", result.Changes.Concat(result.Limitations));
                serialized.ApplyModifiedPropertiesWithoutUndo();

                FailurePoint?.Invoke("save");
                var prefabPath = $"{folder}/{name}.prefab";
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out var saved);
                if (!saved)
                    throw new InvalidOperationException("Unity could not save the adopted prefab.");
                result.PrefabPath = prefabPath;
                result.Success = true;
                return result;
            }
            catch (Exception exception)
            {
                result.Success = false;
                result.Error = "Adoption failed and was undone: " + exception.Message;
                // Remove only what this adoption created.
                if (folder != null && AssetDatabase.IsValidFolder(folder))
                    AssetDatabase.DeleteAsset(folder);
                result.CreatedAssets.Clear();
                result.PrefabPath = null;
                return result;
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
                AssetDatabase.SaveAssets();
            }
        }

        static void SceneManagerMove(GameObject go, UnityEngine.SceneManagement.Scene scene) =>
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);

        public static string SafeName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var clean = new string(name.Trim().Select(c => invalid.Contains(c) ? '_' : c).ToArray());
            return clean.Length == 0 ? "Adopted Asset" : clean.Length > 40 ? clean.Substring(0, 40) : clean;
        }

        static void EnsureAdoptedRoot()
        {
            if (!AssetDatabase.IsValidFolder(WorkshopPaths.AdoptedAssets))
                AssetDatabase.CreateFolder(WorkshopPaths.Root, Path.GetFileName(WorkshopPaths.AdoptedAssets));
        }

        /// <summary>Turn, scale and offset go on the model child only; the wrapper root stays at rotation zero, scale one.</summary>
        static void PlaceVisual(AdoptionRequest request, GameObject visual, AdoptionResult result)
        {
            visual.transform.localRotation = Quaternion.Euler(0f, request.FacingTurn, 0f);
            var bounds = MeasureBounds(visual);
            var target = TargetHeight(request.Size);
            if (target > 0f && bounds.size.y > 0.0001f)
            {
                var factor = target / bounds.size.y;
                visual.transform.localScale = Vector3.Scale(visual.transform.localScale, Vector3.one * factor);
                result.Changes.Add($"Scaled the model to {target} m tall.");
                bounds = MeasureBounds(visual);
            }
            var offset = Vector3.zero;
            if (request.AlignToFloor)
                offset.y = -bounds.min.y;
            if (request.CenterOnPivot)
            {
                offset.x = -bounds.center.x;
                offset.z = -bounds.center.z;
            }
            visual.transform.localPosition += offset;
            if (request.AlignToFloor)
                result.Changes.Add("Placed the model so its lowest point sits on the ground.");
            if (Mathf.Abs(request.FacingTurn) > 0.01f)
                result.Changes.Add($"Turned the model {request.FacingTurn:0}° to face forward.");
        }

        /// <summary>Disables parts that would fight the workshop rig, in the owned instance only.</summary>
        static void NeutraliseConflicts(AdoptedRole role, GameObject visual, AdoptionResult result)
        {
            foreach (var component in visual.GetComponentsInChildren<Component>(true))
            {
                switch (component)
                {
                    case Camera camera when camera.enabled:
                        camera.enabled = false;
                        result.Changes.Add($"Turned off the model's camera on '{camera.name}' (the game has its own).");
                        break;
                    case AudioListener listener when listener.enabled:
                        listener.enabled = false;
                        result.Changes.Add($"Turned off the model's Audio Listener on '{listener.name}'.");
                        break;
                    case CharacterController controller when controller.enabled:
                        controller.enabled = false;
                        result.Changes.Add($"Turned off the model's Character Controller on '{controller.name}' (the workshop motor moves the player).");
                        break;
                    case Rigidbody body when !body.isKinematic:
                        body.isKinematic = true;
                        body.useGravity = false;
                        result.Changes.Add($"Made the physics body on '{body.name}' kinematic so it cannot be pushed around.");
                        break;
                    case Animator animator when animator.applyRootMotion:
                        animator.applyRootMotion = false;
                        result.Changes.Add($"Turned off root motion on '{animator.name}'.");
                        break;
                    default:
                        if (IsVendorScript(component) && ((MonoBehaviour)component).enabled)
                        {
                            ((MonoBehaviour)component).enabled = false;
                            result.Changes.Add($"Turned off script '{component.GetType().Name}' on '{component.name}'. Its behaviour is not supported in the workshop.");
                        }
                        break;
                }
            }
            if (role == AdoptedRole.PlayerLook)
                foreach (var collider in visual.GetComponentsInChildren<Collider>(true).Where(c => c.enabled))
                {
                    collider.enabled = false;
                    result.Changes.Add($"Turned off collider on '{collider.name}' (the player's capsule handles collision).");
                }
        }

        static void ApplyColliderPolicy(AdoptionRequest request, GameObject root, GameObject visual, AdoptionAnalysis analysis, AdoptionResult result)
        {
            if (request.Role == AdoptedRole.PlayerLook)
                return;
            var policy = request.Colliders;
            if (policy == ColliderPolicy.Automatic)
                policy = analysis.HasColliders && !analysis.HasMeshColliders ? ColliderPolicy.KeepExisting : ColliderPolicy.AddBox;

            switch (policy)
            {
                case ColliderPolicy.KeepExisting:
                    result.Changes.Add("Kept the model's own colliders.");
                    break;
                case ColliderPolicy.AddBox:
                    foreach (var collider in visual.GetComponentsInChildren<Collider>(true).Where(c => c.enabled))
                    {
                        collider.enabled = false;
                        result.Changes.Add($"Turned off the model's {collider.GetType().Name} on '{collider.name}' in favour of a simple box.");
                    }
                    var bounds = MeasureBounds(visual);
                    var box = root.AddComponent<BoxCollider>();
                    box.center = root.transform.InverseTransformPoint(bounds.center);
                    box.size = bounds.size;
                    result.Changes.Add($"Added a {bounds.size.x:0.##} × {bounds.size.y:0.##} × {bounds.size.z:0.##} m box collider.");
                    break;
                case ColliderPolicy.None:
                    foreach (var collider in visual.GetComponentsInChildren<Collider>(true).Where(c => c.enabled))
                        collider.enabled = false;
                    result.Changes.Add("No collision: players walk through it.");
                    break;
            }
        }

        /// <summary>Owned URP copies for recognised built-in materials, shared between renderers that used the same original.</summary>
        static void ConvertMaterials(GameObject visual, string folder, AdoptionResult result)
        {
            var copies = new Dictionary<Material, Material>();
            var notes = new List<string>();
            foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                var changed = false;
                for (var i = 0; i < materials.Length; i++)
                {
                    var original = materials[i];
                    if (original == null || WorkshopMaterialConverter.Classify(original) != MaterialSupport.Convertible)
                        continue;
                    if (!copies.TryGetValue(original, out var copy))
                    {
                        if (copies.Count == 0)
                            AssetDatabase.CreateFolder(folder, MaterialsFolder);
                        var path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{MaterialsFolder}/{SafeName(original.name)}.mat");
                        copy = WorkshopMaterialConverter.CreateOwnedCopy(original, path, notes);
                        copies[original] = copy;
                        result.CreatedAssets.Add(path);
                        result.Changes.Add($"Made a URP copy of material '{original.name}' (the original is unchanged).");
                    }
                    materials[i] = copy;
                    changed = true;
                }
                if (changed)
                    renderer.sharedMaterials = materials;
            }
            result.Limitations.AddRange(notes);
        }

        /// <summary>
        /// Player looks: Humanoid models with Idle and Run clips get an owned controller driven by Speed;
        /// everything else is shown as a still model. Root motion is always off.
        /// </summary>
        static Animator SetUpAnimation(AdoptionRequest request, GameObject visual, string folder, AdoptionAnalysis analysis, AdoptionResult result)
        {
            var animator = visual.GetComponentInChildren<Animator>(true);
            if (request.Role != AdoptedRole.PlayerLook)
            {
                if (animator != null)
                {
                    animator.enabled = false;
                    result.Changes.Add("Props do not animate: turned off the model's Animator.");
                }
                return null;
            }

            if (!analysis.IsHumanoid)
            {
                if (animator != null)
                    animator.enabled = false;
                result.Limitations.Add("Not a Humanoid rig, so it is used as a still model. Generic rigs need a hand-made controller mapping.");
                return null;
            }

            if (request.IdleClip == null || request.RunClip == null)
            {
                if (animator != null)
                    animator.enabled = false;
                result.Limitations.Add("Humanoid, but no Idle and Run clips were chosen, so it is used as a still model.");
                return null;
            }

            var problem = ValidateHumanoidClip(request.IdleClip) ?? ValidateHumanoidClip(request.RunClip);
            if (problem != null)
            {
                if (animator != null)
                    animator.enabled = false;
                result.Limitations.Add(problem + " The model is used as a still model.");
                return null;
            }

            if (animator == null)
                animator = visual.AddComponent<Animator>();
            var importer = (ModelImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(request.Source));
            var avatar = AssetDatabase.LoadAllAssetsAtPath(importer.assetPath).OfType<Avatar>().FirstOrDefault();
            if (avatar == null || !avatar.isValid || !avatar.isHuman)
            {
                animator.enabled = false;
                result.Limitations.Add("The Humanoid avatar is missing or invalid, so it is used as a still model.");
                return null;
            }
            animator.avatar = avatar;
            animator.applyRootMotion = false;
            animator.enabled = true;
            var controllerPath = $"{folder}/{SafeName(request.Source.name)} Controller.controller";
            animator.runtimeAnimatorController = CreateLocomotionController(controllerPath, request.IdleClip, request.RunClip);
            result.CreatedAssets.Add(controllerPath);
            result.Changes.Add("Created an owned animation controller: Idle and Run blended by Speed.");
            return animator;
        }

        public static string ValidateHumanoidClip(AnimationClip clip) =>
            clip == null ? "A clip is missing." :
            !clip.humanMotion ? $"Clip '{clip.name}' is not a Humanoid animation." : null;

        /// <summary>
        /// Owned controller with the parameters the <see cref="PlayerVisualAdapter"/> drives: a 1D blend tree from
        /// Idle (Speed 0) to Run (Speed 1), plus Grounded and VerticalSpeed for later use.
        /// </summary>
        public static AnimatorController CreateLocomotionController(string path, AnimationClip idle, AnimationClip run)
        {
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
            controller.AddParameter("VerticalSpeed", AnimatorControllerParameterType.Float);
            var state = controller.CreateBlendTreeInController("Locomotion", out var tree, 0);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.AddChild(idle, 0f);
            tree.AddChild(run, 1f);
            controller.layers[0].stateMachine.defaultState = state;
            return controller;
        }

        // ---- Using adopted assets ------------------------------------------------------------------

        /// <summary>All adopted wrappers with a given role.</summary>
        public static List<AdoptedAsset> Find(AdoptedRole role)
        {
            var found = new List<AdoptedAsset>();
            if (!AssetDatabase.IsValidFolder(WorkshopPaths.AdoptedAssets))
                return found;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { WorkshopPaths.AdoptedAssets }))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (prefab != null && prefab.TryGetComponent(out AdoptedAsset adopted) && adopted.Role == role)
                    found.Add(adopted);
            }
            return found.OrderBy(a => a.DisplayName).ToList();
        }

        public const string AdoptedLookName = "Adopted Look";

        /// <summary>
        /// Shows an adopted look on the player: hides the default model (kept for restoring), adds the look,
        /// and points the visual adapter at it. One Undo step. First-person bodies render shadows only.
        /// </summary>
        public static GameObject ApplyPlayerLook(WorkshopPlayer player, AdoptedAsset look)
        {
            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();
            var adapter = player.GetComponent<PlayerVisualAdapter>();
            if (adapter == null)
            {
                adapter = Undo.AddComponent<PlayerVisualAdapter>(player.gameObject);
                var defaultVisual = player.transform.Find("Visual");
                var adapterObject = new SerializedObject(adapter);
                adapterObject.FindProperty("m_Visual").objectReferenceValue = defaultVisual;
                adapterObject.ApplyModifiedProperties();
            }

            RemoveAdoptedLook(player, adapter, restoreDefault: false);
            var original = player.transform.Find("Visual");
            if (original != null && original.gameObject.activeSelf)
            {
                Undo.RecordObject(original.gameObject, "Use adopted look");
                original.gameObject.SetActive(false);
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(look.gameObject, player.gameObject.scene);
            Undo.RegisterCreatedObjectUndo(instance, "Use adopted look");
            Undo.SetTransformParent(instance.transform, player.transform, "Use adopted look");
            instance.name = AdoptedLookName;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            if (player.Facing == PlayerFacing.CameraDirection)
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                {
                    Undo.RecordObject(renderer, "Use adopted look");
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                }

            var instanceMarker = instance.GetComponent<AdoptedAsset>();
            var serialized = new SerializedObject(adapter);
            serialized.FindProperty("m_Visual").objectReferenceValue = instance.transform;
            serialized.FindProperty("m_Animator").objectReferenceValue = instanceMarker.Animator;
            serialized.FindProperty("m_VisualOffset").vector3Value = Vector3.zero;
            serialized.FindProperty("m_VisualTurn").floatValue = 0f;
            serialized.FindProperty("m_VisualScale").floatValue = 1f;
            serialized.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(adapter);
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
            return instance;
        }

        /// <summary>Removes an adopted look and shows the template's own model again.</summary>
        public static void RestoreDefaultLook(WorkshopPlayer player)
        {
            Undo.IncrementCurrentGroup();
            var adapter = player.GetComponent<PlayerVisualAdapter>();
            RemoveAdoptedLook(player, adapter, restoreDefault: true);
            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        }

        static void RemoveAdoptedLook(WorkshopPlayer player, PlayerVisualAdapter adapter, bool restoreDefault)
        {
            var existing = player.transform.Find(AdoptedLookName);
            if (existing != null)
                Undo.DestroyObjectImmediate(existing.gameObject);
            if (!restoreDefault)
                return;
            var original = player.transform.Find("Visual");
            if (original != null)
            {
                Undo.RecordObject(original.gameObject, "Restore default look");
                original.gameObject.SetActive(true);
            }
            if (adapter != null)
            {
                var serialized = new SerializedObject(adapter);
                serialized.FindProperty("m_Visual").objectReferenceValue = original;
                serialized.FindProperty("m_Animator").objectReferenceValue = null;
                serialized.ApplyModifiedProperties();
            }
        }

        public static bool HasAdoptedLook(WorkshopPlayer player) => player.transform.Find(AdoptedLookName) != null;
    }
}
