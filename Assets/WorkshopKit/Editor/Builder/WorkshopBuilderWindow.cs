using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityWorkshop
{
    /// <summary>Tools > Unity Workshop > Workshop Builder: create, build, set rules, check and get help.</summary>
    public sealed class WorkshopBuilderWindow : EditorWindow
    {
        enum Tab { Start, Build, Rules, Check, Help }

        static readonly string[] TabLabels = { "Start", "Build", "Rules", "Check", "Help" };

        Tab m_Tab;
        Vector2 m_Scroll;

        // Start
        string m_TeamId = "";
        string m_Title = "";
        string m_Objective = "";
        int m_TemplateIndex;
        List<GameTemplateDefinition> m_Templates;

        // Build
        readonly Dictionary<CatalogCategory, bool> m_Expanded = new Dictionary<CatalogCategory, bool>();

        // Check
        List<PreflightIssue> m_Issues;
        Scene m_CheckedScene;

        // Rules
        Editor m_RulesEditor;

        [MenuItem(WorkshopPaths.MenuRoot + "Workshop Builder", priority = 0)]
        public static void Open()
        {
            var window = GetWindow<WorkshopBuilderWindow>();
            window.titleContent = new GUIContent("Workshop Builder");
            window.minSize = new Vector2(360, 420);
            window.Show();
        }

        void OnEnable()
        {
            EditorSceneManager.activeSceneChangedInEditMode += OnSceneChanged;
            EditorApplication.hierarchyChanged += Repaint;
            m_Templates = null;
        }

        void OnDisable()
        {
            EditorSceneManager.activeSceneChangedInEditMode -= OnSceneChanged;
            EditorApplication.hierarchyChanged -= Repaint;
            if (m_RulesEditor != null)
                DestroyImmediate(m_RulesEditor);
        }

        void OnSceneChanged(Scene previous, Scene next)
        {
            m_Issues = null;
            Repaint();
        }

        void OnGUI()
        {
            m_Tab = (Tab)GUILayout.Toolbar((int)m_Tab, TabLabels, GUILayout.Height(26));
            EditorGUILayout.Space(4);
            DrawSceneLine();

            m_Scroll = EditorGUILayout.BeginScrollView(m_Scroll);
            switch (m_Tab)
            {
                case Tab.Start: DrawStart(); break;
                case Tab.Build: DrawBuild(); break;
                case Tab.Rules: DrawRules(); break;
                case Tab.Check: DrawCheck(); break;
                case Tab.Help: DrawHelp(); break;
            }
            EditorGUILayout.EndScrollView();
        }

        static Scene ActiveScene => SceneManager.GetActiveScene();

        static void DrawSceneLine()
        {
            var scene = ActiveScene;
            var context = WorkshopSceneQuery.FindContext(scene);
            var label = string.IsNullOrEmpty(scene.path) ? "Unsaved scene" : scene.name;
            if (context != null && context.Template != null)
                label += $"  ·  {context.Template.DisplayName}";
            if (scene.isDirty)
                label += "  ·  unsaved changes";
            EditorGUILayout.LabelField(label, EditorStyles.miniLabel);
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                EditorGUILayout.HelpBox("Play mode: changes you make now are lost when you stop. Stop Play mode to edit.", MessageType.Warning);
        }

        // ---- Start ---------------------------------------------------------------------------------

        void DrawStart()
        {
            WorkshopEditorGUI.Header("Create Your Game", "Pick a perspective and name your team. This makes a new scene for your team; nothing else is changed.");

            m_TeamId = EditorGUILayout.TextField(new GUIContent("Team Name", "Letters, numbers, - and _ only."), m_TeamId);
            m_Title = EditorGUILayout.TextField(new GUIContent("Game Title"), m_Title);
            m_Objective = EditorGUILayout.TextField(new GUIContent("Objective Sentence", "One sentence, from your Mission Board."), m_Objective);

            WorkshopEditorGUI.Section("Perspective");
            m_Templates ??= LoadTemplates();
            if (m_Templates.Count == 0)
            {
                EditorGUILayout.HelpBox("No templates found. The kit content is missing; ask a facilitator to reimport it.", MessageType.Error);
                return;
            }
            m_TemplateIndex = Mathf.Clamp(m_TemplateIndex, 0, m_Templates.Count - 1);
            for (var i = 0; i < m_Templates.Count; i++)
                DrawTemplateCard(i, m_Templates[i]);

            var template = m_Templates[m_TemplateIndex];
            var path = TeamSceneFactory.PreviewPath(m_TeamId, template);
            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox(path == null
                ? "Enter a team name to continue."
                : $"Will create: {path}\nThe scene is added to the build list so it can be built later.", path == null ? MessageType.Info : MessageType.None);

            using (new EditorGUI.DisabledScope(path == null || EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("Create Team Game", GUILayout.Height(30)))
                    CreateTeamGame(template);
            }

            WorkshopEditorGUI.Section("Open an Existing Team Game");
            var scenes = AssetDatabase.FindAssets("t:Scene", new[] { WorkshopPaths.TeamGames })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p).ToList();
            if (scenes.Count == 0)
                EditorGUILayout.LabelField("No team games yet.", EditorStyles.miniLabel);
            foreach (var scenePath in scenes)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(scenePath.Substring(WorkshopPaths.TeamGames.Length + 1), GUILayout.MinWidth(120));
                    using (new EditorGUI.DisabledScope(scenePath == ActiveScene.path || EditorApplication.isPlayingOrWillChangePlaymode))
                        if (GUILayout.Button("Open", GUILayout.Width(60)) && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                }
            }
        }

        void DrawTemplateCard(int index, GameTemplateDefinition template)
        {
            var selected = index == m_TemplateIndex;
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                if (template.Preview != null)
                    GUILayout.Label(template.Preview, GUILayout.Width(72), GUILayout.Height(72));
                using (new EditorGUILayout.VerticalScope())
                {
                    if (GUILayout.Toggle(selected, " " + template.DisplayName, EditorStyles.radioButton) && !selected)
                        m_TemplateIndex = index;
                    EditorGUILayout.LabelField(template.Description, EditorStyles.wordWrappedMiniLabel);
                    EditorGUILayout.LabelField(template.Controls.Replace("\n", "   "), EditorStyles.wordWrappedMiniLabel);
                }
            }
        }

        void CreateTeamGame(GameTemplateDefinition template)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            var path = TeamSceneFactory.Create(m_TeamId, m_Title, m_Objective, template, out var error);
            if (path == null)
            {
                EditorUtility.DisplayDialog("Workshop Builder", error, "OK");
                return;
            }
            m_Issues = null;
            m_Tab = Tab.Build;
            EditorUtility.DisplayDialog("Workshop Builder",
                $"Created {path}.\n\nIt already has a player, camera, start, exit and HUD. Press Play to try it, then add pieces from the Build tab.", "OK");
        }

        static List<GameTemplateDefinition> LoadTemplates() =>
            AssetDatabase.FindAssets("t:GameTemplateDefinition", new[] { WorkshopPaths.Templates })
                .Select(guid => AssetDatabase.LoadAssetAtPath<GameTemplateDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(t => t != null)
                .OrderBy(t => t.Perspective)
                .ToList();

        // ---- Build ---------------------------------------------------------------------------------

        void DrawBuild()
        {
            var context = RequireContext();
            if (context == null)
                return;
            var catalog = WorkshopCatalog.Load();
            if (catalog == null)
            {
                EditorGUILayout.HelpBox("The catalog is missing. Ask a facilitator to reimport the kit.", MessageType.Error);
                return;
            }

            WorkshopEditorGUI.Header("Build Your Level", "Add pieces at the centre of the Scene view, or drag a picture into the Scene view. Ctrl+Z undoes.");
            var template = context.Template;
            if (WorkshopSceneQuery.TryGetMovementPlane(ActiveScene, out var planeDepth))
                EditorGUILayout.HelpBox($"Side-scroller: pieces are placed on the play plane (Z = {planeDepth:0.##}).", MessageType.None);
            var all = catalog.AllEntries();
            var hidden = all.Count(e => e.prefab != null && !e.IsCompatible(template));
            if (hidden > 0)
                EditorGUILayout.HelpBox($"{hidden} piece(s) do not suit the {template.DisplayName} template (they need jumping, or would block the view) and are hidden.", MessageType.None);

            foreach (CatalogCategory category in System.Enum.GetValues(typeof(CatalogCategory)))
            {
                var entries = all.Where(e => e.category == category && e.prefab != null && e.IsCompatible(template)).ToList();
                if (entries.Count == 0 && category != CatalogCategory.PlayerAppearance)
                    continue;
                if (!m_Expanded.TryGetValue(category, out var expanded))
                    expanded = true;
                expanded = EditorGUILayout.BeginFoldoutHeaderGroup(expanded, $"{WorkshopCatalog.CategoryLabel(category)} ({entries.Count})");
                m_Expanded[category] = expanded;
                if (expanded)
                {
                    if (category == CatalogCategory.PlayerAppearance)
                        DrawAppearanceHeader(entries.Count);
                    foreach (var entry in entries)
                        DrawCatalogEntry(entry);
                }
                EditorGUILayout.EndFoldoutHeaderGroup();
            }
        }

        static void DrawAppearanceHeader(int looks)
        {
            var player = WorkshopSceneQuery.FindActive<WorkshopPlayer>(ActiveScene).FirstOrDefault();
            if (looks == 0)
                EditorGUILayout.HelpBox("No adopted characters yet. A facilitator can add one: select a character model, then Assets > Adopt for Workshop, Role: Player Look.", MessageType.None);
            if (player != null && WorkshopAssetAdoption.HasAdoptedLook(player))
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("The player uses an adopted look.", EditorStyles.wordWrappedLabel);
                    if (GUILayout.Button("Default Look", GUILayout.Width(100)))
                        WorkshopAssetAdoption.RestoreDefaultLook(player);
                }
        }

        void DrawCatalogEntry(CatalogEntry entry)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                var preview = AssetPreview.GetAssetPreview(entry.prefab);
                if (preview == null && AssetPreview.IsLoadingAssetPreview(entry.prefab.GetEntityId()))
                    Repaint();
                var rect = GUILayoutUtility.GetRect(56, 56, GUILayout.Width(56), GUILayout.Height(56));
                GUI.Box(rect, preview != null ? preview : AssetPreview.GetMiniThumbnail(entry.prefab));
                HandleDrag(rect, entry);

                using (new EditorGUILayout.VerticalScope())
                {
                    EditorGUILayout.LabelField(entry.displayName, EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(entry.description, EditorStyles.wordWrappedMiniLabel);
                    if (!string.IsNullOrEmpty(entry.constraints))
                        EditorGUILayout.LabelField(entry.constraints, EditorStyles.wordWrappedMiniLabel);
                }
                using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                    if (GUILayout.Button(entry.playerLook != null ? "Use" : "Add", GUILayout.Width(52), GUILayout.Height(40)))
                        AddEntry(entry, WorkshopPlacement.DefaultPlacementPoint());
            }
        }

        /// <summary>Drag data key; the Scene view drop handler places the entry through the same path as Add.</summary>
        public const string DragDataKey = "UnityWorkshop.CatalogEntry";

        static void HandleDrag(Rect rect, CatalogEntry entry)
        {
            var e = Event.current;
            if (e.type == EventType.MouseDrag && rect.Contains(e.mousePosition))
            {
                DragAndDrop.PrepareStartDrag();
                // No object references: Unity would otherwise drop a raw prefab with no grouping, snapping or wiring.
                DragAndDrop.objectReferences = new Object[0];
                DragAndDrop.SetGenericData(DragDataKey, entry);
                DragAndDrop.StartDrag(entry.displayName);
                e.Use();
            }
        }

        /// <summary>Adds a catalog entry at a point (or applies a player look). Shared by Add and drag-and-drop.</summary>
        public static GameObject AddEntry(CatalogEntry entry, Vector3 point)
        {
            var scene = ActiveScene;
            if (entry.playerLook != null)
            {
                var player = WorkshopSceneQuery.FindActive<WorkshopPlayer>(scene).FirstOrDefault();
                if (player == null)
                {
                    EditorUtility.DisplayDialog("Workshop Builder", "There is no Player to change. Run Check first.", "OK");
                    return null;
                }
                var look = WorkshopAssetAdoption.ApplyPlayerLook(player, entry.playerLook);
                Selection.activeGameObject = player.gameObject;
                return look;
            }
            if (IsSingleInstance(entry.prefab, scene, out var existing))
            {
                EditorUtility.DisplayDialog("Workshop Builder", $"The scene already has a {entry.displayName}. A game uses exactly one, so it was selected instead.", "OK");
                Selection.activeGameObject = existing;
                EditorGUIUtility.PingObject(existing);
                return null;
            }
            Undo.IncrementCurrentGroup();
            var instance = WorkshopPlacement.Place(entry.prefab, scene, point, Quaternion.identity, entry.groupName, entry.gridSize);
            Undo.SetCurrentGroupName($"Add {entry.displayName}");
            if (instance != null)
                Selection.activeGameObject = instance;
            return instance;
        }

        static bool IsSingleInstance(GameObject prefab, Scene scene, out GameObject existing)
        {
            existing = null;
            if (prefab.GetComponent<PlayerSpawn>() != null)
                existing = WorkshopSceneQuery.FindActive<PlayerSpawn>(scene).Select(s => s.gameObject).FirstOrDefault();
            return existing != null;
        }

        // ---- Rules ---------------------------------------------------------------------------------

        void DrawRules()
        {
            var context = RequireContext();
            if (context == null)
                return;
            Editor.CreateCachedEditor(context, typeof(GameSetupEditor), ref m_RulesEditor);
            m_RulesEditor.OnInspectorGUI();
        }

        // ---- Check ---------------------------------------------------------------------------------

        void DrawCheck()
        {
            WorkshopEditorGUI.Header("Check Your Game", "Finds setup problems before you play. It cannot tell whether your level is fun or fair; peer testing does that.");
            var scene = ActiveScene;
            if (GUILayout.Button("Run Check", GUILayout.Height(28)) || (m_Issues != null && m_CheckedScene != scene))
            {
                m_Issues = WorkshopPreflight.Run(scene);
                m_CheckedScene = scene;
            }

            if (m_Issues != null)
            {
                var errors = m_Issues.Count(i => i.Severity == IssueSeverity.Error);
                var warnings = m_Issues.Count(i => i.Severity == IssueSeverity.Warning);
                EditorGUILayout.HelpBox(errors == 0 ? $"Ready to play. {warnings} warning(s) to review." : $"Not ready: {errors} problem(s) to fix, {warnings} warning(s).",
                    errors == 0 ? MessageType.Info : MessageType.Error);
                foreach (var issue in m_Issues.OrderBy(i => i.Severity))
                    if (DrawIssue(issue))
                    {
                        m_Issues = WorkshopPreflight.Run(scene);
                        GUIUtility.ExitGUI();
                    }
            }

            DrawChecklist(scene);
        }

        /// <summary>Draws one issue. Returns true when a fix was applied and the list must be refreshed.</summary>
        static bool DrawIssue(PreflightIssue issue)
        {
            var type = issue.Severity == IssueSeverity.Error ? MessageType.Error
                : issue.Severity == IssueSeverity.Warning ? MessageType.Warning : MessageType.Info;
            EditorGUILayout.HelpBox(issue.Message, type);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (issue.Target != null && GUILayout.Button("Select", GUILayout.Width(60)))
                {
                    Selection.activeObject = issue.Target;
                    EditorGUIUtility.PingObject(issue.Target);
                }
                if (issue.Target is Component || issue.Target is GameObject)
                    if (GUILayout.Button("Frame", GUILayout.Width(60)))
                    {
                        Selection.activeObject = issue.Target;
                        SceneView.FrameLastActiveSceneView();
                    }
                if (issue.Fix != null && GUILayout.Button(issue.FixLabel ?? "Fix", GUILayout.Width(110)))
                {
                    Undo.IncrementCurrentGroup();
                    issue.Fix();
                    Undo.SetCurrentGroupName(issue.FixLabel ?? "Workshop fix");
                    return true;
                }
            }
            return false;
        }

        void DrawChecklist(Scene scene)
        {
            WorkshopEditorGUI.Section("Workshop Checklist");
            var context = WorkshopSceneQuery.FindContext(scene);
            var saved = !string.IsNullOrEmpty(scene.path) && !scene.isDirty;
            var spawns = WorkshopSceneQuery.FindActive<PlayerSpawn>(scene);
            var players = WorkshopSceneQuery.FindActive<WorkshopPlayer>(scene);
            var environment = WorkshopSceneQuery.FindActive<Collider>(scene)
                .Count(c => !c.isTrigger && c.GetComponentInParent<WorkshopPlayer>() == null);

            ChecklistItem("Save your scene", saved);
            ChecklistItem("Choose a template", context != null && context.Template != null);
            ChecklistItem("Build an environment", environment >= 3);
            ChecklistItem("Exactly one Start Point", spawns.Count == 1);
            ChecklistItem("Player can move", players.Count == 1 && context != null && context.Movement != null);
            ChecklistItem("Add an exit", WorkshopSceneQuery.FindActive<GoalZone>(scene).Count > 0);
            ChecklistItem("Write an objective", context != null && !string.IsNullOrWhiteSpace(context.Rules.objective));
            ChecklistItem("Add a challenge", WorkshopSceneQuery.FindActive<Hazard>(scene).Count > 0);
            TeamItem(scene, "PeerTest", "Peer test with another team");
            TeamItem(scene, "Improve", "Make one Level Up improvement");
            ChecklistItem("Final save", saved);
        }

        static void ChecklistItem(string label, bool done)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(done ? "✔" : "○", GUILayout.Width(18));
                EditorGUILayout.LabelField(label + "  (checked)", done ? EditorStyles.label : EditorStyles.boldLabel);
            }
        }

        static void TeamItem(Scene scene, string key, string label)
        {
            var prefKey = $"UnityWorkshop.Checklist.{AssetDatabase.AssetPathToGUID(scene.path)}.{key}";
            using (new EditorGUILayout.HorizontalScope())
            {
                var done = EditorPrefs.GetBool(prefKey, false);
                var next = EditorGUILayout.ToggleLeft(label + "  (team confirms)", done);
                if (next != done && !string.IsNullOrEmpty(scene.path))
                    EditorPrefs.SetBool(prefKey, next);
            }
        }

        // ---- Help ----------------------------------------------------------------------------------

        void DrawHelp()
        {
            var context = WorkshopSceneQuery.FindContext(ActiveScene);
            WorkshopEditorGUI.Header("Help", "Quick reminders for building and testing.");

            WorkshopEditorGUI.Section("Controls in Your Game");
            EditorGUILayout.LabelField(context != null && context.Template != null
                ? context.Template.Controls.TrimEnd() + "\nEsc: Pause"
                : "Create or open a team game to see its controls.", EditorStyles.wordWrappedLabel);

            WorkshopEditorGUI.Section("Moving Around the Scene View");
            EditorGUILayout.LabelField(
                "Right mouse + WASD: fly around\nMiddle mouse drag: pan\nScroll: zoom\nF: frame the selected object\nW / E: move / rotate tool",
                EditorStyles.wordWrappedLabel);

            WorkshopEditorGUI.Section("Editing");
            EditorGUILayout.LabelField(
                "Ctrl+D: duplicate\nCtrl+Z / Ctrl+Y: undo / redo\nCtrl+S: save (do this before every test)\nDelete: remove the selected object",
                EditorStyles.wordWrappedLabel);

            WorkshopEditorGUI.Section("Testing");
            EditorGUILayout.HelpBox("Save first, then press Play. Restart in the game reloads your saved scene. Anything you change during Play mode is lost when you stop.", MessageType.Info);

            if (WorkshopEditorGUI.BeginAdvanced("BuilderFacilitator"))
            {
                DrawFacilitatorTools(context);
                WorkshopEditorGUI.EndAdvanced();
            }
        }

        int m_PerspectiveIndex;
        string m_RecoveryTeam = "";

        void DrawFacilitatorTools(WorkshopGameContext context)
        {
            var scene = ActiveScene;
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                WorkshopEditorGUI.Section("Repair");
                EditorGUILayout.LabelField("Re-adds or replaces broken system objects (Game Setup, Player, Camera, HUD, Event System). The level is kept.", EditorStyles.wordWrappedMiniLabel);
                if (GUILayout.Button("Restore System Rig…"))
                    ConfirmAndApply(scene, null, "Restore System Rig");

                WorkshopEditorGUI.Section("Change Perspective");
                m_Templates ??= LoadTemplates();
                if (m_Templates.Count > 0)
                {
                    m_PerspectiveIndex = Mathf.Clamp(m_PerspectiveIndex, 0, m_Templates.Count - 1);
                    m_PerspectiveIndex = EditorGUILayout.Popup("New Template", m_PerspectiveIndex, m_Templates.Select(t => t.DisplayName).ToArray());
                    using (new EditorGUI.DisabledScope(context == null || context.Template == m_Templates[m_PerspectiveIndex]))
                        if (GUILayout.Button("Preview and Change…"))
                            ConfirmAndApply(scene, m_Templates[m_PerspectiveIndex], "Change Perspective");
                }

                WorkshopEditorGUI.Section("Recovery Milestones");
                var milestones = WorkshopRigTools.Milestones(context != null ? context.Template : null);
                if (milestones.Length == 0)
                    EditorGUILayout.LabelField("Open a team game to see its template's milestones.", EditorStyles.miniLabel);
                else
                {
                    EditorGUILayout.LabelField("Copies a finished stage of the template into a team folder as a NEW scene. The current scene is not changed.", EditorStyles.wordWrappedMiniLabel);
                    m_RecoveryTeam = EditorGUILayout.TextField("Team Name", m_RecoveryTeam);
                    foreach (var path in milestones)
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            var label = System.IO.Path.GetFileNameWithoutExtension(path);
                            EditorGUILayout.LabelField(label);
                            using (new EditorGUI.DisabledScope(TeamSceneFactory.SanitizeTeamId(m_RecoveryTeam).Length == 0))
                                if (GUILayout.Button("Copy for Team", GUILayout.Width(110)))
                                    CopyMilestone(context.Template, path, label);
                        }
                }

                WorkshopEditorGUI.Section("Third-Party Assets");
                if (GUILayout.Button("Adopt for Workshop…"))
                    WorkshopAdoptionWindow.OpenFromMenu();
            }
        }

        void ConfirmAndApply(Scene scene, GameTemplateDefinition template, string title)
        {
            var plan = WorkshopRigTools.Plan(scene, template);
            if (!plan.CanApply)
            {
                EditorUtility.DisplayDialog(title, plan.Blocker, "OK");
                return;
            }
            var message = "Will:\n• " + string.Join("\n• ", plan.Actions);
            if (plan.Warnings.Count > 0)
                message += "\n\nAfterwards:\n• " + string.Join("\n• ", plan.Warnings);
            message += "\n\nThe level is kept. You can undo this with Ctrl+Z.";
            if (!EditorUtility.DisplayDialog(title, message, "Apply", "Cancel"))
                return;
            WorkshopRigTools.Apply(scene, template);
            m_Issues = WorkshopPreflight.Run(scene);
            m_CheckedScene = scene;
            m_Tab = Tab.Check;
        }

        void CopyMilestone(GameTemplateDefinition template, string path, string label)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            var suffix = $"{template.Perspective}_{TeamSceneFactory.SanitizeTeamId(label)}";
            var created = TeamSceneFactory.Create(m_RecoveryTeam, null, null, template, out var error, path, suffix);
            EditorUtility.DisplayDialog("Recovery Milestone", created != null ? $"Created and opened {created}." : error, "OK");
        }

        // ---- Shared --------------------------------------------------------------------------------

        WorkshopGameContext RequireContext()
        {
            var context = WorkshopSceneQuery.FindContext(ActiveScene, out var count);
            if (context != null)
                return context;
            EditorGUILayout.HelpBox(count == 0
                ? "This scene is not a workshop game. Create one from the Start tab, or open your team's game."
                : $"This scene has {count} Game Setup objects. Run Check to fix it.", MessageType.Warning);
            if (GUILayout.Button(count == 0 ? "Go to Start" : "Go to Check"))
                m_Tab = count == 0 ? Tab.Start : Tab.Check;
            return null;
        }
    }
}
