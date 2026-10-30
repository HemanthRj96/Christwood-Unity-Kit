using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityWorkshop
{
    /// <summary>
    /// Team-owned copies of shared settings. Shared presets are read-only in the student flow; a team that
    /// wants different numbers gets its own copy next to its scene, so other teams are never affected.
    /// </summary>
    public static class WorkshopTeamAssets
    {
        public static bool IsTeamScene(Scene scene) =>
            !string.IsNullOrEmpty(scene.path) && scene.path.StartsWith(WorkshopPaths.TeamGames + "/");

        /// <summary>True when <paramref name="asset"/> lives in the team folder of <paramref name="scene"/>.</summary>
        public static bool IsOwnedBy(Object asset, Scene scene)
        {
            var path = AssetDatabase.GetAssetPath(asset);
            var folder = System.IO.Path.GetDirectoryName(scene.path)?.Replace('\\', '/');
            return !string.IsNullOrEmpty(path) && IsTeamScene(scene) && path.StartsWith(folder + "/");
        }

        /// <summary>
        /// Copies <paramref name="source"/> into the team folder as "&lt;Scene&gt; Movement.asset" (unique name) and
        /// assigns it as the Game Setup's custom movement. Returns null for scenes outside TeamGames.
        /// </summary>
        public static MovementSettings CreateCustomMovement(WorkshopGameContext context, MovementSettings source)
        {
            var scene = context.gameObject.scene;
            if (!IsTeamScene(scene) || source == null)
                return null;
            var folder = System.IO.Path.GetDirectoryName(scene.path).Replace('\\', '/');
            var path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{scene.name} Movement.asset");
            var copy = Object.Instantiate(source);
            copy.name = System.IO.Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(copy, path);
            AssetDatabase.SaveAssets();

            var serialized = new SerializedObject(context);
            serialized.FindProperty("m_CustomMovement").objectReferenceValue = copy;
            serialized.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(scene);
            return copy;
        }
    }
}
