using UnityEngine;
using UnityEngine.InputSystem;

// Application lifecycle control.
//
// Quit paths are intentionally separate:
//   - In-game Quit: hold gamepad Start (Menu) + View (Select) together for ~0.75s, or press F10.
//     Calls Application.Quit() for a fast, clean exit (the process ends itself instead of waiting
//     for Steam's reaper to stop a non-Steam game).
//   - Native Steam "Exit Game" sends a SIGINT/SIGTERM signal — it does NOT call the combo above.
//
// Graceful-shutdown cleanup therefore lives in OnApplicationQuit(), which Unity runs on EVERY exit
// path (our combo, the native Steam signal exit, and window close). Networking teardown
// (StopConnection / SteamAPI.Shutdown) will be added here in Phase 2D-C/P4.
public class AppControl : MonoBehaviour
{
    const float HoldToQuit = 0.75f;
    float _held;

    void Update()
    {
        if (QuitRequested())
            Quit();
    }

    bool QuitRequested()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.f10Key.wasPressedThisFrame)
            return true;

        var gp = Gamepad.current;
        if (gp != null && gp.startButton.isPressed && gp.selectButton.isPressed)
        {
            _held += Time.unscaledDeltaTime;
            if (_held >= HoldToQuit)
                return true;
        }
        else
        {
            _held = 0f;
        }
        return false;
    }

    public void Quit()
    {
        Debug.Log("[AppControl] Quit requested.");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // Runs for ALL exit paths (in-game Quit, native Steam signal exit, window close).
    void OnApplicationQuit()
    {
        Debug.Log("[AppControl] OnApplicationQuit — graceful shutdown (networking teardown added in P4).");
    }
}
