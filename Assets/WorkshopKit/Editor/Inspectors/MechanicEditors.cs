using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityWorkshop
{
    /// <summary>Counts of channel pieces in one scene, shared by inspectors, gizmos and preflight.</summary>
    public readonly struct ChannelCounts
    {
        public readonly int Keys, Switches, Doors;

        public ChannelCounts(int keys, int switches, int doors)
        {
            Keys = keys;
            Switches = switches;
            Doors = doors;
        }

        public bool HasOpener => Keys + Switches > 0;

        public static ChannelCounts For(Scene scene, ChannelColor channel) => new ChannelCounts(
            WorkshopSceneQuery.FindActive<KeyPickup>(scene).Count(k => k.Channel == channel),
            WorkshopSceneQuery.FindActive<TriggerSwitch>(scene).Count(s => s.Channel == channel),
            WorkshopSceneQuery.FindActive<Door>(scene).Count(d => d.Channel == channel));
    }

    [CustomEditor(typeof(Checkpoint)), CanEditMultipleObjects]
    sealed class CheckpointEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            WorkshopEditorGUI.Header("Checkpoint", "Touch it to make it your respawn point. Hazards and falls return the player here, keeping items and time. Restart clears it.");
            WorkshopEditorGUI.ContextLine((Component)target);

            WorkshopEditorGUI.Section("Facing");
            var checkpoint = (Checkpoint)target;
            EditorGUI.showMixedValue = targets.Length > 1;
            EditorGUI.BeginChangeCheck();
            var yaw = EditorGUILayout.Slider(new GUIContent("Respawn Facing", "Which way the player faces after respawning here, in degrees."),
                Mathf.Repeat(checkpoint.transform.eulerAngles.y, 360f), 0f, 359f);
            if (EditorGUI.EndChangeCheck())
                foreach (var t in targets)
                {
                    var transform = ((Component)t).transform;
                    Undo.RecordObject(transform, "Change checkpoint facing");
                    transform.rotation = Quaternion.Euler(0f, Mathf.Round(yaw), 0f);
                }
            EditorGUI.showMixedValue = false;

            WorkshopEditorGUI.Section("Status");
            WorkshopEditorGUI.Status(WorkshopPreflight.CheckCheckpoint(checkpoint));
            WorkshopEditorGUI.PreviewToggle();
        }

        void OnSceneGUI()
        {
            var transform = ((Checkpoint)target).transform;
            var position = transform.position + Vector3.up * 0.05f;
            EditorGUI.BeginChangeCheck();
            Handles.color = new Color(0.35f, 0.9f, 0.5f);
            var rotation = Handles.Disc(Quaternion.Euler(0f, transform.eulerAngles.y, 0f), position, Vector3.up,
                HandleUtility.GetHandleSize(position) * 1.1f, false, 15f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(transform, "Rotate checkpoint facing");
                transform.rotation = Quaternion.Euler(0f, rotation.eulerAngles.y, 0f);
            }
        }
    }

    [CustomEditor(typeof(FallZone)), CanEditMultipleObjects]
    sealed class FallZoneEditor : Editor
    {
        readonly BoxBoundsHandle m_Handle = new BoxBoundsHandle();

        public override void OnInspectorGUI()
        {
            WorkshopEditorGUI.Header("Fall Zone", "An invisible area that counts as falling out of the level, for pits and gaps. The player returns to the last checkpoint or the Start Point.");
            WorkshopEditorGUI.ContextLine((Component)target);
            WorkshopEditorGUI.Section("Size");
            var boxes = targets.Select(t => (Object)((FallZone)t).Trigger).ToArray();
            var boxObject = new SerializedObject(boxes);
            boxObject.Update();
            EditorGUILayout.PropertyField(boxObject.FindProperty("m_Size"), new GUIContent("Area", "Size of the invisible area in metres. Drag the purple handles in the Scene view."));
            boxObject.ApplyModifiedProperties();
            EditorGUILayout.HelpBox("The purple box is invisible in the game. Players are caught even during the short protection after a respawn.", MessageType.Info);
            WorkshopEditorGUI.PreviewToggle();
        }

        void OnSceneGUI()
        {
            var zone = (FallZone)target;
            var box = zone.Trigger;
            using (new Handles.DrawingScope(new Color(0.7f, 0.4f, 1f), zone.transform.localToWorldMatrix))
            {
                m_Handle.center = box.center;
                m_Handle.size = box.size;
                EditorGUI.BeginChangeCheck();
                m_Handle.DrawHandle();
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(box, "Resize fall zone");
                    box.size = Vector3.Max(m_Handle.size, Vector3.one * 0.5f);
                    box.center = m_Handle.center;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(box);
                }
            }
        }
    }

    [CustomEditor(typeof(HazardMover)), CanEditMultipleObjects]
    sealed class HazardMoverEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            WorkshopEditorGUI.Header("Moving Hazard", "Slides back and forth between where it is placed and the end point. Drag the orange end handle in the Scene view to change the path.");
            serializedObject.Update();
            WorkshopEditorGUI.Field(serializedObject, "m_Speed", "Speed");
            var speed = serializedObject.FindProperty("m_Speed");
            if (!speed.hasMultipleDifferentValues)
                EditorGUILayout.LabelField(" ", $"{HazardMover.MetresPerSecond((MoveSpeed)speed.enumValueIndex):0.0} metres per second on average", EditorStyles.miniLabel);
            WorkshopEditorGUI.Field(serializedObject, "m_WaitAtEnds", "Wait at Each End");
            WorkshopEditorGUI.Field(serializedObject, "m_Travel", "Travel");
            serializedObject.ApplyModifiedProperties();

            WorkshopEditorGUI.Section("Status");
            WorkshopEditorGUI.Status(WorkshopPreflight.CheckMover((HazardMover)target));
            WorkshopEditorGUI.PreviewToggle();
        }

        void OnSceneGUI()
        {
            var mover = (HazardMover)target;
            var start = mover.transform.position;
            var end = start + mover.Travel;
            Handles.color = new Color(1f, 0.6f, 0.15f);
            EditorGUI.BeginChangeCheck();
            var moved = Handles.PositionHandle(end, Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(mover, "Move hazard end point");
                var serialized = new SerializedObject(mover);
                serialized.FindProperty("m_Travel").vector3Value = moved - start;
                serialized.ApplyModifiedProperties();
            }
            Handles.Label(end + Vector3.up * 0.8f, "End point");
        }
    }

    [CustomEditor(typeof(ChannelPiece), true), CanEditMultipleObjects]
    sealed class ChannelPieceEditor : Editor
    {
        static readonly GUIContent[] ChannelLabels = System.Enum.GetValues(typeof(ChannelColor)).Cast<ChannelColor>()
            .Select(c => new GUIContent(Channels.Label(c))).ToArray();

        public override void OnInspectorGUI()
        {
            var piece = (ChannelPiece)target;
            switch (piece)
            {
                case KeyPickup _:
                    WorkshopEditorGUI.Header("Key", "Touch it to pick it up. Every door with the same colour and symbol opens and stays open. Keys do not count toward Collect All.");
                    break;
                case TriggerSwitch _:
                    WorkshopEditorGUI.Header("Pressure Switch", "Standing on it opens every door with the same colour and symbol. Stepping off closes them, unless Latch is on. Any one pressed switch is enough.");
                    break;
                default:
                    WorkshopEditorGUI.Header("Door", "Opens for keys and switches with the same colour and symbol. It waits to close until nobody is in the doorway.");
                    break;
            }
            WorkshopEditorGUI.ContextLine(piece);

            serializedObject.Update();
            WorkshopEditorGUI.Section("Channel");
            var channel = serializedObject.FindProperty(ChannelPiece.ChannelPropertyName);
            EditorGUI.showMixedValue = channel.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            var chosen = EditorGUILayout.Popup(new GUIContent("Colour and Symbol", channel.tooltip), channel.enumValueIndex, ChannelLabels);
            if (EditorGUI.EndChangeCheck())
            {
                var group = Undo.GetCurrentGroup();
                foreach (var t in targets)
                    Undo.RecordObjects(((ChannelPiece)t).LookTargets().Where(o => o != null).ToArray(), "Change channel");
                channel.enumValueIndex = chosen;
                serializedObject.ApplyModifiedProperties();
                foreach (var t in targets)
                {
                    var p = (ChannelPiece)t;
                    p.ApplyChannelLook();
                    foreach (var look in p.LookTargets())
                        if (look != null)
                            PrefabUtility.RecordPrefabInstancePropertyModifications(look);
                }
                Undo.CollapseUndoOperations(group);
            }
            EditorGUI.showMixedValue = false;

            if (piece is TriggerSwitch)
                WorkshopEditorGUI.Field(serializedObject, TriggerSwitch.LatchPropertyName, "Latch (stay pressed)");
            serializedObject.ApplyModifiedProperties();

            WorkshopEditorGUI.Section("Status");
            if (targets.Length == 1)
                WorkshopEditorGUI.Status(WorkshopPreflight.CheckChannelPiece(piece));
            WorkshopEditorGUI.PreviewToggle();
            WorkshopChannelLinks.ShowAll = EditorGUILayout.ToggleLeft("Show links for every channel", WorkshopChannelLinks.ShowAll);
        }
    }

    /// <summary>Scene-view preference for drawing every channel's links, not just the selected piece's.</summary>
    public static class WorkshopChannelLinks
    {
        public static bool ShowAll
        {
            get => EditorPrefs.GetBool("UnityWorkshop.ShowAllChannelLinks", false);
            set
            {
                if (value == ShowAll)
                    return;
                EditorPrefs.SetBool("UnityWorkshop.ShowAllChannelLinks", value);
                SceneView.RepaintAll();
            }
        }
    }
}
