using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityWorkshop
{
    [CustomEditor(typeof(WorkshopGameContext))]
    sealed class GameSetupEditor : Editor
    {
        List<PreflightIssue> m_LastCheck;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var context = (WorkshopGameContext)target;

            WorkshopEditorGUI.Header("Game Setup", "Your game's title, objective and win rule. Every piece in the scene uses these settings automatically.");

            WorkshopEditorGUI.Section("Your Game");
            WorkshopEditorGUI.Field(serializedObject, "m_Rules.title", "Title");
            WorkshopEditorGUI.Field(serializedObject, "m_Rules.objective", "Objective Sentence");
            WorkshopEditorGUI.Field(serializedObject, "m_Rules.goalRule", "How to Win");
            WorkshopEditorGUI.Field(serializedObject, "m_Rules.collectibleName", "Collectible Name");
            ClampText("m_Rules.title", WorkshopGameRules.MaxTitleLength);
            ClampText("m_Rules.objective", WorkshopGameRules.MaxObjectiveLength);
            DrawTimerChoice();

            DrawMovementChoice(context);

            WorkshopEditorGUI.Section("Falling");
            WorkshopEditorGUI.Field(serializedObject, "m_UseFallBoundary", "Catch Falling Players");
            var useFall = serializedObject.FindProperty("m_UseFallBoundary");
            using (new EditorGUI.DisabledScope(!useFall.boolValue && !useFall.hasMultipleDifferentValues))
            {
                WorkshopEditorGUI.Field(serializedObject, "m_FallHeight", "Fall Height");
                if (GUILayout.Button(new GUIContent("Set 5 m Below the Lowest Floor", "Measures the solid objects in this scene.")))
                    SetFallHeightBelowLevel(context);
            }

            serializedObject.ApplyModifiedProperties();

            WorkshopEditorGUI.Section("What Players Will See");
            DrawPlayerPreview(context);

            WorkshopEditorGUI.Section("Status");
            if (targets.Length == 1)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Run Check"))
                        m_LastCheck = WorkshopPreflight.Run(context.gameObject.scene);
                    if (GUILayout.Button("Open Workshop Builder"))
                        WorkshopBuilderWindow.Open();
                }
                if (m_LastCheck != null)
                {
                    var errors = m_LastCheck.Count(i => i.Severity == IssueSeverity.Error);
                    var warnings = m_LastCheck.Count(i => i.Severity == IssueSeverity.Warning);
                    EditorGUILayout.HelpBox(errors == 0
                            ? $"Ready to play. {warnings} warning(s) to review in the Workshop Builder's Check tab."
                            : $"{errors} problem(s) must be fixed before the game is ready. See the Workshop Builder's Check tab.",
                        errors == 0 ? MessageType.Info : MessageType.Error);
                }
            }

            if (WorkshopEditorGUI.BeginAdvanced(nameof(GameSetupEditor)))
            {
                serializedObject.Update();
                using (new EditorGUI.DisabledScope(true))
                    WorkshopEditorGUI.Field(serializedObject, "m_Template", "Template");
                WorkshopEditorGUI.RemainingProperties(serializedObject, "m_Rules", "m_Template", "m_Movement", "m_CustomMovement", "m_UseFallBoundary", "m_FallHeight");
                serializedObject.ApplyModifiedProperties();
                WorkshopEditorGUI.EndAdvanced();
            }
        }

        void ClampText(string path, int max)
        {
            var property = serializedObject.FindProperty(path);
            if (property != null && !property.hasMultipleDifferentValues && property.stringValue != null && property.stringValue.Length > max)
                property.stringValue = property.stringValue.Substring(0, max);
        }

        static readonly GUIContent[] TimerLabels =
        {
            new GUIContent("Off"),
            new GUIContent($"Relaxed ({WorkshopHUD.FormatTime(WorkshopGameRules.RelaxedSeconds)}, {WorkshopGameRules.RelaxedSeconds:0} seconds)"),
            new GUIContent($"Challenge ({WorkshopHUD.FormatTime(WorkshopGameRules.ChallengeSeconds)}, {WorkshopGameRules.ChallengeSeconds:0} seconds)"),
        };

        void DrawTimerChoice()
        {
            var property = serializedObject.FindProperty("m_Rules.timer");
            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            var chosen = EditorGUILayout.Popup(new GUIContent("Time Limit", property.tooltip), property.enumValueIndex, TimerLabels);
            if (EditorGUI.EndChangeCheck())
                property.enumValueIndex = chosen;
            EditorGUI.showMixedValue = false;
        }

        void SetFallHeightBelowLevel(WorkshopGameContext context)
        {
            var lowest = WorkshopPreflight.LowestSolidPoint(context.gameObject.scene);
            if (!lowest.HasValue)
            {
                EditorUtility.DisplayDialog("Fall Height", "There are no solid objects in this scene yet. Build a floor first.", "OK");
                return;
            }
            serializedObject.FindProperty("m_FallHeight").floatValue = Mathf.Floor(lowest.Value - 5f);
        }

        void DrawMovementChoice(WorkshopGameContext context)
        {
            var property = serializedObject.FindProperty("m_Movement");
            var template = context.Template;
            if (template == null || template.MovementPresets == null || template.MovementPresets.Length == 0)
                return;

            var presets = template.MovementPresets.Where(p => p != null).ToArray();
            var labels = presets.Select(p => new GUIContent(p.DisplayName, p.Description)).ToArray();
            var current = System.Array.IndexOf(presets, property.objectReferenceValue as MovementSettings);
            if (current < 0)
                current = 0;

            var custom = serializedObject.FindProperty("m_CustomMovement");
            if (custom.objectReferenceValue is MovementSettings own)
            {
                DrawCustomMovement(context, custom, own);
                return;
            }

            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            var chosen = EditorGUILayout.Popup(new GUIContent("Movement Feel", "How fast and snappy the player moves. Only approved presets are listed."), current, labels);
            if (EditorGUI.EndChangeCheck())
                property.objectReferenceValue = chosen == 0 ? null : presets[chosen];
            EditorGUI.showMixedValue = false;

            using (new EditorGUI.DisabledScope(targets.Length > 1 || !WorkshopTeamAssets.IsTeamScene(context.gameObject.scene)))
                if (GUILayout.Button(new GUIContent("Customise for This Team…", "Copies the chosen feel into your team's folder so you can change the numbers. Shared presets never change.")))
                {
                    var source = context.Movement;
                    serializedObject.ApplyModifiedProperties();
                    var copy = WorkshopTeamAssets.CreateCustomMovement(context, source);
                    if (copy != null)
                        GUIUtility.ExitGUI();
                }
        }

        static void DrawCustomMovement(WorkshopGameContext context, SerializedProperty custom, MovementSettings own)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Custom Movement (this team only)", EditorStyles.boldLabel);
                var settings = new SerializedObject(own);
                settings.Update();
                foreach (var name in new[] { "m_MoveSpeed", "m_Acceleration", "m_Deceleration", "m_TurnSpeed", "m_JumpHeight", "m_Gravity", "m_AirControl" })
                {
                    var p = settings.FindProperty(name);
                    EditorGUILayout.PropertyField(p, new GUIContent(ObjectNames.NicifyVariableName(name.Substring(2)), p.tooltip));
                }
                settings.ApplyModifiedProperties();
                if (GUILayout.Button("Stop Using Custom Movement"))
                    custom.objectReferenceValue = null;
            }
        }

        static void DrawPlayerPreview(WorkshopGameContext context)
        {
            var rules = context.Rules;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(rules.Title, EditorStyles.boldLabel);
                EditorGUILayout.LabelField(rules.Objective, EditorStyles.wordWrappedLabel);
                if (context.Template != null)
                {
                    EditorGUILayout.Space(2);
                    EditorGUILayout.LabelField(context.Template.Controls.TrimEnd() + "\nEsc: Pause", EditorStyles.wordWrappedMiniLabel);
                }
                EditorGUILayout.Space(2);
                var count = WorkshopSceneQuery.FindActive<Collectible>(context.gameObject.scene).Count;
                EditorGUILayout.LabelField(rules.goalRule == GoalRule.CollectAllThenExit
                    ? $"Win: collect all {count} {rules.CollectibleLabel}(s), then enter the exit."
                    : "Win: enter the exit.", EditorStyles.miniBoldLabel);
                if (rules.TimeLimit > 0f)
                    EditorGUILayout.LabelField($"Lose: the {WorkshopHUD.FormatTime(rules.TimeLimit)} countdown ({rules.TimeLimit:0} seconds) runs out.", EditorStyles.miniBoldLabel);
            }
        }
    }
}
