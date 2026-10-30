using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace UnityWorkshop
{
    /// <summary>Lets full restart reload team scenes in Play mode even when they are not in the build list.</summary>
    [InitializeOnLoad]
    static class WorkshopEditorHooks
    {
        static WorkshopEditorHooks()
        {
            WorkshopSceneLoader.EditorLoader = path =>
                EditorSceneManager.LoadSceneInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single));
        }
    }
}
