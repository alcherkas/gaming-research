# Shipping a Unity 6 Physics Multiplayer Game from a MacBook to Steam Deck (Native Linux): The Complete 2026 Pipeline

## TL;DR

- **Yes, you can ship a native Linux Steam Deck build of a Unity 6 + FishNet + Steamworks game entirely from an Apple Silicon MacBook with no Windows machine.** Unity 6 supports IL2CPP cross-compilation from macOS arm64 to Linux x86_64 (the `com.unity.toolchain.macos-arm64-linux-x86_64` package — v2.0.5, first stable in Unity 6000.2, December 5, 2025  — runs partially “through Rosetta” per Unity’s own docs),   and Steamworks ships universal `libsteam_api` libraries for Linux, macOS arm64, and Windows in a single SDK download.
- **The deployment loop that actually works on a Mac:** develop in the macOS editor → build a Linux IL2CPP standalone → copy via `rsync`/SSH (or the unofficial macOS port of Valve’s SteamOS Devkit Client, or `steamcmd`’s `builder_osx`) to a Steam Deck in Developer Mode → register the binary as a non-Steam game (or use a private Steam beta branch). Use the Mac editor + Steam Deck as a second client for real 3–4 player Steam P2P testing because Steam only allows one running instance per account per machine.
- **The two unavoidable caveats:** (1) Unity 6.0 LTS Editor is *not* fully Apple-Silicon-native — Rosetta 2 is still required (the `UnityPackageManager` sub-process was Intel until Unity 6.3);  (2) Steamworks’ optional `SteamPipeGUI` is Windows-only, but `steamcmd` itself runs natively on macOS via `builder_osx/steamcmd.sh`, so all real upload work from a Mac is supported.

-----

## Key Findings

1. **Unity 6’s Linux IL2CPP cross-compiler officially supports macOS hosts — including Apple Silicon.** The package `com.unity.toolchain.macos-arm64-linux-x86_64` v2.0.5 was released December 5, 2025 for Unity 6000.2  and is the first non-prerelease build. Earlier 1.x and 2.0.4 versions were marked pre-release.  Unity’s docs note the arm64 host toolchain runs “through Rosetta,”  but the resulting Linux x86_64 player is a fully native Linux ELF suitable for Steam Deck.
1. **Steamworks ships native Linux `.so` libraries in the same SDK redistributable that a Mac developer downloads** — `libsteam_api.so` (Linux x86_64), `libsteam_api.dylib` (macOS universal arm64+x64), and `steam_api64.dll` (Windows). Steamworks.NET and Facepunch.Steamworks both reference the appropriate native library by build target.  
1. **`steamcmd` runs natively on macOS** via `tools/ContentBuilder/builder_osx/steamcmd.sh` from the Steamworks SDK; full SteamPipe uploads to dev branches work from a Mac. The Windows-only piece is the optional `SteamPipeGUI` helper, which just generates `.vdf` files  (which you can hand-write).
1. **Steam Deck retail units double as devkits.** Per Valve’s Steamworks FAQ: *“Can you use a retail Steam Deck as a dev-kit? Yes - there’s nothing special about the dev-kits, no special hardware or software that makes them easier to develop for.”*  Enable Developer Mode → Desktop Mode → set a password → `sudo systemctl enable --now sshd`,  then SCP/rsync builds from your Mac and add as a non-Steam game.
1. **Valve’s SteamOS Devkit Client is officially Windows/Linux only**, but there is an actively maintained community port that runs natively on macOS 14/15: `github.com/3Samourai/SteamOS-Devkit-Client-MacOS`. This gives you the same rsync-over-SSH “Devkit Game: YourGame” pipeline Windows devs use. A second fork, `github.com/amirrajan/steamos-devkit`, also adds macOS support.
1. **Vulkan is mandatory in practice for Steam Deck.** Valve explicitly recommends *“targeting Vulkan as your primary graphics API for best performance and battery life. If you use an engine like Unity or Unreal, enabling Vulkan in your build for all users will result in the highest performance/longevity.”* 
1. **Steam goes native ARM on Apple Silicon — but only via the public beta channel as of mid-2025.** Valve shipped a universal2 Steam client June 12, 2025;  the stable `.dmg` installer still ships an Intel-only bootstrapper  that requires Rosetta 2 to launch.
1. **Steam permits only one running instance per machine per account.** ParrelSync, Unity 6’s Multiplayer Play Mode, and editor+build-on-same-Mac approaches all need a workaround for *real* Steamworks P2P testing. The Steam Deck is therefore your single most valuable test peer — it is a second Linux machine running Steam under a second Steam account.
1. **At GDC 2026 (March 11, 2026), Unity announced first-party Steam, native Linux, Steam Deck, and Steam Machine support via the Platform Toolkit for Steamworks** — current documented version is `com.unity.platformtoolkit.steam` 1.0.2 (docs page generated April 17, 2026),  tested against Steamworks SDK 1.62,   requiring Unity 6.3. Per James Stone, Unity Platforms Team: *“we’ve actually made some native improvements to the Linux player that targets the Steam Deck hardware. Offering a potential improvement in performance over a build running on Proton and that’s actually available today.”*   This is *additive* to FishySteamworks/Steamworks.NET, not a replacement; most indies will continue using the community stack through 2026.

