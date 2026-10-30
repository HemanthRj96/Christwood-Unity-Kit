using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UnityWorkshop
{
    /// <summary>
    /// Command-line entry points for developer verification, for example:
    /// Unity.exe -batchmode -projectPath . -executeMethod UnityWorkshop.WorkshopDeveloperBatch.Generate -quit
    /// Each method exits with code 0 on success and 1 on failure when run in batch mode.
    /// </summary>
    public static class WorkshopDeveloperBatch
    {
        public const string BuildRoot = "DeveloperVerification/Builds";

        public static void Generate() => Run(() =>
        {
            WorkshopContentGenerator.GenerateAll();
            return PreflightGeneratedScenes();
        });

        public static void Preflight() => Run(PreflightGeneratedScenes);

        [MenuItem(WorkshopPaths.MenuRoot + "Developer/Build Windows Example Player", priority = 210)]
        /// <summary>Builds one Windows player per example scene, e.g. Builds/Windows/TopDown_Example/WorkshopExample.exe.</summary>
        public static void BuildWindowsExample() => Run(() =>
        {
            var ok = true;
            foreach (var scene in WorkshopContentGenerator.ExampleScenes)
            {
                var output = Path.Combine(BuildRoot, "Windows", Path.GetFileNameWithoutExtension(scene), "WorkshopExample.exe");
                var options = new BuildPlayerOptions
                {
                    scenes = new[] { scene },
                    locationPathName = output,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None,
                };
                var summary = BuildPipeline.BuildPlayer(options).summary;
                Debug.Log($"[Workshop] Build {summary.result}: {summary.outputPath}, {summary.totalSize / (1024 * 1024)} MB, " +
                          $"{summary.totalErrors} error(s), {summary.totalWarnings} warning(s), {summary.totalTime.TotalSeconds:0}s");
                ok &= summary.result == BuildResult.Succeeded;
            }
            return ok;
        });

        static bool PreflightGeneratedScenes()
        {
            var ok = true;
            var scenes = AssetDatabase.FindAssets("t:Scene", new[] { WorkshopPaths.Templates, WorkshopPaths.Examples, WorkshopPaths.Recovery })
                .Select(AssetDatabase.GUIDToAssetPath).ToArray();
            foreach (var path in scenes)
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var issues = WorkshopPreflight.Run(scene);
                // Generated scenes are not in the project's build list on purpose; that warning is expected.
                var relevant = issues.Where(i => !i.Message.Contains("not in the build list")).ToList();
                foreach (var issue in relevant)
                    Debug.Log($"[Workshop] Preflight {path}: {issue.Severity}: {issue.Message}");
                var errors = relevant.Count(i => i.Severity == IssueSeverity.Error);
                Debug.Log($"[Workshop] Preflight {path}: {errors} error(s), {relevant.Count(i => i.Severity == IssueSeverity.Warning)} warning(s)");
                ok &= errors == 0;
            }
            return ok && scenes.Length > 0;
        }

        static void Run(Func<bool> action)
        {
            bool success;
            try
            {
                success = action();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                success = false;
            }
            Debug.Log($"[Workshop] Result: {(success ? "SUCCESS" : "FAILURE")}");
            if (Application.isBatchMode)
                EditorApplication.Exit(success ? 0 : 1);
        }
    }
}
