# Steam Deck Architecture & Performance Audit

_Audited 2026-06-08 · Unity 6 (6000.4.10f1) / URP 17.4.0 · target: native-Linux Steam Deck._

A verification of the prototype's **overall architecture** and **Steam-Deck performance posture**.
No code or config was changed to produce this report — it is an assessment with a prioritized,
actionable roadmap.

> This is a **dated snapshot** (the audit that seeded the performance docs). For the current,
> expanded, and re-verified action list, see [`roadmap.md`](roadmap.md) — it supersedes the
> *Prioritized roadmap* section below.

**How it was produced:** direct read of all ~30 first-party scripts + every relevant
`ProjectSettings/` and `Assets/Settings/` URP asset, cross-checked by a 10-dimension audit that
adversarially verified each finding against the cited file. **72 findings raised → 72 survived
verification → 0 refuted.** Severities are Steam-Deck-relative (RDNA2 8-CU GPU, Zen2 4c/8t, 16 GB
**shared** LPDDR5, ~15 W TDP, 800p, 60 Hz LCD / 90 Hz OLED; goal = 60 fps @ 800p with 3–4 networked
players).

---

## Verdict

| Axis | Result |
|---|---|
| **Architecture** | **Sound for a prototype.** Coherent listen-server topology, clean component-per-concern decomposition, idiomatic server-authoritative combat. The main issue is *documentation*, not structure. |
| **Steam-Deck performance** | **On-track.** A 2D unlit game at 800p is nowhere near fill- or solver-bound here. No unbounded leaks found. |
| **Real risk** | **Host-side CPU/GC churn + wasted GPU watts** — not a framerate ceiling. Concentrated in ~5 concrete, low-regret items. |
| **Severity spread** | 2 high · 13 medium · 46 low · 11 info. (The 2 highs are the *same* issue — hot-path logging — seen from two dimensions.) |

The prototype will hit 60 fps @ 800p at current scene complexity. The optimizations below are mostly
about **battery/thermal headroom and host scalability** as player/enemy/projectile counts grow —
which matters because on the Deck the *host* carries the full physics + AI + combat sim for all
4 players inside the same 15 W budget.

### Baseline that is already correct (don't regress)
- **Build:** IL2CPP · Release · `OptimizeSpeed` · managed stripping **High** · `stripEngineCode` · **incremental GC** · StandaloneLinux64/x86_64 · **Vulkan→OpenGLCore** (set in `ProjectSettings.asset` *and* programmatically in `BuildLinux.cs`). `link.xml` preserves FishNet.Runtime / Assembly-CSharp / Steamworks.NET against High stripping.
- **Render:** SRP Batcher on, single shared 64×32 sprite atlas + one material ≈ **1 draw call**, MSAA off, HDR off, renderScale 1.0, single ortho camera, no lights/post in use.
- **Physics:** zero gravity, static wall colliders (no Rigidbody), `AutoSyncTransforms` off, **deliberate documented 50 Hz** step.
- **Runtime:** borderless fullscreen + native res for gamescope, dual-backend input for Steam Input's Linux quirk, VSync-for-battery default.

---

## Architecture assessment

**Networking model (verified): a listen-server _hybrid_ — not what either doc claims.**
- **Player movement → client/owner-authoritative.** `NetworkPlayer` enables `PlayerController` only for the owner; `Player.prefab` NetworkTransform has `_clientAuthoritative:1`, no prediction/reconciliation. Good — hides Steam-relay RTT for the local player.
- **Combat / health / mana / status / AI / projectiles → server-authoritative.** `[Server]`, `[ServerRpc]`, server-written SyncVars, `IsServer`-gated simulation. Clients cannot fabricate damage.
- **Naming nuance:** "server-authoritative" applies only to combat, not movement — it's a listen-server *hybrid*. Also `DamageSystem.cs` is *only* enums + a `DamageInfo` struct; the actual damage "processing" lives in `Health.TakeDamage`.

**Structural smells (non-blocking, will not scale unaddressed):**
- **Player composition duplicated across 3 paths** — `GameBootstrap.BuildPlayer`, `NetworkSetup.CreatePlayerPrefab`, `BuildLinux.SetupPrefabs` (which defensively re-injects missing components). Silent drift risk.
- **Test scaffolding in shipping paths** — `-headlessTest` argv parsing inside `Health.TakeDamage`; global static mock-input in `InputBridge`; `AutoTestManager` always added by `GameBootstrap`.
- **Authoritative gameplay timing on render-frame `deltaTime`** (mana/DoT/i-frames in `Update`) rather than the fixed tick — host-framerate-coupled; minor today.
- **No assembly definitions / namespaces** — editor/test code isn't excluded at the assembly level.

