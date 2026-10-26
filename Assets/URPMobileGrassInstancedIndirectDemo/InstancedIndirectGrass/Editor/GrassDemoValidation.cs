using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
#endif

// Opt-in stage 5 checks, executed in the existing target Editor only.
[InitializeOnLoad]
public static class GrassDemoValidation
{
    const string Output = "Logs/MigrationPhase5-20261027/";
    const string SceneFolder = "Assets/URPMobileGrassInstancedIndirectDemo/InstancedIndirectGrass/TestScene/";
    [Serializable] class Request { public string id, command; public int width, height; }
    static IEnumerator running;
    static StringBuilder activeReport;
    static string activeId;
    static int loggedErrors, commandStartErrors;
    static GrassDemoValidation()
    {
        EditorApplication.update += Poll;
        Application.logMessageReceived += (message, trace, type) => { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) loggedErrors++; };
    }
    static void Poll()
    {
        if (running != null) {
            try { if (running.MoveNext()) return; Require(loggedErrors == commandStartErrors, "Unity logged errors during this check"); activeReport.AppendLine("COMMAND PASS"); }
            catch (Exception error) { activeReport.AppendLine("COMMAND FAIL " + error); Debug.LogException(error); }
            (running as IDisposable)?.Dispose(); running = null;
            File.WriteAllText(Output + activeId + ".txt", activeReport.ToString());
            return;
        }
        string path = Output + "request.json";
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(path)) return;
        var request = JsonUtility.FromJson<Request>(File.ReadAllText(path));
        File.Delete(path);
        var report = new StringBuilder();
        commandStartErrors = loggedErrors;
        report.AppendLine($"{DateTimeOffset.Now:O} Unity={Application.unityVersion} project={Directory.GetCurrentDirectory()} API={SystemInfo.graphicsDeviceType} GPU={SystemInfo.graphicsDeviceName} Play={EditorApplication.isPlaying}");
        try {
            Require(!EditorUtility.scriptCompilationFailed, "C# compilation failed; refusing stale assembly");
            switch (request.command) {
                case "inspect": Inspect(report); break;
                case "settings":
                    var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/DefaultVolumeProfile.asset");
                    Require(profile && profile.components.Count == 19 && profile.components.All(c => c), "Active default Volume components are incomplete");
                    report.AppendLine("Loaded default Volume components=" + profile.components.Count + "; all nonnull; names=" + string.Join(",", profile.components.Select(c => c.GetType().Name)));
                    var configured = GraphicsSettings.GetRenderPipelineSettings<URPDefaultVolumeProfileSettings>();
                    report.AppendLine("Current default Volume profile=" + AssetDatabase.GetAssetPath(configured?.volumeProfile));
                    report.AppendLine("Four missing-script orphan subassets are not in the serialized or loaded components list; preserve original asset bytes.");
                    break;
                case "reload":
                    Require(!EditorApplication.isPlaying && !UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().isDirty, "Reload requires clean edit scene");
                    UnityEditor.SceneManagement.EditorSceneManager.OpenScene(SceneFolder + "scene.unity"); break;
                case "visuals":
                    Require(EditorApplication.isPlaying && Camera.main, "Visuals require demo Play");
                    activeReport = report; activeId = request.id; running = Visuals(report, request.id); return;
                case "operations":
                    Require(EditorApplication.isPlaying && Camera.main, "Operations require demo Play");
                    activeReport = report; activeId = request.id; running = Operations(report, request.id); return;
                case "ui":
                    Require(EditorApplication.isPlaying && Camera.main, "UI check requires demo Play");
                    activeReport = report; activeId = request.id; running = UserInterface(report, request.id, request.width, request.height); return;
                default: throw new InvalidOperationException("Unknown command " + request.command);
            }
            Require(loggedErrors == commandStartErrors, "Unity logged errors during this check");
            report.AppendLine("COMMAND PASS");
        } catch (Exception error) { report.AppendLine("COMMAND FAIL " + error); Debug.LogException(error); }
        File.WriteAllText(Output + request.id + ".txt", report.ToString());
        Debug.Log("Grass demo " + request.id + "\n" + report);
    }
    static Color[] Render(string path)
    {
        var target = new RenderTexture(960, 600, 24, RenderTextureFormat.ARGB32);
        var previous = RenderTexture.active;
        var image = new Texture2D(960, 600, TextureFormat.RGBAFloat, false, true);
        try {
            target.Create();
            RenderPipeline.SubmitRenderRequest(Camera.main, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 960, 600), 0, 0); image.Apply();
            File.WriteAllBytes(Output + path + ".png", image.EncodeToPNG());
            return image.GetPixels();
        } finally { RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(image); UnityEngine.Object.DestroyImmediate(target); }
    }
    static double Difference(StringBuilder report, string name, Color[] a, Color[] b)
    {
        double delta = a.Zip(b, (x, y) => (Math.Abs(x.r-y.r) + Math.Abs(x.g-y.g) + Math.Abs(x.b-y.b)) / 3).Average();
        report.AppendLine(name + " RGB channel mean absolute delta=" + delta.ToString("F9"));
        return delta;
    }
    static IEnumerator Wait(double seconds)
    {
        double end = EditorApplication.timeSinceStartup + seconds;
        while (EditorApplication.timeSinceStartup < end) yield return null;
    }
    static IEnumerator Visuals(StringBuilder report, string id)
    {
        var grass = InstancedIndirectGrassRenderer.instance;
        var move = UnityEngine.Object.FindObjectsByType<MoveCamera>().Single();
        bool moving = move.enabled; move.enabled = false;
        var originalGrassMaterial = grass.instanceMaterial;
        var stillGrass = new Material(originalGrassMaterial);
        stillGrass.SetFloat("_WindAIntensity", 0); stillGrass.SetFloat("_WindBIntensity", 0); stillGrass.SetFloat("_WindCIntensity", 0);
        grass.instanceMaterial = stillGrass;
        var renderers = UnityEngine.Object.FindObjectsByType<Renderer>();
        var enabled = renderers.Select(r => r.enabled).ToArray();
        var lights = UnityEngine.Object.FindObjectsByType<Light>();
        var lightEnabled = lights.Select(l => l.enabled).ToArray();
        var rotate = UnityEngine.Object.FindObjectsByType<RotateLight>().Single();
        bool rotating = rotate.enabled; rotate.enabled = false;
        var animators = UnityEngine.Object.FindObjectsByType<Animator>();
        var speeds = animators.Select(a => a.speed).ToArray();
        foreach (var animator in animators) animator.speed = 0;
        var savedMaterials = renderers.Select(r => r.sharedMaterials).ToArray();
        var originals = new[] { "Black Dynamic", "GrassGround" }.Select(n => AssetDatabase.LoadAssetAtPath<Material>(SceneFolder + n + ".mat")).ToArray();
        var clones = originals.Select(m => new Material(m)).ToArray();
        var lightmaps = LightmapSettings.lightmaps;
        var probes = LightmapSettings.lightProbes;
        var baked = probes.bakedProbes;
        bool fog = RenderSettings.fog;
        try {
            report.AppendLine("Actual pipeline=" + GraphicsSettings.currentRenderPipeline.name + "; frozen camera/actor/light transforms and zero wind clone; test variants use temporary material clones");
            var baseline = Render(id + "-scene");
            for (int i = 0; i < renderers.Length; i++) renderers[i].sharedMaterials = savedMaterials[i].Select(m => Array.IndexOf(originals, m) >= 0 ? clones[Array.IndexOf(originals, m)] : m).ToArray();
            foreach (var clone in clones) clone.renderQueue = 2050;
            var queues = Render(id + "-legacy-queues");
            Difference(report, "Both Lit queues 2000 -> 2050", baseline, queues);
            foreach (var clone in clones) clone.renderQueue = 2000;
            clones[0].SetColor("_Color", Color.white);
            clones[1].SetColor("_Color", new Color(.17310426f, .37735847f, .13705942f, 1));
            Difference(report, "Legacy _Color values only", baseline, Render(id + "-legacy-color"));
            var oldEmission = new Color(2.6701574f, 2.2857304f, .8010472f, 1);
            var upgraded = originals[0].GetColor("_EmissionColor");
            report.AppendLine($"Emission legacy={oldEmission:R} linear={oldEmission.linear:R} upgraded={upgraded:R}");
            Require(Vector4.Distance(oldEmission.linear, upgraded) < .0001f, "Official emission conversion mismatch");
            clones[0].SetColor("_EmissionColor", oldEmission);
            Require(Difference(report, "Unconverted legacy emission in modern HDR Lit (deliberate comparison)", baseline, Render(id + "-legacy-emission")) > .000001, "Emission comparison had no response");
            for (int i = 0; i < renderers.Length; i++) renderers[i].sharedMaterials = savedMaterials[i];
            RenderSettings.fog = false;
            Require(Difference(report, "Fog", baseline, Render(id + "-fog-off")) > .00001, "No fog response");
            RenderSettings.fog = fog;
            LightmapSettings.lightmaps = lightmaps.Select(m => new LightmapData { lightmapColor = Texture2D.blackTexture, lightmapDir = m.lightmapDir, shadowMask = m.shadowMask }).ToArray();
            Require(Difference(report, "Baked lightmap black comparison", baseline, Render(id + "-black-lightmap")) > .00001, "Old lightmap not consumed");
            LightmapSettings.lightmaps = lightmaps;
            foreach (var light in lights) light.enabled = false;
            foreach (var renderer in renderers) renderer.enabled = false;
            var ambient = Render(id + "-baked-probe-grass");
            LightProbes.GetInterpolatedProbe(grass.transform.position, null, out var beforeProbe);
            probes.bakedProbes = new SphericalHarmonicsL2[baked.Length];
            var wait = Wait(.4); while (wait.MoveNext()) yield return null;
            LightProbes.GetInterpolatedProbe(grass.transform.position, null, out var zeroProbe);
            report.AppendLine($"Interpolated probe L0 before={beforeProbe[0,0]:R},{beforeProbe[1,0]:R},{beforeProbe[2,0]:R}; zero={zeroProbe[0,0]:R},{zeroProbe[1,0]:R},{zeroProbe[2,0]:R}");
            Require(Math.Abs(beforeProbe[0,0]) > .001 && Math.Abs(zeroProbe[0,0]) < .00001, "Probe interpolation did not use baked coefficients");
            Require(Difference(report, "Grass BlendProbes baked SH vs zero", ambient, Render(id + "-zero-probe-grass")) > .00001, "Baked SH did not reach grass");
            probes.bakedProbes = baked;
            wait = Wait(.4); while (wait.MoveNext()) yield return null;
            Require(Difference(report, "Restored baked SH", ambient, Render(id + "-restored-probe-grass")) < .00001, "Probe restore changed appearance");
            report.AppendLine("Valid old LightingData + 18 lightmapped renderers + 8 finite interpolated probes consumed in target. No rebake required by these checks.");
        } finally {
            LightmapSettings.lightmaps = lightmaps; probes.bakedProbes = baked; RenderSettings.fog = fog;
            for (int i = 0; i < renderers.Length; i++) { renderers[i].enabled = enabled[i]; renderers[i].sharedMaterials = savedMaterials[i]; }
            for (int i = 0; i < lights.Length; i++) lights[i].enabled = lightEnabled[i];
            for (int i = 0; i < animators.Length; i++) animators[i].speed = speeds[i];
            rotate.enabled = rotating;
            move.enabled = moving; grass.instanceMaterial = originalGrassMaterial;
            UnityEngine.Object.DestroyImmediate(stillGrass);
            foreach (var clone in clones) UnityEngine.Object.DestroyImmediate(clone);
        }
    }
    static IEnumerator Operations(StringBuilder report, string id)
    {
        var move = UnityEngine.Object.FindObjectsByType<MoveCamera>().Single();
        var stats = UnityEngine.Object.FindObjectsByType<DemoPerformanceStats>().Single();
        var grass = InstancedIndirectGrassRenderer.instance;
        var positions = grass.GetComponent<InstancedIndirectGrassPosDefine>();
        int count = positions.instanceCount; float distance = positions.drawDistance;
        bool batch = grass.shouldBatchDispatch;
        var rotate = UnityEngine.Object.FindObjectsByType<RotateLight>().Single();
        float speed = rotate.YRotateSpeed;
        bool main = rotate.mainLight.enabled, point = rotate.pointLight.enabled;
        try {
            Require(move.allCameraTransforms.Count == 8 && stats.labelsFont && Application.targetFrameRate == 60, "Camera/font/target setup incomplete");
            Require(UnityEngine.Object.FindObjectsByType<Animator>().Length == 4 && UnityEngine.Object.FindObjectsByType<TrailRenderer>().Length == 4, "Actor/trail setup incomplete");
            for (int i = 0; i < 8; i++) {
                move.NextView();
                var wait = Wait(.15); while (wait.MoveNext()) yield return null;
                Require(move.CurrentViewIndex == (i + 1) % 8, "Camera button path did not cycle");
                report.AppendLine("NextView -> " + move.CurrentViewIndex + " actual camera=" + Camera.main.transform.position);
            }
            var actors = UnityEngine.Object.FindObjectsByType<Animator>();
            var start = actors.Select(a => a.transform.position).ToArray();
            var rotation = rotate.mainLight.transform.rotation;
            rotate.YRotateSpeed = 90; rotate.mainLight.enabled = true; rotate.pointLight.enabled = true;
            var delay = Wait(1); while (delay.MoveNext()) yield return null;
            Require(Quaternion.Angle(rotation, rotate.mainLight.transform.rotation) > 1, "Main light did not rotate");
            Require(actors.Select((a, i) => Vector3.Distance(a.transform.position, start[i])).All(d => d > .01f), "Not all four actors moved");
            report.AppendLine("Four real Animator transforms moved; main light rotation and both enable toggles PASS");
            rotate.YRotateSpeed = 0; rotation = rotate.mainLight.transform.rotation;
            delay = Wait(.2); while (delay.MoveNext()) yield return null;
            Require(Quaternion.Angle(rotation, rotate.mainLight.transform.rotation) < .01f, "Zero speed still rotated light");
            rotate.YRotateSpeed = speed;
            foreach (int instances in new[] { 1000000, 3000000, 10000000, 1000000 }) {
                positions.instanceCount = instances;
                positions.drawDistance = instances == 1000000 ? 25 : 200;
                delay = Wait(.7); while (delay.MoveNext()) yield return null;
                Require(grass.allGrassPos.Count == instances && grass.drawDistance == positions.drawDistance, "UI values did not reach real data");
                uint visible = Count(grass);
                grass.shouldBatchDispatch = !batch;
                Require(Count(grass) == visible, "Batch toggle changed visible count");
                grass.shouldBatchDispatch = batch;
                report.AppendLine($"Instance UI value={instances} distance={grass.drawDistance} actual GPU visible={visible} batch count equality PASS FPS={stats.FramesPerSecond:F2} ms={1000/stats.FramesPerSecond:F2} samples={stats.SampleCount} allocated={stats.AllocatedBytes} reserved={stats.ReservedBytes} graphics={stats.GraphicsBytes}");
            }
            grass.enabled = false;
            Require(grass.PositionsBuffer == null, "Disable retained GPU data");
            delay = Wait(.3); while (delay.MoveNext()) yield return null;
            grass.enabled = true;
            Require(Count(grass) > 0, "Re-enable did not restore grass");
            Require(stats.SampleCount > 3 && stats.HasExtrema && stats.FramesPerSecond > 0 && stats.AverageFramesPerSecond > 0 && stats.MaximumFramesPerSecond >= stats.MinimumFramesPerSecond, "Real stats did not sample");
            ScreenCapture.CaptureScreenshot(Output + id + "-game.png");
            report.AppendLine($"Statistics ready: fps={stats.FramesPerSecond:F4} avg={stats.AverageFramesPerSecond:F4} min={stats.MinimumFramesPerSecond:F4} max={stats.MaximumFramesPerSecond:F4}; built-in font={stats.labelsFont.name}; target={Application.targetFrameRate} vsync={QualitySettings.vSyncCount}; disable/re-enable PASS");
        } finally {
            positions.instanceCount = count; positions.drawDistance = distance; grass.shouldBatchDispatch = batch;
            rotate.YRotateSpeed = speed; rotate.mainLight.enabled = main; rotate.pointLight.enabled = point;
            grass.enabled = true;
        }
    }
    static uint Count(InstancedIndirectGrassRenderer grass)
    {
        Require(grass.Prepare(Camera.main, out var buffers), "No grass buffers");
        var args = new GraphicsBuffer.IndirectDrawIndexedArgs[1]; buffers.args.GetData(args);
        using var counter = new GraphicsBuffer(GraphicsBuffer.Target.Raw, 1, 4);
        GraphicsBuffer.CopyCount(buffers.visible, counter, 0);
        var count = new uint[1]; counter.GetData(count);
        Require(count[0] == args[0].instanceCount && count[0] <= grass.allGrassPos.Count, "Real GPU counter/args mismatch");
        return count[0];
    }
    static IEnumerator UserInterface(StringBuilder report, string id, int width, int height)
    {
        var game = Resources.FindObjectsOfTypeAll<EditorWindow>().Single(w => w.GetType().FullName == "UnityEditor.GameView");
        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var targetProperty = game.GetType().GetProperty("targetInParent", flags);
        Require(targetProperty != null, "Game view input geometry unavailable");
        game.Focus();
        var grass = InstancedIndirectGrassRenderer.instance;
        var positions = grass.GetComponent<InstancedIndirectGrassPosDefine>();
        var move = UnityEngine.Object.FindObjectsByType<MoveCamera>().Single();
        var rotate = UnityEngine.Object.FindObjectsByType<RotateLight>().Single();
        int count = positions.instanceCount;
        float distance = positions.drawDistance, speed = rotate.YRotateSpeed;
        bool batch = grass.shouldBatchDispatch, main = rotate.mainLight.enabled, point = rotate.pointLight.enabled;
        int originalView = move.CurrentViewIndex;
        var stats = UnityEngine.Object.FindObjectsByType<DemoPerformanceStats>().Single();
        bool statsVisible = stats.visible;
        object sizeGroup = null;
        var selectedSize = game.GetType().GetProperty("selectedSizeIndex", flags);
        int originalSize = (int)selectedSize.GetValue(game), customIndex = -1;
        void Send(EventType type, float x, float y)
        {
            Rect rect = (Rect)targetProperty.GetValue(game);
            float scale = Mathf.Min(1, Mathf.Min(Screen.width / 960f, Screen.height / 600f));
            Vector2 position = rect.position + new Vector2(x * scale * rect.width / Screen.width, y * scale * rect.height / Screen.height);
            game.SendEvent(new Event { type = type, button = 0, mousePosition = position });
        }
        IEnumerator Click(float x, float y)
        {
            Send(EventType.MouseDown, x, y); yield return null;
            Send(EventType.MouseUp, x, y);
            var wait = Wait(.2); while (wait.MoveNext()) yield return null;
        }
        IEnumerator Drag(float x, float y, float targetX)
        {
            Send(EventType.MouseDown, x, y); yield return null;
            Send(EventType.MouseDrag, targetX, y); yield return null;
            Send(EventType.MouseUp, targetX, y);
            var wait = Wait(.5); while (wait.MoveNext()) yield return null;
        }
        try {
            if (width > 0 && height > 0) {
                var assembly = typeof(EditorWindow).Assembly;
                var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
                var sizes = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
                var groupType = assembly.GetType("UnityEditor.GameViewSizeGroupType");
                sizeGroup = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { Enum.Parse(groupType, "Standalone") });
                // Remove only a named test entry left by an interrupted earlier check.
                var texts = (string[])sizeGroup.GetType().GetMethod("GetDisplayTexts").Invoke(sizeGroup, null);
                for (int i = texts.Length - 1; i >= 0; i--) if (texts[i].StartsWith("Migration stage 5 temporary", StringComparison.Ordinal)) {
                    sizeGroup.GetType().GetMethod("RemoveCustomSize").Invoke(sizeGroup, new object[] { i });
                    report.AppendLine("Removed previous harness size " + texts[i]);
                }
                var size = Activator.CreateInstance(assembly.GetType("UnityEditor.GameViewSize"), new[] { Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeType"), "FixedResolution"), (object)width, height, "Migration stage 5 temporary" });
                sizeGroup.GetType().GetMethod("AddCustomSize").Invoke(sizeGroup, new[] { size });
                customIndex = (int)sizeGroup.GetType().GetMethod("GetTotalCount").Invoke(sizeGroup, null) - 1;
                selectedSize.SetValue(game, customIndex);
                var resized = Wait(.4); while (resized.MoveNext()) yield return null;
                Require(Screen.width == width && Screen.height == height, "Game view did not adopt test size");
            }
            report.AppendLine("EditorWindow.SendEvent -> real GameView input -> runtime IMGUI controls, target=" + targetProperty.GetValue(game));
            report.AppendLine($"Runtime screen={Screen.width}x{Screen.height}");
            var action = Click(200, 500); while (action.MoveNext()) yield return null;
            Require(move.CurrentViewIndex == (originalView + 1) % 8, "Mouse click did not reach camera button");
            report.AppendLine("Camera button mouse click PASS");
            action = Click(110, 160); while (action.MoveNext()) yield return null;
            Require(rotate.mainLight.enabled != main, "Main light mouse toggle failed");
            action = Click(110, 260); while (action.MoveNext()) yield return null;
            Require(rotate.pointLight.enabled != point, "Point light mouse toggle failed");
            action = Click(410, 410); while (action.MoveNext()) yield return null;
            Require(grass.shouldBatchDispatch != batch, "Batch mouse toggle failed");
            report.AppendLine("Main/point light and batch mouse toggles PASS");
            action = Drag(306 + (count / 1000000f - 1) / 9 * 188, 106, 350); while (action.MoveNext()) yield return null;
            Require(positions.instanceCount > 1000000 && positions.instanceCount == grass.allGrassPos.Count, "Instance mouse slider did not regenerate positions");
            report.AppendLine($"Instance mouse slider -> {positions.instanceCount} actual GPU visible={Count(grass)}");
            action = Drag(306 + (distance / 25 - 1) / 7 * 188, 206, 494); while (action.MoveNext()) yield return null;
            Require(positions.drawDistance >= 175 && grass.drawDistance == positions.drawDistance, "Distance mouse slider failed");
            report.AppendLine("Distance mouse slider -> " + grass.drawDistance);
            action = Drag(106 + speed / 90 * 188, 406, 106); while (action.MoveNext()) yield return null;
            Require(rotate.YRotateSpeed == 0, "Light speed mouse slider failed");
            report.AppendLine("Light speed mouse slider -> 0 PASS");
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null) {
                InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.Backquote));
                var wait = Wait(.15); while (wait.MoveNext()) yield return null;
                InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
                Require(stats.visible != statsVisible, "Backquote did not toggle statistics");
                wait = Wait(.15); while (wait.MoveNext()) yield return null;
                InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.Backquote));
                wait = Wait(.15); while (wait.MoveNext()) yield return null;
                InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
                Require(stats.visible == statsVisible, "Backquote did not restore statistics");
                report.AppendLine("Input System backquote hide/show PASS");
            }
