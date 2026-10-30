using UnityEditor;
using UnityEngine;

namespace UnityWorkshop
{
    [CustomEditor(typeof(SideScrollerCameraRig)), CanEditMultipleObjects]
    sealed class SideScrollerCameraRigEditor : Editor
    {
        static readonly Color BoundsColor = new Color(1f, 0.75f, 0.2f);

        public override void OnInspectorGUI()
        {
            WorkshopEditorGUI.Header("Side-Scroller Camera", "Watches the level from the side. Players move left and right on the play plane. The camera only follows up and down when the player leaves the calm zone, and never shows outside its bounds.");
            WorkshopEditorGUI.ContextLine((Component)target);

            serializedObject.Update();
            EditorGUI.BeginChangeCheck();
            WorkshopEditorGUI.Section("View");
            WorkshopEditorGUI.Field(serializedObject, "m_Distance", "Distance");
            var distance = serializedObject.FindProperty("m_Distance");
            if (!distance.hasMultipleDifferentValues)
                EditorGUILayout.LabelField(" ", $"{SideScrollerCameraRig.MetresFor((CameraDistance)distance.enumValueIndex):0} m from the play plane", EditorStyles.miniLabel);
            WorkshopEditorGUI.Field(serializedObject, "m_FollowDelay", "Follow Delay");
            WorkshopEditorGUI.Field(serializedObject, "m_VerticalCalmZone", "Up/Down Calm Zone");

            WorkshopEditorGUI.Section("Camera Bounds");
            WorkshopEditorGUI.Field(serializedObject, "m_UseBounds", "Keep Inside Bounds");
            using (new EditorGUI.DisabledScope(!serializedObject.FindProperty("m_UseBounds").boolValue))
            {
                WorkshopEditorGUI.Field(serializedObject, "m_BoundsMin", "Bottom Left");
                WorkshopEditorGUI.Field(serializedObject, "m_BoundsMax", "Top Right");
            }
            EditorGUILayout.HelpBox("Drag the orange corner handles in the Scene view. The camera's aim point stays inside the rectangle.", MessageType.None);

            if (WorkshopEditorGUI.BeginAdvanced(nameof(SideScrollerCameraRigEditor)))
            {
                WorkshopEditorGUI.Field(serializedObject, "m_PlaneDepth", "Play Plane Depth (Z)");
                EditorGUILayout.HelpBox("Changing the plane depth does not move existing pieces. Run Check and use Snap to Plane afterwards.", MessageType.Warning);
                WorkshopEditorGUI.EndAdvanced();
            }
            var changed = EditorGUI.EndChangeCheck();
            serializedObject.ApplyModifiedProperties();

            WorkshopEditorGUI.Section("Preview");
            if (GUILayout.Button("Frame the Start Point") || changed)
                foreach (var t in targets)
                    FrameStart((SideScrollerCameraRig)t);
            WorkshopEditorGUI.PreviewToggle();
        }

        static void FrameStart(SideScrollerCameraRig rig)
        {
            var spawns = WorkshopSceneQuery.FindActive<PlayerSpawn>(rig.gameObject.scene);
            if (spawns.Count == 0 || rig.Camera == null)
                return;
            Undo.RecordObjects(new Object[] { rig.transform, rig.Camera.transform }, "Frame start point");
            rig.FrameFeet(spawns[0].SpawnPose.position);
            PrefabUtility.RecordPrefabInstancePropertyModifications(rig.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(rig.Camera.transform);
        }

        void OnSceneGUI()
        {
            var rig = (SideScrollerCameraRig)target;
            if (!rig.UsesBounds)
                return;
            var z = rig.PlaneDepth;
            var min = rig.BoundsMin;
            var max = rig.BoundsMax;
            Handles.color = BoundsColor;
            EditorGUI.BeginChangeCheck();
            var newMin = CornerHandle(new Vector3(min.x, min.y, z));
            var newMax = CornerHandle(new Vector3(max.x, max.y, z));
            if (EditorGUI.EndChangeCheck())
            {
                var serialized = new SerializedObject(rig);
                serialized.FindProperty("m_BoundsMin").vector2Value = new Vector2(Mathf.Round(newMin.x * 2f) / 2f, Mathf.Round(newMin.y * 2f) / 2f);
                serialized.FindProperty("m_BoundsMax").vector2Value = new Vector2(Mathf.Round(newMax.x * 2f) / 2f, Mathf.Round(newMax.y * 2f) / 2f);
                serialized.ApplyModifiedProperties();
            }
            Handles.Label(new Vector3(min.x, min.y, z) + Vector3.down * 0.5f, "Camera bounds: bottom left");
            Handles.Label(new Vector3(max.x, max.y, z) + Vector3.up * 0.5f, "top right");
        }

        static Vector3 CornerHandle(Vector3 position)
        {
            var size = HandleUtility.GetHandleSize(position) * 0.12f;
            // Slide only within the play plane (X and Y).
            return Handles.Slider2D(position, Vector3.back, Vector3.right, Vector3.up, size, Handles.DotHandleCap, Vector2.zero);
        }
    }
}
