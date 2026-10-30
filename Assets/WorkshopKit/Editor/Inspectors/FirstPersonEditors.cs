using UnityEditor;
using UnityEngine;

namespace UnityWorkshop
{
    [CustomEditor(typeof(FirstPersonCameraRig)), CanEditMultipleObjects]
    sealed class FirstPersonCameraRigEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            WorkshopEditorGUI.Header("First-Person Camera", "Sees through the player's eyes. Move the mouse to look; Esc pauses and frees the mouse. The view never flips upside down.");
            WorkshopEditorGUI.ContextLine((Component)target);

            serializedObject.Update();
            EditorGUI.BeginChangeCheck();
            WorkshopEditorGUI.Section("Look");
            WorkshopEditorGUI.Field(serializedObject, "m_Sensitivity", "Mouse Sensitivity");
            var sensitivity = serializedObject.FindProperty("m_Sensitivity");
            if (!sensitivity.hasMultipleDifferentValues)
                EditorGUILayout.LabelField(" ", $"{FirstPersonCameraRig.DegreesPerPixel((LookSensitivity)sensitivity.enumValueIndex):0.00}° per pixel of mouse movement", EditorStyles.miniLabel);
            WorkshopEditorGUI.Field(serializedObject, "m_FieldOfView", "View Width");
            var changed = EditorGUI.EndChangeCheck();
            serializedObject.ApplyModifiedProperties();
            if (changed)
                foreach (var t in targets)
                {
                    var rig = (FirstPersonCameraRig)t;
                    if (rig.Camera == null)
                        continue;
                    Undo.RecordObject(rig.Camera, "Preview view width");
                    rig.ApplyCameraPose();
                    PrefabUtility.RecordPrefabInstancePropertyModifications(rig.Camera);
                }

            EditorGUILayout.LabelField(" ", $"Looking up and down stops at ±{FirstPersonCameraRig.MaxPitch:0}°. Eye height {FirstPersonCameraRig.EyeHeight} m.", EditorStyles.miniLabel);

            WorkshopEditorGUI.Section("Preview");
            if (GUILayout.Button("Look From the Start Point"))
                LookFromStart((FirstPersonCameraRig)target);
            EditorGUILayout.HelpBox("The Game view now shows what players see when the game starts. The camera moves to the player automatically when you press Play.", MessageType.None);
            WorkshopEditorGUI.PreviewToggle();
        }

        static void LookFromStart(FirstPersonCameraRig rig)
        {
            var spawns = WorkshopSceneQuery.FindActive<PlayerSpawn>(rig.gameObject.scene);
            if (spawns.Count == 0)
                return;
            var pose = spawns[0].SpawnPose;
            Undo.RecordObject(rig.transform, "Look from start point");
            rig.transform.SetPositionAndRotation(pose.position + Vector3.up * FirstPersonCameraRig.EyeHeight, pose.rotation);
            if (rig.Camera != null)
            {
                Undo.RecordObject(rig.Camera.transform, "Look from start point");
                rig.Camera.transform.localPosition = Vector3.zero;
                rig.Camera.transform.localRotation = Quaternion.identity;
            }
        }
    }
}
