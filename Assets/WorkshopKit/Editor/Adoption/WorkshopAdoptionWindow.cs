using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityWorkshop
{
    /// <summary>
    /// Assets > Adopt for Workshop. Shows what was detected and what will be created before anything is
    /// written, then reports exactly what changed and what is not supported.
    /// </summary>
    public sealed class WorkshopAdoptionWindow : EditorWindow
    {
        Object m_Target;
        AdoptionAnalysis m_Analysis;
        readonly AdoptionRequest m_Request = new AdoptionRequest();
        AdoptionResult m_Result;
        Vector2 m_Scroll;

        [MenuItem("Assets/Adopt for Workshop", priority = 1000)]
        static void AdoptSelected() => Open(Selection.activeObject);

        [MenuItem("Assets/Adopt for Workshop", true)]
        static bool CanAdoptSelected() => Selection.activeObject != null && !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(Selection.activeObject));

        [MenuItem(WorkshopPaths.MenuRoot + "Adopt for Workshop", priority = 20)]
        public static void OpenFromMenu() => Open(Selection.activeObject);

        public static void Open(Object target)
        {
            var window = GetWindow<WorkshopAdoptionWindow>(true, "Adopt for Workshop");
            window.minSize = new Vector2(440, 520);
            window.SetTarget(target);
            window.Show();
        }

        void SetTarget(Object target)
        {
            m_Target = target;
            m_Result = null;
            m_Analysis = target != null ? WorkshopAssetAdoption.Analyze(target) : null;
            m_Request.Source = target as GameObject;
            m_Request.DisplayName = target != null ? target.name : "";
            m_Request.Role = m_Analysis != null && m_Analysis.IsHumanoid ? AdoptedRole.PlayerLook : AdoptedRole.Prop;
        }

        void OnGUI()
        {
            m_Scroll = EditorGUILayout.BeginScrollView(m_Scroll);
            WorkshopEditorGUI.Header("Adopt for Workshop", "Makes a workshop copy that points at the original model. The original files are never changed, so the vendor package stays intact.");

            EditorGUI.BeginChangeCheck();
            var target = EditorGUILayout.ObjectField("Model or Prefab", m_Target, typeof(Object), false);
            if (EditorGUI.EndChangeCheck())
                SetTarget(target);

            if (m_Analysis == null)
            {
                EditorGUILayout.HelpBox("Choose a model or prefab from the Project window.", MessageType.Info);
                EditorGUILayout.EndScrollView();
                return;
            }
            if (!m_Analysis.Supported)
            {
                EditorGUILayout.HelpBox(m_Analysis.Problem, MessageType.Warning);
                EditorGUILayout.EndScrollView();
                return;
            }

            DrawDetection();
            DrawOptions();
            DrawPlan();

            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                if (GUILayout.Button("Adopt", GUILayout.Height(30)))
                {
                    m_Result = WorkshopAssetAdoption.Adopt(m_Request);
                    if (m_Result.Success)
                        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(m_Result.PrefabPath);
                }
            DrawResult();
            EditorGUILayout.EndScrollView();
        }

        void DrawDetection()
        {
            WorkshopEditorGUI.Section("Detected");
            var size = m_Analysis.Bounds.size;
            EditorGUILayout.LabelField($"{m_Analysis.Kind} · {m_Analysis.RendererCount} visible part(s) · {size.x:0.##} × {size.y:0.##} × {size.z:0.##} m", EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField(m_Analysis.IsHumanoid ? "Humanoid rig: can animate as a player look." :
                m_Analysis.HasAnimator ? "Generic rig: shown as a still model." : "No rig: a still model.", EditorStyles.wordWrappedMiniLabel);
            if (m_Analysis.Conflicts.Count > 0)
                EditorGUILayout.HelpBox("Will be turned off in the workshop copy:\n• " + string.Join("\n• ", m_Analysis.Conflicts), MessageType.Info);
            foreach (var warning in m_Analysis.Warnings)
                EditorGUILayout.HelpBox(warning, MessageType.Warning);
            if (m_Analysis.UnsupportedMaterials.Count > 0)
                EditorGUILayout.HelpBox("No automatic conversion for: " + string.Join(", ", m_Analysis.UnsupportedMaterials.Select(m => $"{m.name} ({m.shader.name})")) +
                                        ". They are kept as they are and may look wrong in URP.", MessageType.Warning);
        }

        void DrawOptions()
        {
            WorkshopEditorGUI.Section("Use It As");
            m_Request.Role = (AdoptedRole)EditorGUILayout.EnumPopup(new GUIContent("Role", "Prop: placed in levels from the Build tab. Player Look: replaces how the player looks."), m_Request.Role);
            m_Request.DisplayName = EditorGUILayout.TextField("Name", m_Request.DisplayName);

            WorkshopEditorGUI.Section("Placement");
            m_Request.Size = (AdoptionSize)EditorGUILayout.EnumPopup("Size", m_Request.Size);
            m_Request.FacingTurn = EditorGUILayout.Slider(new GUIContent("Turn to Face Forward", "Degrees, if the model faces sideways or backwards."), m_Request.FacingTurn, -180f, 180f);
            m_Request.AlignToFloor = EditorGUILayout.Toggle("Stand on the Ground", m_Request.AlignToFloor);
            m_Request.CenterOnPivot = EditorGUILayout.Toggle("Centre on the Pivot", m_Request.CenterOnPivot);
            using (new EditorGUI.DisabledScope(m_Request.Role == AdoptedRole.PlayerLook))
                m_Request.Colliders = (ColliderPolicy)EditorGUILayout.EnumPopup(new GUIContent("Collision", "Player looks never collide; the player's capsule does."), m_Request.Colliders);

            if (m_Request.Role == AdoptedRole.PlayerLook && m_Analysis.IsHumanoid)
            {
                WorkshopEditorGUI.Section("Animation (optional)");
                m_Request.IdleClip = (AnimationClip)EditorGUILayout.ObjectField("Idle Clip", m_Request.IdleClip, typeof(AnimationClip), false);
                m_Request.RunClip = (AnimationClip)EditorGUILayout.ObjectField("Run Clip", m_Request.RunClip, typeof(AnimationClip), false);
                var problem = m_Request.IdleClip != null || m_Request.RunClip != null
                    ? WorkshopAssetAdoption.ValidateHumanoidClip(m_Request.IdleClip) ?? WorkshopAssetAdoption.ValidateHumanoidClip(m_Request.RunClip)
                    : "Without both clips the look is a still model.";
                EditorGUILayout.HelpBox(problem ?? "Will blend Idle and Run by the player's speed.", problem == null ? MessageType.Info : MessageType.None);
            }
        }

        void DrawPlan()
        {
            WorkshopEditorGUI.Section("Will Create");
            var name = WorkshopAssetAdoption.SafeName(string.IsNullOrWhiteSpace(m_Request.DisplayName) ? m_Target.name : m_Request.DisplayName);
            var folder = AssetDatabase.GenerateUniqueAssetPath($"{WorkshopPaths.AdoptedAssets}/{name}");
            var lines = $"{folder}/{Path.GetFileName(folder)}.prefab (points at the original)";
            if (m_Analysis.ConvertibleMaterials.Count > 0)
                lines += $"\n{m_Analysis.ConvertibleMaterials.Count} URP material copy(ies) in {folder}/{WorkshopAssetAdoption.MaterialsFolder}";
            if (m_Request.Role == AdoptedRole.PlayerLook && m_Analysis.IsHumanoid && m_Request.IdleClip != null && m_Request.RunClip != null)
                lines += $"\nAn animation controller in {folder}";
            EditorGUILayout.HelpBox(lines + "\nNothing outside this new folder is changed.", MessageType.None);
        }

        void DrawResult()
        {
            if (m_Result == null)
                return;
            if (!m_Result.Success)
            {
                EditorGUILayout.HelpBox(m_Result.Error, MessageType.Error);
                return;
            }
            EditorGUILayout.HelpBox($"Adopted: {m_Result.PrefabPath}\n" + (m_Request.Role == AdoptedRole.Prop
                ? "It is now in the Workshop Builder's Build tab under Decoration."
                : "Use it from the Build tab under Player Appearance."), MessageType.Info);
            if (m_Result.Changes.Count > 0)
                EditorGUILayout.HelpBox("Changed in the workshop copy:\n• " + string.Join("\n• ", m_Result.Changes), MessageType.None);
            if (m_Result.Limitations.Count > 0)
                EditorGUILayout.HelpBox("Limitations:\n• " + string.Join("\n• ", m_Result.Limitations), MessageType.Warning);
        }
    }
}
