using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Generates the 2D top-down scene headlessly so it can be authored from CI/CLI.
// The saved scene holds only an orthographic camera + a Bootstrap object; all
// visuals are built procedurally at runtime (see GameBootstrap / RoomBuilder).
public static class SceneBootstrap
{
    const string SceneDir = "Assets/Game/Scenes";
    const string ScenePath = SceneDir + "/Main.unity";

    [MenuItem("Game/Build Main Scene")]
    public static void BuildMainScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 6f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.05f, 0.05f, 0.08f);
        cam.transform.position = new Vector3(GameBootstrap.RoomA.x, GameBootstrap.RoomA.y, -10f);
        camGo.AddComponent<AudioListener>();
        camGo.AddComponent<RoomCamera>();

        var boot = new GameObject("Bootstrap");
        boot.AddComponent<RoomManager>();
        boot.AddComponent<GameBootstrap>();
        boot.AddComponent<PhysicsConfig>();
        boot.AddComponent<DebugHud>();
        boot.AddComponent<AppControl>();
        boot.AddComponent<FrameRatePolicy>();

        Directory.CreateDirectory(SceneDir);
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        Debug.Log("[SceneBootstrap] Created 2D scene " + ScenePath);
    }

    [MenuItem("Game/Build Main Scene + Networking")]
    public static void BuildMainSceneWithNetworking()
    {
        NetworkSetup.CreatePlayerPrefab();
        BuildMainScene();
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        NetworkSetup.AddNetworkingToCurrentScene();
        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log("[SceneBootstrap] Networking added to " + ScenePath);
    }
}
