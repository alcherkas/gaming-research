using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Profiling;

// On-screen performance overlay for debugging efficiency on the Steam Deck.
// Toggle: gamepad View/Select button, or F3.
//   - Always available: FPS, frame ms (avg/min/max), CPU/GPU ms (FrameTimingManager), memory.
//   - Development builds only: draw calls / batches / SetPass / tris / verts / GC-per-frame
//     (ProfilerRecorder counters are not populated in non-development players).
// Rendered with IMGUI so it needs no fonts/prefabs and is headless-build safe. The overlay only
// allocates while visible (toggled on for debugging); steady-state gameplay is unaffected.
public class DebugHud : MonoBehaviour
{
    [SerializeField] bool visibleOnStart;

    bool _visible;
    GUIStyle _style;
    Texture2D _bg;

    // FPS / frame-time window
    const float Window = 0.5f;
    float _accum;
    int _frames;
    float _timer;
    float _fps, _msAvg, _msMin, _msMax;
    float _curMin = float.MaxValue, _curMax;

    readonly FrameTiming[] _timings = new FrameTiming[1];
    double _cpuMs, _gpuMs;

    ProfilerRecorder _drawCalls, _setPass, _batches, _tris, _verts, _gcFrame;

    readonly StringBuilder _sb = new StringBuilder(320);
    readonly GUIContent _content = new GUIContent();
    string _text = "";

    void OnEnable()
    {
        _visible = visibleOnStart;
        _drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
        _setPass   = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
        _batches   = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Total Batches Count");
        _tris      = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
        _verts     = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Vertices Count");
        _gcFrame   = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
    }

    void OnDisable()
    {
        _drawCalls.Dispose(); _setPass.Dispose(); _batches.Dispose();
        _tris.Dispose(); _verts.Dispose(); _gcFrame.Dispose();
        if (_bg != null) Destroy(_bg);
    }

    void Update()
    {
        if (Toggled())
            _visible = !_visible;

        float ms = Time.unscaledDeltaTime * 1000f;
        _accum += Time.unscaledDeltaTime;
        _frames++;
        if (ms < _curMin) _curMin = ms;
        if (ms > _curMax) _curMax = ms;

        _timer += Time.unscaledDeltaTime;
        if (_timer >= Window)
        {
            _fps = _frames / _accum;
            _msAvg = (_accum / _frames) * 1000f;
            _msMin = _curMin;
            _msMax = _curMax;
            _accum = 0f; _frames = 0; _timer = 0f;
            _curMin = float.MaxValue; _curMax = 0f;

            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, _timings) > 0)
            {
                _cpuMs = _timings[0].cpuFrameTime;
                _gpuMs = _timings[0].gpuFrameTime;
            }

            if (_visible) Rebuild();
        }
    }

    static bool Toggled()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.f3Key.wasPressedThisFrame) return true;
        var gp = Gamepad.current;
        if (gp != null && gp.selectButton.wasPressedThisFrame) return true;
        return false;
    }

    void Rebuild()
    {
        var sb = _sb;
        sb.Clear();
        sb.Append(Debug.isDebugBuild ? "[DEV] " : "[REL] ")
          .Append("FPS ").Append(Mathf.RoundToInt(_fps))
          .Append("   frame ").Append(_msAvg.ToString("0.0")).Append(" ms")
          .Append(" (min ").Append(_msMin.ToString("0.0"))
          .Append(" / max ").Append(_msMax.ToString("0.0")).Append(')').Append('\n');

        // Under VSync the frame ms is pinned to the refresh; CPU/GPU ms show the real work + headroom.
        sb.Append("work: CPU ").Append(_cpuMs.ToString("0.0")).Append(" ms")
          .Append("   GPU ").Append(_gpuMs.ToString("0.0")).Append(" ms")
          .Append("   (budget 16.7)").Append('\n');

        sb.Append("draw ").Append(Stat(_drawCalls))
          .Append("  setpass ").Append(Stat(_setPass))
          .Append("  batches ").Append(Stat(_batches))
          .Append("   tris ").Append(Stat(_tris))
          .Append("  verts ").Append(Stat(_verts)).Append('\n');

        sb.Append("mem total ").Append(Bytes(Profiler.GetTotalReservedMemoryLong()))
          .Append(" (alloc ").Append(Bytes(Profiler.GetTotalAllocatedMemoryLong())).Append(")\n");
        sb.Append("  mono ").Append(Bytes(Profiler.GetMonoUsedSizeLong()))
          .Append(" / heap ").Append(Bytes(Profiler.GetMonoHeapSizeLong()))
          .Append("   gfx ").Append(Bytes(Profiler.GetAllocatedMemoryForGraphicsDriver()))
          .Append("   GC/frame ").Append(_gcFrame.Valid ? Bytes(_gcFrame.LastValue) : "n/a(rel)").Append('\n');

        sb.Append("cap ").Append(FrameRatePolicy.Current)
          .Append("  vSync ").Append(QualitySettings.vSyncCount)
          .Append("  res ").Append(Screen.width).Append('x').Append(Screen.height);

        _text = sb.ToString();
    }

    static string Stat(ProfilerRecorder r) => r.Valid ? r.LastValue.ToString() : "n/a";

    static string Bytes(long b)
    {
        if (b <= 0) return "0";
        if (b < 1024) return b + " B";
        if (b < 1024 * 1024) return (b / 1024f).ToString("0.0") + " KB";
        return (b / (1024f * 1024f)).ToString("0.0") + " MB";
    }

    void OnGUI()
    {
        if (!_visible) return;

        if (_style == null)
        {
            _bg = new Texture2D(1, 1);
            _bg.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.65f));
            _bg.Apply();
            _style = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(Screen.height * 0.024f),
                normal = { textColor = Color.white },
                padding = new RectOffset(10, 10, 8, 8),
                richText = false,
            };
        }

        _content.text = _text;
        Vector2 size = _style.CalcSize(_content);
        var rect = new Rect(8, 8, size.x + 8, size.y);
        GUI.DrawTexture(rect, _bg, ScaleMode.StretchToFill);
        GUI.Label(rect, _content, _style);
    }
}