---

## Performance findings vs the Steam-Deck budget

### HIGH
1. **Unstripped `Debug.Log` with string interpolation on per-hit combat hot paths.** `Health.TakeDamage`
   logs ~6×/hit, `Projectile.OnTriggerEnter2D` logs on every collision, and `SpellSystem` logs per
   cast/attack — all interpolated, boxing strings. **Managed stripping High does NOT remove reachable
   `Debug.Log`**, and `m_StackTraceTypes` is `ScriptOnly` for *all* log types, so each call also
   walks/formats a managed stack and writes synchronously to `Player.log`. Continuous GC + main-thread
   I/O eroding the 16.7 ms budget on the 15 W host.
   _(Health.cs:51,57,64,69,90,97; Projectile.cs:120,125,132,141,146; SpellSystem.cs:102,151; ProjectSettings.asset:57)_
   _(Correction: the per-tick `EnemyAI.FixedUpdate` log this audit first cited at `EnemyAI.cs:62` no longer exists in the current tree; the hot-path logging has since narrowed to the files above. Still HIGH.)_

### MEDIUM
2. **Per-tick allocating `Physics2D.OverlapCircleAll`, no LayerMask.** `EnemyAI.FindNearestPlayer` runs
   every FixedUpdate per enemy (before the freeze early-out), allocating a fresh `Collider2D[]` + a
   `GetComponent` per hit; with `QueriesHitTriggers` on and an all-`ff` collision matrix it also returns
   walls and the two large room triggers. Same pattern in `SpellSystem.CmdAttack` melee. Steady
   Incremental-GC garbage that scales linearly with enemy count. _(EnemyAI.cs:102; SpellSystem.cs:92)_
3. **Heavy "PC" quality tier active on Standalone.** The Deck build resolves to quality index 1 = PC →
   `PC_RPAsset` + **`PC_Renderer` (Deferred, `m_RenderingMode:2`) with an _active_ full-res depth-normals
   SSAO feature**, plus `RequireDepthTexture`/`RequireOpaqueTexture` forcing full-screen passes, 2048
   shadowmaps, soft shadows — **zero visible effect** on unlit sprites with no lights/Light2D. Pure wasted
   GPU fill/bandwidth/watts on 8 CUs. A lean `Mobile_RPAsset` (depth 0, opaque 0, addl-shadows 0) already
   exists but **excludes Standalone**, so it's unreachable. _(QualitySettings.asset:7,104,129; PC_Renderer.asset:56–95; PC_RPAsset.asset:22,23,46,66)_
4. **No FishNet object pool.** `NetworkManager._objectPool` is unset, so every spell cast does
   `Resources.Load` + `Instantiate` + Spawn → Despawn/Destroy 2.5 s later; `RpcShowSlash` does
   `new GameObject` + `AddComponent` + `Destroy` per swing on every client. Cooldown-rate-limited
   (~10 cycles/sec at 4 players), so a steady churn, not an avalanche — but recurring host GC.
   _(Main.unity:481; SpellSystem.cs:136,144,155,109–124; Projectile.cs:53–59)_
5. **No 60 fps cap → 90 Hz OLED Deck free-runs at 90 fps under VSync** (~50% more frames than this game
   needs), raising APU draw + heat. 60 Hz LCD unaffected; gamescope *may* cap in Gaming Mode. _(FrameRatePolicy.cs:38–41)_
6. **Build scene list hardcoded to `Main.unity`** while the shipped menu exposes "DOTS Stress Test"
   (`SceneManager.LoadScene("Stress")`) — a **user-reachable broken menu entry** in the Deck build, and the
   on-device soak path can't be run from a release build. _(BuildLinux.cs:59; EditorBuildSettings.asset; ConnectBootstrap.cs:36,144)_

### LOW / INFO (representative)
- `Health.TakeDamage` calls `Environment.GetCommandLineArgs()` (alloc) per hit; uncached `GetComponent<StatusEffectManager>` per hit.
- `Rigidbody2D` interpolation off + `Continuous` CCD on bodies — 50 Hz-vs-60/90 Hz micro-judder (owner body + host-simulated enemies have no FishNet smoothing); CCD unneeded at these speeds.
- Player NetworkTransform syncs rotation+scale every tick though neither changes; silent **30 Hz** default FishNet tick (no explicit `TimeManager`) vs 50 Hz physics vs 60/90 Hz render — three unsynced rates.
- `m_ReuseCollisionCallbacks` off → per-contact `Collision2D` allocs in `EnemyAI.OnCollisionStay2D`.
- `Camera.main` in `PlayerController.MouseMove` (FixedUpdate path); `ConnectBootstrap` reflects into FishNet's private `_transports` (fragile under IL2CPP/High strip); Adaptive Performance flags on with no package installed.

