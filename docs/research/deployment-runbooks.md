# Deployment Runbooks

Step-by-step procedures for setting up the pipeline, deploying to a Steam Deck, and the
per-iteration build/test loop. For the architectural overview of these paths, see
[`../../ARCHITECTURE.md`](../../ARCHITECTURE.md) §5.

---

## Editor & modules (one-time, on the Mac)

Install via Unity Hub on macOS:

- **Unity 6** (6000.x LTS, or **Unity 6.3** for a fully native Apple Silicon experience
  including `UnityPackageManager`).
- Modules: **Linux Build Support (IL2CPP)** and **Linux Build Support (Mono)** — both ship in
  the Hub for macOS editor installs. Optionally **Linux Dedicated Server Build Support** for a
  headless server.
- The IL2CPP cross-compiler toolchain is auto-installed when you switch to the Linux target. If
  not, install manually via Package Manager:
  - Apple Silicon host: `com.unity.toolchain.macos-arm64-linux-x86_64`
  - Intel Mac host: `com.unity.toolchain.macos-x86_64-linux-x86_64`

### Build settings

- File → Build Profiles → Linux → Architecture: **`x86_64`** (the Deck APU is x86_64 / RDNA 2).
- Scripting Backend: **IL2CPP** (Mono is the safe fallback).
- API Compatibility Level: .NET Standard 2.1 or .NET Framework (both work with
  FishNet/Steamworks.NET).
- Player Settings → Other Settings → Graphics APIs for Linux: **Vulkan first**, then OpenGLCore.
- IL2CPP Code Generation: "Faster runtime" for ship, "Faster (smaller) builds" while iterating.
- Output: a folder containing `YourGame.x86_64`, `UnityPlayer.so`, `YourGame_Data/`, and the
  Plugins payload.

---

## Deployment methods

Pick by iteration cadence. See the trade-off summary in
[`../../ARCHITECTURE.md`](../../ARCHITECTURE.md) §5.2.

### Method A — Steam private/dev beta branch via `steamcmd` (production-quality)

1. Download the Steamworks SDK on your Mac.
2. `cd sdk/tools/ContentBuilder`. Write `app_build_<appid>.vdf` and `depot_build_<depotid>.vdf`
   by hand (the GUI generator is Windows-only; the files are plain VDF text).
3. Run:
   ```bash
   bash ./builder_osx/steamcmd.sh +login <user> \
     +run_app_build ../scripts/app_build_<appid>.vdf +quit
   ```
   First run bootstraps `steamcmd`; subsequent runs reuse cached `config.vdf` for Steam Guard.
4. In Steamworks partner site → Builds tab, push the build to a private beta branch (e.g.
   `devtest`) with a password.
5. On the Deck: Desktop Mode → Steam → right-click your dev appID → Properties → Betas → enter
   password → select branch. Back to Gaming Mode; the Deck downloads and runs under your dev
   account.

This is the most ship-like pipeline: overlay, cloud saves, achievements, and real
FishySteamworks lobbies all behave as players will see them.

### Method B — Direct SCP/rsync + non-Steam game (fastest iteration)

1. On the Deck: Steam → Settings → System → enable **Developer Mode** → Power → Switch to
   Desktop. In Konsole: `passwd` to set a password, then:
   ```bash
   sudo systemctl enable --now sshd
   ```
2. On the Mac:
   ```bash
   ssh-keygen -t ed25519
   ssh-copy-id deck@<deck-ip>
   ```
3. From the Mac, sync the build:
   ```bash
   rsync -avh --delete ~/MyGame/Build/Linux/ deck@<deck-ip>:/home/deck/Games/MyGame/
   ```
4. On the Deck (Desktop Mode), once:
   ```bash
   chmod +x /home/deck/Games/MyGame/MyGame.x86_64
   ```
   (`rsync -a` preserves the exec bit afterward.)
5. Steam (Desktop Mode) → Add a Non-Steam Game → browse to `MyGame.x86_64` (set the file filter
   to "All Files"). Back to Gaming Mode; the game appears under "Non-Steam." The shortcut
   persists across iterations.

### Method C — SteamOS Devkit Client (community Mac port)

- Valve's official Devkit Client is Windows/Linux only. The community macOS port runs natively
  on macOS 14 Sonoma / 15 Tahoe beta using `uv` (modern pip replacement) with Python 3.12.
- Pair via Settings → System → Pair new host on the Deck, register your Mac in the client, and
  use the Title Upload tab. Set **Steam Play** to *unchecked* so the Deck runs it as native
  Linux.

---

## One-time setup checklist (≈ 2 hours)

1. Install Unity Hub → Unity 6.3 (or latest LTS) with **Linux Build Support (IL2CPP)** and
   **Linux Build Support (Mono)**. Confirm `com.unity.toolchain.macos-arm64-linux-x86_64` is installed.
2. Install FishNet, Steamworks.NET, FishySteamworks. Wire up Steamworks.NET's `SteamManager`;
   set `steam_appid.txt` (use **480 / Space War** — Valve's free test appID — for early testing).
3. Get a Steamworks partner account; register the real appID once you have a store page.
4. Enable Developer Mode on the Deck, set a password, enable `sshd`, copy your SSH pubkey, test
   `ssh deck@<ip>`.
5. Set up the `builder_osx/steamcmd.sh` flow **or** install the community devkit client port.
6. On the Deck, log in as a **separate Steam account** dedicated to testing.

---

## Per-iteration build & test loop (≈ 3–5 minutes)

1. In Unity (macOS editor): Build Profiles → Linux → x86_64 → IL2CPP → Vulkan first → Build.
   Output to `~/Builds/Linux/`.
2. From the Mac:
   ```bash
   rsync -avh --delete --info=progress2 \
     ~/Builds/Linux/ deck@steamdeck.local:/home/deck/Games/MyGame/
   ```
   (`-a` preserves the exec bit Unity sets — no `chmod +x` after the first transfer.)
3. (First time only) On the Deck, Desktop Mode → Steam → Add a Non-Steam Game → All Files →
   `MyGame.x86_64`.
4. Gaming Mode → launch from Non-Steam. Concurrently launch the Mac editor in Play Mode (or a
   second build) as host.
5. Test 2-client networking: Mac hosts, Deck joins via Steam ID, FishySteamworks routes through
   Steam Datagram Relay.

### Cadence recommendation

- Daily: develop in the Mac editor with **ParrelSync + Tugboat (UDP)** for fast multi-instance
  testing.
- Weekly (minimum) and before every milestone: build to Linux and test on the Deck — catches
  Vulkan shader issues, gamepad mapping, on-screen keyboard text input, 800p legibility, and 15W
  TDP performance early.
- Use `SetGameLauncherMode` to translate gamepad input to keyboard/mouse if you have a native
  launcher UI.

---

## Debugging Linux builds from the Mac

- Set **Development Build** + **Script Debugging** in Build Profiles. Optionally enable IL2CPP
  C++ source generation for stack traces.
- `Player.log` lives at `~/.config/unity3d/<Company>/<Product>/Player.log` on the Deck. Stream it
  during runs:
  ```bash
  ssh deck@steamdeck.local tail -F ~/.config/unity3d/MyCompany/MyGame/Player.log
  ```
- For Steam overlay/relay issues, check `SteamNetworkingUtils::InitRelayNetworkAccess` returns
  success, and verify the dev appID has SDR turned on in Steamworks settings.
- For Vulkan diagnostics: `MANGOHUD=1 ./MyGame.x86_64` (install MangoHud from the Deck's Discover
  store).
