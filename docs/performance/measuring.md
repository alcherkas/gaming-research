# Measuring performance

Four complementary tools. Use the in-game overlay for game-level numbers, MangoHud for power/thermals,
and the Unity Profiler for deep dives.

## 1. In-game overlay (`DebugHud`)

Toggle: gamepad **View/Select** button, or **F3**. Source:
[`unity/Assets/Game/Scripts/DebugHud.cs`](../../unity/Assets/Game/Scripts/DebugHud.cs).

```
[DEV] FPS 60   frame 16.7 ms (min 16.6 / max 16.9)
work: CPU 2.1 ms   GPU 1.8 ms   (budget 16.7)
draw 5  setpass 2  batches 5   tris 44  verts 96
mem total 280.0 MB (alloc 240.0 MB)
  mono 18.0 MB / heap 22.0 MB   gfx 120.0 MB   GC/frame 0 B
cap VSync  vSync 1  res 1280x800
```

Reading it:

- **`frame ms`** is the metric you budget against (16.7 ms = 60 fps). It's `1000 / FPS` but linear and
  additive, so you can attribute cost per system. **min/max** expose stutter an average FPS hides.
- **`work: CPU/GPU ms`** is the *real* per-frame work (FrameTimingManager). **This is how you see
  headroom under VSync** — FPS is pinned to 60, but CPU 2 ms means ~14 ms of spare budget.
- **`draw / setpass / batches`** — render-call counts (development builds only; release shows `n/a`).
  Sprites sharing one atlas/material collapse to ~1 SetPass **in the static scene**; combat fragments
  this (10 sorting orders + per-player HUD canvases + transient VFX) — see `roadmap.md` (Rendering #3).
- **`GC/frame`** — must be **0 B** in steady state. Any non-zero value is a per-frame allocation to hunt.
  It reads 0 B idle but **non-zero in combat today** (logging, per-frame mana write, VFX/SFX); the
  CPU/GC section of `roadmap.md` is the hunt list.
- **`mem`** — broken down so a big number is explainable: **gfx** (driver/textures) + engine reserved
  dominate; **mono** (your managed objects) is small. Memory does **not** cost frame time.
- **`cap`** — press **F4 / gamepad Y** to cycle VSync → Uncapped → Cap30 for benchmarking
  ([`FrameRatePolicy.cs`](../../unity/Assets/Game/Scripts/FrameRatePolicy.cs)).

> Why FPS sits at a flat 60: VSync (Deck 60 Hz + `vSyncCount=1` + gamescope). Correct and
> power-efficient. Uncapping on-device may not exceed 60 because gamescope still composits at vsync —
> read CPU/GPU ms instead.

The overlay is itself **zero-allocation** while visible (reused `StringBuilder` + `GUIContent`), so it
doesn't perturb the GC measurement.

## 2. MangoHud — power, thermals, GPU/CPU (the "is it efficient?" tool)

Best for the **15 W** budget: shows FPS, frametime, CPU/GPU load, **watts**, temps.

- Install once on the Deck from the **Discover** store.
- Gaming Mode: set the non-Steam game's launch options to `MANGOHUD=1 %command%`.
- Desktop Mode / SSH: `MANGO=1 bash tools/deploy/run.sh`
  ([`tools/deploy/run.sh`](../../tools/deploy/run.sh)).

## 3. Steam Quick-Access → Performance

Zero install. Steam (⋯) button → Performance tab → level 1–4: FPS, frametime graph, CPU/GPU, power.
Good for a quick sanity check in Gaming Mode.

## 4. Unity Profiler (remote, deep dive)

Build the **Development** player (`BuildLinux.BuildDev` — sets Development + Autoconnect Profiler),
deploy, launch, then in the Mac editor **Window ▸ Analysis ▸ Profiler** connect to the Deck. Gives
per-script CPU, GC spikes, draw-call breakdown, and memory on real hardware.

## Metric che-sheet

| Question | Look at |
|---|---|
| Am I hitting 60? | `frame ms` ≈ 16.7, FPS 60 |
| How much headroom? | `CPU ms` / `GPU ms` vs 16.7 |
| Am I CPU- or GPU-bound? | whichever of CPU/GPU ms is higher |
| Any per-frame garbage? | `GC/frame` must be 0 B |
| Too many draws? | `draw` / `setpass` |
| Is it power-efficient? | MangoHud **watts** + temps |
| Why is memory high? | `mem` breakdown (gfx vs mono); use a **release** build |
