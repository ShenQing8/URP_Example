using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static partial class GrassMigrationValidation
{
    const string BundleDirectory = Output + "ShaderBundle";
    const string BundleName = "grass-shader-validation";
    static void BuildShaderBundle(StringBuilder report)
    {
        Check(!EditorApplication.isPlaying, "Build shader validation bundle outside Play");
        Directory.CreateDirectory(BundleDirectory);
        report.AppendLine("Narrow shader/compute AssetBundle compilation for Windows; no Player build or Build Settings change.");
        report.AppendLine("Target graphics APIs=" + string.Join(",", PlayerSettings.GetGraphicsAPIs(BuildTarget.StandaloneWindows64)));
        var manifest = BuildPipeline.BuildAssetBundles(BundleDirectory, new[] { new AssetBundleBuild {
            assetBundleName = BundleName,
            assetNames = new[] { Core + "InstancedIndirectGrass.mat", Core + "CullingCompute.compute", Core + "GrassBending/GrassBendingRTPrePassPS.mat", "Assets/URPMobileGrassInstancedIndirectDemo/SkyBox/SkyBox.shader" }
        } }, BuildAssetBundleOptions.ForceRebuildAssetBundle | BuildAssetBundleOptions.StrictMode, BuildTarget.StandaloneWindows64);
        Check(manifest, "Shader validation bundle compilation failed; inspect Editor.log");
        string path = BundleDirectory + "/" + BundleName;
        report.AppendLine("Compiled bundle=" + Path.GetFullPath(path) + " bytes=" + new FileInfo(path).Length + " SHA256=" + BitConverter.ToString(System.Security.Cryptography.SHA256.Create().ComputeHash(File.ReadAllBytes(path))).Replace("-", ""));
    }
    static void UseShaderBundle(StringBuilder report, string id)
    {
        Check(EditorApplication.isPlaying && Camera.main, "Load shader bundle with demo in Play");
        Check(GraphicsSettings.currentRenderPipeline.name.StartsWith("PC", StringComparison.Ordinal), "Windows shader bundle validates the included PC pipeline");
        var grass = InstancedIndirectGrassRenderer.instance;
        var originalMaterial = grass.instanceMaterial; var originalCompute = grass.cullingComputeShader;
        var oldCamera = Camera.main; string oldTag = oldCamera.tag;
        var cameraObject = new GameObject("Compiled bundle validation camera");
        AssetBundle bundle = null;
        var trails = UnityEngine.Object.FindObjectsByType<TrailRenderer>();
        var trailMaterials = System.Array.ConvertAll(trails, trail => trail.sharedMaterial);
        var originalSky = RenderSettings.skybox;
        Material bundleSky = null;
        try {
            bundle = AssetBundle.LoadFromFile(Path.GetFullPath(BundleDirectory + "/" + BundleName)); Check(bundle, "Bundle failed to load");
            grass.instanceMaterial = bundle.LoadAsset<Material>(Core + "InstancedIndirectGrass.mat");
            grass.cullingComputeShader = bundle.LoadAsset<ComputeShader>(Core + "CullingCompute.compute");
            Check(grass.instanceMaterial && grass.instanceMaterial.shader.isSupported && grass.cullingComputeShader, "Compiled shader/compute asset missing or unsupported");
            var trailMaterial = bundle.LoadAsset<Material>(Core + "GrassBending/GrassBendingRTPrePassPS.mat");
            Check(trailMaterial && trailMaterial.shader.isSupported, "Compiled trail shader missing or unsupported");
            foreach (var trail in trails) trail.sharedMaterial = trailMaterial;
            bundleSky = new Material(originalSky) { shader = bundle.LoadAsset<Shader>("Assets/URPMobileGrassInstancedIndirectDemo/SkyBox/SkyBox.shader") };
            Check(bundleSky.shader && bundleSky.shader.isSupported, "Compiled sky shader missing or unsupported"); RenderSettings.skybox = bundleSky;
            var camera = cameraObject.AddComponent<Camera>(); camera.CopyFrom(oldCamera); camera.enabled = true;
            cameraObject.AddComponent<UniversalAdditionalCameraData>();
            camera.transform.SetPositionAndRotation(oldCamera.transform.position, oldCamera.transform.rotation);
            oldCamera.tag = "Untagged"; camera.tag = "MainCamera";
            report.AppendLine("Rendering a fresh camera with material/Shader/Compute loaded from the actual Windows bundle. This camera has no earlier Editor-material indirect submissions.");
            Read(grass, camera, report);
            RenderSuite(report, id + "-bundle");
            Appearance(report, id + "-bundle-appearance");
            report.AppendLine("Windows compiled bundle GPU cull, Forward+ lighting/shadows, wind and bending PASS");
        } finally {
            grass.instanceMaterial = originalMaterial; grass.cullingComputeShader = originalCompute;
            for (int i = 0; i < trails.Length; i++) trails[i].sharedMaterial = trailMaterials[i];
            RenderSettings.skybox = originalSky; if (bundleSky) UnityEngine.Object.DestroyImmediate(bundleSky);
            cameraObject.tag = "Untagged"; oldCamera.tag = oldTag;
            UnityEngine.Object.DestroyImmediate(cameraObject);
            if (bundle) bundle.Unload(true);
        }
    }
}
