using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CrimeSceneVR.Editor
{
    public static class QuestPlayerBuilder
    {
        private const string ResultFile = "build_result.txt";

        [MenuItem("Forensic/Fix PostProcessingVolume Missing Scripts")]
        public static void FixPostProcessingVolume()
        {
            var sb = new StringBuilder();
            sb.AppendLine("FixPostProcessingVolume started.");
            string prefabPath = "Assets/FreeOpenBuilding_1.1.2/PostProcessing/PostProcessingVolume.prefab";
            if (File.Exists(prefabPath))
            {
                using (var scope = new PrefabUtility.EditPrefabContentsScope(prefabPath))
                {
                    var root = scope.prefabContentsRoot;
                    int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(root);
                    sb.AppendLine($"Removed {removed} missing scripts from {prefabPath}");
                }
            }

            var sceneGo = GameObject.Find("PostProcessingVolume");
            if (sceneGo != null)
            {
                int removedScene = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(sceneGo);
                sb.AppendLine($"Removed {removedScene} missing scripts from scene {sceneGo.name}");
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(sceneGo.scene);
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(sceneGo.scene);
            }
            Debug.Log(sb.ToString());
        }

        [MenuItem("Forensic/Build Quest Android Player")]
        public static void RunBuildFromMenu()
        {
            ExecuteBuild();
        }

        public static void ExecuteBuild()
        {
            var sb = new StringBuilder();
            sb.AppendLine("================================================================================");
            sb.AppendLine($"QUEST ANDROID BUILD REPORT - {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine("Unity Version: " + Application.unityVersion);
            sb.AppendLine("Target Platform: Android / Meta Quest");
            sb.AppendLine("================================================================================\n");

            string buildDir = Path.Combine(Directory.GetCurrentDirectory(), "Builds", "Android");
            if (!Directory.Exists(buildDir))
            {
                Directory.CreateDirectory(buildDir);
            }
            string apkPath = Path.Combine(buildDir, "CrimeScene.apk");

            string[] scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled && !string.IsNullOrEmpty(s.path))
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                scenes = new string[] { "Assets/Scenes/CrimeScene.unity" };
            }

            sb.AppendLine($"Build Scenes: {string.Join(", ", scenes)}");
            sb.AppendLine($"Output APK Path: {apkPath}\n");

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                sb.AppendLine($"Switching active build target from {EditorUserBuildSettings.activeBuildTarget} to Android...");
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            }

            var buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = apkPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None
            };

            Debug.Log($"[QuestPlayerBuilder] Starting Android build to {apkPath}...");
            sb.AppendLine("Starting BuildPipeline.BuildPlayer...");

            var startTime = DateTime.UtcNow;
            BuildReport report = null;
            try
            {
                report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            }
            catch (Exception ex)
            {
                sb.AppendLine($"EXCEPTION during BuildPlayer: {ex.Message}\n{ex.StackTrace}");
                Debug.LogError($"[QuestPlayerBuilder] Exception during build: {ex}");
            }

            var duration = DateTime.UtcNow - startTime;

            if (report != null)
            {
                sb.AppendLine("\n--------------------------------------------------------------------------------");
                sb.AppendLine($"BUILD RESULT: {report.summary.result}");
                sb.AppendLine($"Total Duration: {duration.TotalSeconds:F1}s");
                sb.AppendLine($"Total Errors: {report.summary.totalErrors}");
                sb.AppendLine($"Total Warnings: {report.summary.totalWarnings}");
                sb.AppendLine($"Total Size: {report.summary.totalSize} bytes");
                sb.AppendLine($"Output Path: {report.summary.outputPath}");
                sb.AppendLine("--------------------------------------------------------------------------------\n");

                if (report.summary.result != BuildResult.Succeeded)
                {
                    sb.AppendLine("BUILD STEP ERRORS:");
                    foreach (var step in report.steps)
                    {
                        foreach (var message in step.messages)
                        {
                            if (message.type == LogType.Error || message.type == LogType.Exception)
                            {
                                sb.AppendLine($"[{step.name}] {message.content}");
                            }
                        }
                    }
                }
            }

            string reportContent = sb.ToString();
            File.WriteAllText(ResultFile, reportContent);
            Debug.Log($"[QuestPlayerBuilder] Finished. Result written to {ResultFile}:\n{reportContent}");
        }
    }
}
