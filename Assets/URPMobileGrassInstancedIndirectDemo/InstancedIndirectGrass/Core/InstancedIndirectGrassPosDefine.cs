using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(InstancedIndirectGrassRenderer))]
public class InstancedIndirectGrassPosDefine : MonoBehaviour
{
    [Range(1, 40000000)] public int instanceCount = 1000000;
    public float drawDistance = 125;
    int cacheCount = -1;
    Matrix4x4 cachedTransform;
    InstancedIndirectGrassRenderer grass;
    void OnEnable() { grass = GetComponent<InstancedIndirectGrassRenderer>(); cacheCount = -1; }
    void Update() { UpdatePosIfNeeded(); if (grass) grass.drawDistance = drawDistance; }
    void OnGUI()
    {
        using var gui = DemoGUI.Begin();
        GUI.Label(new Rect(300, 50, 200, 30), "Instance Count: " + instanceCount / 1000000 + "Million");
        instanceCount = Mathf.Max(1, (int)GUI.HorizontalSlider(new Rect(300, 100, 200, 30), instanceCount / 1000000f, 1, 10)) * 1000000;
        GUI.Label(new Rect(300, 150, 200, 30), "Draw Distance: " + drawDistance);
        drawDistance = Mathf.Max(1, (int)GUI.HorizontalSlider(new Rect(300, 200, 200, 30), drawDistance / 25f, 1, 8)) * 25;
    }
    void UpdatePosIfNeeded()
    {
        if (!grass) return;
        int count = Mathf.Max(0, instanceCount);
        if (cacheCount == count && cachedTransform == transform.localToWorldMatrix) return;
        // Preserve the demo's density on count changes; allow subsequent transform edits to regenerate positions.
        if (cacheCount != count) {
            float scale = Mathf.Sqrt(count / 4f) / 2f;
            transform.localScale = new Vector3(scale, transform.localScale.y, scale);
        }
        var state = Random.state;
        Random.InitState(123);
        var positions = new List<Vector3>(count);
        for (int i = 0; i < count; i++)
            positions.Add(transform.position + new Vector3(Random.Range(-1f, 1f) * transform.lossyScale.x, 0, Random.Range(-1f, 1f) * transform.lossyScale.z));
        Random.state = state;
        grass.SetPositions(positions);
        cacheCount = count; cachedTransform = transform.localToWorldMatrix;
    }
}
