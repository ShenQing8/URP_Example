using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Opt-in file commands let an already open Editor perform validation without a second project session.
[InitializeOnLoad]
public static partial class GrassMigrationValidation
{
    const string Core = "Assets/URPMobileGrassInstancedIndirectDemo/InstancedIndirectGrass/Core/";
    const string Demo = "Assets/URPMobileGrassInstancedIndirectDemo/InstancedIndirectGrass/TestScene/scene.unity";
    const string Output = "Logs/MigrationPhase234-20261027/";
    [Serializable] class Request { public string id, command; public int quality; }
    static GrassMigrationValidation() { EditorApplication.update += Poll; }
    static void Poll()
    {
        string path = Output + "request.json";
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(path)) return;
        var request = JsonUtility.FromJson<Request>(File.ReadAllText(path)); File.Delete(path);
        var report = new StringBuilder();
        report.AppendLine($"{DateTimeOffset.Now:O} Unity={Application.unityVersion} project={Directory.GetCurrentDirectory()} API={SystemInfo.graphicsDeviceType} GPU={SystemInfo.graphicsDeviceName}");
        try {
            Check(!EditorUtility.scriptCompilationFailed || request.command == "refresh" || request.command == "stop", "Unity C# compilation is failing; refusing to validate a stale loaded assembly");
            switch (request.command) {
                case "setup": Setup(); report.AppendLine("Modern PC/Mobile Features connected before SSAO."); break;
                case "gpu": Gpu(report); break;
                case "render": RenderSuite(report, request.id); break;
                case "dumpgraph": DumpGraph(typeof(UnityEngine.Rendering.RenderGraphModule.RenderGraph).Assembly.GetType("UnityEngine.Rendering.RenderGraphModule.RenderGraphDebugSession"), Output + request.id + ".json", report); break;
                case "capacity": Capacity(report); break;
                case "interaction": Interaction(report, request.id); break;
                case "appearance": Appearance(report, request.id); break;
                case "shaderbundle": BuildShaderBundle(report); break;
                case "usebundle": UseShaderBundle(report, request.id); break;
                case "cameras": Cameras(report, request.id); break;
                case "refresh": AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate); report.AppendLine("Refresh requested"); break;
                case "graph": EditorApplication.ExecuteMenuItem("Window/Analysis/Render Graph Viewer"); report.AppendLine("Viewer opened"); break;
                case "gameview": EditorApplication.ExecuteMenuItem("Window/General/Game"); report.AppendLine("Game view opened"); break;
                case "open":
                    if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Current scene is dirty; refusing to replace it.");
                    SessionState.SetString("GrassMigrationPreviousScene", EditorSceneManager.GetActiveScene().path);
                    EditorSceneManager.OpenScene(Demo); report.AppendLine("Opened demo without saving it."); break;
                case "play": EditorApplication.isPlaying = true; report.AppendLine("Play requested; runtime results require later capture."); break;
                case "stop": EditorApplication.isPlaying = false; report.AppendLine("Stop requested."); break;
                case "restore":
                    if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play first.");
                    if (EditorSceneManager.GetActiveScene().isDirty) { report.AppendLine("Demo has unsaved Editor edits; preserved it open rather than discarding edits."); break; }
                    string previous = SessionState.GetString("GrassMigrationPreviousScene", "Assets/Scenes/SampleScene.unity");
                    EditorSceneManager.OpenScene(previous); report.AppendLine("Restored " + previous); break;
                case "quality":
                    Check(EditorApplication.isPlaying, "Pipeline override is only allowed during Play validation");
                    string pipelineName = request.quality == 0 ? "Mobile" : "PC";
                    QualitySettings.renderPipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/" + pipelineName + "_RPAsset.asset");
                    report.AppendLine("Play-only current quality pipeline override=" + pipelineName + " (excluded platform quality levels are not runtime indices)"); break;
                case "capture": Capture(report, request.id); break;
                case "shaders": Shaders(report); break;
                default: throw new InvalidOperationException("Unknown command " + request.command);
            }
            report.AppendLine("COMMAND PASS");
        } catch (Exception exception) { report.AppendLine("COMMAND FAIL " + exception); Debug.LogException(exception); }
        File.WriteAllText(Output + request.id + ".txt", report.ToString());
        Debug.Log("Grass migration " + request.id + "\n" + report);
    }
    static void Setup()
    {
        foreach (var name in new[] { "PC", "Mobile" }) {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/" + name + "_Renderer.asset");
            if (asset.rendererFeatures.OfType<GrassBendingRTPrePass>().Any()) continue;
            var feature = ScriptableObject.CreateInstance<GrassBendingRTPrePass>(); feature.name = "Grass Bending and Depth (RenderGraph)";
            AssetDatabase.AddObjectToAsset(feature, asset); asset.rendererFeatures.Insert(0, feature); feature.Create();
            var serialized = new SerializedObject(asset);
            var map = serialized.FindProperty("m_RendererFeatureMap"); map.arraySize = asset.rendererFeatures.Count;
            for (int i = 0; i < map.arraySize; i++) {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset.rendererFeatures[i], out string guid, out long id);
                map.GetArrayElementAtIndex(i).longValue = id;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(asset);
        }
        AssetDatabase.SaveAssets();
    }
    static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    static uint[] Read(InstancedIndirectGrassRenderer renderer, Camera camera, StringBuilder report)
    {
        if (!renderer.Prepare(camera, out var buffers)) { Check(renderer.allGrassPos.Count == 0 && renderer.PositionsBuffer == null, "Prepare failed with nonempty data or leaked empty buffer"); report.AppendLine("positions=0 no GPU allocation or draw PASS"); return Array.Empty<uint>(); }
        var args = new GraphicsBuffer.IndirectDrawIndexedArgs[1]; buffers.args.GetData(args);
        using var counter = new GraphicsBuffer(GraphicsBuffer.Target.Raw, 1, 4);
        GraphicsBuffer.CopyCount(buffers.visible, counter, 0); var count = new uint[1]; counter.GetData(count);
        Check(args[0].instanceCount == count[0], "Append counter differs from indirect instanceCount");
        Check(count[0] <= renderer.allGrassPos.Count, "Counter exceeds capacity");
        var ids = new uint[count[0]]; if (ids.Length > 0) buffers.visible.GetData(ids, 0, 0, ids.Length);
        Check(ids.Distinct().Count() == ids.Length && ids.All(id => id < renderer.allGrassPos.Count), "Duplicate or out-of-range ID");
        Array.Sort(ids);
        // Independent CPU oracle over the uploaded (cell-sorted) pivots, using the shared
        // conservative blade radius. This also checks that CPU Cells do not lose GPU candidates.
        var positions = new Vector3[renderer.PositionsBuffer.count]; renderer.PositionsBuffer.GetData(positions);
        float radius = (float)typeof(InstancedIndirectGrassRenderer).GetMethod("DeformationRadius", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(renderer, new object[] { camera });
        var frustum = GeometryUtility.CalculateFrustumPlanes(camera.cullingMatrix);
        var expected = Enumerable.Range(0, positions.Length).Where(i => Vector3.Distance(positions[i], camera.transform.position) <= Mathf.Max(0, renderer.drawDistance) + radius && frustum.All(plane => plane.GetDistanceToPoint(positions[i]) >= -radius)).Select(i => (uint)i).ToArray();
        Check(ids.SequenceEqual(expected), "GPU visible IDs differ from independent CPU sphere/frustum oracle");
        report.AppendLine($"positions={renderer.allGrassPos.Count} append={count[0]} args={args[0].instanceCount} cells={buffers.visibleCells} dispatches={buffers.dispatches} unique/range=PASS idsSHA256={BitConverter.ToString(System.Security.Cryptography.SHA256.Create().ComputeHash(ids.SelectMany(BitConverter.GetBytes).ToArray())).Replace("-", "")}");
        return ids;
    }
    static void Gpu(StringBuilder report)
    {
        var root = new GameObject("Migration GPU validation") { hideFlags = HideFlags.HideAndDontSave };
        var cameraObject = new GameObject("Migration GPU camera") { hideFlags = HideFlags.HideAndDontSave };
        var previous = InstancedIndirectGrassRenderer.instance;
        try {
            var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
            camera.transform.position = new Vector3(0, 4, -20); camera.transform.LookAt(new Vector3(0, 1, 20)); camera.farClipPlane = 250;
            var renderer = root.AddComponent<InstancedIndirectGrassRenderer>();
            renderer.instanceMaterial = AssetDatabase.LoadAssetAtPath<Material>(Core + "InstancedIndirectGrass.mat");
            renderer.cullingComputeShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(Core + "CullingCompute.compute"); renderer.drawDistance = 200;
            foreach (int count in new[] { 0, 1, 63, 64, 65, 127, 128, 129, 5003 }) {
                var positions = new List<Vector3>();
                for (int i = 0; i < count; i++) positions.Add(new Vector3((i % 19) * .1f, 0, 10 + i / 19 * .02f));
                renderer.SetPositions(positions); renderer.shouldBatchDispatch = true;
                var batch = Read(renderer, camera, report); Check(batch.Length == count, "All-inside boundary case lost instances");
                renderer.shouldBatchDispatch = false;
                Check(batch.SequenceEqual(Read(renderer, camera, report)), "Batch visible sets differ");
            }
            var random = new System.Random(123);
            var mixed = Enumerable.Range(0, 5003).Select(i => new Vector3((float)random.NextDouble() * 160 - 80, (i % 7) * .1f, (float)random.NextDouble() * 200 - 50)).ToList();
            renderer.SetPositions(mixed); renderer.drawDistance = 60;
            foreach (bool orthographic in new[] { false, true }) {
                camera.orthographic = orthographic; camera.orthographicSize = 25;
                renderer.shouldBatchDispatch = true; var a = Read(renderer, camera, report);
                renderer.shouldBatchDispatch = false; var b = Read(renderer, camera, report);
                Check(a.Length > 0 && a.Length < mixed.Count && a.SequenceEqual(b), "Mixed culling or batch equivalence failed");
                for (int angle = 0; angle < 360; angle += 45) {
                    camera.transform.rotation = Quaternion.Euler(10, angle, 0);
                    report.AppendLine($"orthographic={orthographic} camera yaw={angle}"); Read(renderer, camera, report);
                }
                camera.transform.LookAt(new Vector3(0, 1, 20));
            }
            // Same-count replacement, then in-place movement, single axis, zero extents and disable/re-enable.
            foreach (var shape in new[] { "point", "x", "z", "rectangle", "translated" }) {
                var positions = Enumerable.Range(0, 65).Select(i => shape == "point" ? new Vector3(0, 2, 10) : new Vector3(shape == "z" ? 0 : i * .3f, i % 3, shape == "x" ? 10 : 10 + i * .6f)).ToList();
                if (shape == "translated") for (int i = 0; i < positions.Count; i++) positions[i] += new Vector3(0, 0, 1000);
                renderer.SetPositions(positions); report.AppendLine("shape=" + shape); Read(renderer, camera, report);
            }
            for (int i = 0; i < renderer.allGrassPos.Count; i++) renderer.allGrassPos[i] = new Vector3(0, 0, 10);
            renderer.MarkPositionsDirty(); Check(Read(renderer, camera, report).Length == 65, "Same-count dirty refresh failed");
            for (int i = 0; i < 3; i++) { renderer.enabled = false; Check(renderer.PositionsBuffer == null, "Disable did not release positions"); renderer.enabled = true; Check(Read(renderer, camera, report).Length == 65, "Re-enable lost instances"); }
            report.AppendLine("GPU boundaries, mixed culling, batch equality, same-count edits and lifecycle PASS");
        } finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(cameraObject); InstancedIndirectGrassRenderer.instance = previous; }
    }
    static void Capture(StringBuilder report, string id)
    {
        report.AppendLine("Play=" + EditorApplication.isPlaying + " quality=" + QualitySettings.names[QualitySettings.GetQualityLevel()]);
        var grass = InstancedIndirectGrassRenderer.instance;
        if (grass && Camera.main) Read(grass, Camera.main, report);
        var texture = Shader.GetGlobalTexture("_GrassBendingRT") as RenderTexture;
        report.AppendLine($"Bending enabled={Shader.GetGlobalFloat("_GrassBendingEnabled")} texture={texture?.name} format={texture?.graphicsFormat} size={texture?.width}x{texture?.height}");
        if (texture) {
            var previous = RenderTexture.active; RenderTexture.active = texture;
            var copy = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false, true);
            copy.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); copy.Apply();
            var pixels = copy.GetPixels32(); report.AppendLine($"RT min={pixels.Min(p => p.r)} max={pixels.Max(p => p.r)} nonwhite={pixels.Count(p => p.r < 254)}");
            File.WriteAllBytes(Output + id + "-bending.png", copy.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(copy); RenderTexture.active = previous;
        }
        ScreenCapture.CaptureScreenshot(Output + id + "-game.png");
        Shaders(report);
    }
    static void Shaders(StringBuilder report)
    {
        foreach (var shader in new[] { Shader.Find("MobileDrawMeshInstancedIndirect/SingleGrass"), Shader.Find("URPMobileGrassInstancedIndirectDemo/GrassBendingRTPrePassPS"), Shader.Find("URPMobileGrassInstancedIndirectDemo/SkyBox") }) {
            Check(shader, "Missing grass, trail or sky shader");
            report.AppendLine($"Shader {shader.name} supported={shader.isSupported}");
            foreach (var message in ShaderUtil.GetShaderMessages(shader)) report.AppendLine($"{message.severity} {message.file}:{message.line} {message.message}");
            Check(!ShaderUtil.ShaderHasError(shader), "Shader error: " + shader.name);
        }
    }
}
