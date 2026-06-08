# Optimization roadmap

_Forward-looking performance work for the prototype. Last reviewed 2026-06-08 · Unity 6
(6000.4.10f1) / URP 17.4 · target: native-Linux Steam Deck._

This is the **living, prioritized list of optimizations not yet applied** — the companion to
[`techniques.md`](techniques.md) (what is already done, don't regress) and
[`steam-deck-audit.md`](steam-deck-audit.md) (the point-in-time architecture + performance audit it
expands on). Every item below was extracted from the current code and adversarially verified against
the cited `file:line`.

## Top priorities

The highest-leverage, lowest-regret items — do these first:

1. **Strip/guard hot-path `Debug.Log`** in `Health.TakeDamage`, `Projectile.OnTriggerEnter2D`, and
   `SpellSystem` via `[Conditional("DEVELOPMENT_BUILD")]` + `SetStackTraceLogType(LogType.Log, None)`.
   The only HIGH finding, and a low-effort GC/CPU win.
   _(Health.cs:51-97, Projectile.cs:120-146, SpellSystem.cs:102,151)_
2. **Move the Standalone render path off the heavy PC tier** (Deferred + active SSAO + depth/opaque)
   to Forward/Mobile by extending `RenderConfig.cs` — biggest wasted-watt fix, no visual change.
   _(QualitySettings.asset:129, PC_Renderer.asset:56,71,73)_
3. **Add the missing 60 fps cap:** `Application.targetFrameRate = 60` alongside `vSyncCount = 1` in
   `FrameRatePolicy` VSync mode — stops 90 Hz OLED Decks free-running at 90 fps. _(FrameRatePolicy.cs:38-41)_
4. **Cache `GetComponent<StatusEffectManager>` in `Health.Awake`** and read the `-headlessTest` flag
   once at boot instead of `GetCommandLineArgs()` per hit. _(Health.cs:115,54)_
5. **Guard the 32-element `OverlapCircle` buffers** against silent overflow (assert + bump to 64) and
   add a Players `LayerMask` + `ContactFilter2D(useTriggers:false)` so dense rooms don't break enemy
   targeting. _(EnemyAI.cs:112,116; SpellSystem.cs:96,105; Physics2DSettings `QueriesHitTriggers`)_
6. **Set `m_ReuseCollisionCallbacks: 1`** to kill per-contact `Collision2D` allocations in
   `OnCollisionStay2D`. _(Physics2DSettings.asset:45)_
7. **Stop the per-frame mana SyncVar write** (write only on meaningful change / skip when full) —
   removes the per-frame Canvas rebuild *and* the biggest SyncVar-churn source in one change.
   _(SpellSystem.cs:51)_
8. **Assign a FishNet `DefaultObjectPool`** and pool `Projectile` + spell-impact VFX + slash visuals —
   the dominant combat GC/Instantiate churn once logging is gone.
   _(SpellSystem.cs:171,131; VFXSpawner.cs:53-106; BossAI/RangedEnemyAI spawns)_

---

## How to read this roadmap

This is a **forward-looking** roadmap for a 2D-unlit top-down co-op prototype on the Steam Deck (RDNA2 8-CU, Zen2 4c/8t, 16 GB shared LPDDR5, ~15 W, 800p, 60 Hz LCD / 90 Hz OLED). Two facts frame every entry below:

1. **The game is not fill- or solver-bound.** A flat unlit 800p scene with ~1 material easily clears 60 fps today. So most "GPU" tips are really about **wasted watts/heat** (battery + thermal headroom), and most "perf" tips are really about **host-side CPU/GC churn** as player/enemy/projectile counts grow.
2. **The host carries everything.** On a FishNet listen-server the host runs all physics + AI + combat + serialization for 3–4 players inside one 15 W budget. Every "low" item compounds on that single Zen2 host.

Severities are Steam-Deck-relative. `effort` is implementation cost. Each item links the exact `file:line` in the current tree and the overlay/Profiler metric to watch.

> **Measure first.** None of the combat-peak numbers are measured yet (no physical-Deck 4-player capture exists). Before pooling/refactoring, capture a 4-player + boss + enemies session and read `CPU ms` / `GPU ms` / `GC/frame` from the in-game overlay (F3 / View) and watts from MangoHud. Several items below are "do unconditionally" (logging, fps cap, render tier); the rest are "do if the capture confirms the cost."

---

## Rendering / GPU

> Reminder: on unlit 2D at 800p these are **wasted-watt / heat** problems, not framerate problems. The big one is the active render *tier*.

### 1. Standalone resolves to the heavy "PC" quality tier (Deferred + active SSAO + depth/opaque passes) — pure wasted GPU watts
- **Problem:** `QualitySettings` `Standalone: 1` → the Deck build runs the **PC** tier, which uses `PC_Renderer` with `m_RenderingMode: 2` (**Deferred**) and an **active** ScreenSpaceAmbientOcclusion feature, plus depth/opaque texture passes and 2048 shadowmaps — all with **zero visible effect** on unlit sprites with no lights/Light2D. A lean `Mobile` tier exists but excludes Standalone, so it is unreachable. This is the single largest power/heat waste on the 8-CU GPU.
- **Recommendation:** Disable the SSAO renderer feature, switch `PC_Renderer` to **Forward** (`m_RenderingMode: 0`), set RequireDepthTexture/RequireOpaqueTexture to 0 — cleanest by **extending the existing `RenderConfig.cs`** (which already hardens MSAA/HDR/renderScale) to also set rendering mode and toggle renderer features. Or repoint Standalone's default quality at the Mobile tier.
- **Where:** `unity/ProjectSettings/QualitySettings.asset:129` (Standalone→1); `unity/Assets/Settings/PC_Renderer.asset:56,71,73`; extend `unity/Assets/Game/Editor/RenderConfig.cs`.
- **Severity:** medium · **Effort:** low
- **Measure:** MangoHud **GPU watts/load** and overlay **GPU ms** before/after; expect a measurable watt/temperature drop with no visual change.

### 2. No explicit 60 fps cap → 90 Hz OLED Deck free-runs at 90 fps under VSync
- **Problem:** `FrameRatePolicy` VSync mode sets `vSyncCount = 1` but `Application.targetFrameRate = -1`. On a 90 Hz OLED Deck that renders ~50% more frames than this game needs, raising APU draw and heat. (60 Hz LCD is unaffected; gamescope *may* cap in Gaming Mode.)
- **Recommendation:** Set `Application.targetFrameRate = 60` alongside `vSyncCount = 1` in the VSync branch.
- **Where:** `unity/Assets/Game/Scripts/FrameRatePolicy.cs:38-41`.
- **Severity:** medium · **Effort:** low
- **Measure:** Uncap with F4, read MangoHud FPS/watts on a 90 Hz unit; with the cap, overlay FPS should pin to 60 and watts drop.

### 3. SetPass/batching fragmentation from 10 distinct hardcoded sorting orders
- **Problem:** Sprites use 10 distinct sorting orders (-10, 0, 5, 8, 10, 12, 14, 15, 18, 20) hardcoded across the codebase. URP sorts by sortingOrder before SRP-Batcher batching; each unique order is a batch break. Today's static room batches near-1, but combat (projectiles + VFX + HUD canvases) fragments it. This is a **CPU render-thread** cost on the host, not a GPU one.
- **Recommendation:** Consolidate into 3–5 buckets (background: floor/walls; gameplay: enemies/players/items/projectiles; FX; foreground: HUD). Differentiate within a bucket by transform depth, not unique orders. Define the bucket constants in one place so the 11 call-sites stop drifting.
- **Where:** `RoomBuilder.cs:55`, `PlayerVisual.cs:16`, `Projectile.cs:81`, `SpellSystem.cs:140`, `PlayerHUD.cs:56`, `VFXSpawner.cs:18,41,87`, `ItemPickup.cs:41`, `KeyPickup.cs:35`, `DoorLock.cs:47`.
- **Severity:** medium · **Effort:** low
- **Measure:** Overlay **setpass** / **batches** during heavy combat (dev build only); aim to keep setpass ≤ ~5.

### 4. WorldSpace HUD Canvas per player + per-frame mana fill triggers Canvas rebuilds
- **Problem:** `PlayerHUD` builds one world-space Canvas per player. Health/mana fills update via **events** (good — not polled), but mana regenerates every frame (`SpellSystem.cs:51` writes the mana SyncVar every frame), so `OnManaChanged` fires every frame for any non-full owner, setting `Image.fillAmount` and dirtying the canvas → a layout/rebuild pass per player per frame. Bounded today (≤4 canvases), grows with HUD complexity.
- **Recommendation:** Either (a) stop the per-frame mana SyncVar write (only write when mana changes by a threshold, or clamp-and-skip when already full), which removes the per-frame canvas dirty, or (b) replace the Image bars with SpriteRenderer quads driven by localScale/UV (no Canvas rebuild). Option (a) is the cheap, high-leverage fix and also reduces SyncVar churn (see Networking #2).
- **Where:** `PlayerHUD.cs:141-145` (OnManaChanged → fillAmount); root cause `SpellSystem.cs:51`.
- **Severity:** medium · **Effort:** medium
- **Measure:** Profiler **Canvas.SendWillRenderCanvases** / UI rebuild cost, and overlay **CPU ms** with 3–4 players regenerating mana.

### 5. StatusEffectManager writes SpriteRenderer.color unconditionally on every mask change (+ allocates a Color)
- **Problem:** `OnEffectsMaskChanged` calls `UpdateVisuals()` on every SyncVar mask change, which assigns `_sr.color = new Color(...)` (lines 185–205) without comparing to the current color. Burn/poison ticks toggle the mask every 0.5–1.5 s across 7–8 networked actors → redundant material-state writes and a small heap `Color` each time.
- **Recommendation:** Cache the last written color and early-out if unchanged; promote the 6 tint colors to `static readonly` fields (eliminates the per-call `Color` alloc). Optionally drive the tint with a shader keyword instead of a color write.
- **Where:** `StatusEffectManager.cs:172-175` (callback), `:185-205` (color writes).
- **Severity:** medium · **Effort:** low
- **Measure:** Overlay **GC/frame** and **setpass** while several actors are burning/poisoned; both should stay flat after the fix.

### 6. World-space HUD canvases have no distance/visibility culling
- **Problem:** Player HUD canvases render every frame regardless of camera distance/occlusion. Only players have HUDs (not enemies), so the ceiling is ~4 canvases — manageable now, linear with future on-actor UI.
- **Recommendation:** Deactivate HUD canvases beyond a camera-distance threshold, or rely on a culling mask. Low urgency until on-actor UI multiplies.
- **Where:** `PlayerHUD.cs:54-56`.
- **Severity:** low · **Effort:** low
- **Measure:** Overlay **draw** count across rooms.

### 7. No deactivation/culling of off-screen room sprites
- **Problem:** `RoomBuilder` creates floor/wall quads per room and never deactivates them; inactive rooms are frustum-checked every frame. Cheap now (5 rooms, unlit), scales poorly with room decoration density.
- **Recommendation:** A `RoomManager` that disables renderers in rooms > ~2× room-width from the active camera.
- **Where:** `RoomBuilder.cs:13-26`, `:55`.
- **Severity:** low · **Effort:** low
- **Measure:** Overlay **draw** count as rooms gain props.

### 8. FrameTimingManager.CaptureFrameTimings runs even when the overlay is hidden
- **Problem:** `DebugHud.Update` calls `FrameTimingManager.CaptureFrameTimings()` every 0.5 s **before** the `if (_visible)` guard, so it runs even with the overlay off. RDNA2 tolerates this, but on a 15 W budget a mistimed driver round-trip can microstutter.
- **Recommendation:** Move the capture inside the `_visible` branch so it only runs when the overlay is shown.
- **Where:** `DebugHud.cs:78-85`.
- **Severity:** low · **Effort:** low
- **Measure:** Toggle overlay on; watch for a periodic 0.5 s blip in **GPU ms**.

---

## CPU / GC

> This is where the listen-server actually lives or dies. The #1 issue is hot-path `Debug.Log`. Pooling is the second theme.

### 1. Unstripped `Debug.Log` with string interpolation on per-hit combat paths
- **Problem:** `Projectile.OnTriggerEnter2D` (lines 120,125,132,141,146), `Health.TakeDamage` (lines 51,57,64,69,90,97), and `SpellSystem.CmdAttack/CmdCastSpell` (lines 102,151) log interpolated, boxing strings on every hit/cast/collision. **Managed stripping High does NOT remove reachable `Debug.Log`**, and with `m_StackTraceTypes: ScriptOnly` each call also walks/formats a managed stack and writes synchronously to `Player.log`. Continuous GC + main-thread I/O directly erode the 16.7 ms budget on the 15 W host.
- **Recommendation:** Route all gameplay logs through a `[Conditional("DEVELOPMENT_BUILD")]` helper (compiles out entirely in release) and delete the per-collision/per-hit ones outright. Add `Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None)` at boot.
- **Where:** `Projectile.cs:120,125,132,141,146`; `Health.cs:51,57,64,69,90,97`; `SpellSystem.cs:102,151`.
- **Severity:** high · **Effort:** low
- **Measure:** Overlay **GC/frame** (must be 0 B in steady state) and **CPU ms** during combat; strip and re-measure — expect a clear CPU/GC drop.

### 2. No object pooling for projectiles (Instantiate + FishNet Spawn/Despawn per cast/shot)
- **Problem:** `SpellSystem.CmdCastSpell:171`, `RangedEnemyAI.ShootAtPlayer:92`, and `BossAI` (`:171,192,216`) each `Instantiate(_projectilePrefab)` then `ServerManager.Spawn`, with `ServerManager.Despawn` on hit/lifetime. No FishNet object pool is assigned. Boss Phase 3 alone is 1 lightning / 1.8 s + 6 poison / 2.5 s; with 3–4 players casting this is steady host GC + Spawn-RPC + Rigidbody2D/collider lifecycle churn. (Cooldown-rate-limited — steady churn, not an avalanche.)
- **Recommendation:** Assign a FishNet `DefaultObjectPool` and make `Projectile` `IResettable` (reset velocity/owner/damage/lifetime on reuse). Pre-warm ~64. This is the single biggest GC lever after logging.
- **Where:** `SpellSystem.cs:171,182`; `RangedEnemyAI.cs:92,103`; `BossAI.cs:171,180,192,201,216,227`; `Projectile.cs:108-114`.
- **Severity:** high · **Effort:** medium
- **Measure:** Profiler **GC.Alloc/frame** and FishNet spawn/despawn counts during a boss fight; overlay **CPU ms** spikes on salvo events.

### 3. No pooling for spell-impact VFX (7 GameObjects + 7 AddComponent per impact)
- **Problem:** `VFXSpawner.SpawnSpellImpact` builds a root + 6 child GameObjects with a SpriteRenderer each (7 `new GameObject`, 7 `AddComponent`, two arrays `new SpriteRenderer[6]`/`new Vector2[6]`, plus per-frame closure allocations) and `Destroy`s after 0.25 s. Called from `Projectile.OnTriggerEnter2D:150` (`RpcSpawnImpact`) on every projectile hit and from `BossAI` poison AoE — ~tens of GameObjects/sec during dense combat. Heavy host GC.
- **Recommendation:** Pool VFX bundles (root + 6 cached discs); `Reset()` positions/colors/scales and return to a queue on lifetime end. Cache the child SpriteRenderers; avoid per-frame closures. Or replace the 6-disc ring with a single ParticleSystem or one animated quad.
- **Where:** `VFXSpawner.cs:53-106` (SpawnSpellImpact), `:28-51` (SpawnDashTrail same pattern).
- **Severity:** high · **Effort:** medium
- **Measure:** Profiler **GC.Alloc/frame** during boss Phase 3 / spell spam; watch GameObject.Instantiate count.

### 4. `RpcShowSlash` allocates a GameObject + SpriteRenderer per melee swing
- **Problem:** Every sword swing runs `new GameObject("SlashVisual") + AddComponent<SpriteRenderer> + Destroy(0.1s)` on all observers (`SpellSystem.cs:131-142`). At 4 players × 0.3 s cooldown ≈ 13 slashes/sec system-wide, each a transient alloc.
- **Recommendation:** Pool 2–4 reusable slash objects per player (SetActive on/off), or render the slash as a single persistent quad toggled per swing.
- **Where:** `SpellSystem.cs:128-145`.
- **Severity:** high · **Effort:** medium
- **Measure:** Profiler GameObject Instantiate/Destroy and **GC.Alloc/frame** during melee spam.

### 5. SFXManager allocates a `float[]` + builds an `AudioClip` per sound
- **Problem:** All five SFX methods do `new float[sampleCount]` (352–8 820 floats, ~1.4–35 KB) and `AudioClip.Create` per call (`SFXManager.cs:14,34,75,85,96,106,116,126,142`). `Projectile.OnStartClient:73` triggers `PlaySpellCast` per projectile across networked clients. At ~10–15 sounds/sec in combat this is ~170–390 KB/sec of audio GC on the host.
- **Recommendation:** Pre-generate one cached `AudioClip` per sound type at boot; `AudioSource.PlayClipAtPoint(cached, pos)` per event (pitch-shift ±10–15 % for variety). Drops per-cast alloc from tens of KB to ~100 B.
- **Where:** `SFXManager.cs:14,34,85,106,126` (and the matching `AudioClip.Create` lines).
- **Severity:** medium · **Effort:** medium
- **Measure:** Profiler memory **GC.Alloc** filtered to SFXManager; byte count during combat should go near-flat.

### 6. `StatusEffectManager.UpdateVisuals` allocates `new Color` per call
- **Problem:** Six `_sr.color = new Color(...)` allocations in `UpdateVisuals` (lines 185–205), invoked on every mask change across networked actors. Small but cumulative; pairs with Rendering #5.
- **Recommendation:** Promote the six tints to `static readonly Color` fields.
- **Where:** `StatusEffectManager.cs:185-205`.
- **Severity:** medium · **Effort:** low
- **Measure:** Overlay **GC/frame** while effects toggle.

### 7. `Health.TakeDamage` recomputes `GetComponent<StatusEffectManager>()` + `GetCommandLineArgs()` every hit
- **Problem:** Line 115 calls `GetComponent<StatusEffectManager>()` per damage event though the component is `RequireComponent`-guaranteed and cached in `StatusEffectManager.Awake`; line 54 calls `Environment.GetCommandLineArgs()` (allocates a string[]) **every hit** just to check a headless-test flag. On the host with many hits/sec this is needless CPU + GC.
- **Recommendation:** Cache the `StatusEffectManager` reference in `Health.Awake` (mirror `_rb`). Read the `-headlessTest` flag **once** at boot into a static bool (or move test-invincibility entirely into the test harness).
- **Where:** `Health.cs:115` (GetComponent), `Health.cs:54` (GetCommandLineArgs).
- **Severity:** high · **Effort:** low
- **Measure:** Profiler Deep Profile **GetComponent** call count and **GC.Alloc/frame** during sustained combat.

### 8. `PlayerHUD.Update` polls 4 status-effect SyncVar getters every frame
- **Problem:** `Update` reads `_sem.IsBurning/IsFrozen/IsPoisoned/IsShocked` every frame (lines 156–181) and compares to cached bools. With 4 players that's 16 SyncVar property reads/frame. `StatusEffectManager` already exposes `OnEffectsMaskChanged` (line 172) which is unused by the HUD.
- **Recommendation:** Subscribe the HUD to `OnEffectsMaskChanged` and update icon visibility in the callback; delete the per-frame polling.
- **Where:** `PlayerHUD.cs:152-183`; event source `StatusEffectManager.cs:52,172`.
- **Severity:** medium · **Effort:** low
- **Measure:** Profiler Scripting property-getter calls; PlayerHUD.Update cost → ~0.

### 9. `PlayerController.MouseMove` does an uncached `Camera.main` lookup every idle FixedUpdate
- **Problem:** When stick input is idle, `FixedUpdate` falls back to `MouseMove()` which calls `Camera.main` (tag dictionary lookup) **before** the `leftButton.isPressed` short-circuit (`PlayerController.cs:136-137`). On the Deck, where Steam Input sometimes doesn't expose the gamepad, this is a warm path.
- **Recommendation:** Cache `Camera.main` once in `Awake`; reorder so the `leftButton.isPressed` check short-circuits before any camera/input read.
- **Where:** `PlayerController.cs:133-143`.
- **Severity:** low · **Effort:** low
- **Measure:** Profiler Scripting; trivial but free.

### 10. (Test-only) `AutoTestManager.Update` runs unconditional `FindObjectsByType` scans
- **Problem:** Per-frame `FindObjectsByType<Projectile/EnemyAI>` and `FindFirstObjectByType<BossAI>` (lines 49,81,90,104,118,138,144). **Gated behind `-headlessTest` (line 26) and disabled otherwise**, so it is *not* in the shipping path; checkpoint bools also make most scans one-shot.
- **Recommendation:** Leave for now; if extended, gate finds to every N frames or convert to event subscriptions (enemy death / boss spawn).
- **Where:** `AutoTestManager.cs:26` (gate), `:90,104,118`.
- **Severity:** low (test infra only) · **Effort:** medium

---

## Physics

> The base config is already correct for the Deck (zero gravity, 50 Hz, static walls, AutoSyncTransforms off, single-threaded). Forward items are about query frequency, the silent overlap-buffer cap, and per-contact allocs.

### 1. Detection-buffer overflow risk: fixed `Collider2D[32]` with no overflow guard, plus `QueriesHitTriggers` on and an all-`ff` collision matrix
- **Problem:** `EnemyAI.FindNearestPlayer` uses a static `Collider2D[32]` over a 6.5-unit radius (`EnemyAI.cs:112,116`). With `m_QueriesHitTriggers: 1` and a full collision matrix, the query also returns walls and the two large room triggers, so 3–4 players + enemies + projectiles + pickups can exceed 32 and **silently truncate** — breaking enemy targeting in dense co-op. (`SpellSystem.CmdAttack` uses the same 32-buffer at a 0.8-unit radius — far less likely to overflow.)
- **Recommendation:** Switch to a non-allocating `ContactFilter2D` with `useTriggers:false` + a **Players** LayerMask (also prunes the collision matrix); add `Debug.Assert(count < buffer.Length)` in dev builds; bump the buffer to 64. Best: maintain a static player list via `PlayerController.OnEnable/OnDisable` and skip the physics query entirely (see AI/Combat #4).
- **Where:** `EnemyAI.cs:112,116`; `SpellSystem.cs:96,105`; `unity/ProjectSettings/Physics2DSettings.asset:42` (QueriesHitTriggers).
- **Severity:** high · **Effort:** low
- **Measure:** Dev-build assert trips in a 4-player stress; verify enemies still chase above ~30 entities in one room.

### 2. AI re-acquires targets at 50 Hz (every FixedUpdate) on the host
- **Problem:** Every enemy/boss calls `FindNearestPlayer()` (an `OverlapCircleNonAlloc` + per-hit `GetComponent`) every FixedUpdate (`EnemyAI.cs:88,116`; `RangedEnemyAI.cs:35`; `BossAI.cs:62`). At 5–6 actors × 50 Hz on a shared 15 W host this is the dominant AI cost and scales linearly with enemy count.
- **Recommendation:** Re-acquire targets at ~10 Hz (every ~5th FixedUpdate, staggered per actor by instance id), caching the result between refreshes. Combine with the static player-list cache (AI/Combat #4) to make each refresh O(players).
- **Where:** `EnemyAI.cs:88,116`; `RangedEnemyAI.cs:35`; `BossAI.cs:62`.
- **Severity:** medium · **Effort:** medium
- **Measure:** Profiler **Physics2D.OverlapCircle** duration + EnemyAI.FixedUpdate self-time; overlay **CPU ms** with 8+ enemies.

### 3. `Invoke`/`CancelInvoke` for projectile despawn (O(n) scheduler, name lookup)
- **Problem:** `Projectile.OnStartServer:66` schedules despawn via `Invoke(nameof(DespawnProjectile), Lifetime)`; `OnTriggerEnter2D:151` cancels via `CancelInvoke`. Unity's Invoke list is O(n) on cancel and name-based; with dozens of concurrent projectiles this is steady host overhead. (Folds naturally into pooling — a pooled projectile uses a `_despawnTimer` decremented in FixedUpdate.)
- **Recommendation:** Replace with a `float _despawnTimer` decremented in FixedUpdate; despawn at ≤ 0; set to -1 on hit. Do this as part of the projectile pool (CPU/GC #2).
- **Where:** `Projectile.cs:66,151`.
- **Severity:** medium · **Effort:** low
- **Measure:** Profiler Scripting Invoke overhead; frame-time delta with 20+ projectiles.

### 4. `m_ReuseCollisionCallbacks` off → per-contact `Collision2D` allocations
- **Problem:** `Physics2DSettings.asset:45` `m_ReuseCollisionCallbacks: 0`, so every `OnCollisionStay2D` (EnemyAI/BossAI, fired every frame during contact) allocates a fresh `Collision2D`. Steady Incremental-GC garbage that scales with contact count.
- **Recommendation:** Set `m_ReuseCollisionCallbacks: 1`.
- **Where:** `unity/ProjectSettings/Physics2DSettings.asset:45`.
- **Severity:** medium · **Effort:** low
- **Measure:** Overlay **GC/frame** while several enemies press against players.

### 5. Bodies use `Continuous` CCD + no interpolation → micro-judder + needless solver cost
- **Problem:** `PlayerController:34` sets `CollisionDetectionMode2D.Continuous`; at these speeds (dash ~16 m/s, enemies ~2.8 m/s, boss charge ~7.8 m/s) Continuous CCD is unnecessary and host-simulated bodies have no FishNet smoothing, so 50 Hz physics vs 60/90 Hz render shows micro-judder.
- **Recommendation:** Set players/enemies to `Discrete` + `Rigidbody2D.Interpolate` (keep 50 Hz). Set EnemyAI/BossAI collision mode explicitly to `Discrete` in Awake to document intent.
- **Where:** `PlayerController.cs:34`; `EnemyAI.cs:32-40` (no mode set).
- **Severity:** low · **Effort:** low
- **Measure:** Visual judder check on a 60/90 Hz unit; Physics2D solver self-time.

### 6. Default `m_TimeToSleep: 0.5` with frequent spawn/despawn wake/sleep churn
- **Problem:** Projectiles use trigger colliders (so they're out of the solver islands — limits impact), but enemies spawn/despawn frequently and `m_TimeToSleep` is the default 0.5 s. Secondary, not primary.
- **Recommendation:** Tune `m_TimeToSleep` → ~1.0 so idle enemies sleep longer; keep projectiles as kinematic-velocity triggers (already trigger colliders).
- **Where:** `unity/ProjectSettings/Physics2DSettings.asset:18`.
- **Severity:** medium · **Effort:** high (needs solver profiling to justify)
- **Measure:** Physics2D profiler island count / contacts per frame, boss enraged.

---

## Networking

> FishNet networking is **not** a 60 fps blocker on 2D-unlit 800p — it's about host CPU/serialization headroom and battery as counts grow. There are **no per-frame RPCs**. Items here are "soon/later" scaling work, with one correctness caveat that must be checked on real hardware.

### 1. No interest management — every projectile/enemy is replicated to all players regardless of room/distance
- **Problem:** All `ServerManager.Spawn` calls (`BossAI.cs:180,201,227`; `RangedEnemyAI.cs:103`; `SpellSystem.cs:182`; `EnemySpawner.cs`) replicate to **all** observers. A Phase-3 poison burst (6 objects) syncs to 3 distant players = 18 redundant spawns. No `SetObservers` anywhere; the room structure to scope it exists but is unused.
- **Recommendation:** Add a room-keyed `NetworkObserver` condition (the rooms + camera are already room-aware). Scope projectiles/enemies to players in the same room before Spawn.
- **Where:** `BossAI.cs:180,201,227`; `RangedEnemyAI.cs:103`; `SpellSystem.cs:182`; rooms in `GameBootstrap.cs`.
- **Severity:** high · **Effort:** medium
- **Measure:** FishNet statistics: Spawned/Despawned per tick and bandwidth, vs player-count × projectile-rate.

### 2. High-frequency SyncVar writes: per-frame mana + per-hit health, no batching/threshold
- **Problem:** `SpellSystem.cs:51` writes the mana SyncVar **every frame** during regen; `Health.cs:94` writes the health SyncVar on every hit/DoT tick; `StatusEffectManager` writes the mask per effect change. FishNet's default ~30 Hz tick batches these, so the aggregate is modest today, but the per-frame mana write is the worst offender and also forces the HUD canvas rebuild (Rendering #4).
- **Recommendation:** Only write mana when it actually changes meaningfully (threshold of a few points, and clamp-skip when full); coalesce DoT health deltas into the network tick instead of per-tick writes. Quantize health to an int/byte if bandwidth ever shows up.
- **Where:** `SpellSystem.cs:51`; `Health.cs:94`; `StatusEffectManager.cs:84-100`.
- **Severity:** medium · **Effort:** medium
- **Measure:** FishNet statistics Serialize CPU + bandwidth at 4 players + boss + 8 enemies; the per-frame mana write should disappear.

### 3. No explicit FishNet TickRate; three unsynchronized rates (30 Hz net / 50 Hz physics / 60–90 Hz render) and NetworkTransform syncs unused rotation
- **Problem:** No `TimeManager` component exists, so FishNet runs its silent 30 Hz default while physics is 50 Hz and render 60/90 Hz — three unsynced rates. `Player.prefab` NetworkTransform has `_interval: 1` (every tick) and `_synchronizeRotation: 1` though rotation never changes — wasted bytes + host serialize work every tick.
- **Recommendation:** Add an explicit, documented `TimeManager`/TickRate; trim the Player NetworkTransform to **position-only** (`_synchronizeRotation: 0`, drop scale).
- **Where:** no `TimeManager` in scenes/prefabs; `unity/Assets/Game/Resources/Player.prefab:284,291`.
- **Severity:** medium · **Effort:** medium
- **Measure:** FishNet statistics tick rate + per-tick message size; bytes/tick drop after trimming the transform.

### 4. Bursty synchronous Spawn/Despawn on the host (no queueing) — folds into pooling + interest mgmt
- **Problem:** Boss Phase 3 can dispatch ~20 Spawn/Despawn messages in a single FixedUpdate (`SpellSystem.cs:182`; `BossAI.cs:180,201,227`), causing tick-time spikes.
- **Recommendation:** Largely solved by the object pool (no Spawn/Despawn churn — reuse NetworkObject identity) plus interest management. A dedicated spawn queue at tick-end is a later refinement if spikes persist.
- **Where:** `SpellSystem.cs:182`; `BossAI.cs:180,201,227`.
- **Severity:** medium · **Effort:** high
- **Measure:** FishNet statistics peak-vs-average spawn rate per tick.

### CORRECTNESS CAVEAT (verify on a 2-machine session, not a perf item)
`Projectile.prefab` has **no NetworkTransform**, and `Direction`/`Speed`/`SpellType` are set on the server after `Instantiate` with velocity applied only in server-only `OnStartServer`. On a **remote client** a projectile may spawn at the right spot but **never move** and render the default colour. Works on the host (it is the server). Confirm a Mac-host ↔ Deck-client session before relying on ranged combat in real multiplayer. (`SpellType` is a SyncVar, so colour should replicate; position/velocity will not without a NetworkTransform.)

---

## Build / IL2CPP

> The build pipeline is **already correct** and is the project's strongest area — preserve it. The only forward item is the broken release menu entry.

### 1. Build scene list hardcoded to `Main.unity` while the shipped menu exposes a "DOTS Stress Test" entry
- **Problem:** `BuildLinux.cs:59` hardcodes scenes to `Main.unity`, but `ConnectBootstrap` exposes a menu item that `SceneManager.LoadScene("Stress")` — a **user-reachable broken menu entry** in the Deck release; the on-device soak scene also can't run from a release build.
- **Recommendation:** Drive `BuildLinux` scenes from `EditorBuildSettings.scenes`, or hide the Stress menu item in release.
- **Where:** `BuildLinux.cs:59`; `ConnectBootstrap.cs` menu/LoadScene path.
- **Severity:** medium · **Effort:** low
- **Measure:** Click the menu entry in a release build — it should no longer dead-end.

### Preserve (already correct — do not regress)
IL2CPP · Release · `OptimizeSpeed` · managed stripping **High** + `link.xml` preserving FishNet/Assembly-CSharp/Steamworks · `stripEngineCode` · **incremental GC** (`ProjectSettings.asset:853`) · Vulkan→OpenGLCore · `enableFrameTimingStats` · single-threaded Physics2D · dev/release split that keeps the profiler working without an auto-connect stall. (Note: a deep-profile build is the right tool to confirm the GC/CPU wins above on real hardware.)

---

## Frame & Power

### 1. No frame-budget / thermal-adaptation loop (data is collected but unused)
- **Problem:** `DebugHud` reads CPU/GPU ms via FrameTimingManager (lines 78–83) but only displays them; nothing throttles when the APU thermal-limits and frame time spikes past 16.7 ms. Spawn rates, projectile counts, and AI tick all stay fixed under pressure. This is the right *future* lever for sustained 4-player co-op, but it depends on a real-Deck capture to set thresholds.
- **Recommendation:** Add a budget monitor: if smoothed CPU ms exceeds a threshold for several frames, enter a low-power mode (lower projectile counts, raise AI re-acquire interval, defer non-critical VFX). Build this *after* pooling + logging cleanup, since those remove the cheap headroom first.
- **Where:** `DebugHud.cs:78-83` (data source); consumers in `EnemySpawner`, `RangedEnemyAI`, `EnemyAI`.
- **Severity:** medium (high *value* but speculative until measured) · **Effort:** high
- **Measure:** Sustained 4-player capture on a physical Deck; MangoHud thermal curve + overlay CPU ms variance.

### 2. Authoritative gameplay timing runs on render-frame `Time.deltaTime` instead of the fixed tick
- **Problem:** Mana regen (`SpellSystem.cs:51`), burn/poison ticks (`StatusEffectManager.cs:112-164`, `Time.time` gates at 113,139), and ranged/melee/boss shoot gates (`RangedEnemyAI.cs:69`, `EnemyAI.cs:152`, `BossAI.cs:242`) use `Update`/`Time.time`. Under thermal throttling these drift relative to the 50 Hz sim and become bursty/non-deterministic across clients — chiefly a **determinism/consistency** issue (and bursty network traffic), less a raw perf one. (Some AI timers already correctly use `Time.fixedDeltaTime` — the pattern is inconsistent.)
- **Recommendation:** Move authoritative timers/DoT/cooldowns onto `TimeManager.OnTick` (or accumulators in FixedUpdate with `Time.fixedDeltaTime`). Replace `Time.time >=` gates with decrementing accumulators.
- **Where:** `SpellSystem.cs:51,54-55`; `StatusEffectManager.cs:113,139`; `RangedEnemyAI.cs:69`; `EnemyAI.cs:152`; `BossAI.cs:242`.
- **Severity:** medium (consistency) / low (perf) · **Effort:** low–medium
- **Measure:** Shoot-interval / DoT-tick variance under a forced 30 fps cap (F4); network damage-RPC rhythm.

### Preserve (already correct)
VSync-by-default power policy + F4 uncap for benchmarking · documented 50 Hz physics step · proper Update/FixedUpdate separation · zero-steady-state-cost `DebugHud` · SIGTERM-safe `OnApplicationQuit` teardown · bitmask status state.

---

## AI / Combat scaling

> Most AI/Combat tips converge on the same two root causes already listed under CPU/GC (pooling) and Physics (query frequency / 32-buffer). The unique scaling items:

### 1. `FindNearestPlayer` is O(players) per enemy per FixedUpdate with no spatial structure or player cache
- **Problem:** Each enemy physics-queries then linearly scans hits calling `GetComponent<PlayerController>` (`EnemyAI.cs:116,125`). At 4 enemies it's fine; it grows O(enemies × candidates) when the spawner scales to 10+.
- **Recommendation:** Maintain a static `List<PlayerController>` via `PlayerController.OnEnable/OnDisable`; enemies iterate that O(players) list directly (no physics query, no per-hit GetComponent). This also fixes the 32-buffer overflow (Physics #1) and the 50 Hz re-acquire (Physics #2) in one stroke.
- **Where:** `EnemyAI.cs:114-139`; add registration to `PlayerController`.
- **Severity:** low now / high at scale · **Effort:** low
- **Measure:** FindNearestPlayer self-time should stay < 0.1 ms even at 10+ enemies.

### 2. `FindNearestPlayer` loop has no early-exit and re-runs `GetComponent` on every hit
- **Problem:** The loop checks every collider even after the nearest is found and calls `GetComponent<PlayerController>` per hit (`EnemyAI.cs:120-137`). Subsumed by the player-cache fix (#1); if the physics query is kept, at least skip non-player layers via a LayerMask so the loop only sees players.
- **Where:** `EnemyAI.cs:120-137`.
- **Severity:** medium · **Effort:** low
- **Measure:** Profiler GetComponent count per frame.

### 3. Uncached `GetComponent` in collision callbacks
- **Problem:** `OnCollisionStay2D` calls `GetComponent<PlayerController>` + `GetComponent<Health>` per contact frame (`EnemyAI.cs:147-148`; `BossAI.cs:237-238`) — fires every frame during contact across all enemies.
- **Recommendation:** Cache the player/Health refs on contact-enter (or check a Players layer first to skip non-player contacts early).
- **Where:** `EnemyAI.cs:147-148`; `BossAI.cs:237-238`.
- **Severity:** low · **Effort:** low
- **Measure:** Profiler GetComponent count during multi-enemy contact.

### 4. Boss Phase-3 projectile spam has no global projectile cap
- **Problem:** Boss fires 1 lightning/1.8 s + 6 poison/2.5 s in Phase 3 (`BossAI.cs:143,151,209-228`), plus ranged enemies. Single-boss-per-encounter today bounds it, but there is no global cap if encounters ever stack.
- **Recommendation:** Once pooled (CPU/GC #2), enforce a per-type quota (e.g. max ~40 active); skip/delay spawns past the cap. Keep one boss per encounter by design until then.
- **Where:** `BossAI.cs:209-228`.
- **Severity:** low (current design) / medium (multi-boss) · **Effort:** medium
- **Measure:** Active-projectile count + Physics2D contacts at phase transitions.

### 5. StatusEffect visual repaint on every mask change with no consolidation
- **Problem:** `OnEffectsMaskChanged` repaints per mask change (`StatusEffectManager.cs:172`). Real impact is minimal because `Health.TakeDamage` applies only one effect per call (if/else, lines 118–121), so same-frame multi-effect stacking is rare without AoE/chain mechanics.
- **Recommendation:** Lowest priority; fold into the color-cache fix (Rendering #5). Defer to a dirty-flag in LateUpdate only if AoE/stacking is added later.
- **Where:** `StatusEffectManager.cs:172`.
- **Severity:** low · **Effort:** low

### Preserve (already correct)
Static pooled `Collider2D[]` query buffers with reference-nulling · server-authority `IsServer` gates · components cached in `Awake` · prefab `Resources.Load` cached at init · invincibility-frame gating (`Health.cs:12`) · phase-driven boss branching · bitmask status queries.
