# Caveats & Roadmap

Known limitations, Apple Silicon / ARM gotchas, the forward-looking GDC 2026 picture, the staged
rollout plan with its graduation benchmarks, and the conditions under which you should change the
recommended stack.

---

## Apple Silicon / ARM gotchas

- **Unity Editor:** Unity 6.0 LTS on Apple Silicon still requires **Rosetta 2** because the
  `UnityPackageManager` sub-process was Intel-only until **Unity 6.3** (first version with no
  Intel sub-processes, per Unity staff confirmation). Builds to other targets are unaffected.
- **Cross-compile toolchain:** the `com.unity.toolchain.macos-arm64-linux-x86_64` package runs
  partially **under Rosetta 2** on Apple Silicon. Build times are still acceptable (2–5 minutes
  for small-to-medium projects on M2/M3). The **output Linux x86_64 ELF is unaffected** and
  identical regardless of host.
- **Steamworks library on Mac:** `libsteam_api.dylib` is a universal binary (arm64 + x86_64);
  Steamworks.NET and Facepunch.Steamworks load it natively under both architectures.
- **Steam client on Mac:** the stable installer DMG still ships an Intel-only `Steam.app`
  bootstrapper that demands Rosetta 2. The **publicbeta** channel runs fully arm64 (universal2
  since June 12, 2025). Opt into Steam Beta (Settings → Beta participation → "Steam Beta Update")
  for the best Mac dev experience.
- **Avoid Docker for cross-builds.** Docker Desktop's `linux/amd64` emulation on M-series Macs
  (even with "Use Rosetta") can hang for hours. Unity's native cross-compiler is dramatically
  faster.

---

## Hard limitations to design around

- **`SteamPipeGUI` is Windows-only.** You hand-write `.vdf` build scripts on the Mac — simple
  plain-text files, a one-time investment. `steamcmd` itself runs natively on macOS.
- **Valve's official SteamOS Devkit Client does not support macOS.** Community ports exist
  (`3Samourai/SteamOS-Devkit-Client-MacOS`, `amirrajan/steamos-devkit`). The most robust path is
  plain SSH + rsync + non-Steam game — zero external dependencies, survives any community-port
  abandonment.
- **Steam allows only one running instance per account per machine.** Real Steam-transport P2P
  testing needs at least two Steam accounts on two devices (Mac + Deck). ParrelSync alone is not
  sufficient for Steam transport testing.
- **You cannot meaningfully test Verified-on-Deck without owning a Steam Deck.** Linux VMs /
  Manjaro KDE are useful for software bring-up but cannot validate GPU, screen, TDP, or input
  mapping.

---

## Rosetta 2 sunset timeline

Per Apple's published statement (MacRumors, June 10, 2025): Rosetta will be available "for the
next two major macOS releases — through **macOS 27**," with a subset retained beyond that for
older unmaintained gaming titles relying on Intel-based frameworks.

- Plan to be on **Unity 6.3+** by ship time if your game has a long support tail.
- **Unity 6.0 LTS goes EOL (October 2027)** right around when Rosetta 2 is sunsetted in macOS 28
  (~fall 2027).

---

## GDC 2026 roadmap — Unity first-party Steam support

- At **GDC 2026 (March 11, 2026)**, Unity announced first-party Steam, native Linux, Steam Deck,
  and Steam Machine support via the **Platform Toolkit for Steamworks**
  (`com.unity.platformtoolkit.steam` — documented version **1.0.2**, tested against Steamworks
  **SDK 1.62**, requiring **Unity 6.3**).
- Per James Stone (Unity Platforms Team): native improvements to the Linux player targeting Steam
  Deck hardware offer a *potential* performance improvement over running a build on Proton, and
  are *"available today."*
- **This is additive**, not a replacement for FishySteamworks / Steamworks.NET. Treat it as a
  future migration option once stable; most indies will continue using the community stack
  through 2026. Unity published **no benchmarks** alongside the announcement — measure with
  MangoHUD on your actual title before assuming wins over a Proton-run Windows build.

---

## Staged rollout & milestones

A suggested sequence so pipeline risk is retired before networking code is written. Each stage
has an explicit gate before moving on.

**Stage 1 — Set up the pipeline before writing any networking code (Week 1):**

- Install Unity 6.3 + Linux Build Support (IL2CPP) on the Mac. Verify a Hello-World cube project
  builds to Linux and runs on the Deck as a non-Steam game.
- Buy a Steam Deck OLED — at $789 USD it is non-optional; skipping it surfaces Verified-on-Deck
  failures at the worst possible time.
- Create a free secondary Steam account dedicated to Deck testing.
- **Gate:** don't move on until this build → deploy → launch round-trip works in **under 5
  minutes**.

**Stage 2 — Wire up Steamworks and one-peer test (Week 2):**

- Add Steamworks.NET + FishNet + FishySteamworks via UPM. Use appID **480** for now.
- Build for Linux, deploy to Deck, host on Mac (editor Play Mode), join from Deck. Confirm
  `SteamAPI.IsSteamRunning()` is true on both, and a FishySteamworks `ServerManager.StartConnection`
  succeeds against the Mac's `steamID64`.
- **Benchmark to graduate:** 2-peer connection with physics objects syncing over the Steam relay
  between Mac editor and Deck build, **≤ 100 ms RTT**.

**Stage 3 — 3–4 peer testing (Week 3–4):**

- Register the real appID with Steamworks once you have a store page; switch from 480 to it.
- Set up `steamcmd`-on-Mac with a `devtest` private beta branch (push = 1 command); distribute
  the password to 1–3 testers.
- Use FishNet's Multipass so both Tugboat (ParrelSync editor testing) and FishySteamworks (Steam
  beta-branch testing) are selectable at runtime.

**Stage 4 — Verified-on-Deck readiness (last 25% of project):**

- Vulkan-first graphics; no shader requiring features outside Steam Deck RDNA 2's capability set.
- On-screen keyboard via `ShowFloatingGamepadTextInput` / `ShowGamepadTextInput` wherever text
  input is required (Verified badge requirement).
- Default controller config covers all functionality; no external launcher (launchers fail Deck
  UX expectations).
- **Targets:** 60 fps at 800p with 4 networked physics players, ≤ 15W typical TDP,
  offline-mode-tolerant for any singleplayer content.

---

## When to change the recommendation

- **Iteration speed is a bottleneck and you have ~$20/month:** spin up a Hetzner CCX13
  (~$19.99/mo) with a headless Steam install as Client 3, replacing the "friend on Discord" peer
  for daily 3-peer testing.
- **You outgrow Steamworks.NET / Facepunch integration friction:** migrate to Unity's Platform
  Toolkit for Steamworks (`com.unity.platformtoolkit.steam` v1.0.2, Unity 6.3+).
- **The Unity 6.0 LTS Rosetta dependency annoys you:** upgrade to Unity 6.3+ —
  `UnityPackageManager` went native there.
- **FishySteamworks shows P2P relay limits at 4 players with heavy physics:** consider Unity
  Transport (UTP) over Steam Datagram Relay, or dedicated-server mode with FishyUnityTransport —
  FishNet's Multipass makes the swap trivial.
