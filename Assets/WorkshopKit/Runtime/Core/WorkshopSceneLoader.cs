using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityWorkshop
{
    /// <summary>
    /// Reloads a team scene from its saved file for a full restart. In the Editor, the editor assembly
    /// installs <see cref="EditorLoader"/> so scenes that are not in the build list still reload;
    /// player builds use the build index.
    /// </summary>
    public static class WorkshopSceneLoader
    {
        /// <summary>
        /// Installed by the editor assembly at load. Intentionally not cleared at runtime initialization:
        /// it belongs to the Editor session, not to a play session.
        /// </summary>
        public static Action<string> EditorLoader;

        public static bool Reload(Scene scene)
        {
            if (!scene.IsValid())
                return false;

            if (Application.isEditor && EditorLoader != null && !string.IsNullOrEmpty(scene.path))
            {
                EditorLoader(scene.path);
                return true;
            }

            if (scene.buildIndex >= 0)
            {
                SceneManager.LoadScene(scene.buildIndex, LoadSceneMode.Single);
                return true;
            }

            Debug.LogError($"Restart failed: scene '{scene.name}' is not in the build. Add it with the Workshop Builder before building.");
            return false;
        }
    }
}
