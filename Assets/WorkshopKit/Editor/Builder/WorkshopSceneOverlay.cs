using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityWorkshop
{
    /// <summary>
    /// Scene view panel: the game's template, where pieces snap, what the selected piece does, and the
    /// last Check result. The same actions exist in the Workshop Builder.
    /// </summary>
    [Overlay(typeof(SceneView), "unity-workshop", "Unity Workshop", true)]
    sealed class WorkshopSceneOverlay : IMGUIOverlay
    {
        static readonly Dictionary<Type, string> s_Purpose = new Dictionary<Type, string>
        {
            { typeof(WorkshopGameContext), "Game Setup: title, objective, win rule, timer and movement." },
            { typeof(WorkshopPlayer), "The player. It is placed on the Start Point when the game begins." },
            { typeof(PlayerSpawn), "Start Point: where the player begins. Keep exactly one." },
            { typeof(Collectible), "Collectible: touch to pick up. Counts toward Collect All." },
            { typeof(GoalZone), "Exit Goal: walk in to win once the rule is met." },
            { typeof(Hazard), "Hazard: sends the player back to the start or checkpoint." },
            { typeof(HazardMover), "Moves back and forth. Drag its orange end handle." },
            { typeof(Checkpoint), "Checkpoint: becomes the respawn point when touched." },
            { typeof(FallZone), "Fall Zone: invisible area that counts as falling out." },
            { typeof(KeyPickup), "Key: opens every door with the same colour and symbol." },
            { typeof(Door), "Door: opened by keys and switches of its colour and symbol." },
            { typeof(TriggerSwitch), "Pressure Switch: stand on it to open doors of its colour." },
            { typeof(WorkshopCameraRig), "Gameplay camera. Change its settings, not the camera inside it." },
            { typeof(WorkshopHUD), "On-screen interface. Its text comes from Game Setup." },
            { typeof(AdoptedAsset), "An adopted model. Its notes list what adoption changed." },
            { typeof(PieceTraits), "A level piece. Move it with W, turn it with E." },
        };

        static Scene s_CheckedScene;
        static int s_Errors = -1;
        static int s_Warnings;

        public static string Describe(GameObject selected)
        {
            if (selected == null)
                return null;
            foreach (var component in selected.GetComponents<Component>())
                foreach (var pair in s_Purpose)
                    if (component != null && pair.Key.IsInstanceOfType(component))
                        return pair.Value;
            return null;
        }

        public override void OnGUI()
        {
            var scene = SceneManager.GetActiveScene();
            var context = WorkshopSceneQuery.FindContext(scene);
            if (context == null)
            {
                GUILayout.Label("Not a workshop game.", EditorStyles.miniLabel);
                if (GUILayout.Button("Open Workshop Builder"))
                    WorkshopBuilderWindow.Open();
                return;
            }

            GUILayout.Label(context.Template != null ? context.Template.DisplayName : "No template", EditorStyles.boldLabel);
            GUILayout.Label(WorkshopSceneQuery.TryGetMovementPlane(scene, out var depth)
                ? $"Pieces snap to the play plane (Z = {depth:0.##})."
                : "Builder snaps floors to 4 m, other pieces to 1 m.", EditorStyles.miniLabel);

            var purpose = Describe(Selection.activeGameObject);
            if (purpose != null)
                GUILayout.Label(purpose, EditorStyles.wordWrappedMiniLabel, GUILayout.MaxWidth(240));

            if (s_CheckedScene != scene)
                s_Errors = -1;
            GUILayout.Label(s_Errors < 0 ? "Check: not run yet"
                : s_Errors == 0 ? $"Check: ready ({s_Warnings} warning(s))"
                : $"Check: {s_Errors} problem(s)", EditorStyles.miniBoldLabel);
            using (new GUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                    if (GUILayout.Button("Run Check"))
                    {
                        var issues = WorkshopPreflight.Run(scene);
                        s_CheckedScene = scene;
                        s_Errors = issues.Count(i => i.Severity == IssueSeverity.Error);
                        s_Warnings = issues.Count(i => i.Severity == IssueSeverity.Warning);
                    }
                if (GUILayout.Button("Builder"))
                    WorkshopBuilderWindow.Open();
            }
        }
    }
}
