using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Builds the DOTS stress-test scene headlessly and registers both scenes in Build Settings
// (Main at index 0, Stress at index 1).
public static class StressSceneBuilder
{
    const string SceneDir = "Assets/Game/Scenes";
    const string MainPath = SceneDir + "/Main.unity";
    const string StressPath = SceneDir + "/Stress.unity";

    [MenuItem("Game/Build Stress Scene")]
    public static void Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 9f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.04f, 0.04f, 0.06f);
        cam.transform.position = new Vector3(0f, 0f, -10f);
        camGo.AddComponent<AudioListener>();

        var go = new GameObject("Stress");
        go.AddComponent<StressBootstrap>();
        go.AddComponent<DebugHud>();
        go.AddComponent<FrameRatePolicy>();
        go.AddComponent<AppControl>();

        System.IO.Directory.CreateDirectory(SceneDir);
        EditorSceneManager.SaveScene(scene, StressPath);

        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(MainPath, true),
            new EditorBuildSettingsScene(StressPath, true),
        };
        Debug.Log("[StressSceneBuilder] Created " + StressPath + " and registered both scenes.");
    }
}
