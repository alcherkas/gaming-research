# Architecture

This document describes the architecture of a **Unity 6 physics-based multiplayer game**
built and shipped as a **native-Linux Steam Deck title** entirely from an **Apple Silicon
MacBook** — no Windows machine in the loop.

It is the structural, "what is this and why" view. For step-by-step procedures, error
tables, version pins, and pricing data, see [`docs/research/`](docs/research/).

---

## 1. Context & Goal

Ship a 3–4 player physics multiplayer game that runs **natively on the Steam Deck (Linux,
x86_64)** and is developed end-to-end on an Apple Silicon Mac. The goal is a working,
"Verified-on-Deck"-ready pipeline where a developer with only a Mac and a Steam Deck can
author, build, deploy, and run real Steam peer-to-peer multiplayer sessions.

Why this is now possible without Windows:

- Unity 6 supports **IL2CPP cross-compilation from macOS arm64 to Linux x86_64** via the
  `com.unity.toolchain.macos-arm64-linux-x86_64` toolchain package (first stable in Unity
  6000.2, December 5, 2025).
- Steamworks ships **native Linux `.so`, macOS universal `.dylib`, and Windows `.dll`**
  libraries in a single SDK download.
- `steamcmd` runs natively on macOS, so SteamPipe uploads work from a Mac.
- A retail Steam Deck doubles as a devkit.

---

## 2. System Overview

The system spans three planes — authoring on the Mac, a build/deploy pipeline, and the
runtime that executes on the Deck and connects peers over Steam's relay.

```
┌────────────────────────── AUTHORING PLANE (Apple Silicon Mac) ───────────────────────────┐
│                                                                                           │
│   Unity 6 Editor (Rosetta-assisted on 6.0 LTS; native on 6.3+)                            │
│     • URP project, IL2CPP scripting backend                                               │
│     • FishNet + Multipass  ·  Steamworks.NET  ·  FishySteamworks                          │
│     • Fast loop: ParrelSync clones + Tugboat (UDP) transport                              │
│                                                                                           │
└───────────────────────────────────────────┬─────────────────────────────────────────────┘
                                             │  Build Profiles → Linux / x86_64 / IL2CPP / Vulkan-first
                                             ▼
┌────────────────────────── BUILD & DEPLOY PIPELINE ───────────────────────────────────────┐
│                                                                                           │
│   IL2CPP cross-compile (toolchain runs under Rosetta) ──► native Linux x86_64 ELF         │
│        output: MyGame.x86_64 · UnityPlayer.so · MyGame_Data/ · Plugins/                   │
│                                                                                           │
│   Deploy (pick by cadence):                                                               │
│     A. steamcmd → private Steam beta branch   (ship-like)                                 │
│     B. rsync/SSH → non-Steam game on Deck     (fastest iteration)                         │
│     C. SteamOS Devkit Client (macOS port)     (GUI rsync-over-SSH)                         │
│                                                                                           │
└───────────────────────────────────────────┬─────────────────────────────────────────────┘
                                             │
                                             ▼
┌────────────────────────── RUNTIME PLANE ─────────────────────────────────────────────────┐
│                                                                                           │
│   Steam Deck (RDNA 2 APU, Vulkan/RADV, 800p, ~15W)  ── 2nd Steam account = client/peer    │
│                          ▲                                                                │
│                          │  FishySteamworks  ⇄  Steam Datagram Relay (SDR) P2P            │
│                          ▼                                                                │
│   Mac editor / build (host)   +   optional 3rd–4th peers (cloud Linux VM, friend)         │
│                                                                                           │
└─────────────────────────────────────────────────────────────────────────────────────────┘
```

---

## 3. Target Platform & Constraints

The Steam Deck is the primary deployment target, and its hardware dictates several
non-negotiable architectural choices.

