# gaming-research

A **working prototype** for shipping a **Unity 6 top-down 2D co-op game** as a **native-Linux Steam
Deck title**, developed entirely from an **Apple Silicon MacBook** (no Windows machine). The
prototype doubles as a **performance reference** (60 fps @ 800p @ ≤15 W, zero steady-state GC).

- **[`unity/`](unity/)** — the Unity 6 project: a 2D top-down co-op dungeon slice (FishNet +
  FishySteamworks over Steam relay / Tugboat LAN), server-authoritative combat, an in-game
  performance overlay, and a headless build/test pipeline (`Assets/Game/Editor/BuildLinux.cs`).
- **[`tools/deploy/`](tools/deploy/)** — Mac → Steam Deck iteration scripts (rsync deploy, log
  streaming, MangoHud launch) and one-time SSH setup.
- **[`docs/performance/`](docs/performance/)** — the performance reference: how to measure, the
  applied-techniques catalog, the Steam Deck audit, and the forward-looking optimization roadmap.
