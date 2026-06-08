# Performance Reference

This game is a **reference implementation of Unity performance techniques** for a 2D top-down
co-op title on the **Steam Deck** (native Linux, built from an Apple Silicon Mac). Every technique
below is *applied in the codebase*, *measurable with the in-game overlay*, and explained with the
*why* and *where*.

The target bar:

| Budget | Target |
|---|---|
| Frame time | **≤ 16.7 ms** (60 fps) at native **800p** |
| Power | **≤ 15 W** typical TDP |
| Steady-state GC | **0 B / frame** |
| Draw / SetPass | a handful (sprites batch to ~1) |

> These are **targets**. The 0 B/frame and ~1 draw/SetPass figures are met by the idle/static release
> scene; combat currently allocates on hot paths (logging, the per-frame mana write, VFX and projectile
> instantiation) and fragments batching — see [`roadmap.md`](roadmap.md).

Contents:

- [`measuring.md`](measuring.md) — the tools: in-game overlay, MangoHud, Steam overlay, Unity Profiler,
  and how to read each metric (why **ms**, not FPS; why memory ≠ frame cost).
- [`techniques.md`](techniques.md) — the catalog of applied techniques: rendering, CPU/GC, physics,
  build/IL2CPP, frame & power, networking — each with what / why / how-measured / code link.
- [`roadmap.md`](roadmap.md) — the **forward-looking** optimization roadmap: prioritized, verified
  performance work not yet applied, each with `file:line` and the metric to watch.
- [`steam-deck-audit.md`](steam-deck-audit.md) — the point-in-time architecture & performance audit
  (verdict, findings vs the Deck budget, strengths to preserve).

## TL;DR for newcomers

- **Optimize against frame time (ms), not FPS.** 60 fps = 16.7 ms. Under VSync the FPS is pinned to
  the 60 Hz screen, so use **CPU ms / GPU ms** to see real headroom.
- **VSync-cap on purpose.** Rendering faster than 60 on a handheld just burns watts/heat for frames
  the screen never shows.
- **Memory is mostly fixed engine baseline**, not your game. A release build (stripped) is far
  smaller than a development build — don't read a dev build's footprint as "the game is heavy."
