using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public static partial class GrassMigrationValidation
{
    static TrailRenderer[] originalTrails;
    static bool[] originalTrailEnabled;
    static GameObject[] probeObjects;
    static Mesh probeMesh;
    static bool[] originalTrailEmitting;
    static float recoveryStart;
    static string recoveryId;
    static Camera recoveryCamera;
    static Color[] BendingPixels(Camera camera, string label, StringBuilder report)
    {
        var target = new RenderTexture(320, 180, 24, RenderTextureFormat.ARGB32); target.Create();
        var copy = new RenderTexture(512, 512, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear); copy.Create();
        var handle = RTHandles.Alloc(copy);
        bool recorded = false;
        var previous = GrassBendingRTPrePass.ValidationRecord;
        GrassBendingRTPrePass.ValidationRecord = (graph, texture, renderCamera, kind) => {
            if (renderCamera != camera || kind != "bending") return;
            recorded = true;
            using var builder = graph.AddRasterRenderPass<CopyData>("Validation trail pixel copy", out var data);
            data.source = texture; builder.UseTexture(texture, AccessFlags.Read);
            builder.SetRenderAttachment(graph.ImportTexture(handle), 0, AccessFlags.Write);
            builder.SetRenderFunc(static (CopyData pass, RasterGraphContext context) => Blitter.BlitTexture(context.cmd, pass.source, new Vector4(1, 1, 0, 0), 0, false));
        };
        try {
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            Check(recorded, "No bending pass during " + label);
            var pixels = SaveTexture(copy, Output + label + "-bending.png");
            report.AppendLine($"{label}: red min={pixels.Min(pixel => pixel.r):F5} max={pixels.Max(pixel => pixel.r):F5} nonwhite={pixels.Count(pixel => pixel.r < .99f)}");
            return pixels;
        } finally {
            GrassBendingRTPrePass.ValidationRecord = previous;
            handle.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(copy);
        }
    }
    static void Interaction(StringBuilder report, string id)
    {
        Check(EditorApplication.isPlaying && Camera.main, "Interaction test needs demo in Play");
        Check(originalTrails == null, "A recovery test is already active");
        originalTrails = UnityEngine.Object.FindObjectsByType<TrailRenderer>();
        originalTrailEnabled = originalTrails.Select(trail => trail.enabled).ToArray();
        originalTrailEmitting = originalTrails.Select(trail => trail.emitting).ToArray();
        Check(originalTrails.Length == 4, "Expected four original actor trails");
        var camera = Camera.main;
        var grass = InstancedIndirectGrassRenderer.instance;
        var cameraObject = new GameObject("Validation second camera") { hideFlags = HideFlags.HideAndDontSave };
        bool recoveryScheduled = false;
        try {
            foreach (var trail in originalTrails) trail.enabled = false;
            Check(BendingPixels(camera, id + "-empty", report).All(pixel => pixel.r == 1), "No-trail texture is not white");
            for (int i = 0; i < originalTrails.Length; i++) {
                var trail = originalTrails[i]; trail.enabled = true;
                var pixels = BendingPixels(camera, id + "-actor" + i, report);
                Check(pixels.Any(pixel => pixel.r < .99f), "Actor did not contribute a track: " + i);
                Check(trail.positionCount > 0, "Original trail has no runtime points");
                var tip = trail.GetPosition(trail.positionCount - 1);
                var area = grass.BendingArea;
                int x = Mathf.RoundToInt(((tip.x - area.x) / area.z + .5f) * 511);
                int y = Mathf.RoundToInt(((tip.z - area.y) / area.w + .5f) * 511);
                float localMin = 1;
                for (int dy = -4; dy <= 4; dy++) for (int dx = -4; dx <= 4; dx++)
                    if (x + dx >= 0 && x + dx < 512 && y + dy >= 0 && y + dy < 512) localMin = Mathf.Min(localMin, pixels[(y + dy) * 512 + x + dx].r);
                report.AppendLine($"Actor {i} newest world={tip} expected texel=({x},{y}) local red min={localMin:F4} lifetime={trail.time} points={trail.positionCount}");
                Check(localMin < .99f, "Track projection/UV mismatch at actor " + i);
                trail.enabled = false;
            }
            foreach (var trail in originalTrails) trail.enabled = true;
            var front = BendingPixels(camera, id + "-all", report);
            var second = cameraObject.AddComponent<Camera>(); second.CopyFrom(camera); second.enabled = false;
            second.gameObject.AddComponent<UniversalAdditionalCameraData>();
            second.transform.SetPositionAndRotation(camera.transform.position, camera.transform.rotation * Quaternion.Euler(0, 180, 0));
            var back = BendingPixels(second, id + "-offscreen-second-camera", report);
            Check(front.Zip(back, (a, b) => Mathf.Abs(a.r - b.r)).Max() < .005, "Bending changed with the main-view frustum");
            Check(grass.TryGetCameraBuffers(camera, out var firstBuffers) && grass.TryGetCameraBuffers(second, out var secondBuffers) && firstBuffers.visible != secondBuffers.visible && firstBuffers.args != secondBuffers.args, "Cameras share mutable GPU visible/args buffers");
            report.AppendLine("Opposite-view camera produces same track pixels; visible/args buffer identities are isolated PASS");
            foreach (var trail in originalTrails) trail.enabled = false;
            // Controlled flat geometry isolates the real Trail shader's multiplicative blend.
            // The four original TrailRenderers supply the separate lifetime/recovery test.
            probeMesh = new Mesh { name = "Validation blend plane" };
            probeMesh.vertices = new[] { new Vector3(-12, 0, -12), new Vector3(12, 0, -12), new Vector3(12, 0, 12), new Vector3(-12, 0, 12) };
            probeMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            probeMesh.colors = Enumerable.Repeat(new Color(.5f, .5f, .5f, 1), 4).ToArray();
            probeMesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            probeObjects = new GameObject[2];
            for (int i = 0; i < 2; i++) {
                probeObjects[i] = new GameObject("Validation Trail shader overlap " + i);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(probeObjects[i], grass.gameObject.scene);
                probeObjects[i].transform.position = grass.PositionBounds.center;
                probeObjects[i].AddComponent<MeshFilter>().sharedMesh = probeMesh;
                var renderer = probeObjects[i].AddComponent<MeshRenderer>();
                renderer.sharedMaterial = originalTrails[0].sharedMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
            recoveryStart = Time.time; recoveryId = id; recoveryCamera = camera;
            // Let the engine register the temporary renderers before reading their actual GPU blend.
            EditorApplication.update += OverlapUpdate;
            recoveryScheduled = true;
            report.AppendLine("Overlap and 5-second lifetime recovery scheduled after a real engine frame; separate reports follow.");
        } finally {
            UnityEngine.Object.DestroyImmediate(cameraObject);
            if (!recoveryScheduled) RestoreTrails();
        }
    }
    static void OverlapUpdate()
    {
        if (!EditorApplication.isPlaying) { EditorApplication.update -= OverlapUpdate; RestoreTrails(); return; }
        if (Time.time - recoveryStart < .15f) return;
        EditorApplication.update -= OverlapUpdate;
        var report = new StringBuilder();
        try {
            probeObjects[1].GetComponent<MeshRenderer>().enabled = false;
            float single = BendingPixels(recoveryCamera, recoveryId + "-overlap-single", report).Min(pixel => pixel.r);
            probeObjects[1].GetComponent<MeshRenderer>().enabled = true;
            float overlap = BendingPixels(recoveryCamera, recoveryId + "-overlap-double", report).Min(pixel => pixel.r);
            report.AppendLine($"Overlap single={single:F5} double={overlap:F5} expected product={single * single:F5}; real elapsed={Time.time - recoveryStart:F3}s");
            Check(single > .4 && single < .6 && Mathf.Abs(overlap - single * single) < .015, "Multiplicative blend failed");
            report.AppendLine("OVERLAP PASS");
            foreach (var obj in probeObjects) obj.transform.position += Vector3.up * 2;
            Check(BendingPixels(recoveryCamera, recoveryId + "-above-interaction-slab", report).All(pixel => pixel.r == 1), "Track outside the source +/-0.5m root slab still bends grass");
            report.AppendLine("Original vertical interaction slab exclusion PASS");
            foreach (var obj in probeObjects) UnityEngine.Object.DestroyImmediate(obj);
            probeObjects = null;
            UnityEngine.Object.DestroyImmediate(probeMesh); probeMesh = null;
            foreach (var trail in originalTrails) { trail.enabled = true; trail.emitting = false; }
            recoveryStart = Time.time;
            report.AppendLine("Original four trails stopped emitting; real lifetime recovery begins now.");
            EditorApplication.update += RecoveryUpdate;
        } catch (Exception exception) { report.AppendLine("OVERLAP FAIL " + exception); RestoreTrails(); }
        File.WriteAllText(Output + recoveryId + "-overlap.txt", report.ToString());
    }
    static void RecoveryUpdate()
    {
        if (!EditorApplication.isPlaying) { EditorApplication.update -= RecoveryUpdate; RestoreTrails(); return; }
        if (Time.time - recoveryStart < 5.2f) return;
        EditorApplication.update -= RecoveryUpdate;
        var report = new StringBuilder(); report.AppendLine($"{DateTimeOffset.Now:O} actual Play elapsed={Time.time - recoveryStart:F3}s");
        try {
            var pixels = BendingPixels(recoveryCamera, recoveryId + "-recovered", report);
            Check(pixels.All(pixel => pixel.r == 1), "Trails did not recover to white after their real lifetime");
            report.AppendLine("RECOVERY PASS");
        } catch (Exception exception) { report.AppendLine("RECOVERY FAIL " + exception); }
        finally { RestoreTrails(); File.WriteAllText(Output + recoveryId + "-recovery.txt", report.ToString()); }
    }
    static void RestoreTrails()
    {
        if (probeObjects != null) foreach (var obj in probeObjects) if (obj) UnityEngine.Object.DestroyImmediate(obj);
        if (probeMesh) UnityEngine.Object.DestroyImmediate(probeMesh); probeMesh = null;
        if (originalTrails != null) for (int i = 0; i < originalTrails.Length; i++) if (originalTrails[i]) { originalTrails[i].enabled = originalTrailEnabled[i]; originalTrails[i].emitting = originalTrailEmitting[i]; }
        originalTrails = null; probeObjects = null;
    }
}
