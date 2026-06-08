# Techniques catalog

Every optimization applied in this project, with **what / why / how to measure / where**. Grouped by
domain. Paths are under [`unity/Assets/Game/`](../../unity/Assets/Game/) unless noted.

---

## Rendering / GPU

### Runtime sprite atlas → ~1 draw call
- **What:** all placeholder sprites (floor, walls, players) are sub-rects of **one** shared 64×32
  texture, so they share a single material.
- **Why:** same texture + same shader ⇒ the **SRP Batcher** collapses the scene's sprites into very
  few SetPass/draw calls. Texture switches are the classic cause of extra 2D draw calls.
- **Measure:** overlay `draw` / `setpass` stay tiny as the room fills with sprites.
- **Caveat:** ~1 is the **static-scene** best case. In combat, 10 distinct hardcoded sorting orders +
  per-player world-space HUD canvases + transient VFX/slash GameObjects fragment batching above ~1 —
  see [`roadmap.md`](roadmap.md) (Rendering #3).
- **Where:** `Scripts/SpriteFactory.cs`.

### URP tuned for the Deck (MSAA off, HDR off, render scale 1.0)
- **What:** every URP asset set to MSAA 1×, no HDR, native-res render scale.
- **Why:** flat 2D unlit art gains nothing from MSAA/HDR; both cost bandwidth/fill on the RDNA2 APU.
  Render scale 1.0 = render at 800p, no supersampling.
- **Measure:** overlay `GPU ms`; MangoHud GPU load/watts.
- **Caveat:** `RenderConfig.cs` only sets MSAA/HDR/renderScale. The Standalone build still resolves to
  the heavier **PC** quality tier (Deferred + active SSAO + depth/opaque passes), not the lean Mobile
  tier — wasted GPU watts with no visual effect. Hardening the rendering mode is the top forward item —
  see [`roadmap.md`](roadmap.md) (Rendering #1).
- **Where:** editor `Editor/RenderConfig.cs` (run via `Game ▸ Apply Render Config`).

### No realtime lights / shadows
- **What:** unlit sprites, no 2D light setup.
- **Why:** realtime lighting/shadows are among the biggest GPU costs; a flat top-down game doesn't
  need them.
- **Measure:** `GPU ms` ≈ 1–2 ms for the room scene.

### SRP Batcher (deliberately forced on, kept compatible)
- **What:** explicitly enabled on the URP assets (`Mobile_RPAsset.asset` `m_UseSRPBatcher: 1`) with
  legacy dynamic batching explicitly disabled (`m_SupportsDynamicBatching: 0`); materials kept
  SRP-Batcher-compatible (single shared sprite material).
- **Why:** binds per-object data on the GPU, slashing CPU render-thread cost vs per-object setup. This
  is a deliberate, load-bearing config choice, not a left-at-default. (Note: the Standalone build
  currently resolves to the PC tier — see the URP caveat above and [`roadmap.md`](roadmap.md).)

---

## CPU / GC

### Zero per-frame allocations (overlay + idle movement)
- **What:** no LINQ / boxing / string-building in the overlay or movement `Update`; the debug overlay
  reuses a `StringBuilder` and a `GUIContent`; movement reads input into structs.
- **Why:** every per-frame allocation feeds the GC, which causes frame-time spikes (stutter). The bar
  is **0 B/frame** in steady state.
- **Measure:** overlay `GC/frame` reads **0 B** in the idle/static scene (overlay on or off).
- **Caveat:** this holds for the **idle** scene. Combat is **not** yet zero-alloc — unstripped hot-path
  `Debug.Log`, the per-frame mana SyncVar write, and VFX/projectile/SFX `Instantiate`/`AudioClip.Create`
  allocate during play. These are the top CPU/GC items in [`roadmap.md`](roadmap.md).
- **Where:** `Scripts/DebugHud.cs`, `Scripts/PlayerController.cs`.

### Cached component references
- **What:** `Rigidbody2D` / components fetched once in `Awake`, never `GetComponent` in `Update`.
- **Why:** `GetComponent` per frame is needless CPU and can allocate.
- **Where:** `Scripts/PlayerController.cs`, `Scripts/NetworkPlayer.cs`.

---

## Physics (2D)

### Static colliders + minimal bodies
- **What:** walls/floor are colliders with **no Rigidbody2D** (static); only players have bodies.
- **Why:** the 2D solver only simulates dynamic bodies; static geometry is free to collide against.
- **Where:** `Scripts/RoomBuilder.cs`, player in `Scripts/GameBootstrap.cs`.

### Zero gravity + 50 Hz fixed step
- **What:** `Physics2D.gravity = 0`; `fixedDeltaTime = 0.02` (50 Hz).
- **Why:** top-down needs no gravity; 50 Hz physics is smooth for token movement and ~17% cheaper than
  the 60 Hz default on a power-limited APU.
- **Measure:** overlay `CPU ms`.
- **Where:** `Scripts/PhysicsConfig.cs`.

---

## Build / IL2CPP

### IL2CPP "Faster runtime" + High stripping + engine code stripping (release)
- **What:** release builds use `Il2CppCodeGeneration.OptimizeSpeed`, `ManagedStrippingLevel.High`,
  `stripEngineCode = true`. Dev builds stay lightly stripped so the profiler/overlay work.
- **Why:** smaller binary, faster startup, **lower memory footprint**. Measured here:
  **release ≈ 850 MB vs dev ≈ 2.08 GB** build size; `PhysicsProto_Data` ≈ **21 MB**.
- **Caveat:** High stripping can remove reflection-/native-reached code → `Assets/link.xml` preserves
  FishNet + Steamworks + game assemblies.
- **Where:** `Editor/BuildLinux.cs`, `unity/Assets/link.xml`.

### Vulkan-first, IL2CPP, x86_64
- **What:** Vulkan primary (OpenGLCore fallback), IL2CPP backend, native Linux x86_64.
- **Why:** Valve-recommended for the Deck; IL2CPP gives faster runtime than Mono.
- **Where:** `Editor/BuildLinux.cs`.

---

## Frame rate & power

### VSync by default, deliberate cap policy, benchmark uncap
- **What:** default `vSyncCount = 1` (cap to the 60 Hz screen). `FrameRatePolicy` cycles
  VSync → Uncapped → Cap30 (F4 / gamepad-North) for benchmarking.
- **Why:** rendering faster than the display burns CPU/GPU **watts and heat** for frames nobody sees —
  the opposite of efficient on a 15 W handheld. Capping is the single biggest "efficiency" lever.
- **Measure:** MangoHud **watts**; overlay `CPU/GPU ms` show the real headroom under the cap.
- **Where:** `Scripts/FrameRatePolicy.cs`.

### Fast, clean shutdown
- **What:** in-game Quit (hold Start+Select / F10) → `Application.Quit()`; teardown in
  `OnApplicationQuit` so it runs on the **native Steam exit** too.
- **Why:** avoids Steam's reaper waiting on a non-Steam process ("Stopping…" lag) and leaves no
  dangling Steam P2P session.
- **Where:** `Scripts/AppControl.cs`, `Scripts/ConnectBootstrap.cs`.

---

## Networking (FishNet + Steam)

### Owner-authoritative movement + replicated transform
- **What:** only the local owner runs input; remotes are driven by `NetworkTransform`.
- **Why:** minimal, responsive, and avoids server round-trips for local movement.
- **Where:** `Scripts/NetworkPlayer.cs`, prefab `Assets/Game/Resources/Player.prefab`.

### Multipass: Tugboat (LAN) + FishySteamworks (Steam)
- **What:** one transport stack, swappable at runtime; LAN/Tugboat for fast iteration, Steam relay to
  ship — no code changes.
- **Why:** test co-op instantly over LAN (no Steam account), then validate real SDR P2P.
- **Where:** `Scripts/ConnectBootstrap.cs`, `Editor/NetworkSetup.cs`.

### Interest management (room-scoped) — *next step*
- **What:** the room structure is in place to scope replication per room (FishNet observers /
  distance condition) so clients only receive nearby objects.
- **Why:** bandwidth + CPU scale with *visible* objects, not the whole world.
- **Status:** rooms + camera are room-aware; adding a FishNet observer condition to the player prefab
  is the documented next increment.

---

## DOTS / Burst / Jobs (stress-test showcase)

### Burst + C# Job System + GPU instancing
- **What:** a separate scene moves **tens of thousands** of agents in a `[BurstCompile]`
  `IJobParallelFor` (multicore SIMD) and draws them with **one** GPU-instanced quad mesh
  (`Graphics.RenderMeshInstanced` over a `NativeArray<Matrix4x4>`).
- **Why:** demonstrates the data-oriented ceiling — cache-friendly `NativeArray` data, Burst-compiled
  native code, parallel jobs, and instanced rendering — far beyond what `GameObject`/`MonoBehaviour`
  reaches. (Kept separate from FishNet, which pairs with Netcode for Entities, not DOTS.)
- **Measure:** overlay `frame ms` / `CPU ms` as you raise the agent count (LB/RB or ←/→).
- **Where:** `Scripts/StressBootstrap.cs`, scene `Assets/Game/Scenes/Stress.unity`.

---

## How to reproduce the numbers

1. Deploy a build (`tools/deploy/deploy.sh`) and launch on the Deck.
2. Toggle the overlay (**View** / F3); capture FPS, CPU/GPU ms, draw/setpass, GC/frame, mem.
3. Run MangoHud (`MANGO=1 tools/deploy/run.sh` or `MANGOHUD=1 %command%`) for watts/temps.
4. For DOTS: open the stress scene, raise the count, watch `frame ms` scale.
5. Compare a **release** vs **development** build for the true memory/size footprint.
