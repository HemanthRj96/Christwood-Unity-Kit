using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace UnityWorkshop
{
    [CustomEditor(typeof(ThirdPersonCameraRig)), CanEditMultipleObjects]
    sealed class ThirdPersonCameraRigEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            WorkshopEditorGUI.Header("Third-Person Camera", "Follows behind the player. Move the mouse to orbit; W walks away from the camera. Walls push the camera in so the player always stays visible.");
            WorkshopEditorGUI.ContextLine((Component)target);

            serializedObject.Update();
            EditorGUI.BeginChangeCheck();
            WorkshopEditorGUI.Section("Camera");
            WorkshopEditorGUI.Field(serializedObject, "m_Distance", "Distance");
            var distance = serializedObject.FindProperty("m_Distance");
            if (!distance.hasMultipleDifferentValues)
                EditorGUILayout.LabelField(" ", $"{ThirdPersonCameraRig.MetresFor((CameraDistance)distance.enumValueIndex):0.#} m behind the player when nothing is in the way", EditorStyles.miniLabel);
            WorkshopEditorGUI.Field(serializedObject, "m_Sensitivity", "Mouse Sensitivity");
            WorkshopEditorGUI.Field(serializedObject, "m_FieldOfView", "View Width");
            var changed = EditorGUI.EndChangeCheck();
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.LabelField(" ", $"Orbit tilt is limited to {ThirdPersonCameraRig.MinPitch:0}° … {ThirdPersonCameraRig.MaxPitch:0}°.", EditorStyles.miniLabel);

            WorkshopEditorGUI.Section("Preview");
            if (GUILayout.Button("Look From Behind the Start Point") || changed)
                foreach (var t in targets)
                    PreviewFromStart((ThirdPersonCameraRig)t);
            EditorGUILayout.HelpBox("The Game view shows the opening view, already pulled in if a wall is behind the start. The camera moves to the player automatically in Play mode.", MessageType.None);
            WorkshopEditorGUI.PreviewToggle();
        }

        static void PreviewFromStart(ThirdPersonCameraRig rig)
        {
            var spawns = WorkshopSceneQuery.FindActive<PlayerSpawn>(rig.gameObject.scene);
            if (spawns.Count == 0 || rig.Camera == null)
                return;
            Undo.RecordObjects(new Object[] { rig.transform, rig.Camera.transform, rig.Camera }, "Preview third-person camera");
            rig.PlaceBehind(spawns[0].SpawnPose);
            PrefabUtility.RecordPrefabInstancePropertyModifications(rig.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(rig.Camera.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(rig.Camera);
        }
    }

    [CustomEditor(typeof(PlayerVisualAdapter))]
    sealed class PlayerVisualAdapterEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            WorkshopEditorGUI.Header("Player Visual", "Places the character model on the player and, if it has an Animator, tells it how fast the player moves. The model never moves the player itself.");
            var adapter = (PlayerVisualAdapter)target;

            serializedObject.Update();
            EditorGUI.BeginChangeCheck();
            WorkshopEditorGUI.Section("Model Placement");
            WorkshopEditorGUI.Field(serializedObject, "m_VisualOffset", "Offset");
            WorkshopEditorGUI.Field(serializedObject, "m_VisualTurn", "Turn");
            WorkshopEditorGUI.Field(serializedObject, "m_VisualScale", "Size");
            var placementChanged = EditorGUI.EndChangeCheck();
            serializedObject.ApplyModifiedProperties();
            if (placementChanged && adapter.Visual != null)
            {
                Undo.RecordObject(adapter.Visual, "Place player model");
                adapter.ApplyPlacement();
                PrefabUtility.RecordPrefabInstancePropertyModifications(adapter.Visual);
            }

            WorkshopEditorGUI.Section("Animation (optional)");
            serializedObject.Update();
            WorkshopEditorGUI.Field(serializedObject, "m_Animator", "Animator");
            serializedObject.ApplyModifiedProperties();
            DrawAnimatorStatus(adapter.Animator);

            if (WorkshopEditorGUI.BeginAdvanced(nameof(PlayerVisualAdapterEditor)))
            {
                serializedObject.Update();
                WorkshopEditorGUI.Field(serializedObject, "m_Visual", "Model Root");
                serializedObject.ApplyModifiedProperties();
                WorkshopEditorGUI.EndAdvanced();
            }
        }

        static void DrawAnimatorStatus(Animator animator)
        {
            if (animator == null)
            {
                EditorGUILayout.HelpBox("No Animator: the model is shown without animation. That is fine.", MessageType.None);
                return;
            }
            if (animator.applyRootMotion)
                EditorGUILayout.HelpBox("Root motion is on. It is switched off when the game starts, because the workshop motor moves the player.", MessageType.Info);
            if (!(animator.runtimeAnimatorController is AnimatorController controller))
            {
                EditorGUILayout.HelpBox("The Animator has no controller, so nothing will animate.", MessageType.Warning);
                return;
            }
            var names = controller.parameters.Select(p => p.name).ToArray();
            var used = new[] { "Speed", "Grounded", "VerticalSpeed" }.Where(n => names.Contains(n)).ToArray();
            EditorGUILayout.HelpBox(used.Length == 0
                    ? "The controller has none of the parameters Speed (Float), Grounded (Bool) or VerticalSpeed (Float), so it will not react to movement."
                    : $"Will drive: {string.Join(", ", used)}.",
                used.Length == 0 ? MessageType.Warning : MessageType.Info);
        }
    }
}
