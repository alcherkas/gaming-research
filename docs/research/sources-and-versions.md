# Sources, Versions & Pricing

Pinned package versions, key dates, pricing data points, and the notable quotes that anchor the
architecture, with their attributions.

---

## Pinned package & SDK versions

| Component | Version / detail | Date |
|---|---|---|
| Unity Linux IL2CPP toolchain (Apple Silicon host) | `com.unity.toolchain.macos-arm64-linux-x86_64` **2.0.5** — first non-prerelease, for Unity 6000.2 | Dec 5, 2025 |
| Unity Linux IL2CPP toolchain (Intel Mac host) | `com.unity.toolchain.macos-x86_64-linux-x86_64` **2.0.10** | Aug 19, 2025 |
| Unity editor — fully native Apple Silicon | **Unity 6.3** (first version with no Intel sub-processes; `UnityPackageManager` native) | — |
| Unity Platform Toolkit for Steamworks | `com.unity.platformtoolkit.steam` **1.0.2** (docs generated Apr 17, 2026); tested against Steamworks SDK 1.62; requires Unity 6.3 | GDC 2026 (Mar 11, 2026) |
| Steam client — universal2 (arm64) | shipped via public beta channel | Jun 12, 2025 |
| Test appID | **480 / Space War** (Valve's free test appID) | — |

---

## Native Steamworks libraries (by target)

| Target | Library | Notes |
|---|---|---|
| Linux x86_64 | `libsteam_api.so` | Some wrappers (Facepunch) expect `libsteam_api64.so` |
| macOS | `libsteam_api.dylib` | Universal arm64 + x86_64 |
| Windows | `steam_api64.dll` | — |

---

## Pricing data points

| Item | Price | As of |
|---|---|---|
| Steam Deck OLED 512GB | **$789 USD** (up from $549 pre-May 2026; Valve cited rising memory/storage costs for the +$240) | May 27, 2026 (Gematsu) |
| Hetzner CCX13 (cloud peer) | **$19.99/month USD** (after a ~38% across-the-board Hetzner price adjustment) | Apr 1, 2026 (PriceTimeline) |

---

## Real-world data point

- YouTuber **Taranasus** reported building a Unity game directly on a Steam Deck and registering
  it as a non-Steam game took *"a total of 40 minutes from booting up into Steam Desktop mode to
  launching the game within the Steam Deck interface."* From a Mac with rsync, an existing
  project, and a warm cache, per-iteration round-trip drops to ~2–3 minutes.

---

## Notable quotes & attributions

- **Valve, Steamworks FAQ (retail Deck as devkit):** *"Can you use a retail Steam Deck as a
  dev-kit? Yes — there's nothing special about the dev-kits, no special hardware or software that
  makes them easier to develop for."*
- **Valve, Steam Deck recommendations (Vulkan):** *"We recommend targeting Vulkan as your primary
  graphics API for best performance and battery life. If you use an engine like Unity or Unreal,
  enabling Vulkan in your build for all users will result in the highest performance/longevity."*
- **FishySteamworks README (one-instance limitation):** *"Steam has limitations which prevent you
  from connecting to yourself locally over two builds. To do so, you must have two steam Ids, on
  two separate devices. You however may run as server and client in a single executable."*
- **Unity toolchain docs (Rosetta):** the `macos-arm64-linux-x86_64` 2.0.5 package *"supplies a
  toolchain and sysroot for building Linux IL2CPP players on a macOS arm64 host (through
  Rosetta)."*
- **Neonlyte, Unity Discussions (Apr 26, 2026):** *"Activity Monitor showed that
  UnityPackageManager is still 'Intel', despite the rest of the Unity 6.0(f62) binaries are
  already Apple Silicon-native."*
- **Adrian, Unity staff (Apr 26, 2026):** *"UnityPackageManager is already native Apple Silicon
  from at least Unity 6.3."*
- **James Stone, Unity Platforms Team (GDC 2026):** *"we've actually made some native
  improvements to the Linux player that targets the Steam Deck hardware. Offering a potential
  improvement in performance over a build running on Proton and that's actually available
  today."*
- **Apple (MacRumors, Jun 10, 2025):** *"Rosetta was designed to make the transition to Apple
  silicon easier, and we plan to make it available for the next two major macOS releases —
  through macOS 27 — as a general-purpose tool for Intel apps to help developers complete the
  migration of their apps. Beyond this timeframe, we will keep a subset of Rosetta functionality
  aimed at supporting older unmaintained gaming titles, that rely on Intel-based frameworks."*

---

## Community tools referenced

- `github.com/3Samourai/SteamOS-Devkit-Client-MacOS` — community macOS port of Valve's devkit
  client (macOS 14/15, `uv` + Python 3.12).
- `github.com/amirrajan/steamos-devkit` — fork that also adds macOS support.
