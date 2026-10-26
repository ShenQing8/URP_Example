using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

[ExecuteAlways]
public class InstancedIndirectGrassRenderer : MonoBehaviour
{
    public float drawDistance = 125;
    public Material instanceMaterial;
    public ComputeShader cullingComputeShader;
    [NonSerialized] public List<Vector3> allGrassPos = new List<Vector3>();
    public static InstancedIndirectGrassRenderer instance;
    public bool shouldBatchDispatch = true;
    // Positions are world space. In-place edits require MarkPositionsDirty, including same-count edits.
    public void SetPositions(List<Vector3> positions) { allGrassPos = positions ?? new List<Vector3>(); MarkPositionsDirty(); }
    public void MarkPositionsDirty() { dirty = true; }
    public sealed class CameraBuffers : IDisposable
    {
        public GraphicsBuffer visible, args;
        public readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        public int visibleCells, dispatches;
        public void Dispose() { visible?.Dispose(); args?.Dispose(); visible = args = null; }
    }
    readonly Dictionary<Camera, CameraBuffers> cameras = new Dictionary<Camera, CameraBuffers>();
    readonly List<Camera> deadCameras = new List<Camera>();
    readonly List<int> visibleCells = new List<int>();
    readonly Plane[] planes = new Plane[6];
    readonly Vector4[] planeVectors = new Vector4[6];
    GraphicsBuffer positionsBuffer;
    List<Vector3> uploadedList;
    int uploadedCount = -1;
    bool dirty = true;
    int[] offsets = Array.Empty<int>();
    Bounds[] cells = Array.Empty<Bounds>();
    Bounds positionBounds;
    Mesh mesh;
    int lastVisibleCells, lastDispatches;
    static readonly int InstanceCountOffset = FindInstanceCountOffset();
    static int FindInstanceCountOffset()
    {
        // In 6000.6 the public members are properties, not marshalable fields. Probe the official
        // structure layout rather than assume a five-uint layout on every supported backend.
        int size = Marshal.SizeOf<GraphicsBuffer.IndirectDrawIndexedArgs>();
        if (size != GraphicsBuffer.IndirectDrawIndexedArgs.size) throw new NotSupportedException("Unexpected indirect argument layout");
        var memory = Marshal.AllocHGlobal(size);
        try {
            const uint marker = 0x12345678;
            Marshal.StructureToPtr(new GraphicsBuffer.IndirectDrawIndexedArgs { instanceCount = marker }, memory, false);
            for (int offset = 0; offset < size; offset += sizeof(uint))
                if (Marshal.ReadInt32(memory, offset) == marker) return offset;
            throw new NotSupportedException("Cannot locate indirect instanceCount");
        } finally { Marshal.FreeHGlobal(memory); }
    }
    public GraphicsBuffer PositionsBuffer => positionsBuffer;
    public bool TryGetCameraBuffers(Camera camera, out CameraBuffers buffers)
    {
        buffers = null;
        return isActiveAndEnabled && camera && instanceMaterial && cullingComputeShader && positionsBuffer != null &&
            (camera.cullingMask & (1 << gameObject.layer)) != 0 && cameras.TryGetValue(camera, out buffers);
    }
    public Mesh GrassMesh => mesh;
    public Bounds PositionBounds => positionBounds;
    public Vector4 BendingArea => new Vector4(positionBounds.center.x, positionBounds.center.z, Mathf.Max(positionBounds.size.x, 1), Mathf.Max(positionBounds.size.z, 1));
    void OnEnable()
    {
        instance = this;
        dirty = true;
        RenderPipelineManager.beginCameraRendering += BeginCamera;
    }
    void BeginCamera(ScriptableRenderContext context, Camera camera)
    {
        Shader.SetGlobalFloat("_GrassBendingEnabled", 0);
        if (camera.cameraType == CameraType.Preview || (camera.cullingMask & (1 << gameObject.layer)) == 0) return;
        if (camera.TryGetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(out var data) && data.renderType == UnityEngine.Rendering.Universal.CameraRenderType.Overlay) return;
        if (!Prepare(camera, out var buffers)) return;
        var bounds = positionBounds;
        bounds.Expand(DeformationRadius(camera) * 2);
        var rp = new RenderParams(instanceMaterial) { camera = camera, layer = gameObject.layer, worldBounds = bounds, matProps = buffers.properties, shadowCastingMode = ShadowCastingMode.Off, receiveShadows = true, lightProbeUsage = LightProbeUsage.BlendProbes };
        Graphics.RenderMeshIndirect(in rp, mesh, buffers.args);
    }
    // Shared with the graph depth consumer and Editor validation. Only GPU CopyCount writes instanceCount.
    public bool Prepare(Camera camera, out CameraBuffers buffers)
    {
        buffers = null;
        if (!isActiveAndEnabled || !camera || !instanceMaterial || !cullingComputeShader || !SystemInfo.supportsComputeShaders || !SystemInfo.supportsInstancing) return false;
        EnsurePositions();
        if (positionsBuffer == null) return false;
        if (!cameras.TryGetValue(camera, out buffers))
        {
            buffers = new CameraBuffers {
                visible = new GraphicsBuffer(GraphicsBuffer.Target.Append, uploadedCount, sizeof(uint)),
                args = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, GraphicsBuffer.IndirectDrawIndexedArgs.size)
            };
            buffers.args.SetData(new[] { new GraphicsBuffer.IndirectDrawIndexedArgs { indexCountPerInstance = mesh.GetIndexCount(0), instanceCount = 0, startIndex = mesh.GetIndexStart(0), baseVertexIndex = mesh.GetBaseVertex(0), startInstance = 0 } });
            cameras.Add(camera, buffers);
        }
        deadCameras.Clear();
        foreach (var pair in cameras) if (!pair.Key) { pair.Value.Dispose(); deadCameras.Add(pair.Key); }
        foreach (var key in deadCameras) cameras.Remove(key);
        float radius = DeformationRadius(camera);
        GeometryUtility.CalculateFrustumPlanes(camera.cullingMatrix, planes);
        for (int i = 0; i < planes.Length; i++) planeVectors[i] = new Vector4(planes[i].normal.x, planes[i].normal.y, planes[i].normal.z, planes[i].distance);
        visibleCells.Clear();
        Profiler.BeginSample("Grass CPU Cell Culling");
        for (int i = 0; i < cells.Length; i++)
        {
            if (offsets[i + 1] == offsets[i]) continue;
            var bounds = cells[i]; bounds.Expand(radius * 2);
            if (GeometryUtility.TestPlanesAABB(planes, bounds) && bounds.SqrDistance(camera.transform.position) <= drawDistance * drawDistance) visibleCells.Add(i);
        }
        Profiler.EndSample();
        buffers.visible.SetCounterValue(0);
        int kernel = cullingComputeShader.FindKernel("CSMain");
        cullingComputeShader.SetBuffer(kernel, "_AllInstancesPosWSBuffer", positionsBuffer);
        cullingComputeShader.SetBuffer(kernel, "_VisibleInstancesOnlyPosWSIDBuffer", buffers.visible);
        cullingComputeShader.SetVectorArray("_FrustumPlanes", planeVectors);
        cullingComputeShader.SetVector("_CameraPositionWS", camera.transform.position);
        cullingComputeShader.SetFloat("_MaxDrawDistance", Mathf.Max(0, drawDistance));
        cullingComputeShader.SetFloat("_DeformationRadius", radius);
        cullingComputeShader.SetInt("_TotalCount", uploadedCount);
        buffers.dispatches = 0;
        for (int i = 0; i < visibleCells.Count; i++)
        {
            int start = offsets[visibleCells[i]], end = offsets[visibleCells[i] + 1];
            if (shouldBatchDispatch) while (i + 1 < visibleCells.Count && visibleCells[i + 1] == visibleCells[i] + 1) end = offsets[visibleCells[++i] + 1];
            while (start < end)
            {
                int length = Mathf.Min(end - start, 65535 * 64);
                cullingComputeShader.SetInt("_StartOffset", start);
                cullingComputeShader.SetInt("_JobLength", length);
                cullingComputeShader.Dispatch(kernel, (length + 63) / 64, 1, 1);
                buffers.dispatches++; start += length;
            }
        }
        GraphicsBuffer.CopyCount(buffers.visible, buffers.args, InstanceCountOffset);
        buffers.visibleCells = visibleCells.Count;
        lastVisibleCells = buffers.visibleCells; lastDispatches = buffers.dispatches;
        buffers.properties.SetBuffer("_AllInstancesTransformBuffer", positionsBuffer);
        buffers.properties.SetBuffer("_VisibleInstanceOnlyTransformIDBuffer", buffers.visible);
        buffers.properties.SetVector("_GrassBendingArea", BendingArea);
        buffers.properties.SetBuffer("unity_IndirectDrawArgs", buffers.args);
        buffers.properties.SetInt("unity_BaseCommandID", 0);
        return true;
    }
    float DeformationRadius(Camera camera)
    {
        float width = Mathf.Abs(instanceMaterial.GetFloat("_GrassWidth"));
        float height = Mathf.Abs(instanceMaterial.GetFloat("_GrassHeight"));
        float wind = 0;
        foreach (string group in new[] { "A", "B", "C" })
        {
            var wrap = instanceMaterial.GetVector("_Wind" + group + "Wrap");
            wind += Mathf.Abs(instanceMaterial.GetFloat("_Wind" + group + "Intensity")) * (Mathf.Abs(wrap.x) + Mathf.Abs(wrap.y));
        }
        float farthest = Vector3.Distance(camera.transform.position, positionBounds.center) + positionBounds.extents.magnitude;
        return 2 + width * .5f + 5 * height * (1 + width * .25f) + wind + farthest * .005625f;
    }
    void EnsurePositions()
    {
        int count = allGrassPos?.Count ?? 0;
        if (!dirty && uploadedList == allGrassPos && uploadedCount == count) return;
        ReleaseBuffers(); dirty = false; uploadedList = allGrassPos; uploadedCount = count;
        if (count == 0) { cells = Array.Empty<Bounds>(); offsets = Array.Empty<int>(); return; }
        positionBounds = new Bounds(allGrassPos[0], Vector3.zero);
        foreach (var p in allGrassPos) positionBounds.Encapsulate(p);
        int nx = Mathf.Max(1, Mathf.CeilToInt(positionBounds.size.x / 10));
        int nz = Mathf.Max(1, Mathf.CeilToInt(positionBounds.size.z / 10));
        int cellCount = checked(nx * nz);
        var counts = new int[cellCount];
        int Cell(Vector3 p) {
            int x = Mathf.Clamp(Mathf.FloorToInt(Mathf.InverseLerp(positionBounds.min.x, positionBounds.max.x, p.x) * nx), 0, nx - 1);
            int z = Mathf.Clamp(Mathf.FloorToInt(Mathf.InverseLerp(positionBounds.min.z, positionBounds.max.z, p.z) * nz), 0, nz - 1);
            return x + z * nx;
        }
        foreach (var p in allGrassPos) counts[Cell(p)]++;
        offsets = new int[cellCount + 1];
        for (int i = 0; i < cellCount; i++) offsets[i + 1] = offsets[i] + counts[i];
        var cursors = (int[])offsets.Clone();
        var sorted = new Vector3[count]; cells = new Bounds[cellCount];
        foreach (var p in allGrassPos) {
            int cell = Cell(p);
            if (cursors[cell] == offsets[cell]) cells[cell] = new Bounds(p, Vector3.zero); else cells[cell].Encapsulate(p);
            sorted[cursors[cell]++] = p;
        }
        positionsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, count, 12);
        positionsBuffer.SetData(sorted);
        if (!mesh) {
            mesh = new Mesh { name = "Procedural Grass Blade", hideFlags = HideFlags.HideAndDontSave };
            mesh.vertices = new[] { new Vector3(-.25f, 0), new Vector3(.25f, 0), new Vector3(0, 1) };
            mesh.triangles = new[] { 2, 1, 0 };
        }
    }
    void OnGUI()
    {
        GUI.contentColor = Color.black;
        GUI.Label(new Rect(200, 0, 450, 60), $"After CPU cell frustum culling,\n-Visible cell count = {lastVisibleCells}/{cells.Length}\n-Real compute dispatch count = {lastDispatches}");
        shouldBatchDispatch = GUI.Toggle(new Rect(400, 400, 200, 100), shouldBatchDispatch, "shouldBatchDispatch");
    }
    void ReleaseBuffers() {
        foreach (var buffer in cameras.Values) buffer.Dispose(); cameras.Clear();
        positionsBuffer?.Dispose(); positionsBuffer = null;
    }
    void OnDisable() {
        RenderPipelineManager.beginCameraRendering -= BeginCamera; ReleaseBuffers();
        if (mesh) { if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); } mesh = null;
        if (instance == this) instance = null;
        dirty = true; Shader.SetGlobalFloat("_GrassBendingEnabled", 0);
    }
}
