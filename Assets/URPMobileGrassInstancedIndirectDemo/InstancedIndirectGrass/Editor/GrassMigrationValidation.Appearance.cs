using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static partial class GrassMigrationValidation
{
    static void Appearance(StringBuilder report, string id)
    {
        Check(EditorApplication.isPlaying && Camera.main, "Appearance test requires demo in Play");
        var camera = Camera.main;
        var grass = InstancedIndirectGrassRenderer.instance;
        var material = grass.instanceMaterial; var original = new Material(material);
        var lights = UnityEngine.Object.FindObjectsByType<Light>();
        var enabled = lights.Select(light => light.enabled).ToArray();
        var shadows = lights.Select(light => light.shadows).ToArray();
        var position = lights.Select(light => light.transform.position).ToArray();
        var rotation = lights.Select(light => light.transform.rotation).ToArray();
        var intensity = lights.Select(light => light.intensity).ToArray();
        var range = lights.Select(light => light.range).ToArray();
        var lightType = lights.Select(light => light.type).ToArray();
        var renderers = UnityEngine.Object.FindObjectsByType<MeshRenderer>();
        var modes = renderers.Select(renderer => renderer.shadowCastingMode).ToArray();
        var ambientProbe = RenderSettings.ambientProbe;
        bool fog = RenderSettings.fog;
        var target = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32); target.Create();
        GameObject shadowFixture = null;
        Color[] Render(string label) {
            RenderPipeline.SubmitRenderRequest(camera, new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = target });
            return SaveTexture(target, Output + id + "-" + label + ".png");
        }
        void Difference(string label, Color[] a, Color[] b) {
            double delta = a.Zip(b, (x,y) => Math.Abs(x.r-y.r)+Math.Abs(x.g-y.g)+Math.Abs(x.b-y.b)).Average();
            report.AppendLine(label + " RGB absolute mean delta=" + delta.ToString("F8"));
            Check(delta > .000001, "No grass image response: " + label);
        }
        try {
            report.AppendLine("Actual pipeline=" + GraphicsSettings.currentRenderPipeline.name + "; MeshRenderers cast shadows only so image deltas are on grass.");
            foreach (var renderer in renderers) renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            foreach (var light in lights) light.enabled = false;
            var ambient = Render("ambient");
            material.SetColor("_BaseColor", Color.black); material.SetColor("_GroundColor", Color.black);
            Difference("SampleSH(0) ambient contribution with direct lights off", ambient, Render("ambient-black-albedo"));
            material.CopyPropertiesFromMaterial(original);
            RenderSettings.ambientProbe = new SphericalHarmonicsL2();
            var zeroAmbient = Render("ambient-zero");
            report.AppendLine("Ambient-probe override RGB delta=" + ambient.Zip(zeroAmbient, (x,y) => Math.Abs(x.r-y.r)+Math.Abs(x.g-y.g)+Math.Abs(x.b-y.b)).Average() + "; BlendProbes preserves the source API default, baked probe validity belongs to phase 5.");
            RenderSettings.ambientProbe = ambientProbe;
            var main = lights.First(light => light.type == LightType.Directional); main.enabled = true;
            var normal = Render("original");
            foreach (string group in new[] { "A", "B", "C" }) {
                material.SetFloat("_Wind" + group + "Intensity", 0);
                var single = Render("wind-" + group + "-zero"); Difference("Wind " + group, normal, single);
                material.CopyPropertiesFromMaterial(original);
            }
            material.SetTexture("_BaseColorTexture", Texture2D.whiteTexture);
            Difference("World-space color texture", normal, Render("texture-white"));
            material.CopyPropertiesFromMaterial(original);
            material.SetFloat("_GrassHeight", .5f); material.SetFloat("_GrassWidth", 2);
            Difference("Height/width random blade geometry", normal, Render("shape-test"));
            material.CopyPropertiesFromMaterial(original);
            RenderSettings.fog = false; var noFog = Render("fog-off");
            RenderSettings.fog = true; var yesFog = Render("fog-on"); Difference("Fog", noFog, yesFog); RenderSettings.fog = fog;
            main.enabled = false;
            var point = lights.First(light => light.type == LightType.Point);
            point.type = LightType.Directional; point.transform.rotation = Quaternion.Euler(30, 140, 0); point.intensity = .4f; point.enabled = true;
            Difference("Non-main directional light", ambient, Render("additional-directional"));
            point.type = LightType.Point;
            var actor = renderers.Where(renderer => renderer.bounds.size.y > 1 && renderer.bounds.size.y < 20).OrderBy(renderer => Vector3.Distance(camera.transform.position, renderer.bounds.center)).First();
            point.transform.position = actor.bounds.center + new Vector3(4, 4, -4); point.intensity = 15; point.range = 35;
            if (((UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline).supportsAdditionalLightShadows) {
                shadowFixture = GameObject.CreatePrimitive(PrimitiveType.Cube);
                shadowFixture.name = "Validation point shadow caster";
                Vector3 center = camera.transform.position + camera.transform.forward * 12;
                center.y = grass.PositionBounds.center.y + 2;
                shadowFixture.transform.position = center;
                shadowFixture.transform.localScale = new Vector3(8, 4, 8);
                var caster = shadowFixture.GetComponent<MeshRenderer>();
                caster.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/URPMobileGrassInstancedIndirectDemo/InstancedIndirectGrass/TestScene/GrassGround.mat");
                caster.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                point.transform.position = center + new Vector3(3, 8, -3); point.intensity = 200; point.range = 40;
                report.AppendLine($"Point shadow fixture world={center}, size=(8,4,8), light={point.transform.position}, intensity=200 range=40");
                point.shadows = LightShadows.Soft; var withShadow = Render("point-shadow-on");
                point.shadows = LightShadows.None; var withoutShadow = Render("point-shadow-off");
                Difference("Additional point shadow sampling on grass", withShadow, withoutShadow);
            } else report.AppendLine("Additional shadows disabled by original Mobile RPAsset; preserved.");
            Shaders(report);
        } finally {
            if (shadowFixture) UnityEngine.Object.DestroyImmediate(shadowFixture);
            material.CopyPropertiesFromMaterial(original); UnityEngine.Object.DestroyImmediate(original);
            RenderSettings.ambientProbe = ambientProbe; RenderSettings.fog = fog;
            for (int i = 0; i < lights.Length; i++) { lights[i].type = lightType[i]; lights[i].enabled = enabled[i]; lights[i].shadows = shadows[i]; lights[i].transform.SetPositionAndRotation(position[i], rotation[i]); lights[i].intensity = intensity[i]; lights[i].range = range[i]; }
            for (int i = 0; i < renderers.Length; i++) renderers[i].shadowCastingMode = modes[i];
            UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
