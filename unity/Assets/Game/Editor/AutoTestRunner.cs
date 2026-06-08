using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class AutoTestRunner
{
    [MenuItem("Tools/Run Headless Test")]
    public static void Run()
    {
        Debug.Log("[AutoTestRunner] Preparing headless test...");
        EditorSceneManager.OpenScene("Assets/Game/Scenes/Main.unity");
        
        // Wait until compilation and asset importing finish before starting PlayMode
        EditorApplication.update += WaitAndRun;
    }

    private static void WaitAndRun()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            return;
        }

        EditorApplication.update -= WaitAndRun;
        Debug.Log("[AutoTestRunner] Starting PlayMode for headless test...");
        EditorApplication.isPlaying = true;
    }
}