-----

## Details

### 1. Unity macOS → Linux Build Pipeline

**Editor & modules to install via Unity Hub on macOS:**

- Unity 6 (6000.x LTS, or Unity 6.3 if you want a fully native Apple Silicon experience including `UnityPackageManager`).
- Modules: **Linux Build Support (IL2CPP)** and **Linux Build Support (Mono)** (both ship in the Hub for macOS Editor installs; no Windows machine required). Optionally **Linux Dedicated Server Build Support** if you plan a headless server.
- The IL2CPP cross-compiler toolchain package is auto-installed by Unity when you switch to the Linux target. If it isn’t, install it manually via Package Manager — search for `com.unity.toolchain.macos-arm64-linux-x86_64` (Apple Silicon host) or `com.unity.toolchain.macos-x86_64-linux-x86_64` (Intel Mac host; latest 2.0.10, released August 19, 2025). 

**Confirming IL2CPP cross-compile works from a Mac:** Unity’s manual explicitly states the Linux IL2CPP cross-compiler “is a set of sysroot and toolchain packages that allow you to build Linux IL2CPP Players on any Standalone platform without needing to use the Linux Unity Editor or rely on Mono.”   This was historically Windows/Linux only; the macOS arm64 host toolchain has been stable since Unity 6000.2 (December 5, 2025). Pre-Unity-6 versions required Mono on Mac.

**Build settings:**

- File → Build Profiles → Linux → Architecture: `x86_64` (Steam Deck’s APU is x86_64 / RDNA 2, never ARM).
- Scripting Backend: **IL2CPP** (recommended — better runtime perf, smaller startup).  Mono is the safe fallback if the toolchain package gives you trouble.
- API Compatibility Level: .NET Standard 2.1 or .NET Framework, whichever FishNet/Steamworks.NET need (both work).
- Player Settings → Other Settings → Graphics APIs for Linux: put **Vulkan first**, then OpenGLCore as a fallback. Force Vulkan to top of the list explicitly.
- IL2CPP Code Generation: “Faster runtime” for ship, “Faster (smaller) builds” while iterating.
- Output: Unity produces a folder containing `YourGame.x86_64` (the executable), `UnityPlayer.so`, `YourGame_Data/`, and the Plugins payload.

**Apple Silicon-specific notes:**

