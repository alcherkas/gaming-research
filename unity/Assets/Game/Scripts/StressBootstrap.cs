using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// DOTS stress-test showcase: tens of thousands of agents moved by a Burst-compiled, multicore
// IJobParallelFor and drawn with a single GPU-instanced quad mesh. Demonstrates the data-oriented
// pillars (Burst SIMD + the C# Job System) plus GPU instancing — the "how high can it scale" view.
// Watch the overlay's frame ms as you change the count; on the Deck this is the headroom showcase.
//
// Kept deliberately separate from the FishNet co-op game (DOTS pairs with Netcode for Entities,
// not FishNet).
public class StressBootstrap : MonoBehaviour
{
    const int Min = 1000, Max = 200000, Step = 5000;
    int _count = 20000;
    float _scale = 0.12f;
    float2 _bounds = new float2(15f, 9f);

    NativeArray<float2> _pos, _vel;
    NativeArray<Matrix4x4> _mats;
    Mesh _mesh;
    Material _mat;
    RenderParams _rp;

    [BurstCompile]
    struct MoveAndMatrixJob : IJobParallelFor
    {
        public float dt, scale;
        public float2 bounds;
        public NativeArray<float2> pos;
        public NativeArray<float2> vel;
        [WriteOnly] public NativeArray<Matrix4x4> mats;

        public void Execute(int i)
        {
            float2 p = pos[i] + vel[i] * dt;
            float2 v = vel[i];
            if (p.x < -bounds.x || p.x > bounds.x) { v.x = -v.x; p.x = math.clamp(p.x, -bounds.x, bounds.x); }
            if (p.y < -bounds.y || p.y > bounds.y) { v.y = -v.y; p.y = math.clamp(p.y, -bounds.y, bounds.y); }
            pos[i] = p; vel[i] = v;

            Matrix4x4 m = default;
            m.m00 = scale; m.m11 = scale; m.m22 = 1f; m.m33 = 1f;
            m.m03 = p.x; m.m13 = p.y;
            mats[i] = m;
        }
    }

    void Start()
    {
        _mesh = BuildQuad();
        _mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { enableInstancing = true };
        _mat.color = new Color(0.30f, 0.85f, 1f);
        _rp = new RenderParams(_mat) { worldBounds = new Bounds(Vector3.zero, Vector3.one * 1000f) };
        Allocate(_count);
    }

    void Allocate(int n)
    {
        Release();
        _count = math.clamp(n, Min, Max);
        _pos = new NativeArray<float2>(_count, Allocator.Persistent);
        _vel = new NativeArray<float2>(_count, Allocator.Persistent);
        _mats = new NativeArray<Matrix4x4>(_count, Allocator.Persistent);

        var rnd = new Unity.Mathematics.Random(12345);
        for (int i = 0; i < _count; i++)
        {
            _pos[i] = rnd.NextFloat2(-_bounds, _bounds);
            _vel[i] = math.normalizesafe(rnd.NextFloat2(-1f, 1f)) * rnd.NextFloat(2f, 6f);
        }
    }

    void Release()
    {
        if (_pos.IsCreated) _pos.Dispose();
        if (_vel.IsCreated) _vel.Dispose();
        if (_mats.IsCreated) _mats.Dispose();
    }

    void Update()
    {
        HandleInput();

        new MoveAndMatrixJob
        {
            dt = Time.deltaTime, scale = _scale, bounds = _bounds,
            pos = _pos, vel = _vel, mats = _mats,
        }.Schedule(_count, 256).Complete();

        Graphics.RenderMeshInstanced(_rp, _mesh, 0, _mats, _count);
    }

    void HandleInput()
    {
        var kb = Keyboard.current;
        var gp = Gamepad.current;

        bool up = (kb != null && kb.rightArrowKey.wasPressedThisFrame) ||
                  (gp != null && gp.rightShoulder.wasPressedThisFrame);
        bool down = (kb != null && kb.leftArrowKey.wasPressedThisFrame) ||
                    (gp != null && gp.leftShoulder.wasPressedThisFrame);
        bool back = (kb != null && kb.escapeKey.wasPressedThisFrame) ||
                    (gp != null && gp.bDeviceButton());
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.JoystickButton5)) up = true;     // RB
        if (Input.GetKeyDown(KeyCode.JoystickButton4)) down = true;   // LB
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton1)) back = true; // B
#endif
        if (up) Allocate(_count + Step);
        if (down) Allocate(_count - Step);
        if (back) SceneManager.LoadScene("Main");
    }

    void OnGUI()
    {
        var style = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(Screen.height * 0.03f) };
        style.normal.textColor = Color.white;
        GUI.Label(new Rect(8, Screen.height - 70, Screen.width - 16, 60),
            "DOTS stress: " + _count.ToString("N0") + " agents (Burst job + GPU instancing)\n" +
            "LB/RB or ←/→ change count   ·   B/Esc return", style);
    }

    void OnDestroy()
    {
        Release();
        if (_mat != null) Destroy(_mat);
        if (_mesh != null) Destroy(_mesh);
    }

    static Mesh BuildQuad()
    {
        var m = new Mesh { name = "StressQuad" };
        m.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f),
        };
        m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        m.RecalculateBounds();
        return m;
    }
}

static class GamepadExtensions
{
    // Small helper so StressBootstrap reads the East/B button without a hard dependency on naming.
    public static bool bDeviceButton(this Gamepad gp) => gp.buttonEast.wasPressedThisFrame;
}
