using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Configures every URP asset for the Steam Deck target (P3 rendering hardening):
//   - MSAA off          (2D unlit sprites don't need it; saves bandwidth/fill)
//   - HDR off           (no HDR pipeline cost for flat colors)
//   - renderScale 1.0   (render at native 800p; no supersampling)
// Run headlessly: Unity -batchmode -quit -projectPath unity -executeMethod RenderConfig.Apply
public static class RenderConfig
{
    [MenuItem("Game/Apply Render Config")]
    public static void Apply()
    {
        string[] guids = AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset");
        if (guids.Length == 0)
        {
            Debug.LogWarning("[RenderConfig] No UniversalRenderPipelineAsset found.");
            return;
        }

        int n = 0;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (urp == null) continue;

            urp.msaaSampleCount = 1;
            urp.supportsHDR = false;
            urp.renderScale = 1.0f;
            EditorUtility.SetDirty(urp);
            n++;
            Debug.Log("[RenderConfig] tuned " + path);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[RenderConfig] done: " + n + " URP asset(s) -> MSAA off, HDR off, renderScale 1.0");
    }
}
