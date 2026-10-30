using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UnityWorkshop
{
    /// <summary>Shared layout for workshop inspectors: purpose, essential controls, status, preview, advanced.</summary>
    public static class WorkshopEditorGUI
    {
        static GUIStyle s_Title;
        static GUIStyle s_Purpose;

        static GUIStyle TitleStyle => s_Title ??= new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
        static GUIStyle PurposeStyle => s_Purpose ??= new GUIStyle(EditorStyles.wordWrappedLabel) { fontSize = 11 };

        /// <summary>Gizmo preview toggle shared by all workshop pieces.</summary>
        public static bool ShowPreview
        {
            get => EditorPrefs.GetBool("UnityWorkshop.ShowPreview", true);
            set
            {
                EditorPrefs.SetBool("UnityWorkshop.ShowPreview", value);
                SceneView.RepaintAll();
            }
        }

        public static void Header(string title, string purpose)
        {
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField(title, TitleStyle);
            EditorGUILayout.LabelField(purpose, PurposeStyle);
            EditorGUILayout.Space(4);
        }

        public static void Section(string label)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        }

        /// <summary>Draws a property with a plain-language label and its tooltip.</summary>
        public static void Field(SerializedObject serialized, string propertyPath, string label)
        {
            var property = serialized.FindProperty(propertyPath);
            if (property == null)
            {
                EditorGUILayout.HelpBox($"Missing setting '{propertyPath}'.", MessageType.Error);
                return;
            }
            EditorGUILayout.PropertyField(property, new GUIContent(label, property.tooltip));
        }

        public static void Status(IEnumerable<PreflightIssue> issues)
        {
            var any = false;
            foreach (var issue in issues)
            {
                any = true;
                var type = issue.Severity == IssueSeverity.Error ? MessageType.Error
                    : issue.Severity == IssueSeverity.Warning ? MessageType.Warning : MessageType.Info;
                EditorGUILayout.HelpBox(issue.Message, type);
                if (issue.Fix != null && GUILayout.Button(issue.FixLabel ?? "Fix"))
                    issue.Fix();
            }
            if (!any)
                EditorGUILayout.HelpBox("Ready.", MessageType.None);
        }

        public static void PreviewToggle()
        {
            EditorGUI.BeginChangeCheck();
            var show = EditorGUILayout.ToggleLeft("Show guides in the Scene view", ShowPreview);
            if (EditorGUI.EndChangeCheck())
                ShowPreview = show;
        }

        /// <summary>Facilitator-only section. Collapsed by default; remembered per inspector type.</summary>
        public static bool BeginAdvanced(string key)
        {
            EditorGUILayout.Space(8);
            var open = SessionState.GetBool("UnityWorkshop.Advanced." + key, false);
            var next = EditorGUILayout.Foldout(open, "Advanced (facilitator)", true);
            if (next != open)
                SessionState.SetBool("UnityWorkshop.Advanced." + key, next);
            if (next)
                EditorGUI.indentLevel++;
            return next;
        }

        public static void EndAdvanced() => EditorGUI.indentLevel--;

        /// <summary>Draws every visible property except those listed, for the advanced section.</summary>
        public static void RemainingProperties(SerializedObject serialized, params string[] skip)
        {
            var skipSet = new HashSet<string>(skip) { "m_Script" };
            var iterator = serialized.GetIterator();
            for (var enterChildren = true; iterator.NextVisible(enterChildren); enterChildren = false)
                if (!skipSet.Contains(iterator.propertyPath))
                    EditorGUILayout.PropertyField(iterator, true);
        }

        public static void ContextLine(Component component)
        {
            var context = WorkshopSceneQuery.OwnerOf(component);
            if (context == null)
                EditorGUILayout.HelpBox("This piece has no Game Setup in its scene, so it will switch itself off in Play mode. Open the Workshop Builder and run Check.", MessageType.Error);
        }
    }
}
