using UnityEngine;
using UnityEngine.InputSystem;

// Deliberate, documented frame-rate policy + a benchmark "uncap" toggle.
//
// Default = VSync to the display (60 Hz on the Deck): power-efficient, because rendering only as
// fast as the screen refreshes avoids burning CPU/GPU watts on frames no one ever sees. This is why
// the FPS sits at a flat 60 — it's the correct behaviour, not a bug.
//
// Cycle modes for benchmarking with F4 / gamepad North (Y / Triangle):
//   VSync (60) -> Uncapped -> Cap30
//
// NOTE (Steam Deck): in Gaming Mode, gamescope composits and may still cap presentation regardless
// of this in-app setting, so "Uncapped" might not exceed 60 on-device. To see true headroom under
// any cap, read CPU/GPU ms in the overlay (FrameTimingManager) — that's the real work per frame.
public class FrameRatePolicy : MonoBehaviour
{
    public enum Mode { VSync, Uncapped, Cap30 }
    public static Mode Current { get; private set; } = Mode.VSync;

    void Start() => Apply(Mode.VSync);

    void Update()
    {
        bool cycle = false;
        var kb = Keyboard.current;
        if (kb != null && kb.f4Key.wasPressedThisFrame) cycle = true;
        var gp = Gamepad.current;
        if (gp != null && gp.buttonNorth.wasPressedThisFrame) cycle = true;
        if (cycle) Apply((Mode)(((int)Current + 1) % 3));
    }

    public static void Apply(Mode m)
    {
        Current = m;
        switch (m)
        {
            case Mode.VSync:
                QualitySettings.vSyncCount = 1;
                Application.targetFrameRate = -1;
                break;
            case Mode.Uncapped:
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = -1;
                break;
            case Mode.Cap30:
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = 30;
                break;
        }
        Debug.Log("[FrameRatePolicy] mode=" + m);
    }
}
