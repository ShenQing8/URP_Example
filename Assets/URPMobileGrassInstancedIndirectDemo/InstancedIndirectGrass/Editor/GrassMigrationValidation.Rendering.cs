using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public static partial class GrassMigrationValidation
{
    class CopyData { public TextureHandle source; }
    static void RenderSuite(StringBuilder report, string id)
    {
        Check(EditorApplication.isPlaying && Camera.main, "Render suite requires demo in Play");
        var camera = Camera.main;
        var grass = InstancedIndirectGrassRenderer.instance;
        Check(grass, "No grass renderer");
        var target = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32) { name = "Validation camera" }; target.Create();
        var bend = new RenderTexture(512, 512, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear) { name = "Validation bending copy" }; bend.Create();
        var normals = new RenderTexture(960, 540, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear) { name = "Validation normals copy" }; normals.Create();
        var bendHandle = RTHandles.Alloc(bend); var normalsHandle = RTHandles.Alloc(normals);
        int originalQuality = QualitySettings.GetQualityLevel();
        var originalPipeline = QualitySettings.renderPipeline;
        var lights = UnityEngine.Object.FindObjectsByType<Light>();
        var originalEnabled = lights.Select(light => light.enabled).ToArray();
        var originalShadows = lights.Select(light => light.shadows).ToArray();
        var originalPosition = lights.Select(light => light.transform.position).ToArray();
        var originalIntensity = lights.Select(light => light.intensity).ToArray();
        var originalRange = lights.Select(light => light.range).ToArray();
        var renderers = UnityEngine.Object.FindObjectsByType<Renderer>();
        var originalRenderers = renderers.Select(renderer => renderer.enabled).ToArray();
        var material = grass.instanceMaterial;
        var materialCopy = new Material(material);
        var featureStates = new List<(ScriptableRendererData asset, ScriptableRendererFeature feature, bool active)>();
        bool bendingRecorded = false, normalsRecorded = false;
        GrassBendingRTPrePass.ValidationRecord = (graph, texture, renderCamera, kind) => {
            if (renderCamera != camera) return;
            if (kind == "bending") bendingRecorded = true; else normalsRecorded = true;
            using var builder = graph.AddRasterRenderPass<CopyData>("Validation copy " + kind, out var data);
            data.source = texture;
            builder.UseTexture(texture, AccessFlags.Read);
            builder.SetRenderAttachment(graph.ImportTexture(kind == "bending" ? bendHandle : normalsHandle), 0, AccessFlags.Write);
            builder.SetRenderFunc(static (CopyData pass, RasterGraphContext context) => Blitter.BlitTexture(context.cmd, pass.source, new Vector4(1, 1, 0, 0), 0, false));
        };
        var debugType = typeof(RenderGraph).Assembly.GetType("UnityEngine.Rendering.RenderGraphModule.RenderGraphDebugSession");
        var localType = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType("UnityEngine.Rendering.RenderGraphModule.RenderGraphEditorLocalDebugSession")).FirstOrDefault(type => type != null);
        var create = debugType.GetMethod("Create", BindingFlags.Static | BindingFlags.NonPublic, null, new[] { typeof(Type) }, null);
        if (localType != null) create.Invoke(null, new object[] { localType });
        try {
            Color[] Render(string label, bool expectBending = true) {
                bendingRecorded = normalsRecorded = false;
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                var pixels = SaveTexture(target, Output + id + "-" + label + ".png");
                if (expectBending) {
                    Check(bendingRecorded, "Missing graph producer: " + label);
                    if (GraphicsSettings.currentRenderPipeline.name.StartsWith("PC", StringComparison.Ordinal)) Check(normalsRecorded, "Missing PC SSAO grass normal/depth consumer");
                    var bends = SaveTexture(bend, Output + id + "-" + label + "-bending.png");
                    if (normalsRecorded) SaveTexture(normals, Output + id + "-" + label + "-normals.png");
                    report.AppendLine($"{label}: R8 supported render={SystemInfo.IsFormatSupported(GraphicsFormat.R8_UNorm, GraphicsFormatUsage.Render)} blend={SystemInfo.IsFormatSupported(GraphicsFormat.R8_UNorm, GraphicsFormatUsage.Blend)} bending min={bends.Min(c => c.r):F4} max={bends.Max(c => c.r):F4} nonwhite={bends.Count(c => c.r < .99f)} normal/depth graph copy={normalsRecorded} (Mobile does not request a prepass)");
                } else report.AppendLine(label + ": bendingRecorded=" + bendingRecorded + " enabled=" + Shader.GetGlobalFloat("_GrassBendingEnabled"));
                return pixels;
            }
            void Difference(string label, Color[] a, Color[] b) {
                double delta = a.Zip(b, (x, y) => Math.Abs(x.r-y.r)+Math.Abs(x.g-y.g)+Math.Abs(x.b-y.b)).Average();
                report.AppendLine(label + " RGB absolute mean delta=" + delta.ToString("F8"));
                Check(delta > .00001, "No image response for " + label);
            }
            // Quality changes take effect at the next pipeline frame. Run once per request, after
            // a separate quality command; never mislabel a still-active PC pipeline as Mobile.
            foreach (int quality in new[] { QualitySettings.GetQualityLevel() }) {
                Check(GraphicsSettings.currentRenderPipeline == QualitySettings.renderPipeline, "Quality pipeline has not switched yet");
                report.AppendLine("Actual active pipeline=" + GraphicsSettings.currentRenderPipeline.name);
                string prefix = GraphicsSettings.currentRenderPipeline.name.StartsWith("PC", StringComparison.Ordinal) ? "PC" : "Mobile";
                report.AppendLine("Runtime quality names=" + string.Join(",", QualitySettings.names) + " index=" + quality);
                var full = Render(prefix + "-scene");
                // Render requests deliberately suppress Viewer debug generation in URP 17.6.
                // Dump after a subsequent ordinary Game camera frame using the dumpgraph command.
                // Isolate grass pixels for light response; restore every original renderer in finally.
                foreach (var renderer in renderers) if (!(renderer is TrailRenderer)) renderer.enabled = false;
                foreach (var light in lights) light.enabled = false;
                var ambient = Render(prefix + "-ambient");
                var main = lights.First(light => light.type == LightType.Directional);
                main.enabled = true;
                var mainImage = Render(prefix + "-main"); Difference(prefix + " main light", ambient, mainImage);
                main.shadows = LightShadows.None;
                var noShadows = Render(prefix + "-main-no-shadows");
                // The isolated grass still receives the original scene's shadow maps only when casters are enabled.
                main.shadows = LightShadows.Soft;
                foreach (var renderer in renderers) if (!(renderer is TrailRenderer)) renderer.enabled = originalRenderers[Array.IndexOf(renderers, renderer)];
                var shadowScene = Render(prefix + "-shadows");
                main.shadows = LightShadows.None;
                var noShadowScene = Render(prefix + "-no-shadows"); Difference(prefix + " real shadow sampling", shadowScene, noShadowScene);
                foreach (var renderer in renderers) if (!(renderer is TrailRenderer)) renderer.enabled = false;
                main.enabled = false;
                var point = lights.First(light => light.type == LightType.Point);
                point.enabled = true; point.transform.position = camera.transform.position + camera.transform.forward * 12 + Vector3.up * 2; point.intensity = 10; point.range = 35;
                var pointImage = Render(prefix + "-point"); Difference(prefix + " point light (test position/intensity)", ambient, pointImage);
                point.enabled = false;
                main.enabled = true;
                material.SetFloat("_WindAIntensity", 0); material.SetFloat("_WindBIntensity", 0); material.SetFloat("_WindCIntensity", 0);
                var still = Render(prefix + "-wind-zero");
                material.CopyPropertiesFromMaterial(materialCopy);
                var wind = Render(prefix + "-wind"); Difference(prefix + " wind geometry", still, wind);
                var rendererAsset = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/" + prefix + "_Renderer.asset");
                var feature = rendererAsset.rendererFeatures.OfType<GrassBendingRTPrePass>().Single();
                featureStates.Add((rendererAsset, feature, feature.isActive));
                feature.SetActive(false); rendererAsset.SetDirty();
                var unbent = Render(prefix + "-feature-off", false);
                Check(!bendingRecorded && Shader.GetGlobalFloat("_GrassBendingEnabled") == 0, "Feature-off stale bending state");
                feature.SetActive(true); rendererAsset.SetDirty();
                var bent = Render(prefix + "-feature-on"); Difference(prefix + " bending/Feature response", unbent, bent);
                foreach (var renderer in renderers) renderer.enabled = originalRenderers[Array.IndexOf(renderers, renderer)];
                for (int i = 0; i < lights.Length; i++) { lights[i].enabled = originalEnabled[i]; lights[i].shadows = originalShadows[i]; lights[i].transform.position = originalPosition[i]; lights[i].intensity = originalIntensity[i]; lights[i].range = originalRange[i]; }
            }
            Shaders(report);
        } finally {
            GrassBendingRTPrePass.ValidationRecord = null;
            material.CopyPropertiesFromMaterial(materialCopy); UnityEngine.Object.DestroyImmediate(materialCopy);
            foreach (var state in featureStates) { state.feature.SetActive(state.active); state.asset.SetDirty(); }
            for (int i = 0; i < lights.Length; i++) { lights[i].enabled = originalEnabled[i]; lights[i].shadows = originalShadows[i]; lights[i].transform.position = originalPosition[i]; lights[i].intensity = originalIntensity[i]; lights[i].range = originalRange[i]; }
            for (int i = 0; i < renderers.Length; i++) renderers[i].enabled = originalRenderers[i];
            QualitySettings.SetQualityLevel(originalQuality, true);
            QualitySettings.renderPipeline = originalPipeline;
            bendHandle.Release(); normalsHandle.Release();
            UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(bend); UnityEngine.Object.DestroyImmediate(normals);
        }
    }
    static Color[] SaveTexture(RenderTexture texture, string path)
    {
        var previous = RenderTexture.active; RenderTexture.active = texture;
        var copy = new Texture2D(texture.width, texture.height, TextureFormat.RGBAFloat, false, true);
        copy.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); copy.Apply();
        var pixels = copy.GetPixels(); File.WriteAllBytes(path, copy.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(copy); RenderTexture.active = previous; return pixels;
    }
    static void DumpGraph(Type debugType, string path, StringBuilder report)
    {
        var graphs = (IEnumerable)debugType.GetMethod("GetRegisteredGraphs").Invoke(null, null);
        foreach (string name in graphs) {
            var executions = (IEnumerable)debugType.GetMethod("GetExecutions").Invoke(null, new object[] { name });
            foreach (var execution in executions) {
                var data = debugType.GetMethod("GetDebugData").Invoke(null, new[] { (object)name, execution.GetType().GetProperty("id").GetValue(execution) });
                string json = JsonUtility.ToJson(data, true);
                string executionName = execution.GetType().GetProperty("name").GetValue(execution).ToString();
                report.AppendLine("Graph execution=" + executionName);
                if (json.Contains("Grass Bending Trails")) {
                    File.WriteAllText(path.Replace(".json", "-" + executionName.Replace(" ", "_") + ".json"), json);
                    if (executionName == "Main Camera") { File.WriteAllText(path, json); report.AppendLine("Real main-camera RenderGraph Viewer debug data=" + path); return; }
                }
            }
        }
        report.AppendLine("RenderGraph Viewer debug capture unavailable; graph inspection remains unverified.");
    }
}
