using System;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static partial class GrassMigrationValidation
{
    static void Cameras(StringBuilder report, string id)
    {
        Check(EditorApplication.isPlaying && Camera.main, "Camera tests need demo in Play");
        var camera = Camera.main; var grass = InstancedIndirectGrassRenderer.instance;
        var move = UnityEngine.Object.FindObjectsByType<MoveCamera>().Single();
        Vector3 originalPosition = camera.transform.position; Quaternion originalRotation = camera.transform.rotation;
        var target = new RenderTexture(640, 360, 24, RenderTextureFormat.ARGB32); target.Create();
        GameObject overlayObject = null;
        var data = camera.GetUniversalAdditionalCameraData();
        int originalMask = camera.cullingMask;
        bool originalMSAA = camera.allowMSAA;
        bool originalGrassEnabled = grass.enabled;
        var pipelineAsset = (UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
        float originalRenderScale = pipelineAsset.renderScale;
        UniversalRendererData rendererAsset = null;
        GrassBendingRTPrePass feature = null;
        bool featureActive = true;
        var previousHook = GrassBendingRTPrePass.ValidationRecord;
        try {
            for (int i = 0; i < move.allCameraTransforms.Count; i++) {
                camera.transform.SetPositionAndRotation(move.allCameraTransforms[i].position, move.allCameraTransforms[i].rotation);
                report.AppendLine($"Source camera pose {i} position={camera.transform.position} rotation={camera.transform.eulerAngles}");
                Read(grass, camera, report);
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                SaveTexture(target, Output + id + "-pose-" + i + ".png");
            }
            camera.transform.SetPositionAndRotation(originalPosition, originalRotation);
            RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
            var baseline = SaveTexture(target, Output + id + "-base.png");
            overlayObject = new GameObject("Validation empty overlay camera");
            var overlay = overlayObject.AddComponent<Camera>(); overlay.CopyFrom(camera); overlay.cullingMask = 0;
            overlayObject.transform.SetPositionAndRotation(camera.transform.position, camera.transform.rotation);
            var overlayData = overlay.GetUniversalAdditionalCameraData(); overlayData.renderType = CameraRenderType.Overlay;
            data.cameraStack.Add(overlay);
            bool overlayGrassPass = false;
            GrassBendingRTPrePass.ValidationRecord = (graph, texture, renderCamera, kind) => { if (renderCamera == overlay) overlayGrassPass = true; };
            RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
            var stacked = SaveTexture(target, Output + id + "-overlay.png");
            double delta = baseline.Zip(stacked, (x,y) => Math.Abs(x.r-y.r)+Math.Abs(x.g-y.g)+Math.Abs(x.b-y.b)).Average();
            report.AppendLine("Empty overlay RGB delta=" + delta + "; overlay owns grass buffers=" + grass.TryGetCameraBuffers(overlay, out _));
            Check(!overlayGrassPass && !grass.TryGetCameraBuffers(overlay, out _), "Overlay produced a grass pass or reused base mutable buffers");
            string prefix = GraphicsSettings.currentRenderPipeline.name.StartsWith("PC", StringComparison.Ordinal) ? "PC" : "Mobile";
            rendererAsset = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/" + prefix + "_Renderer.asset");
            feature = rendererAsset.rendererFeatures.OfType<GrassBendingRTPrePass>().Single(); featureActive = feature.isActive;
            feature.SetActive(false); rendererAsset.SetDirty();
            RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
            var unbentStack = SaveTexture(target, Output + id + "-stack-feature-off.png");
            double bendDelta = stacked.Zip(unbentStack, (x,y) => Math.Abs(x.r-y.r)+Math.Abs(x.g-y.g)+Math.Abs(x.b-y.b)).Average();
            report.AppendLine("Same camera stack Feature on/off RGB delta=" + bendDelta);
            Check(bendDelta > .00001, "Camera stack lost bending response");
            feature.SetActive(featureActive); rendererAsset.SetDirty();
            // Attribute empty-overlay image differences; restore the shipped render scale in finally.
            grass.enabled = false;
            foreach (float scale in new[] { originalRenderScale, 1f }) {
                pipelineAsset.renderScale = scale;
                data.cameraStack.Remove(overlay);
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                var nativeBase = SaveTexture(target, Output + id + "-native-base-scale-" + scale + ".png");
                data.cameraStack.Add(overlay);
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                var nativeStack = SaveTexture(target, Output + id + "-native-stack-scale-" + scale + ".png");
                double nativeDelta = nativeBase.Zip(nativeStack, (x,y) => Math.Abs(x.r-y.r)+Math.Abs(x.g-y.g)+Math.Abs(x.b-y.b)).Average();
                report.AppendLine("Native scene only, grass disabled, renderScale=" + scale + " base/overlay RGB delta=" + nativeDelta);
                if (scale == 1f) Check(nativeDelta < .00001, "Native camera stack still differs at unit render scale");
            }
            pipelineAsset.renderScale = originalRenderScale;
            camera.allowMSAA = originalMSAA; grass.enabled = originalGrassEnabled;
            Check(grass.Prepare(camera, out _), "Could not rebuild the cached base-camera set for layer exclusion test");
            bool grassPass = false;
            GrassBendingRTPrePass.ValidationRecord = (graph, texture, renderCamera, kind) => { if (renderCamera == camera) grassPass = true; };
            camera.cullingMask = 0;
            RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
            SaveTexture(target, Output + id + "-mask-zero.png");
            Check(!grassPass && !grass.TryGetCameraBuffers(camera, out _), "Excluded grass layer reused a cached depth/visible set");
            report.AppendLine("Zero culling mask: no grass graph pass or cached buffers consumed PASS");
            report.AppendLine("Source camera poses, final matrices, camera stack isolation PASS");
        } finally {
            camera.cullingMask = originalMask; camera.allowMSAA = originalMSAA; grass.enabled = originalGrassEnabled;
            pipelineAsset.renderScale = originalRenderScale;
            if (feature) { feature.SetActive(featureActive); rendererAsset.SetDirty(); }
            GrassBendingRTPrePass.ValidationRecord = previousHook;
            if (overlayObject) { data.cameraStack.Remove(overlayObject.GetComponent<Camera>()); UnityEngine.Object.DestroyImmediate(overlayObject); }
            camera.transform.SetPositionAndRotation(originalPosition, originalRotation);
            UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