- The Unity 6.0 LTS editor still requires Rosetta 2 on Apple Silicon because the `UnityPackageManager` sub-process was Intel-only until Unity 6.3. From a Unity Discussions thread, user Neonlyte (April 26, 2026): *“Activity Monitor showed that UnityPackageManager is still ‘Intel’, despite the rest of the Unity 6.0(f62) binaries are already Apple Silicon-native.”*  Unity staff member Adrian (April 26, 2026) confirmed: *“UnityPackageManager is already native Apple Silicon from at least Unity 6.3.”*  Rosetta does not affect your build *output* — the Linux .x86_64 binary is identical regardless.
- The `com.unity.toolchain.macos-arm64-linux-x86_64` 2.0.5 package’s own documentation states it *“supplies a toolchain and sysroot for building Linux IL2CPP players on a macOS arm64 host (through Rosetta).”*  The cross-compile step itself runs x86_64 binaries under Rosetta, but the output Linux x86_64 ELF is unaffected. Build times on M1 Pro / M2 / M3 are acceptable (typically 2–5 minutes for small-to-medium projects).

**Graphics API for Steam Deck:** RDNA 2 + Mesa RADV. Vulkan first, OpenGL second. Valve’s official Steam Deck recommendations: *“Vulkan API: We recommend targeting Vulkan as your primary graphics API for best performance and battery life.”*  Avoid relying on platform-specific shader features unique to Metal — URP/HDRP with Vulkan-compatible shaders is the safest path. For a physics-based game targeting 3-4 players, URP is the sweet spot.

### 2. Steam Deck Deployment & Testing from a Mac

**Three viable methods — pick by iteration cadence:**

**Method A — Steam private/dev beta branch via `steamcmd` on Mac (production-quality pipeline):**

1. Download the Steamworks SDK on your Mac.
1. `cd sdk/tools/ContentBuilder`. Write `app_build_<appid>.vdf` and `depot_build_<depotid>.vdf` by hand (the GUI generator is Windows only; the files are plain VDF text).
1. Run: `bash ./builder_osx/steamcmd.sh +login <user> +run_app_build ../scripts/app_build_<appid>.vdf +quit`. First run bootstraps `steamcmd`; subsequent runs reuse cached `config.vdf` for Steam Guard. Per the Steamworks SDK docs: *“To enable SteamCmd on macOS you must complete the following steps: From the terminal, browse to the tools\ContentBuilder\builder_osx\osx32 folder.”* 
1. In Steamworks partner site → Builds tab, push the new build to a private beta branch (e.g., `devtest`) with a password.
1. On the Steam Deck, switch to Desktop Mode, open Steam → right-click your dev appID → Properties → Betas → enter password → select branch. Back to Gaming Mode. The Deck downloads and runs the build under your dev account.

This is the most “shippable” pipeline because the build runs in Steam exactly how players will — overlay, cloud saves, achievements, and real FishySteamworks lobbies.

**Method B — Direct SCP/rsync + non-Steam game (fastest iteration):**

1. On Steam Deck: Steam → Settings → System → enable Developer Mode → Power → Switch to Desktop.  In Konsole: `passwd` to set a password, then `sudo systemctl enable --now sshd`.
1. On Mac: `ssh-keygen -t ed25519` and `ssh-copy-id deck@<deck-ip>`.
1. From Mac terminal: `rsync -avh --delete ~/MyGame/Build/Linux/ deck@<deck-ip>:/home/deck/Games/MyGame/`
1. On Deck (Desktop Mode): `chmod +x /home/deck/Games/MyGame/MyGame.x86_64` (do this once; `rsync -a` preserves the exec bit afterward).
1. Open Steam (Desktop Mode) → Add a Non-Steam Game → browse to `MyGame.x86_64` (change the file filter dropdown to “All Files”). Back to Gaming Mode; the game appears under “Non-Steam.” Iterate: rebuild on Mac, rsync over, relaunch on Deck. The non-Steam shortcut persists across iterations.

A real-world data point: YouTuber Taranasus reported building a Unity game directly on a Steam Deck and registering it as a non-Steam game took *“a total of 40 minutes from booting up into Steam Desktop mode to launching the game within the Steam Deck interface.”*  From a Mac with rsync, an existing project, and a warm cache, per-iteration round-trip drops to ~2–3 minutes.

