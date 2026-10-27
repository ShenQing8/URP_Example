using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Changes only the in-memory build scene. No test runner or extra pipeline enters a release Player.
public sealed class GrassPlayerValidationSceneProcessor : IProcessSceneWithReport
{
    public int callbackOrder => 0;
    public void OnProcessScene(Scene scene, BuildReport report)
    {
        if (!GrassPlayerBuildValidation.IsValidationBuild || report == null || (report.summary.options & BuildOptions.Development) == 0 || !scene.path.EndsWith("/TestScene/scene.unity")) return;
        var runner = new GameObject("Opt-in Player validation").AddComponent<GrassPlayerValidation>();
        runner.pc = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        runner.mobile = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");
        SceneManager.MoveGameObjectToScene(runner.gameObject, scene);
    }
}
