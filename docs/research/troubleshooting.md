# Troubleshooting

Common errors and fixes when building on a Mac and running on a Steam Deck. Distilled from
[`Research_Report.md`](Research_Report.md) §6.

---

## Common errors & fixes

| Error | Cause | Fix |
|---|---|---|
| Linux binary won't launch on Deck (silent failure) | Missing exec bit | `chmod +x MyGame.x86_64`; use `rsync -a` to preserve it |
| `Could not load [lib]steam_api.dll/so/dylib` at runtime | Wrong native library copied for target / Facepunch Posix plugin misconfigured | Verify Plugin Inspector platform filters; for Facepunch toggle Posix plugin OS to "Linux" |
| `Building Linux IL2CPP player requires a sysroot toolchain package` | Package not installed despite the module being installed | Install `com.unity.toolchain.macos-arm64-linux-x86_64` (or the x86_64 host variant for Intel Macs) via Package Manager manually |
| `libsteam_api64.so: cannot open shared object file` on Deck | File not next to binary, or named wrong | Put `libsteam_api.so` (or `libsteam_api64.so` depending on wrapper) in `MyGame_Data/Plugins/x86_64/` or next to the executable; verify with `ldd ./MyGame.x86_64` on the Deck |
| Black screen on Deck launch | Vulkan API not first in Player Settings | Reorder Graphics APIs for Linux; rebuild |
| Steam overlay missing in Deck build but works on Windows | AppID mismatch / launching outside Steam | Use `SteamAPI_RestartAppIfNecessary`; for non-Steam launch, ensure `steam_appid.txt` is next to the binary |
| Editor hangs at "Refreshing scripts" on Apple Silicon | `UnityPackageManager` Intel sub-process under Rosetta | Update to Unity 6.3+ or ensure Rosetta 2 is installed (`softwareupdate --install-rosetta`) |
| Build succeeds on Mac but `Permission denied` on Deck | macOS' SMB/AFS strips the Linux exec bit on copy | Use `rsync`, not Finder/SMB; or `tar`/zip and extract on the Deck |

---

## Steamworks library platform filters

- **Steamworks.NET:** when building for macOS or Linux the wrong `Steamworks.NET.dll` is copied
  by default — create a post-build script to copy the correct version (Windows:
  `steam_api.dll` for 32-bit or `steam_api64.dll` for 64-bit; Linux: `libsteam_api.so`). In Unity 6 the `.unitypackage` ships with correct Plugin Inspector
  platform filters; the gotcha mostly bites when source was cloned manually. Verify each native
  library's "Platform settings" only check the matching OS/architecture.
- **Facepunch.Steamworks:** a long-standing misconfiguration in `Facepunch.Steamworks.Posix`
  sets the "OS" platform to "OSX"; toggle it to **"Linux"** so Linux builds find the library
  (GitHub issue #645). Fix once and commit. Facepunch's wiki documents the Linux filename as
  `libsteam_api64.so` (with the `64` suffix); some SDK versions ship it as `libsteam_api.so` —
  verify and symlink/rename if necessary.

---

## Debugging Linux builds from the Mac

See the dedicated section in
[`deployment-runbooks.md`](deployment-runbooks.md#debugging-linux-builds-from-the-mac) for log
streaming (`Player.log` over SSH), SDR/relay checks
(`SteamNetworkingUtils::InitRelayNetworkAccess`), and Vulkan diagnostics with MangoHud
(`MANGOHUD=1 ./MyGame.x86_64`).
