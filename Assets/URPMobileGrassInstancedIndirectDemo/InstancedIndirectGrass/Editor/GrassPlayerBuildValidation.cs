using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Explicit file requests reuse the existing Editor; no second project session is needed.
[InitializeOnLoad]
public static class GrassPlayerBuildValidation
{
    const string Output = "Logs/MigrationPhase6-20261027/";
    [Serializable] class Request { public string id; public bool development, plainDevelopment; }
    public static bool IsValidationBuild { get; private set; }
    static GrassPlayerBuildValidation() { EditorApplication.update += Poll; }
    static void Poll()
    {
        string path = Output + "build-request.json";
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(path)) return;
        var request = JsonUtility.FromJson<Request>(File.ReadAllText(path)); File.Delete(path);
        var text = new StringBuilder();
        try {
            if (EditorUtility.scriptCompilationFailed || EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Build requires compiled scripts, Edit mode and a clean scene.");
            bool diagnostic = request.development && !request.plainDevelopment;
            var options = new BuildPlayerOptions {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = "Builds/Phase6/" + (request.plainDevelopment ? "PlainDevelopment" : request.development ? "Development" : "Release") + "/GrassDemo.exe",
                target = BuildTarget.StandaloneWindows64,
                options = request.development ? BuildOptions.Development : BuildOptions.None
            };
            if (diagnostic) options.extraScriptingDefines = new[] { "GRASS_PLAYER_VALIDATION" };
            text.AppendLine($"{DateTimeOffset.Now:O} project={Directory.GetCurrentDirectory()} Unity={Application.unityVersion} target={options.target} development={request.development} diagnostics={diagnostic} optionsBits=0x{(int)options.options:X} backend={PlayerSettings.GetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone)}");
            text.AppendLine("Scenes=" + string.Join(",", options.scenes));
            bool frameTiming = PlayerSettings.enableFrameTimingStats;
            var inclusion = GraphicsSettings.GetRenderPipelineSettings<IncludeAdditionalRPAssets>();
            const string globalPath = "Assets/Settings/UniversalRenderPipelineGlobalSettings.asset";
            byte[] globalBytes = File.ReadAllBytes(globalPath);
            bool addedInclusion = diagnostic && inclusion == null;
            if (addedInclusion) {
                EditorGraphicsSettings.PopulateRenderPipelineGraphicsSettings(AssetDatabase.LoadAssetAtPath<RenderPipelineGlobalSettings>(globalPath));
                inclusion = GraphicsSettings.GetRenderPipelineSettings<IncludeAdditionalRPAssets>();
                if (inclusion == null) throw new InvalidOperationException("Additional RP asset settings unavailable");
            }
            var mobile = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");
            string[] labels = AssetDatabase.GetLabels(mobile);
            bool includeByLabel = inclusion?.includeAssetsByLabel ?? false; string label = inclusion?.labelToInclude;
            BuildReport report;
            try {
                if (diagnostic) {
                    PlayerSettings.enableFrameTimingStats = true;
                    // Windows excludes Mobile from its quality list. The official additional RP
                    // asset API includes its variants in this diagnostic build, then is restored.
                    inclusion.includeAssetsByLabel = true; inclusion.labelToInclude = "GrassPhase6Validation";
                    AssetDatabase.SetLabels(mobile, labels.Concat(new[] { inclusion.labelToInclude }).Distinct().ToArray());
                }
                IsValidationBuild = diagnostic;
                report = BuildPipeline.BuildPlayer(options);
            } finally {
                IsValidationBuild = false;
                PlayerSettings.enableFrameTimingStats = frameTiming;
                if (inclusion != null) { inclusion.includeAssetsByLabel = includeByLabel; inclusion.labelToInclude = label; }
                AssetDatabase.SetLabels(mobile, labels);
                if (addedInclusion) { File.WriteAllBytes(globalPath,globalBytes); AssetDatabase.ImportAsset(globalPath); }
            }
            text.AppendLine($"Result={report.summary.result} bytes={report.summary.totalSize} duration={report.summary.totalTime} errors={report.summary.totalErrors} warnings={report.summary.totalWarnings} path={report.summary.outputPath}");
            foreach (var step in report.steps)
                foreach (var message in step.messages)
                    if (message.type != LogType.Log) text.AppendLine($"{step.name}: {message.type}: {message.content}");
            text.AppendLine(report.summary.result == BuildResult.Succeeded ? "BUILD PASS" : "BUILD FAIL");
        } catch (Exception error) { text.AppendLine("BUILD FAIL " + error); Debug.LogException(error); }
        File.WriteAllText(Output + request.id + ".txt", text.ToString());
        Debug.Log(text.ToString());
    }
}
