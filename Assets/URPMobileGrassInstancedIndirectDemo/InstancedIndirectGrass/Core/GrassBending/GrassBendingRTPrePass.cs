using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public class GrassBendingRTPrePass : ScriptableRendererFeature
{
#if UNITY_EDITOR
    // Opt-in graph copies for GPU inspection. No readback or persistent RT in player builds.
    public static System.Action<RenderGraph, TextureHandle, Camera, string> ValidationRecord;
#endif
    BendingPass bending;
    GrassDepthPass depth;
    public override void Create()
    {
        bending = new BendingPass { renderPassEvent = RenderPassEvent.BeforeRenderingPrePasses };
        depth = new GrassDepthPass { renderPassEvent = RenderPassEvent.AfterRenderingPrePasses };
        // Supplement the prepass requested by the active renderer (PC SSAO). Do not force
        // an otherwise unused depth/normal prepass on the Mobile renderer.
    }
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (renderingData.cameraData.cameraType == CameraType.Preview || renderingData.cameraData.renderType == CameraRenderType.Overlay) return;
        renderer.EnqueuePass(bending);
        renderer.EnqueuePass(depth);
    }
    protected override void Dispose(bool disposing) { Shader.SetGlobalFloat("_GrassBendingEnabled", 0); }
    class BendingPass : ScriptableRenderPass
    {
        static readonly int TextureID = Shader.PropertyToID("_GrassBendingRT");
        class PassData { public RendererListHandle list; public Matrix4x4 vp; }
        public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
        {
            var grass = InstancedIndirectGrassRenderer.instance;
            if (!grass || !grass.isActiveAndEnabled || grass.PositionsBuffer == null) return;
            var camera = frameData.Get<UniversalCameraData>().camera;
            if (!grass.TryGetCameraBuffers(camera, out _)) return;
            if (!camera.TryGetCullingParameters(out var parameters)) return;
            var area = grass.BendingArea;
            var bounds = grass.PositionBounds;
            // Preserve the source's root +/- 0.5m interaction slab, extended over actual root Y.
            float top = bounds.max.y + 1;
            var origin = new Vector3(area.x, top, area.y);
            // Camera right = +X, camera up = +Z. Use a dedicated shader matrix, never mutate camera state.
            var view = Matrix4x4.Scale(new Vector3(1, 1, -1)) * Matrix4x4.TRS(origin, Quaternion.LookRotation(Vector3.down, Vector3.forward), Vector3.one).inverse;
            var projection = Matrix4x4.Ortho(-area.z / 2, area.z / 2, -area.w / 2, area.w / 2, .5f, bounds.size.y + 1.5f);
            var logicalVP = projection * view;
            parameters.cullingMatrix = logicalVP;
            parameters.origin = origin;
            parameters.isOrthographic = true;
            parameters.cullingOptions = CullingOptions.None; // No main-view occlusion, lighting or shadow filtering.
            parameters.cullingPlaneCount = 6;
            var planes = GeometryUtility.CalculateFrustumPlanes(logicalVP);
            for (int i = 0; i < 6; i++) parameters.SetCullingPlane(i, planes[i]);
            var cull = frameData.Get<CullContextData>().Cull(ref parameters);
            var drawing = new DrawingSettings(new ShaderTagId("GrassBending"), new SortingSettings(camera) { criteria = SortingCriteria.CommonTransparent }) { perObjectData = PerObjectData.None };
            var list = graph.CreateRendererList(new RendererListParams(cull, drawing, new FilteringSettings(RenderQueueRange.all, camera.cullingMask)));
            var format = GraphicsFormat.R8_UNorm;
            if (!SystemInfo.IsFormatSupported(format, GraphicsFormatUsage.Render) || !SystemInfo.IsFormatSupported(format, GraphicsFormatUsage.Blend)) format = GraphicsFormat.R8G8B8A8_UNorm;
            var texture = graph.CreateTexture(new TextureDesc(512, 512) { name = "Grass Bending (linear white)", colorFormat = format, clearBuffer = true, clearColor = Color.white, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp });
            using (var builder = graph.AddRasterRenderPass<PassData>("Grass Bending Trails", out var data))
            {
                data.list = list;
                // Convert to the RT's GPU projection (including platform Y and reversed Z).
                data.vp = GL.GetGPUProjectionMatrix(projection, true) * view;
                builder.UseRendererList(list);
                builder.SetRenderAttachment(texture, 0, AccessFlags.Write);
                builder.SetGlobalTextureAfterPass(texture, TextureID);
                builder.AllowGlobalStateModification(true);
                builder.AllowPassCulling(false); // White must also be published for an empty RendererList.
                builder.SetRenderFunc(static (PassData pass, RasterGraphContext context) => {
                    context.cmd.SetGlobalMatrix("_GrassBendingVP", pass.vp);
                    context.cmd.SetGlobalFloat("_GrassBendingEnabled", 1);
                    context.cmd.DrawRendererList(pass.list);
                });
            }
#if UNITY_EDITOR
            ValidationRecord?.Invoke(graph, texture, camera, "bending");
#endif
        }
    }
    class GrassDepthPass : ScriptableRenderPass
    {
        class PassData { public Mesh mesh; public Material material; public GraphicsBuffer args; public MaterialPropertyBlock properties; public int pass; }
        public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
        {
            var grass = InstancedIndirectGrassRenderer.instance;
            var camera = frameData.Get<UniversalCameraData>().camera;
            if (!grass || !grass.TryGetCameraBuffers(camera, out var buffers)) return;
            var resources = frameData.Get<UniversalResourceData>();
            if (!resources.cameraDepthTexture.IsValid() || graph.GetTextureDesc(resources.cameraDepthTexture).depthBufferBits == DepthBits.None) return;
            bool hasNormals = resources.cameraNormalsTexture.IsValid();
            using (var builder = graph.AddRasterRenderPass<PassData>("Grass Depth Normals (bent)", out var data))
            {
                data.mesh = grass.GrassMesh; data.material = grass.instanceMaterial; data.args = buffers.args; data.properties = buffers.properties;
                data.pass = data.material.FindPass(hasNormals ? "GrassDepthNormals" : "GrassDepthOnly");
                data.properties.SetMatrix("_GrassDepthVP", GL.GetGPUProjectionMatrix(camera.projectionMatrix, true) * camera.worldToCameraMatrix);
                builder.UseGlobalTexture(Shader.PropertyToID("_GrassBendingRT"), AccessFlags.Read);
                builder.UseBuffer(graph.ImportBuffer(grass.PositionsBuffer), AccessFlags.Read);
                builder.UseBuffer(graph.ImportBuffer(buffers.visible), AccessFlags.Read);
                builder.UseBuffer(graph.ImportBuffer(buffers.args), AccessFlags.Read);
                if (hasNormals) builder.SetRenderAttachment(resources.cameraNormalsTexture, 0, AccessFlags.ReadWrite);
                builder.SetRenderAttachmentDepth(resources.cameraDepthTexture, AccessFlags.ReadWrite);
                builder.SetRenderFunc(static (PassData pass, RasterGraphContext context) =>
                    context.cmd.DrawMeshInstancedIndirect(pass.mesh, 0, pass.material, pass.pass, pass.args, 0, pass.properties));
            }
#if UNITY_EDITOR
            if (hasNormals) ValidationRecord?.Invoke(graph, resources.cameraNormalsTexture, camera, "normals");
#endif
        }
    }
}
