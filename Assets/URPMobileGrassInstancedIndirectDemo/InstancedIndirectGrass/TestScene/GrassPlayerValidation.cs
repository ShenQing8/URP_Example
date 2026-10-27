#if UNITY_EDITOR || GRASS_PLAYER_VALIDATION
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Included only in a development build by the build scene processor. Ordinary launches do nothing.
// All synchronous GPU reads are outside the timed profiling windows.
public sealed class GrassPlayerValidation : MonoBehaviour
{
    public UniversalRenderPipelineAsset pc, mobile;
    static GrassPlayerValidation current;
    string output;
    int errors;
    bool busy;
    [Serializable] sealed class Request { public string id, command; public int seconds = 60, cycles = 20; }
    void Awake()
    {
        string[] args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "-grassValidation");
        if (index < 0 || index + 1 >= args.Length || Application.isEditor) { Destroy(gameObject); return; }
        if (current) { Destroy(gameObject); return; } current = this;
        output = Path.GetFullPath(args[index + 1]); Directory.CreateDirectory(output);
        DontDestroyOnLoad(gameObject); Application.runInBackground = true;
        Application.logMessageReceived += Log;
        File.WriteAllText(Path.Combine(output, "ready.txt"), $"{DateTimeOffset.Now:O} Unity={Application.unityVersion} platform={Application.platform} API={SystemInfo.graphicsDeviceType} GPU={SystemInfo.graphicsDeviceName} driver={SystemInfo.graphicsDeviceVersion} CPU={SystemInfo.processorType} RAM={SystemInfo.systemMemorySize} graphicsCapacity={SystemInfo.graphicsMemorySize} screen={Screen.width}x{Screen.height} quality={QualitySettings.names[QualitySettings.GetQualityLevel()]} pipeline={GraphicsSettings.currentRenderPipeline?.name} development={Debug.isDebugBuild} compute={SystemInfo.supportsComputeShaders} instancing={SystemInfo.supportsInstancing}\n");
    }
    void Log(string message, string trace, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }
    void OnDestroy() { Application.logMessageReceived -= Log; if (current == this) current = null; }
    void Update()
    {
        if (output == null || busy) return;
        string path = Path.Combine(output, "request.json");
        if (!File.Exists(path)) return;
        var request = JsonUtility.FromJson<Request>(File.ReadAllText(path)); File.Delete(path);
        StartCoroutine(Run(request));
    }
    IEnumerator Run(Request request)
    {
        busy = true; int startErrors = errors;
        var report = new StringBuilder($"{DateTimeOffset.Now:O} command={request.command} API={SystemInfo.graphicsDeviceType} scene={SceneManager.GetActiveScene().path}\n");
        IEnumerator command;
        switch (request.command) {
            case "functional": command = Functional(report, request.id); break;
            case "inspect": command = Inspect(report); break;
            case "profile-pc": command = Profile(report, request.id, pc, request.seconds); break;
            case "profile-mobile": command = Profile(report, request.id, mobile, request.seconds); break;
            case "profile-high": command = ProfileHigh(report, request.id, request.seconds); break;
            case "stability": command = Stability(report, request.id, request.seconds, request.cycles); break;
            case "quit": Application.Quit(); yield break;
            default: command = null; break;
        }
        // Flatten nested iterators so exceptions always produce a report and failed verdict.
        var stack = new Stack<IEnumerator>(); if (command != null) stack.Push(command);
        Exception failure = command == null ? new InvalidOperationException("Unknown command") : null;
        while (stack.Count > 0 && failure == null) {
            object current = null; bool next = false;
            try { next = stack.Peek().MoveNext(); if (next) current = stack.Peek().Current; }
            catch (Exception error) { failure = error; }
            if (failure != null) break;
            if (!next) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
            if (current is IEnumerator nested) stack.Push(nested); else yield return current;
        }
        while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
        report.AppendLine($"UnityErrorsDuringCommand={errors-startErrors}");
        report.AppendLine(failure == null && errors == startErrors ? "COMMAND PASS" : "COMMAND FAIL " + failure);
        File.WriteAllText(Path.Combine(output, request.id + ".txt"), report.ToString()); busy = false;
    }
    static void Require(bool ok, string reason) { if (!ok) throw new InvalidOperationException(reason); }
    static InstancedIndirectGrassRenderer Grass => InstancedIndirectGrassRenderer.instance;
    IEnumerator Inspect(StringBuilder report)
    {
        yield return new WaitForEndOfFrame();
        var stats=FindObjectsByType<DemoPerformanceStats>().Single();
        var move=FindObjectsByType<MoveCamera>().Single();
        var lights=FindObjectsByType<RotateLight>().Single();
        report.AppendLine($"StatsVisible={stats.visible} font={stats.labelsFont?.name} fps={stats.FramesPerSecond:F3} samples={stats.SampleCount} view={move.CurrentViewIndex} mainLight={lights.mainLight.enabled} pointLight={lights.pointLight.enabled} rotationSpeed={lights.YRotateSpeed} count={Grass.allGrassPos.Count} distance={Grass.drawDistance} batch={Grass.shouldBatchDispatch}");
        Read(Grass,Camera.main,report); Memory(report,"inspect");
    }
    static readonly BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    static uint[] Read(InstancedIndirectGrassRenderer grass, Camera camera, StringBuilder report, bool oracle = false)
    {
        Require(grass.Prepare(camera, out var buffers), "Nonempty grass has no prepared buffers");
        var args = new GraphicsBuffer.IndirectDrawIndexedArgs[1]; buffers.args.GetData(args);
        using var counter = new GraphicsBuffer(GraphicsBuffer.Target.Raw, 1, 4);
        GraphicsBuffer.CopyCount(buffers.visible, counter, 0); var count = new uint[1]; counter.GetData(count);
        Require(args[0].instanceCount == count[0] && count[0] <= grass.allGrassPos.Count, "Counter/args/capacity mismatch");
        var ids = new uint[count[0]]; if (ids.Length > 0) buffers.visible.GetData(ids, 0, 0, ids.Length);
        Require(ids.Distinct().Count() == ids.Length && ids.All(i => i < grass.allGrassPos.Count), "IDs duplicate/out of range");
        Array.Sort(ids);
        if (oracle) {
            var positions = new Vector3[grass.PositionsBuffer.count]; grass.PositionsBuffer.GetData(positions);
            float radius = (float)typeof(InstancedIndirectGrassRenderer).GetMethod("DeformationRadius", Private).Invoke(grass, new object[] { camera });
            var planes = GeometryUtility.CalculateFrustumPlanes(camera.cullingMatrix);
            var expected = Enumerable.Range(0, positions.Length).Where(i => Vector3.Distance(positions[i], camera.transform.position) <= Mathf.Max(0, grass.drawDistance)+radius && planes.All(p => p.GetDistanceToPoint(positions[i]) >= -radius)).Select(i => (uint)i).ToArray();
            Require(ids.SequenceEqual(expected), "GPU IDs differ from CPU oracle");
        }
        report.AppendLine($"count={grass.allGrassPos.Count} append={count[0]} args={args[0].instanceCount} cells={buffers.visibleCells} dispatch={buffers.dispatches} unique/range=PASS oracle={oracle}");
        return ids;
    }
    IEnumerator Switch(UniversalRenderPipelineAsset pipeline)
    {
        Require(pipeline, "Build did not include test pipeline"); QualitySettings.renderPipeline = pipeline;
        yield return null; yield return new WaitForEndOfFrame();
        Require(GraphicsSettings.currentRenderPipeline == pipeline, "Pipeline switch did not take effect");
    }
    IEnumerator Functional(StringBuilder report, string id)
    {
        Require(SceneManager.GetActiveScene().buildIndex == 0 && Camera.main && Grass, "Demo is not first scene");
        Require(FindObjectsByType<Transform>().All(t => t.GetComponents<Component>().All(c => c)), "Null script component");
        var stats = FindObjectsByType<DemoPerformanceStats>().Single();
        Require(stats.labelsFont && stats.labelsFont.HasCharacter('F') && stats.SampleCount > 0, "Builtin font/statistics missing");
        report.AppendLine($"Font={stats.labelsFont.name} fps={stats.FramesPerSecond:F3} allocated={stats.AllocatedBytes} shaders={Grass.instanceMaterial.shader.name} supported={Grass.instanceMaterial.shader.isSupported}");
        var volume = GraphicsSettings.GetRenderPipelineSettings<URPDefaultVolumeProfileSettings>().volumeProfile;
        Require(volume && volume.components.Count == 19 && volume.components.All(c => c), "Default Volume active list invalid");
        report.AppendLine("DefaultVolume active components=19 all valid; orphan serialized subassets left unchanged");
        volume.TryGet<DepthOfField>(out var dof); volume.TryGet<PaniniProjection>(out var panini);
        Require(dof != null && !dof.IsActive() && panini != null && !panini.IsActive(), "Unexpected active DOF/Panini with stripped shaders");
        report.AppendLine($"Default Volume DOF={dof.mode.value} IsActive={dof.IsActive()}; Panini distance={panini.distance.value} IsActive={panini.IsActive()}; official inactive shader stripping is expected");
        foreach(string name in new[] {"MobileDrawMeshInstancedIndirect/SingleGrass","URPMobileGrassInstancedIndirectDemo/GrassBendingRTPrePassPS","URPMobileGrassInstancedIndirectDemo/SkyBox"})
            Require(Shader.Find(name) && Shader.Find(name).isSupported,"Required shader unsupported: "+name);
        Require(LightmapSettings.lightmaps.Length > 0 && LightmapSettings.lightmaps[0].lightmapColor, "Lightmaps stripped");
        var probes = LightmapSettings.lightProbes;
        Require(probes && probes.count == 8 && probes.bakedProbes.All(p => Enumerable.Range(0,27).All(i => !float.IsNaN(p[i/9,i%9]) && !float.IsInfinity(p[i/9,i%9]))), "Probe data missing/nonfinite");
        Require(ReflectionProbe.defaultTexture,"Default baked reflection missing");
        report.AppendLine($"GI lightmaps={LightmapSettings.lightmaps.Length} mappedRenderers={FindObjectsByType<Renderer>().Count(r => r.lightmapIndex >= 0 && r.lightmapIndex < LightmapSettings.lightmaps.Length)} probes={probes.count} reflection={ReflectionProbe.defaultTexture.name}");
        var move = FindObjectsByType<MoveCamera>().Single(); move.enabled = false;
        var generator = Grass.GetComponent<InstancedIndirectGrassPosDefine>();
        var animators = FindObjectsByType<Animator>(); var before = animators.Select(a => a.transform.position).ToArray();
        Require(animators.Length == 4 && FindObjectsByType<TrailRenderer>().Length == 4, "Characters/trails missing");
        yield return new WaitForSecondsRealtime(1);
        Require(animators.Where((a,i) => Vector3.Distance(before[i],a.transform.position) > .01f).Count() == 4, "Animation not running");
        foreach (var pipeline in new[] { pc, mobile }) {
            yield return Switch(pipeline); report.AppendLine("Pipeline="+pipeline.name+" scale="+pipeline.renderScale);
            for (int i=0;i<move.allCameraTransforms.Count;i++) {
                Camera.main.transform.SetPositionAndRotation(move.allCameraTransforms[i].position, move.allCameraTransforms[i].rotation);
                yield return new WaitForEndOfFrame(); report.AppendLine("Camera="+i); Read(Grass, Camera.main, report, true);
            }
            Camera.main.transform.SetPositionAndRotation(move.allCameraTransforms[0].position,move.allCameraTransforms[0].rotation);
            yield return new WaitForEndOfFrame();
            Grass.shouldBatchDispatch = true; var a = Read(Grass,Camera.main,report);
            Grass.shouldBatchDispatch = false; Require(a.SequenceEqual(Read(Grass,Camera.main,report)),"Batch sets differ"); Grass.shouldBatchDispatch = true;
            foreach (float distance in new[] {25f,200f,125f}) { generator.drawDistance = distance; yield return null; yield return new WaitForEndOfFrame(); Read(Grass,Camera.main,report,true); }
            yield return new WaitForEndOfFrame(); Bending(report,id+"-"+pipeline.name);
            var trails=FindObjectsByType<TrailRenderer>();
            try {
                for(int i=0;i<trails.Length;i++) {
                    for(int j=0;j<trails.Length;j++) trails[j].enabled=j==i;
                    yield return null; yield return new WaitForEndOfFrame(); Bending(report,id+"-"+pipeline.name+"-trail"+i);
                }
            } finally { foreach(var trail in trails) trail.enabled=true; }
            ScreenCapture.CaptureScreenshot(Path.Combine(output,id+"-"+pipeline.name+"-game.png")); yield return null;
        }
        yield return Switch(pc);
        var recovering=FindObjectsByType<TrailRenderer>();
        try {
            foreach(var trail in recovering) trail.emitting=false;
            double recoveryStart=Time.realtimeSinceStartupAsDouble;
            yield return new WaitForSecondsRealtime(5.3f); yield return new WaitForEndOfFrame();
            Bending(report,id+"-recovered",true); report.AppendLine($"Recovery actualSeconds={Time.realtimeSinceStartupAsDouble-recoveryStart:F3}");
        } finally { foreach(var trail in recovering) trail.emitting=true; }
        foreach (int count in new[] {3000000,10000000,1000000}) { generator.instanceCount = count; yield return null; yield return new WaitForEndOfFrame(); Read(Grass,Camera.main,report); Memory(report,"capacity "+count); }
        // A dedicated renderer checks thread-group tails without changing the production GPU count.
        var original = Grass; var root = new GameObject("Player boundary validation"); var test = root.AddComponent<InstancedIndirectGrassRenderer>();
        test.instanceMaterial = original.instanceMaterial; test.cullingComputeShader = original.cullingComputeShader; test.drawDistance = 200;
        try {
            foreach (int count in new[] {0,1,63,64,65,127,128,129,5003}) {
                test.SetPositions(Enumerable.Repeat(Camera.main.transform.position + Camera.main.transform.forward*20,count).ToList());
                if (count==0) Require(!test.Prepare(Camera.main,out _) && test.PositionsBuffer==null,"Empty data allocated");
                else Require(Read(test,Camera.main,report,true).Length==count,"Boundary lost IDs");
            }
        } finally { Destroy(root); InstancedIndirectGrassRenderer.instance = original; }
        move.enabled = true; report.AppendLine("Player rendering, font, GI resources, 4 animations, 8 views, PC/Mobile, counts/distances/batch and GPU boundaries PASS");
    }
    sealed class CopyData { public TextureHandle source; }
    void Bending(StringBuilder report, string id, bool white = false)
    {
        // RenderGraph owns transient textures. Inspect a declared graph copy instead of trying
        // to retrieve a transient handle or command-buffer globals through CPU Shader getters.
        var target = new RenderTexture(960,600,24,RenderTextureFormat.ARGB32); target.Create();
        var texture = new RenderTexture(512,512,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear); texture.Create();
        var normals = new RenderTexture(960,600,0,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear); normals.Create();
        var bendHandle=RTHandles.Alloc(texture); var normalHandle=RTHandles.Alloc(normals);
        bool produced=false,hasNormals=false; string format="";
        var previousCallback=GrassBendingRTPrePass.ValidationRecord;
        GrassBendingRTPrePass.ValidationRecord=(graph,source,camera,kind)=> {
            if(camera!=Camera.main)return;
            if(kind=="bending"){produced=true;format=graph.GetTextureDesc(source).colorFormat.ToString();}else hasNormals=true;
            using var builder=graph.AddRasterRenderPass<CopyData>("Player validation copy "+kind,out var data);
            data.source=source; builder.UseTexture(source,AccessFlags.Read);
            builder.SetRenderAttachment(graph.ImportTexture(kind=="bending"?bendHandle:normalHandle),0,AccessFlags.Write);
            builder.SetRenderFunc(static(CopyData pass,RasterGraphContext context)=>Blitter.BlitTexture(context.cmd,pass.source,new Vector4(1,1,0,0),0,false));
        };
        var previous = RenderTexture.active; var copy = new Texture2D(texture.width,texture.height,TextureFormat.RGBA32,false,true);
        try {
            RenderPipeline.SubmitRenderRequest(Camera.main,new UniversalRenderPipeline.SingleCameraRequest {destination=target});
            Require(produced,"Bending graph producer absent");
            Require(GraphicsSettings.currentRenderPipeline!=pc || hasNormals,"PC grass depth/normals consumer absent");
            RenderTexture.active=texture; copy.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0); copy.Apply(); var pixels=copy.GetPixels32();
            Require(white ? pixels.All(p=>p.r==255) : pixels.Any(p => p.r < 254), white ? "Bending did not recover to white" : "Bending texture is all white");
            report.AppendLine($"Bending format={format} size={texture.width}x{texture.height} nonwhite={pixels.Count(p=>p.r<254)} min={pixels.Min(p=>p.r)} graphCopy=True normalsConsumer={hasNormals}");
            File.WriteAllBytes(Path.Combine(output,id+"-bending.png"),copy.EncodeToPNG());
        } finally {
            GrassBendingRTPrePass.ValidationRecord=previousCallback; RenderTexture.active=previous;
            bendHandle.Release(); normalHandle.Release(); Destroy(copy); Destroy(target); Destroy(texture); Destroy(normals);
        }
    }
    static void Memory(StringBuilder report,string label)
    {
        report.AppendLine($"Memory {label} realtime={Time.realtimeSinceStartupAsDouble:F3} allocated={Profiler.GetTotalAllocatedMemoryLong()} reserved={Profiler.GetTotalReservedMemoryLong()} graphicsEstimate={Profiler.GetAllocatedMemoryForGraphicsDriver()} managed={GC.GetTotalMemory(false)} meshes={Resources.FindObjectsOfTypeAll<Mesh>().Count(m=>m.name=="Procedural Grass Blade")} RTs={Resources.FindObjectsOfTypeAll<RenderTexture>().Length}");
    }
    IEnumerator ProfileHigh(StringBuilder report,string id,int seconds)
    {
        var generator=Grass.GetComponent<InstancedIndirectGrassPosDefine>(); generator.instanceCount=10000000;
        yield return null; yield return Profile(report,id,pc,seconds); generator.instanceCount=1000000; yield return null;
    }
    IEnumerator Profile(StringBuilder report,string id,UniversalRenderPipelineAsset pipeline,int seconds)
    {
        yield return Switch(pipeline); yield return new WaitForSecondsRealtime(5);
        var handles = new List<ProfilerRecorderHandle>(); ProfilerRecorderHandle.GetAvailable(handles);
        File.WriteAllLines(Path.Combine(output,id+"-markers.txt"),handles.Select(h=>ProfilerRecorderHandle.GetDescription(h)).Select(d=>$"{d.Category.Name}\t{d.Name}\t{d.UnitType}"));
        using var main = ProfilerRecorder.StartNew(ProfilerCategory.Internal,"Main Thread",1);
        using var render = ProfilerRecorder.StartNew(ProfilerCategory.Internal,"Render Thread",1);
        using var gpu = ProfilerRecorder.StartNew(ProfilerCategory.Render,"GPU Frame Time",1);
        using var cell = ProfilerRecorder.StartNew(ProfilerCategory.Scripts,"Grass CPU Cell Culling",1);
        report.AppendLine($"ProfilerRecorders Main={main.Valid} Render={render.Valid} GPU={gpu.Valid} Cell={cell.Valid} frameTiming={FrameTimingManager.IsFeatureEnabled()} pipeline={pipeline.name} scale={pipeline.renderScale} count={Grass.allGrassPos.Count} target={Application.targetFrameRate} vSync={QualitySettings.vSyncCount}");
        var timings=new FrameTiming[1]; ulong lastStamp=0; int rows=0,unique=0;
        var csv = new StringBuilder("realtime,mainNs,renderNs,gpuNs,cellNs,frameStart,cpuFrameMs,cpuMainMs,cpuRenderMs,gpuMs,allocated,reserved,graphicsEstimate\n");
        double start=Time.realtimeSinceStartupAsDouble;
        // A bounded binary Profiler capture accompanies the marker CSV, avoiding unbounded log growth.
        Profiler.logFile=Path.Combine(output,id+".raw"); Profiler.enableBinaryLog=true; Profiler.enabled=true;
        try {
            while(Time.realtimeSinceStartupAsDouble-start<Math.Max(5,seconds)) {
                FrameTimingManager.CaptureFrameTimings(); yield return null;
                uint n=FrameTimingManager.GetLatestTimings(1,timings); var t=timings[0];
                if(n>0 && t.frameStartTimestamp!=lastStamp) { unique++; lastStamp=t.frameStartTimestamp; }
                csv.AppendLine(FormattableString.Invariant($"{Time.realtimeSinceStartupAsDouble:F6},{main.LastValue},{render.LastValue},{gpu.LastValue},{cell.LastValue},{(n>0?t.frameStartTimestamp:0)},{t.cpuFrameTime:F6},{t.cpuMainThreadFrameTime:F6},{t.cpuRenderThreadFrameTime:F6},{t.gpuFrameTime:F6},{Profiler.GetTotalAllocatedMemoryLong()},{Profiler.GetTotalReservedMemoryLong()},{Profiler.GetAllocatedMemoryForGraphicsDriver()}")); rows++;
                if(Profiler.enabled && Time.realtimeSinceStartupAsDouble-start>10) Profiler.enabled=false;
            }
        } finally { Profiler.enabled=false; Profiler.enableBinaryLog=false; }
        File.WriteAllText(Path.Combine(output,id+".csv"),csv.ToString());
        report.AppendLine($"Profile actualSeconds={Time.realtimeSinceStartupAsDouble-start:F3} rows={rows} uniqueFrameTimings={unique}; CSV units explicit; Main Thread includes waits; graphics estimate is not dedicated VRAM usage");
        Memory(report,"profile-end");
    }
    IEnumerator Stability(StringBuilder report,string id,int seconds,int cycles)
    {
        yield return Switch(pc);
        var generator=Grass.GetComponent<InstancedIndirectGrassPosDefine>(); generator.instanceCount=1000000;
        yield return null; yield return new WaitForSecondsRealtime(5);
        Memory(report,"start");
        for(int i=0;i<Math.Max(1,cycles);i++) {
            var grass=Grass; var oldMesh=grass.GrassMesh; var oldPosition=grass.PositionsBuffer;
            Require(grass.TryGetCameraBuffers(Camera.main,out var oldCamera),"No camera buffers before disable");
            var oldVisible=oldCamera.visible; var oldArgs=oldCamera.args;
            grass.enabled=false; yield return null; yield return new WaitForEndOfFrame();
            Require(grass.PositionsBuffer==null && !grass.GrassMesh && !oldMesh && !oldPosition.IsValid() && !oldVisible.IsValid() && !oldArgs.IsValid(),"GPU buffer/Mesh not released");
            Require(Shader.GetGlobalFloat("_GrassBendingEnabled")==0,"Disabled grass still consumes bending");
            grass.enabled=true; yield return null; yield return new WaitForEndOfFrame(); Require(Read(grass,Camera.main,report).Length>0,"Re-enable blank");
            if(i%5==0) { yield return Switch((i/5)%2==0?mobile:pc); yield return Resources.UnloadUnusedAssets(); GC.Collect(); Memory(report,"cycle "+i); }
        }
        for(int i=0;i<5;i++) {
            var grass=Grass; var old=grass.PositionsBuffer; var oldMesh=grass.GrassMesh;
            yield return SceneManager.LoadSceneAsync(1); yield return null; yield return Resources.UnloadUnusedAssets(); GC.Collect();
            Require(!grass && !old.IsValid() && !oldMesh && !InstancedIndirectGrassRenderer.instance,"Scene unload retained grass resources");
            Memory(report,"unloaded "+i);
            yield return SceneManager.LoadSceneAsync(0); yield return null; yield return new WaitForEndOfFrame();
            // The injected duplicate destroys itself in Awake; this persistent runner owns requests.
            Require(Read(Grass,Camera.main,report).Length>0,"Reload blank");
        }
        yield return Switch(pc); yield return Resources.UnloadUnusedAssets(); GC.Collect();
        double start=Time.realtimeSinceStartupAsDouble, next=0; int sample=0;
        using var memory=new StreamWriter(Path.Combine(output,id+"-soak.txt"));
        while(Time.realtimeSinceStartupAsDouble-start<Math.Max(5,seconds)) {
            if(Time.realtimeSinceStartupAsDouble-start>=next) {
                var line=new StringBuilder(); Memory(line,"soak "+sample++); memory.Write(line); memory.Flush();
                Read(Grass,Camera.main,line); // Sparse functional reads are intentionally excluded from performance runs.
                next+=30;
            }
            yield return null;
        }
        yield return Resources.UnloadUnusedAssets(); GC.Collect(); yield return null;
        Memory(report,"end"); report.AppendLine($"Repeated disables={cycles} scene roundtrips=5 soakActualSeconds={Time.realtimeSinceStartupAsDouble-start:F3}");
        yield return new WaitForEndOfFrame(); Bending(report,id+"-final");
    }
}
#endif