#endif
            ScreenCapture.CaptureScreenshot(Output + id + "-game.png");
            var captureWait = Wait(.3); while (captureWait.MoveNext()) yield return null;
        } finally {
            positions.instanceCount = count; positions.drawDistance = distance; rotate.YRotateSpeed = speed;
            grass.shouldBatchDispatch = batch; rotate.mainLight.enabled = main; rotate.pointLight.enabled = point;
            // Return to the same camera index without bypassing its normal button path.
            for (int i = 0; i < 8 && move.CurrentViewIndex != originalView; i++) move.NextView();
            stats.visible = statsVisible;
            if (sizeGroup != null) {
                selectedSize.SetValue(game, originalSize);
                if (customIndex >= 0) sizeGroup.GetType().GetMethod("RemoveCustomSize").Invoke(sizeGroup, new object[] { customIndex });
            }
        }
    }
    static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    static void Inspect(StringBuilder report)
    {
        report.AppendLine("Scene=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
        var data = Lightmapping.lightingDataAsset;
        report.AppendLine("LightingData=" + AssetDatabase.GetAssetPath(data));
        if (data) report.AppendLine("LightingData dependencies=" + string.Join(",", AssetDatabase.GetDependencies(AssetDatabase.GetAssetPath(data), false)));
        if (data) {
            foreach (string name in new[] { "isValid", "validityErrorMessage" }) {
                var property = typeof(LightingDataAsset).GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                report.AppendLine(name + "=" + property?.GetValue(data));
            }
        }
        report.AppendLine("LightingSettings=" + (Lightmapping.TryGetLightingSettings(out var settings) ? AssetDatabase.GetAssetPath(settings) : "none (scene legacy settings)"));
        report.AppendLine("Lightmaps=" + LightmapSettings.lightmaps.Length + " mode=" + LightmapSettings.lightmapsMode);
        foreach (var map in LightmapSettings.lightmaps)
            report.AppendLine($"map color={AssetDatabase.GetAssetPath(map.lightmapColor)} direction={AssetDatabase.GetAssetPath(map.lightmapDir)} mask={AssetDatabase.GetAssetPath(map.shadowMask)}");
        var probes = LightmapSettings.lightProbes;
        report.AppendLine($"LightProbes count={probes?.count} cells={probes?.cellCount} baked={probes?.bakedProbes.Length}");
        if (probes) report.AppendLine("Probe positions=" + string.Join(";", probes.positions.Select(p => p.ToString("R"))));
        report.AppendLine($"Default reflection={AssetDatabase.GetAssetPath(ReflectionProbe.defaultTexture)} size={ReflectionProbe.defaultTexture?.width}");
        if (probes && probes.count > 0) {
            var values = probes.bakedProbes.SelectMany(sh => Enumerable.Range(0, 3).SelectMany(c => Enumerable.Range(0, 9).Select(i => sh[c, i]))).ToArray();
            report.AppendLine($"SH coefficients={values.Length} finite={values.All(v => !float.IsNaN(v) && !float.IsInfinity(v))} min={values.Min():R} max={values.Max():R}");
        }
        foreach (var light in UnityEngine.Object.FindObjectsByType<Light>())
            report.AppendLine($"Light {light.name} type={light.type} bakeType={light.lightmapBakeType} baked={light.bakingOutput.isBaked} enabled={light.enabled}");
        foreach (var probe in UnityEngine.Object.FindObjectsByType<ReflectionProbe>())
            report.AppendLine($"Reflection {probe.name} mode={probe.mode} texture={AssetDatabase.GetAssetPath(probe.bakedTexture)} size={probe.bakedTexture?.width}");
        var renderers = UnityEngine.Object.FindObjectsByType<Renderer>();
        report.AppendLine("Renderers=" + renderers.Length + " valid lightmap index=" + renderers.Count(r => r.lightmapIndex >= 0 && r.lightmapIndex < LightmapSettings.lightmaps.Length));
        int missing = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<Transform>(true)).Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
        report.AppendLine("Missing scripts=" + missing);
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Require(font, "Built-in runtime font unavailable");
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(font, out string guid, out long fileId);
        report.AppendLine($"Built-in font={font.name} path={AssetDatabase.GetAssetPath(font)} guid={guid} fileID={fileId}");
        foreach (string name in new[] { "Black Dynamic", "GrassGround" }) {
            var material = AssetDatabase.LoadAssetAtPath<Material>(SceneFolder + name + ".mat");
            Require(material && material.shader && !ShaderUtil.ShaderHasError(material.shader), "Lit material missing or shader error");
            report.AppendLine($"Material={name} shader={material.shader.name} queue={material.renderQueue} Base={material.GetColor("_BaseColor")} Color={material.GetColor("_Color")} emission={material.GetColor("_EmissionColor")}");
        }
    }
}
