using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

public static partial class GrassMigrationValidation
{
    static void Capacity(StringBuilder report)
    {
        Check(!EditorApplication.isPlaying, "Run capacity test outside Play");
        var previous = InstancedIndirectGrassRenderer.instance;
        var root = new GameObject("Capacity validation") { hideFlags = HideFlags.HideAndDontSave };
        var cameraObject = new GameObject("Capacity camera") { hideFlags = HideFlags.HideAndDontSave };
        var watch = Stopwatch.StartNew();
        try {
            var renderer = root.AddComponent<InstancedIndirectGrassRenderer>();
            renderer.instanceMaterial = AssetDatabase.LoadAssetAtPath<Material>(Core + "InstancedIndirectGrass.mat");
            renderer.cullingComputeShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(Core + "CullingCompute.compute");
            var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
            camera.transform.position = new Vector3(0, 4, -30); camera.transform.LookAt(new Vector3(0, 1, 50)); camera.farClipPlane = 300;
            report.AppendLine($"Before: managed={GC.GetTotalMemory(true)} Unity allocated={Profiler.GetTotalAllocatedMemoryLong()} reserved={Profiler.GetTotalReservedMemoryLong()}");
            const int count = 10000000;
            var data = new List<Vector3>(count);
            var random = new System.Random(123);
            for (int i = 0; i < count; i++) data.Add(new Vector3((float)random.NextDouble() * 1600 - 800, 0, (float)random.NextDouble() * 1600 - 800));
            renderer.SetPositions(data);
            Check(renderer.Prepare(camera, out var buffers), "Capacity prepare failed");
            var args = new GraphicsBuffer.IndirectDrawIndexedArgs[1]; buffers.args.GetData(args);
            report.AppendLine($"10M actual allocated buffer objects: positions={renderer.PositionsBuffer.count}x{renderer.PositionsBuffer.stride} visible={buffers.visible.count}x{buffers.visible.stride} args={buffers.args.count}x{buffers.args.stride} GPU instanceCount={args[0].instanceCount} cells={buffers.visibleCells} dispatches={buffers.dispatches}");
            report.AppendLine($"After build/cull/synchronized args read: wall ms={watch.ElapsedMilliseconds} managed={GC.GetTotalMemory(false)} Unity allocated={Profiler.GetTotalAllocatedMemoryLong()} reserved={Profiler.GetTotalReservedMemoryLong()}. These are process snapshots and aggregate wall time, not a GPU timing or driver peak measurement.");
            renderer.enabled = false; Check(renderer.PositionsBuffer == null, "Capacity release failed");
            report.AppendLine("Capacity GPU allocation/culling and explicit release PASS");
        } finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(cameraObject); InstancedIndirectGrassRenderer.instance = previous; }
    }
}