**Method C — SteamOS Devkit Client (community Mac port):**

- Valve’s official Devkit Client is Windows/Linux only; the unofficial macOS port at `github.com/3Samourai/SteamOS-Devkit-Client-MacOS` runs natively on macOS 14 Sonoma and 15 Tahoe beta using `uv` (modern pip replacement) with Python 3.12.  It gives you the GUI rsync-over-SSH workflow with “Devkit Game: YourGameName” entries.
- Pair via Settings → System → Pair new host on the Deck, register your Mac in the client, and use the Title Upload tab.  Setting `Steam Play` to *unchecked* tells the Deck to run it as native Linux.

**On testing without owning a Steam Deck:** You essentially need one. Closest software-only proxy is a Linux VM (Manjaro KDE — historically Valve’s recommended Arch-based proxy — or a Holo ISO derivative) running Steam, but the Deck’s RDNA 2 APU, gamepad, 800p screen, and 15W TDP profile are impossible to fully emulate. The Steam Deck OLED 512GB is currently **$789 USD as of May 27, 2026 per Gematsu** (up from $549 USD pre-May 2026 — Valve cited “rising memory and storage costs” for the $240 increase).  It is the cheapest legitimate test device and the only path to validate Verified-on-Deck criteria.

### 3. Steamworks SDK on macOS