| Constraint | Implication |
|---|---|
| APU is **x86_64 / RDNA 2** (never ARM) | Build target is `x86_64`; runtime shaders target RDNA 2 / Mesa RADV. |
| **Vulkan-first** (Valve's explicit recommendation) | Vulkan is the primary graphics API; OpenGLCore is fallback only. |
| **800p screen, ~15W TDP** | Target 60 fps at 800p with 4 networked physics players; budget for battery/thermals. |
| **Gamepad-first input** | Default controller config must cover all functionality; no external launcher. |
| **On-screen keyboard** for any text input | Use Steam's `ShowFloatingGamepadTextInput` / `ShowGamepadTextInput` (Verified requirement). |
| Offline tolerance | Singleplayer content should tolerate offline mode. |

These collectively define the "Verified-on-Deck" readiness bar; they cannot be validated on
emulators/VMs and require a physical Deck.

---

## 4. Runtime Architecture

The runtime is organized as four cooperating layers.

### 4.1 Engine layer
- **Unity 6** (6.3+ recommended for a fully native Apple Silicon editor experience).
- **URP** as the render pipeline — the sweet spot for a 3–4 player physics game on Deck.
- **IL2CPP** scripting backend for better runtime performance and smaller startup; Mono is
  the fallback if the cross-compile toolchain misbehaves.

### 4.2 Networking layer
- **FishNet** as the netcode framework, using a **host/client** model.
- **Multipass** keeps multiple transports selectable at runtime:
  - **Tugboat (UDP)** — used for fast local iteration (works with ParrelSync clones, no Steam
    dependency).
  - **FishySteamworks** — the shipping transport; routes P2P traffic through the **Steam
    Datagram Relay (SDR)**, giving real relay/NAT behavior.
- Physics sync is validated with FishNet's `TransportManager` latency simulation before
  hardware testing.

### 4.3 Platform-integration layer
- **Steamworks.NET** (preferred for Mac-only development; UPM install is least error-prone) or
  **Facepunch.Steamworks** as the Steam API binding.
- Per-target native libraries selected by build target:

  | Target | Native library |
  |---|---|
  | Linux x86_64 (Deck) | `libsteam_api.so` (some wrappers expect `libsteam_api64.so`) |
  | macOS (editor/client) | `libsteam_api.dylib` — universal arm64 + x86_64 |
  | Windows | `steam_api64.dll` |

- `steam_appid.txt` next to the binary for out-of-Steam testing; `SteamAPI_RestartAppIfNecessary`
  handles app context when launched through Steam. SDR must be enabled for the dev appID.

### 4.4 Rendering layer
- **Vulkan primary, OpenGLCore fallback** in Player Settings → Graphics APIs for Linux.
- Shaders must stay within **RDNA 2 / Mesa RADV** capabilities; avoid Metal-specific shader
  features. URP/HDRP with Vulkan-compatible shaders is the safe path.

---

## 5. Build & Deployment Pipeline Architecture

### 5.1 Cross-compile
- Toolchain package: **`com.unity.toolchain.macos-arm64-linux-x86_64`** (Intel-Mac host variant:
  `com.unity.toolchain.macos-x86_64-linux-x86_64`).
- The toolchain itself runs partially **under Rosetta 2** on Apple Silicon, but the produced
  **Linux x86_64 ELF is fully native** and identical regardless of host — the Rosetta dependency
  never reaches the build output.
- Build settings: Linux / `x86_64` / IL2CPP / Vulkan-first.
- Output folder structure: `MyGame.x86_64`, `UnityPlayer.so`, `MyGame_Data/`, and the Plugins
  payload.

### 5.2 Deployment paths (architectural options)

| Path | Mechanism | When to use |
|---|---|---|
| **A — Steam private beta branch** | `steamcmd` (`builder_osx/steamcmd.sh`) + hand-written `.vdf` scripts → push to a password-protected beta branch | Ship-like validation: real overlay, cloud saves, achievements, real lobbies. |
| **B — rsync / SSH non-Steam game** | `rsync -a` over SSH to the Deck, registered as a non-Steam game | Fastest day-to-day iteration (~2–3 min round-trip). |
| **C — SteamOS Devkit Client (macOS port)** | Community macOS port of Valve's devkit client; GUI rsync-over-SSH with "Devkit Game" entries | Devkit-style workflow preferred; willing to depend on a community port. |

`SteamPipeGUI` is Windows-only; the `.vdf` build scripts for path A are hand-written plain text.
Path B has zero external dependencies and survives any community-port abandonment.

---

## 6. Multiplayer Test Topology

Steam permits **only one running instance per account per machine**, so real Steam-transport
P2P testing requires multiple devices/accounts. The Steam Deck is therefore the single
highest-value test peer.

| Client | Role | Notes |
|---|---|---|
| **1 — Mac editor / build** | Host | `SteamAPI.Init()` against the dev appID. |
| **2 — Steam Deck** | Client/peer | Runs the native Linux build under a **second Steam account**; validates real SDR P2P **and** Deck performance simultaneously. |
| **3 — optional** | Peer | Cloud Linux VM (e.g. Hetzner CCX13) or a second machine for reliable 3+ peer testing. |
| **4 — optional** | Peer | Friend on a private beta branch — surfaces real NAT/relay behavior. |

ParrelSync and Unity's Multiplayer Play Mode are useful for non-Steam (Tugboat) iteration only;
they **cannot** authenticate as separate Steam users and are not a substitute for real Steam
transport testing.

---

## 7. Key Architectural Decisions

| Decision | Rationale | Alternative / fallback |
|---|---|---|
| **IL2CPP** scripting backend | Better runtime perf, smaller startup; native Linux ELF output | Mono (safe fallback if toolchain misbehaves) |
| **Vulkan-first** graphics | Valve-recommended; best Deck performance & battery | OpenGLCore (fallback only) |
| **URP** render pipeline | Best fit for a physics game at 800p / 15W | HDRP (heavier; not ideal for Deck budget) |
| **FishNet + Multipass** | Swap Tugboat (fast iteration) ↔ FishySteamworks (ship) without rewrites | Unity Transport over SDR; dedicated server (FishyUnityTransport) |
| **FishySteamworks + Steamworks.NET** (community stack) | Mature, well-documented, works today on Mac | Unity's first-party Platform Toolkit for Steamworks (future migration, Unity 6.3+) |
| **rsync/SSH non-Steam deploy** for iteration | Fastest loop, zero external dependencies | Devkit client port; steamcmd beta branch for ship-like runs |
| **Steam Deck as 2nd test peer** | Only practical way to do real SDR P2P + hardware validation | Cloud Linux VM as additional peer |
| **Unity 6.3+** | First editor with no Intel sub-process (fully native Apple Silicon) | Unity 6.0 LTS (works, but requires Rosetta 2 for `UnityPackageManager`) |

---

## 8. Cross-References

- Step-by-step setup, deployment methods, and the per-iteration loop:
  [`docs/research/deployment-runbooks.md`](docs/research/deployment-runbooks.md)
- Common errors and fixes, Linux-build debugging:
  [`docs/research/troubleshooting.md`](docs/research/troubleshooting.md)
- Caveats, Apple Silicon gotchas, GDC 2026 roadmap, Rosetta sunset timeline, staged rollout:
  [`docs/research/caveats-and-roadmap.md`](docs/research/caveats-and-roadmap.md)
- Pinned versions, dates, pricing, and key quotes:
  [`docs/research/sources-and-versions.md`](docs/research/sources-and-versions.md)