### Networking correctness caveat (out of this audit's scope — confirm on a 2-machine test)
`Projectile.prefab` has **no NetworkTransform**, and `Direction`/`Speed`/`SpellType` are **plain public
fields (not SyncVars)** set on the server after `Instantiate`; velocity is set only in server-only
`OnStartServer`. So on a **remote client** (Deck joining a Mac host) a projectile should spawn at the
right position but **never move**, and render as the default "fire" colour regardless of true type.
It works on the host (which is the server). Confirm via a Mac-host ↔ Deck-client session before relying
on ranged combat in real multiplayer.

---

## Prioritized roadmap

### Now — trivial/small, highest-impact, lowest-regret
- Strip/guard the hot-path combat logs (`Health.TakeDamage`, `Projectile.OnTriggerEnter2D`, `SpellSystem`) via a `[Conditional("DEVELOPMENT_BUILD")]` helper and delete the per-hit ones outright. Add `Application.SetStackTraceLogType(LogType.Log, None)` at boot.
- Harden the Deck render path: disable the SSAO feature, switch `PC_Renderer` to **Forward**, set `RequireDepth`/`RequireOpaque` to 0 (cleanest via **extending the existing `RenderConfig.cs`**, which already hardens MSAA/HDR/renderScale) — **or** repoint Standalone at the Mobile tier.
- `Application.targetFrameRate = 60` alongside `vSyncCount = 1` in `FrameRatePolicy` VSync mode.
- Drive `BuildLinux` scenes from `EditorBuildSettings.scenes` (or drop the Stress menu item in release).
- Document the real hybrid authority model (movement owner-authoritative, combat server-authoritative) in the README.

### Soon — small/medium, the substantive perf wins
- Non-alloc `OverlapCircle` + reused buffer + `ContactFilter2D(useTriggers:false)` + a Players LayerMask; re-acquire AI targets at ~10 Hz not 50 Hz. Add a Players/Enemies/Walls/Projectiles/Triggers layer scheme + prune the collision matrix; default `QueriesHitTriggers` off.
- Assign a FishNet `DefaultObjectPool`; pool Projectile/Enemy with `IResettable`; cache the `Resources.Load` result; reuse one per-player slash visual.
- `Rigidbody2D` → `Interpolate` + `Discrete` on players/enemies (keep 50 Hz).
- Add an explicit FishNet `TimeManager`/TickRate (documented); set `m_ReuseCollisionCallbacks=1`; trim Player NetworkTransform to position-only.
- Cache the headless-test flag once + move test invincibility into the harness; gate/compile-out the InputBridge mock layer for release.

### Later — medium/large, structural
- Authoritative timers onto `TimeManager.OnTick`; decide knockback authority explicitly.
- `Game`/`Game.Editor`/`Game.Tests` asmdefs + namespaces; single `PlayerComposition` factory (kill the 3-way drift); room-keyed `NetworkObserver` interest management.
- A **representative 4-player networked perf harness** (the current "stress" scene is a non-networked DOTS GPU-instancing demo; `AutoTestManager` is a single-host smoke test — neither gates the real co-op bottleneck).

---

## Strengths to preserve
SRP-batched single-atlas 2D (~1 draw call) · correct power-limited Physics2D setup · clean component
decomposition with `RequireComponent` + SyncVar-OnChange for health/mana (though `PlayerHUD` still polls the status-effect getters every frame — see `roadmap.md` CPU/GC #8) · idiomatic server-authoritative
combat · **no per-frame RPCs anywhere** · idempotent network lifecycle with no-Steam fallback +
SIGTERM-safe teardown · correct IL2CPP/Vulkan Deck build pipeline with a sensible dev/release split ·
allocation-aware Deck-relevant `DebugHud` (CPU/GPU ms via FrameTimingManager) · headless-safe procedural
scene-build + lean MangoHud-integrated deploy scripts.

## Open questions (need a physical Deck / maintainer decision)
- Measure steady-state **and** combat-peak CPU/GPU ms + GC/frame with 3–4 networked players + 4 enemies firing continuously (currently unmeasured).
- Does gamescope already cap the 90 Hz OLED to 60 in Gaming Mode? (reduces urgency, not correctness, of the fps cap).
- Is the hybrid authority model the intended shipping model? (competitive fairness would need FishNet prediction — a design change, not a tweak).
- Deliberate FishNet TickRate vs 50 Hz physics vs 60/90 Hz render?
- Confirm the out-of-Steam launch finds `steam_appid.txt` next to the binary (the build-time copy uses a CWD-relative path).
