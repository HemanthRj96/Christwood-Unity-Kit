using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace UnityWorkshop
{
    [CustomEditor(typeof(PlayerSpawn)), CanEditMultipleObjects]
    sealed class PlayerSpawnEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            WorkshopEditorGUI.Header("Start Point", "Where the player begins and returns to after a hazard. The arrow shows which way they face. Keep exactly one.");
            WorkshopEditorGUI.ContextLine((Component)target);

            WorkshopEditorGUI.Section("Facing");
            var spawn = (PlayerSpawn)target;
            EditorGUI.showMixedValue = targets.Length > 1;
            EditorGUI.BeginChangeCheck();
            var yaw = EditorGUILayout.Slider(new GUIContent("Facing Direction", "Which way the player faces at the start, in degrees."),
                Mathf.Repeat(spawn.transform.eulerAngles.y, 360f), 0f, 359f);
            if (EditorGUI.EndChangeCheck())
                foreach (var t in targets)
                {
                    var transform = ((Component)t).transform;
                    Undo.RecordObject(transform, "Change start facing");
                    transform.rotation = Quaternion.Euler(0f, Mathf.Round(yaw), 0f);
                }
            EditorGUI.showMixedValue = false;

            WorkshopEditorGUI.Section("Status");
            WorkshopEditorGUI.Status(Check(spawn));
            WorkshopEditorGUI.PreviewToggle();
        }

        static IEnumerable<PreflightIssue> Check(PlayerSpawn spawn)
        {
            var scene = spawn.gameObject.scene;
            var count = WorkshopSceneQuery.FindActive<PlayerSpawn>(scene).Count;
            if (count > 1)
                yield return new PreflightIssue(IssueSeverity.Error, $"There are {count} Start Points. Delete all but one.");
            var context = WorkshopSceneQuery.OwnerOf(spawn);
            WorkshopSceneQuery.GetPlayerCapsule(context, out var radius, out var height);
            var problem = WorkshopPreflight.CheckStandingSpace(spawn.SpawnPose.position, radius, height, scene);
            if (problem != null)
                yield return new PreflightIssue(IssueSeverity.Error, "Start Point: " + problem);
            var tilt = spawn.transform.eulerAngles;
            if (Mathf.Abs(Mathf.DeltaAngle(tilt.x, 0f)) > 0.5f || Mathf.Abs(Mathf.DeltaAngle(tilt.z, 0f)) > 0.5f)
                yield return new PreflightIssue(IssueSeverity.Info, "The Start Point is tilted. Only its facing direction is used; the player always stands upright.");
        }

        void OnSceneGUI()
        {
            var spawn = (PlayerSpawn)target;
            var transform = spawn.transform;
            var position = transform.position + Vector3.up * 0.05f;

            EditorGUI.BeginChangeCheck();
            Handles.color = new Color(0.3f, 0.6f, 1f);
            var rotation = Handles.Disc(Quaternion.Euler(0f, transform.eulerAngles.y, 0f), position, Vector3.up,
                HandleUtility.GetHandleSize(position) * 1.1f, false, 15f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(transform, "Rotate start facing");
                transform.rotation = Quaternion.Euler(0f, rotation.eulerAngles.y, 0f);
            }
        }
    }

    [CustomEditor(typeof(Collectible)), CanEditMultipleObjects]
    sealed class CollectibleEditor : Editor
    {
        readonly SphereBoundsHandle m_RadiusHandle = new SphereBoundsHandle();

        public override void OnInspectorGUI()
        {
            WorkshopEditorGUI.Header("Collectible", "The player picks this up by touching it. Every collectible counts toward \"Collect All, Then Exit\".");
            WorkshopEditorGUI.ContextLine((Component)target);

            WorkshopEditorGUI.Section("Pickup");
            var colliders = new List<Object>();
            foreach (var t in targets)
                colliders.Add(((Collectible)t).Trigger);
            var colliderObject = new SerializedObject(colliders.ToArray());
            colliderObject.Update();
            var radius = colliderObject.FindProperty("m_Radius");
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.Slider(radius, 0.25f, 2f, new GUIContent("Pickup Size", "How close the player must get to collect it, in metres."));
            if (EditorGUI.EndChangeCheck())
                colliderObject.ApplyModifiedProperties();

            serializedObject.Update();
            WorkshopEditorGUI.Field(serializedObject, "m_SpinSpeed", "Spin Speed");
            WorkshopEditorGUI.Field(serializedObject, "m_BobHeight", "Float Height");
            serializedObject.ApplyModifiedProperties();

            WorkshopEditorGUI.Section("Status");
            var context = WorkshopSceneQuery.OwnerOf((Component)target);
            if (context != null)
            {
                var count = WorkshopSceneQuery.FindActive<Collectible>(context.gameObject.scene).Count;
                EditorGUILayout.HelpBox(context.Rules.goalRule == GoalRule.CollectAllThenExit
                    ? $"Counts toward the goal. The player must collect all {count} before the exit opens."
                    : $"Optional: the goal is Reach the Exit, so collecting is not required.", MessageType.Info);
            }
            WorkshopEditorGUI.PreviewToggle();

            if (WorkshopEditorGUI.BeginAdvanced(nameof(CollectibleEditor)))
            {
                serializedObject.Update();
                WorkshopEditorGUI.RemainingProperties(serializedObject, "m_SpinSpeed", "m_BobHeight");
                serializedObject.ApplyModifiedProperties();
                WorkshopEditorGUI.EndAdvanced();
            }
        }

        void OnSceneGUI()
        {
            var collectible = (Collectible)target;
            var sphere = collectible.Trigger;
            using (new Handles.DrawingScope(new Color(1f, 0.85f, 0.2f), Matrix4x4.TRS(collectible.transform.TransformPoint(sphere.center), collectible.transform.rotation, Vector3.one * MaxScale(collectible.transform))))
            {
                m_RadiusHandle.center = Vector3.zero;
                m_RadiusHandle.radius = sphere.radius;
                EditorGUI.BeginChangeCheck();
                m_RadiusHandle.DrawHandle();
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(sphere, "Change pickup size");
                    sphere.radius = Mathf.Clamp(m_RadiusHandle.radius, 0.25f, 2f);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(sphere);
                }
            }
        }

        static float MaxScale(Transform t)
        {
            var s = t.lossyScale;
            return Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
        }
    }

    [CustomEditor(typeof(Hazard)), CanEditMultipleObjects]
    sealed class HazardEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            WorkshopEditorGUI.Header("Hazard", "Touching this sends the player back to the Start Point. Collected items and time are kept.");
            WorkshopEditorGUI.ContextLine((Component)target);
            WorkshopEditorGUI.Section("Status");
            var hazard = (Hazard)target;
            var colliders = hazard.GetComponents<Collider>();
            var trigger = System.Array.Exists(colliders, c => c.isTrigger);
            if (colliders.Length == 0)
                EditorGUILayout.HelpBox("No collider: the player can never touch this hazard.", MessageType.Error);
            else if (!trigger)
            {
                EditorGUILayout.HelpBox("This hazard is solid, so touching it does nothing. Hazards must be triggers.", MessageType.Error);
                if (GUILayout.Button("Make Trigger"))
                    foreach (var c in colliders)
                    {
                        Undo.RecordObject(c, "Make hazard a trigger");
                        c.isTrigger = true;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(c);
                    }
            }
            else
                EditorGUILayout.HelpBox("Ready. The red outline in the Scene view is the danger area.", MessageType.None);
            WorkshopEditorGUI.PreviewToggle();
        }
    }

    [CustomEditor(typeof(GoalZone)), CanEditMultipleObjects]
    sealed class GoalZoneEditor : Editor
    {
        readonly BoxBoundsHandle m_BoundsHandle = new BoxBoundsHandle();

        public override void OnInspectorGUI()
        {
            WorkshopEditorGUI.Header("Exit Goal", "Entering this wins the game when the rule in Game Setup is met. Otherwise the player is told what is missing.");
            WorkshopEditorGUI.ContextLine((Component)target);

            WorkshopEditorGUI.Section("Size");
            var boxes = new List<Object>();
            foreach (var t in targets)
                boxes.Add(((GoalZone)t).Trigger);
            var boxObject = new SerializedObject(boxes.ToArray());
            boxObject.Update();
            EditorGUILayout.PropertyField(boxObject.FindProperty("m_Size"), new GUIContent("Entry Area", "The box the player must walk into, in metres."));
            boxObject.ApplyModifiedProperties();

            WorkshopEditorGUI.Section("Rule");
            var context = WorkshopSceneQuery.OwnerOf((Component)target);
            if (context != null)
            {
                EditorGUILayout.HelpBox(RuleSummary(context), MessageType.Info);
                if (GUILayout.Button("Change Rule in Game Setup"))
                    Selection.activeObject = context;
            }
            WorkshopEditorGUI.PreviewToggle();
        }

        public static string RuleSummary(WorkshopGameContext context)
        {
            if (context.Rules.goalRule == GoalRule.ReachExit)
                return "Rule: Reach the Exit. The player wins as soon as they enter.";
            var count = WorkshopSceneQuery.FindActive<Collectible>(context.gameObject.scene).Count;
            return $"Rule: Collect All, Then Exit. The exit opens after all {count} {context.Rules.CollectibleLabel}(s) are collected.";
        }

        void OnSceneGUI()
        {
            var goal = (GoalZone)target;
            var box = goal.Trigger;
            using (new Handles.DrawingScope(new Color(0.3f, 1f, 0.45f), goal.transform.localToWorldMatrix))
            {
                m_BoundsHandle.center = box.center;
                m_BoundsHandle.size = box.size;
                EditorGUI.BeginChangeCheck();
                m_BoundsHandle.DrawHandle();
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(box, "Resize exit");
                    var size = m_BoundsHandle.size;
                    box.size = new Vector3(Mathf.Max(0.5f, size.x), Mathf.Max(0.5f, size.y), Mathf.Max(0.5f, size.z));
                    box.center = m_BoundsHandle.center;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(box);
                }
            }
        }
    }

    [CustomEditor(typeof(TopDownCameraRig)), CanEditMultipleObjects]
    sealed class TopDownCameraRigEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            WorkshopEditorGUI.Header("Top-Down Camera", "Follows the player from above. W always moves up the screen. The Game view shows the framing before you press Play.");
            WorkshopEditorGUI.ContextLine((Component)target);

            serializedObject.Update();
            EditorGUI.BeginChangeCheck();
            WorkshopEditorGUI.Section("View");
            WorkshopEditorGUI.Field(serializedObject, "m_View", "Camera Angle");
            WorkshopEditorGUI.Field(serializedObject, "m_Distance", "Zoom Out");
            WorkshopEditorGUI.Field(serializedObject, "m_Direction", "Look Direction");
            WorkshopEditorGUI.Field(serializedObject, "m_FollowDelay", "Follow Delay");
            var changed = EditorGUI.EndChangeCheck();
            serializedObject.ApplyModifiedProperties();

            if (changed)
                ApplyPreview();

            WorkshopEditorGUI.Section("Preview");
            if (GUILayout.Button("Frame the Start Point"))
                FrameStart();
            WorkshopEditorGUI.PreviewToggle();
        }

        void ApplyPreview()
        {
            foreach (var t in targets)
            {
                var rig = (TopDownCameraRig)t;
                if (rig.Camera == null)
                    continue;
                Undo.RecordObject(rig.Camera.transform, "Preview camera");
                rig.ApplyCameraPose();
                PrefabUtility.RecordPrefabInstancePropertyModifications(rig.Camera.transform);
            }
        }

        void FrameStart()
        {
            var rig = (TopDownCameraRig)target;
            var spawns = WorkshopSceneQuery.FindActive<PlayerSpawn>(rig.gameObject.scene);
            if (spawns.Count == 0)
                return;
            Undo.RecordObject(rig.transform, "Frame start point");
            rig.transform.SetPositionAndRotation(spawns[0].transform.position, Quaternion.identity);
            ApplyPreview();
        }
    }

    [CustomEditor(typeof(WorkshopPlayer))]
    sealed class WorkshopPlayerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            WorkshopEditorGUI.Header("Player", "The character the player controls. It is set up by the template; choose its movement feel in Game Setup.");
            WorkshopEditorGUI.ContextLine((Component)target);
            var context = WorkshopSceneQuery.OwnerOf((Component)target);
            if (context != null && GUILayout.Button("Open Game Setup"))
                Selection.activeObject = context;
            EditorGUILayout.HelpBox("The player is placed on the Start Point when the game begins, so you do not need to move it.", MessageType.Info);

            if (WorkshopEditorGUI.BeginAdvanced(nameof(WorkshopPlayerEditor)))
            {
                serializedObject.Update();
                WorkshopEditorGUI.RemainingProperties(serializedObject);
                serializedObject.ApplyModifiedProperties();
                WorkshopEditorGUI.EndAdvanced();
            }
        }
    }

    [CustomEditor(typeof(WorkshopHUD))]
    sealed class WorkshopHUDEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            WorkshopEditorGUI.Header("Workshop HUD", "Shows your title, objective, controls, progress, pause and results. The text comes from Game Setup.");
            var context = WorkshopSceneQuery.OwnerOf((Component)target);
            if (context != null && GUILayout.Button("Edit Text in Game Setup"))
                Selection.activeObject = context;

            if (WorkshopEditorGUI.BeginAdvanced(nameof(WorkshopHUDEditor)))
            {
                serializedObject.Update();
                WorkshopEditorGUI.RemainingProperties(serializedObject);
                serializedObject.ApplyModifiedProperties();
                WorkshopEditorGUI.EndAdvanced();
            }
        }
    }
}
