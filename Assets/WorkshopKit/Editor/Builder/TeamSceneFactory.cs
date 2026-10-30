using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UnityWorkshop
{
    /// <summary>
    /// Creates a team scene by copying a template's starter scene into TeamGames under a new, unique
    /// name. Never overwrites an existing scene. On failure only the files this call created are removed.
    /// </summary>
    public static class TeamSceneFactory
    {
        public const int MaxTeamIdLength = 24;

        public static string SanitizeTeamId(string teamId)
        {
            var builder = new StringBuilder();
            foreach (var c in (teamId ?? "").Trim())
            {
                if (char.IsLetterOrDigit(c) || c == '-' || c == '_')
                    builder.Append(c);
                else if (c == ' ' && builder.Length > 0 && builder[builder.Length - 1] != '_')
                    builder.Append('_');
                if (builder.Length >= MaxTeamIdLength)
                    break;
            }
            return builder.ToString().Trim('_');
        }

        /// <summary>The path a new scene would get, without creating anything.</summary>
        public static string PreviewPath(string teamId, GameTemplateDefinition template)
        {
            var team = SanitizeTeamId(teamId);
            if (team.Length == 0 || template == null)
                return null;
            var folder = $"{WorkshopPaths.TeamGames}/{team}";
            var name = $"{team}_{template.Perspective}.unity";
            var path = $"{folder}/{name}";
            return AssetDatabase.IsValidFolder(folder) ? AssetDatabase.GenerateUniqueAssetPath(path) : path;
        }

        /// <summary>
        /// Copies the starter scene (or <paramref name="sourceScenePath"/>, e.g. a recovery milestone), applies the
        /// team's title and objective when given, saves it, adds it to the
        /// build list and opens it. Returns the new scene path, or null with <paramref name="error"/> set.
        /// The caller is responsible for asking the user to save the currently open scene first.
        /// </summary>
        public static string Create(string teamId, string title, string objective, GameTemplateDefinition template, out string error,
            string sourceScenePath = null, string nameSuffix = null)
        {
            error = null;
            var team = SanitizeTeamId(teamId);
            if (team.Length == 0)
            {
                error = "Enter a team name using letters or numbers.";
                return null;
            }
            if (template == null)
            {
                error = "Choose a template.";
                return null;
            }
            var starterPath = sourceScenePath ?? AssetDatabase.GUIDToAssetPath(template.StarterSceneGuid);
            if (string.IsNullOrEmpty(starterPath) || AssetDatabase.LoadAssetAtPath<SceneAsset>(starterPath) == null)
            {
                error = sourceScenePath != null
                    ? $"The scene '{sourceScenePath}' is missing."
                    : $"The {template.DisplayName} template's starter scene is missing. Ask a facilitator to reimport the kit.";
                return null;
            }

            var createdFolder = false;
            string scenePath = null;
            try
            {
                var folder = $"{WorkshopPaths.TeamGames}/{team}";
                if (!AssetDatabase.IsValidFolder(WorkshopPaths.TeamGames))
                {
                    error = $"The folder '{WorkshopPaths.TeamGames}' is missing. Ask a facilitator to reimport the kit.";
                    return null;
                }
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    AssetDatabase.CreateFolder(WorkshopPaths.TeamGames, team);
                    createdFolder = true;
                }

                scenePath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{team}_{nameSuffix ?? template.Perspective.ToString()}.unity");
                if (!AssetDatabase.CopyAsset(starterPath, scenePath))
                {
                    error = "Unity could not copy the starter scene.";
                    scenePath = null;
                    return Cleanup(null, createdFolder, folder);
                }

                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                var context = WorkshopSceneQuery.FindContext(scene, out var count);
                if (context == null)
                {
                    error = $"The starter scene has {count} Game Setup objects instead of one.";
                    return Cleanup(scenePath, createdFolder, folder);
                }

                var serialized = new SerializedObject(context);
                if (!string.IsNullOrWhiteSpace(title))
                    serialized.FindProperty("m_Rules.title").stringValue = Clip(title.Trim(), WorkshopGameRules.MaxTitleLength);
                if (!string.IsNullOrWhiteSpace(objective))
                    serialized.FindProperty("m_Rules.objective").stringValue = Clip(objective.Trim(), WorkshopGameRules.MaxObjectiveLength);
                serialized.FindProperty("m_Template").objectReferenceValue = template;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                if (!EditorSceneManager.SaveScene(scene))
                {
                    error = "Unity could not save the new scene.";
                    return Cleanup(scenePath, createdFolder, folder);
                }

                WorkshopPreflight.AddToBuildList(scenePath);
                return scenePath;
            }
            catch (System.Exception exception)
            {
                error = "Creating the scene failed: " + exception.Message;
                Debug.LogException(exception);
                return Cleanup(scenePath, createdFolder, $"{WorkshopPaths.TeamGames}/{team}");
            }
        }

        static string Cleanup(string createdScene, bool createdFolder, string folder)
        {
            if (!string.IsNullOrEmpty(createdScene))
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(createdScene);
            }
            if (createdFolder && AssetDatabase.IsValidFolder(folder) &&
                Directory.GetFileSystemEntries(folder).Length == 0)
                AssetDatabase.DeleteAsset(folder);
            return null;
        }

        static string Clip(string value, int max) => value.Length > max ? value.Substring(0, max) : value;
    }
}