- The Steamworks SDK redistributable (`redistributable_bin/`) is the same zip for every platform; it contains `linux64/libsteam_api.so`, `osx/libsteam_api.dylib` (universal arm64+x64), and `win64/steam_api64.dll`. Per official Steamworks docs: *“macOS libsteam_api.dylib provides both the x64 and arm64 version of the Steam API.”* 
- **Steamworks.NET:** Per the maintainer’s docs: *“When building for OSX or Linux the wrong Steamworks.NET.dll will be copied over by default, it is recommended that you create a post build script to copy the correct version. Windows: steam_api.dll · Linux: libsteam_api.so.”*  In Unity 6, Steamworks.NET’s `.unitypackage` already has Plugin Inspector platform filters set correctly per architecture — the gotcha mostly bites if you cloned source manually. Verify: select each native library in Unity and confirm the “Platform settings” only check the matching OS/architecture.
- **Facepunch.Steamworks:** There is a known long-standing misconfiguration in `Facepunch.Steamworks.Posix` where the “OS” platform configuration is set to “OSX” and must be manually toggled to “Linux” for Linux builds to find the Linux library (per GitHub issue #645).  Fix once and commit. Note the Linux filename Facepunch’s wiki documents is `libsteam_api64.so` (with `64` suffix);  some SDK versions ship it as `libsteam_api.so` — verify and symlink/rename if necessary.
- **`steam_appid.txt`:** Must sit next to your Linux executable for testing outside Steam.  For an app launched through Steam (recommended), `SteamAPI_RestartAppIfNecessary` handles the appid context.
- The Steam client (which Steamworks talks to) runs natively on macOS (universal binary as of June 12, 2025 in the beta channel;  the stable `.dmg` still ships an Intel-only bootstrapper  that triggers Rosetta on first launch).  It also runs on the Steam Deck. During testing you’ll have both running: Mac for editor + one client, Steam Deck for the second/third/fourth client.

### 4. Multiplayer Testing Without a Second PC

This is your biggest workflow constraint. Steam only allows one running Steam instance per user account per machine, and FishySteamworks/FishyFacepunch P2P relies on real Steam IDs.

**Practical 3–4 player rig with only a Mac + Steam Deck:**

- **Client 1:** Mac editor with `SteamAPI.Init()` calling against your dev appID. Acts as host.
- **Client 2:** Steam Deck running the Linux build under a second Steam account (Valve permits free secondary accounts for testing). This validates real network P2P, real Steam relay, and real Deck performance simultaneously. **This is the single highest-value use of the Steam Deck during development.**
- **Client 3 (optional):** A second Mac, an old laptop, an iPad with Steam Link, or a cloud Linux VM running Steam headlessly. For a 3–4 player physics game, a cloud VM such as a Hetzner CCX13 (**$19.99/month US as of April 1, 2026, per PriceTimeline**,  after a ~38% across-the-board Hetzner price adjustment) is the cheapest way to validate 3+ peers reliably.
- **Client 4:** A friend on Discord with an early access key from your dev branch — also how you discover real-world NAT/relay behavior.

**What does NOT work as a substitute for real Steam clients:**

- **ParrelSync** (multiple Unity editor instances via symlinks): great for testing FishNet logic with the default Tugboat (UDP) transport, but Steam only one-instances per account so two ParrelSync clones cannot both authenticate as separate Steam users. The FishySteamworks README states explicitly: *“Steam has limitations which prevent you from connecting to yourself locally over two builds. To do so, you must have two steam Ids, on two separate devices. You however may run as server and client in a single executable.”* 
- **Unity 6’s Multiplayer Play Mode** (built-in virtual players): same limitation against real Steam — useful for non-Steam transports.
- **“Multiplayer Engine for Steam” (MPE4S)** and similar tools simulate Steam lobby/networking calls without real Steam — useful for fast iteration of lobby UI but they bypass the actual relay/NAT path you’ll ship on.

**Recommended workflow:**

1. Rapid iteration on game logic: use the default Tugboat UDP transport in FishNet via ParrelSync clones on the Mac. Zero Steam dependency, fast loop.
1. Once-per-feature Steam validation: switch FishNet’s transport to FishySteamworks (use FishNet’s Multipass to keep both selectable), build to Linux, deploy to the Deck, run host on Mac, client on Deck.
1. Periodic friend-network playtests via private beta branch on Steamworks for real 3–4 player sessions.

**Network simulation:** FishNet supports latency simulation via its TransportManager development settings — useful for testing physics rollback in a multiplayer physics game without leaving the editor.

### 5. Apple Silicon / ARM Specific Gotchas

- **Unity Editor:** Unity 6.0 LTS on Apple Silicon still requires Rosetta 2 (UnityPackageManager sub-process). Unity 6.3 is the first version with no Intel sub-processes per Unity staff confirmation.  Builds *to other targets* are unaffected.
- **Steamworks library on Mac:** `libsteam_api.dylib` is a universal binary (arm64 + x86_64) per Steamworks docs.  Steamworks.NET and Facepunch.Steamworks load it natively under both architectures.
- **Steam client on Mac:** The stable installer DMG still installs an Intel-only `Steam.app` bootstrapper that demands Rosetta 2.  Opt into the publicbeta channel (Settings → Beta participation → “Steam Beta Update”) and Steam runs fully arm64.  **Per Apple’s published statement (MacRumors, June 10, 2025): *“Rosetta was designed to make the transition to Apple silicon easier, and we plan to make it available for the next two major macOS releases – through macOS 27 – as a general-purpose tool for Intel apps to help developers complete the migration of their apps. Beyond this timeframe, we will keep a subset of Rosetta functionality aimed at supporting older unmaintained gaming titles, that rely on Intel-based frameworks.”*   Plan accordingly — Unity 6.0 LTS goes EOL right around the same time Rosetta 2 is sunsetted in macOS 28 (~fall 2027). 
- **For your build output:** None of this matters. The Linux x86_64 binary you produce from your Mac runs on the Deck’s x86_64 APU exactly the same regardless of whether the cross-compiler ran under Rosetta on your Mac.
- **Avoid Docker for cross-builds.** Docker Desktop’s `linux/amd64` emulation on M-series Macs with “Use Rosetta” enabled can hang for hours (a Next.js build was reported to take 33,656 seconds vs. 37 seconds native arm64).  Unity’s native cross-compiler is dramatically faster.

### 6. Practical End-to-End Workflow

**One-time setup (≈ 2 hours):**

1. Install Unity Hub on Mac → Unity 6.3 (or latest LTS if you need stability) with **Linux Build Support (IL2CPP)** and **Linux Build Support (Mono)** modules. Confirm `com.unity.toolchain.macos-arm64-linux-x86_64` 2.0.5+ is installed in Package Manager.
1. Install FishNet (Asset Store or git), Steamworks.NET (UPM or `.unitypackage`), FishySteamworks. Wire up Steamworks.NET’s `SteamManager`, set your `steam_appid.txt` (use 480 / Space War for early testing — Valve’s free test appID). 
1. Get a Steamworks partner account; register your game’s real appID once it has a store page. Testing can proceed against 480 until then.
1. Buy or borrow a Steam Deck. Enable Developer Mode, set a password, enable sshd, copy your SSH pubkey, test `ssh deck@<ip>` from Mac.
1. Either set up the `tools/ContentBuilder/builder_osx/steamcmd.sh` flow OR install the `3Samourai/SteamOS-Devkit-Client-MacOS` port (uv-based Python install).
1. On the Steam Deck, install Steam normally and log in as a separate Steam account dedicated to testing (different from your primary).

**Per-iteration build & test loop (≈ 3–5 minutes):**

1. In Unity (macOS editor): Build Profiles → Linux → x86_64 → IL2CPP → Vulkan first → Build. Output goes to `~/Builds/Linux/`.
1. From Mac terminal: `rsync -avh --delete --info=progress2 ~/Builds/Linux/ deck@steamdeck.local:/home/deck/Games/MyGame/` (use `.local` mDNS or the Deck’s IP). `-a` preserves the executable bit Unity sets, so no `chmod +x` needed after the first transfer.
1. (First time only) On Deck Desktop Mode: Steam → Add a Non-Steam Game → All Files → select `MyGame.x86_64`.
1. Return to Gaming Mode on Deck → launch from Non-Steam category. Concurrently launch Unity Editor on Mac in Play Mode (or a second build) as host.
1. Test 2-client networking: Mac hosts, Deck joins via Steam ID, FishySteamworks routes through Steam Datagram Relay. 

**Cadence recommendation:**

- Develop daily in the Mac editor with ParrelSync + Tugboat (default UDP) transport for fast multi-instance testing.
- Build to Linux and test on Deck **weekly minimum**, and before every milestone. Catches Vulkan shader issues, gamepad mapping, on-screen keyboard text input (required for Verified-on-Deck), text legibility at 800p, and 15W TDP performance early.
- Use `SetGameLauncherMode` API to translate gamepad input to keyboard/mouse if your game has any native launcher UI. 

**Common errors & fixes:**

|Error                                                               |Cause                                                                        |Fix                                                                                                                                                                      |
|--------------------------------------------------------------------|-----------------------------------------------------------------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
|Linux binary won’t launch on Deck (silent failure)                  |Missing exec bit                                                             |`chmod +x MyGame.x86_64`; use `rsync -a` to preserve it                                                                                                                  |
|`Could not load [lib]steam_api.dll/so/dylib` at runtime             |Wrong native library copied for target / Facepunch Posix plugin misconfigured|Verify Plugin Inspector platform filters; for Facepunch toggle Posix plugin OS to “Linux”                                                                                |
|`Building Linux IL2CPP player requires a sysroot toolchain package` |Package not installed despite module being installed                         |Install `com.unity.toolchain.macos-arm64-linux-x86_64` (or x86_64 host variant for Intel Macs) via Package Manager manually                                              |
|`libsteam_api64.so: cannot open shared object file` on Deck         |File not next to binary, or named wrong                                      |Put `libsteam_api.so` (or `libsteam_api64.so` depending on wrapper) in `MyGame_Data/Plugins/x86_64/` or next to the executable; verify with `ldd ./MyGame.x86_64` on Deck|
|Black screen on Deck launch                                         |Vulkan API not first in Player Settings                                      |Reorder Graphics APIs for Linux; rebuild                                                                                                                                 |
|Steam overlay missing in Deck build but works on Windows            |AppID mismatch / launching outside Steam                                     |Use `SteamAPI_RestartAppIfNecessary`; for non-Steam launch, ensure `steam_appid.txt` next to binary                                                                      |
|Editor hangs at “Refreshing scripts” on Apple Silicon               |`UnityPackageManager` Intel sub-process under Rosetta                        |Update to Unity 6.3+ or ensure Rosetta 2 is installed (`softwareupdate --install-rosetta`)                                                                               |
|Build succeeds on Mac but `Permission denied` on Deck               |macOS’ SMB/AFS strips Linux exec bit on copy                                 |Use `rsync` not Finder/SMB; or `tar`/zip and extract on Deck                                                                                                             |

**Debugging Linux builds from Mac:**

- Set `Development Build` + `Script Debugging` in Build Profiles. Optionally enable IL2CPP C++ source generation for stack traces.
- `Player.log` lives at `~/.config/unity3d/<Company>/<Product>/Player.log` on Deck. From Mac: `ssh deck@steamdeck.local tail -F ~/.config/unity3d/MyCompany/MyGame/Player.log`. Stream it during runs.
- For Steam overlay/relay issues, check `SteamNetworkingUtils::InitRelayNetworkAccess`  returns success and verify the dev appID has SDR turned on in Steamworks settings.
- For Vulkan diagnostics: `MANGOHUD=1 ./MyGame.x86_64`; install MangoHud from the Deck’s Discover store.

-----

## Recommendations

**Stage 1 — Set up the pipeline before writing any networking code (Week 1):**

- Install Unity 6.3 + Linux Build Support (IL2CPP) on your Mac. Verify a Hello-World cube project builds to Linux and runs on the Deck as a non-Steam game. **Don’t move on until this round-trip works in under 5 minutes.**
- Buy a Steam Deck OLED. At the current $789 USD price point it is non-optional for this project — skip it and you’ll discover real Verified-on-Deck failures at the worst possible time.
- Create a free secondary Steam account dedicated to Deck testing.

**Stage 2 — Wire up Steamworks and one-peer test (Week 2):**

- Add Steamworks.NET + FishNet + FishySteamworks via UPM. Use appID 480 for now.
- Build for Linux, deploy to Deck, host on Mac (editor Play Mode), join from Deck. Confirm `SteamAPI.IsSteamRunning()` returns true on both, and a FishySteamworks `ServerManager.StartConnection` succeeds with the Mac’s `steamID64`.
- *Benchmark to graduate:* 2-peer connection with physics object syncing over Steam relay between Mac editor and Deck build, ≤ 100 ms RTT.

**Stage 3 — 3–4 peer testing (Week 3–4):**

- Register real appID with Steamworks once you have a store page; switch from appID 480 to your real one.
- Set up `steamcmd`-on-Mac with a `devtest` private beta branch. Pushing builds: 1 command. Distribute the password to 1–3 testers.
- Use FishNet’s Multipass to have both Tugboat (for ParrelSync editor testing on Mac) and FishySteamworks (for Steam beta branch testing) selectable at runtime.

**Stage 4 — Verified-on-Deck readiness (last 25% of project):**

- Vulkan-first graphics API. Ensure no shader requires features outside Steam Deck RDNA 2’s capability set.
- On-screen keyboard via Steamworks’ `ShowFloatingGamepadTextInput` or `ShowGamepadTextInput` whenever text input is required (required for Verified badge). 
- Default controller config covers all functionality. No external launcher (per Valve: launchers fail UX expectations on Deck). 
- 60 fps at 800p with 4 networked physics players, ≤ 15W typical TDP, offline-mode-tolerant for any singleplayer content.

**Switch your recommendation if any of these become true:**

- *If iteration speed becomes a bottleneck and you have ~$20/month of cloud budget:* spin up a Hetzner CCX13 ($19.99/mo) with a headless Steam install as Client 3, replacing the “friend on Discord” peer for daily 3-peer testing.
- *If you outgrow Steamworks.NET / Facepunch.Steamworks integration friction:* migrate to Unity’s official Platform Toolkit for Steamworks (`com.unity.platformtoolkit.steam` v1.0.2,  tested against Steamworks SDK 1.62,  requires Unity 6.3+). This is Unity’s first-party path post-GDC 2026.
- *If Unity 6.0 LTS Rosetta dependency annoys you:* upgrade to Unity 6.3+ — `UnityPackageManager` went native on Apple Silicon there. 
- *If FishySteamworks shows P2P relay limitations at 4 players with heavy physics:* consider migrating to Unity Transport (UTP) over Steam Datagram Relay, or to dedicated server mode with FishyUnityTransport — FishNet’s Multipass makes this swap trivial.

-----

## Caveats

- **Unity 6.0 LTS Editor is not fully Apple Silicon native through end-of-life (October 2027).** Rosetta 2 remains required for the `UnityPackageManager` process. Apple confirmed at WWDC 2025 that Rosetta 2 will be available *“for the next two major macOS releases – through macOS 27”*  with a subset retained for older games beyond  — so plan to be on Unity 6.3+ by the time you ship if your game has a long support tail.
- **The macOS arm64 → Linux x86_64 IL2CPP toolchain itself runs partially under Rosetta 2** per Unity’s docs. Build times on Apple Silicon are still acceptable (2–5 minutes for small-to-medium projects on M2/M3) but not as fast as a fully-native arm64 toolchain would be. Output binaries are unaffected.
- **`SteamPipeGUI` is Windows only.** You will write `.vdf` build scripts by hand. They are simple text files; one-time investment.  Steamworks.NET maintainer Riley Labrecque and the broader community publish working VDF templates.
- **Valve’s official SteamOS Devkit Client does not officially support macOS.** Workable community ports (`3Samourai/SteamOS-Devkit-Client-MacOS`, `amirrajan/steamos-devkit`) exist, and the more conservative path is plain SSH + rsync + non-Steam game, which has zero external dependencies and survives any community-port abandonment.
- **Facepunch.Steamworks’ Linux Posix plugin OS setting is misconfigured by default** — set “OS” to “Linux” on `Facepunch.Steamworks.Posix` after import.  Steamworks.NET via UPM is generally less error-prone for Mac-only development today.
- **Steam will not let two clients run under the same account.** You need at least two Steam accounts and two devices (Mac + Deck) for actual P2P testing. ParrelSync alone is not sufficient for Steam transport testing.
- **The Unity GDC 2026 native-Steam announcement is partly forward-looking.** Per James Stone (Unity Platforms Team, March 2026 GDC livestream): *“we’ve actually made some native improvements to the Linux player that targets the Steam Deck hardware. Offering a potential improvement in performance over a build running on Proton and that’s actually available today.”* The broader Platform Toolkit Steamworks integration is still maturing through 2026. Don’t assume it replaces the FishySteamworks/Steamworks.NET stack for your project mid-development; treat it as a future migration option once stable.
- **You cannot meaningfully test Verified-on-Deck without owning a Steam Deck.** Linux VMs and Manjaro KDE installs are useful proxies for software bring-up but cannot validate the hardware constraints (GPU, screen, TDP, input mapping). The Steam Deck OLED 512GB is currently $789 USD (May 2026)  — budget for it from day one.
- **Steam client on Apple Silicon stable channel still requires Rosetta 2** as of mid-2026; the publicbeta channel runs fully arm64 since June 12, 2025.   Opt into Steam Beta on your Mac for the best development experience, especially given Apple’s Rosetta 2 sunset timeline.
- **The “native Linux improvements” for Steam Deck Unity announced at GDC 2026 are described as a *potential* performance improvement over Proton** — Unity did not publish benchmarks alongside the announcement,  so do not assume guaranteed wins versus shipping a Windows build via Proton. Measure with MangoHUD on your actual title before making the call.