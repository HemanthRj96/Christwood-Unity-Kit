using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace UnityWorkshop
{
    /// <summary>What a rig restore or template change would do, shown before anything changes.</summary>
    public sealed class RigPlan
    {
        public bool CanApply = true;
        public string Blocker;
        public readonly List<string> Actions = new List<string>();
        public readonly List<string> Warnings = new List<string>();
    }

    /// <summary>
    /// Facilitator repairs that keep the level: restoring missing or broken system objects (Game Setup,
    /// Player, Camera, HUD, Event System) and switching a game to another template. Level pieces are never
    /// removed. Everything is one Undo step; preflight runs afterwards.
    /// </summary>
    public static class WorkshopRigTools
    {
        /// <summary>Describes a restore (<paramref name="template"/> = current) or a template change.</summary>
        public static RigPlan Plan(Scene scene, GameTemplateDefinition template)
        {
            var plan = new RigPlan();
            var contexts = WorkshopSceneQuery.FindActive<WorkshopGameContext>(scene);
            if (contexts.Count > 1)
            {
                plan.CanApply = false;
                plan.Blocker = $"There are {contexts.Count} Game Setup objects. Delete the extra ones first; the tool will not guess which to keep.";
                return plan;
            }
            if (PrefabStageUtility.GetCurrentPrefabStage() != null || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                plan.CanApply = false;
                plan.Blocker = "Leave Play mode and Prefab mode first.";
                return plan;
            }
            template ??= contexts.FirstOrDefault()?.Template;
            if (template == null)
            {
                plan.CanApply = false;
                plan.Blocker = "Choose a template.";
                return plan;
            }

            var context = contexts.FirstOrDefault();
            if (context == null)
                plan.Actions.Add("Add a Game Setup.");
            else if (context.Template != template)
                plan.Actions.Add($"Change the template from {(context.Template != null ? context.Template.DisplayName : "none")} to {template.DisplayName}. Title, objective, rules and timer are kept.");

            var player = WorkshopSceneQuery.FindActive<WorkshopPlayer>(scene);
            if (player.Count == 0)
                plan.Actions.Add($"Add the {template.DisplayName} player at the Start Point.");
            else if (player.Count > 1 || !Matches(player[0].gameObject, template.PlayerPrefab))
                plan.Actions.Add($"Replace the player with the {template.DisplayName} player" + (player.Any(p => WorkshopAssetAdoption.HasAdoptedLook(p)) ? " (its adopted look is kept)." : "."));

            var rigs = WorkshopSceneQuery.FindActive<WorkshopCameraRig>(scene);
            if (rigs.Count == 0)
                plan.Actions.Add($"Add the {template.DisplayName} camera.");
            else if (rigs.Count > 1 || !Matches(rigs[0].gameObject, template.CameraRigPrefab))
                plan.Actions.Add($"Replace the camera with the {template.DisplayName} camera.");

            if (WorkshopSceneQuery.FindActive<WorkshopHUD>(scene).Count == 0)
                plan.Actions.Add("Add the Workshop HUD.");
            if (WorkshopSceneQuery.FindActive<EventSystem>(scene).Count == 0 && WorkshopSceneQuery.FindActive<WorkshopHUD>(scene).Count > 0)
                plan.Actions.Add("Add an Event System.");

            foreach (var traits in WorkshopSceneQuery.FindActive<PieceTraits>(scene))
            {
                var reason = PieceTraits.Unsuitability(traits.gameObject, template);
                if (reason != null)
                    plan.Warnings.Add($"'{traits.name}' {reason}. It is kept; Check will list it.");
            }
            if (template.Perspective == TemplatePerspective.SideScroller)
            {
                var spawn = WorkshopSceneQuery.FindActive<PlayerSpawn>(scene).FirstOrDefault();
                var depth = spawn != null ? spawn.transform.position.z : 0f;
                var off = WorkshopSceneQuery.FindActive<WorkshopSceneBehaviour>(scene)
                    .Count(p => !(p is WorkshopCameraRig || p is WorkshopHUD || p is WorkshopPlayer) && Mathf.Abs(p.transform.position.z - depth) > WorkshopPreflight.PlaneTolerance);
                if (off > 0)
                    plan.Warnings.Add($"{off} gameplay piece(s) are off the new play plane (Z = {depth:0.##}). Check can snap them, but the level layout may need rework.");
            }

            if (plan.Actions.Count == 0)
                plan.Actions.Add("Nothing to do: the system objects are complete and match the template.");
            return plan;
        }

        /// <summary>True when <paramref name="instance"/> is an unbroken instance of <paramref name="prefab"/>.</summary>
        static bool Matches(GameObject instance, GameObject prefab)
        {
            if (prefab == null || PrefabUtility.GetCorrespondingObjectFromSource(instance) != prefab)
                return false;
            return instance.GetComponentsInChildren<Transform>(true).All(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0);
        }

        /// <summary>Carries out <see cref="Plan"/>. Returns the plan that was applied.</summary>
        public static RigPlan Apply(Scene scene, GameTemplateDefinition template)
        {
            var plan = Plan(scene, template);
            if (!plan.CanApply)
                return plan;

            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(template == null ? "Restore system rig" : $"Use {template.DisplayName} template");

            var context = WorkshopSceneQuery.FindContext(scene);
            if (context == null)
            {
                context = Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(WorkshopPaths.GameSetupPrefab), scene, Vector3.zero, Quaternion.identity)
                    .GetComponent<WorkshopGameContext>();
            }
            template ??= context.Template;
            if (context.Template != template)
            {
                var serialized = new SerializedObject(context);
                serialized.FindProperty("m_Template").objectReferenceValue = template;
                serialized.FindProperty("m_Movement").objectReferenceValue = null;
                serialized.ApplyModifiedProperties();
            }

            var spawn = WorkshopSceneQuery.FindActive<PlayerSpawn>(scene).FirstOrDefault();
            var start = spawn != null ? spawn.SpawnPose : new Pose(Vector3.zero, Quaternion.identity);

            // Player: keep a matching one; otherwise replace, carrying over an adopted look.
            var players = WorkshopSceneQuery.FindActive<WorkshopPlayer>(scene);
            if (players.Count != 1 || !Matches(players[0].gameObject, template.PlayerPrefab))
            {
                AdoptedAsset look = null;
                foreach (var old in players)
                {
                    var oldLook = old.transform.Find(WorkshopAssetAdoption.AdoptedLookName);
                    if (oldLook != null && look == null)
                        look = PrefabUtility.GetCorrespondingObjectFromSource(oldLook.gameObject)?.GetComponent<AdoptedAsset>();
                    Undo.DestroyObjectImmediate(old.gameObject);
                }
                var player = Instantiate(template.PlayerPrefab, scene, start.position, start.rotation).GetComponent<WorkshopPlayer>();
                if (look != null)
                    WorkshopAssetAdoption.ApplyPlayerLook(player, look);
            }

            // Camera: same rule. A side-scroller camera puts its play plane through the Start Point.
            var rigs = WorkshopSceneQuery.FindActive<WorkshopCameraRig>(scene);
            if (rigs.Count != 1 || !Matches(rigs[0].gameObject, template.CameraRigPrefab))
            {
                foreach (var old in rigs)
                    Undo.DestroyObjectImmediate(old.gameObject);
                var rig = Instantiate(template.CameraRigPrefab, scene, start.position, start.rotation).GetComponent<WorkshopCameraRig>();
                FrameNewRig(rig, start);
            }

            if (WorkshopSceneQuery.FindActive<WorkshopHUD>(scene).Count == 0)
                Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(WorkshopPaths.HudPrefab), scene, Vector3.zero, Quaternion.identity);
            if (WorkshopSceneQuery.FindActive<EventSystem>(scene).Count == 0)
            {
                var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                SceneManager.MoveGameObjectToScene(eventSystem, scene);
                Undo.RegisterCreatedObjectUndo(eventSystem, "Add Event System");
            }

            foreach (var root in scene.GetRootGameObjects())
                WorkshopPlacement.AssignContext(root, context);
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            return plan;
        }

        static GameObject Instantiate(GameObject prefab, Scene scene, Vector3 position, Quaternion rotation)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            Undo.RegisterCreatedObjectUndo(instance, $"Add {prefab.name}");
            instance.transform.SetPositionAndRotation(position, rotation);
            return instance;
        }

        static void FrameNewRig(WorkshopCameraRig rig, Pose start)
        {
            switch (rig)
            {
                case SideScrollerCameraRig side:
                    var serialized = new SerializedObject(side);
                    serialized.FindProperty("m_PlaneDepth").floatValue = start.position.z;
                    serialized.FindProperty("m_BoundsMin").vector2Value = new Vector2(start.position.x - 10f, start.position.y);
                    serialized.FindProperty("m_BoundsMax").vector2Value = new Vector2(start.position.x + 30f, start.position.y + 10f);
                    serialized.ApplyModifiedProperties();
                    side.FrameFeet(start.position);
                    break;
                case ThirdPersonCameraRig orbit:
                    orbit.PlaceBehind(start);
                    break;
                case FirstPersonCameraRig _:
                    rig.transform.position = start.position + Vector3.up * FirstPersonCameraRig.EyeHeight;
                    rig.ApplyCameraPose();
                    break;
                default:
                    rig.transform.rotation = Quaternion.identity;
                    rig.ApplyCameraPose();
                    break;
            }
        }

        // ---- Recovery milestones -------------------------------------------------------------------

        /// <summary>Recovery milestone scenes for a template, in order.</summary>
        public static string[] Milestones(GameTemplateDefinition template)
        {
            if (template == null)
                return new string[0];
            var folder = $"{WorkshopPaths.Recovery}/{template.Perspective}";
            if (!AssetDatabase.IsValidFolder(folder))
                return new string[0];
            return AssetDatabase.FindAssets("t:Scene", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p).ToArray();
        }
    }
}
