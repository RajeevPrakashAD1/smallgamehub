# SmallGameHub — Architecture & Content Design (HLD)

Status: DRAFT for review — 2026-07-22. Author: Rajeev Prakash.
Companion to `TrailTrap-GDD.md` / `TrailTrap-LLD.md` (Trail Trap = game #1 / the template).

> This document designs the **hub app itself** — the shell that lists downloadable games,
> downloads a game's assets on demand, launches it, and comes back. It is written to be
> **reusable across projects**: a different game catalogue + branding should give a
> different hub with no changes to the hub's core.

---

## 0. The core bet

SmallGameHub's differentiator is **speed**: a home screen that opens instantly, games that
download in seconds (because the art footprint is tiny), and zero-lag transitions in and out
of games. Every infrastructure choice below is judged against one question: *does it keep the
hub and its games small, fast, and lag-free?* Low art isn't a limitation we tolerate — it's
the feature we sell, and the architecture is built to protect it.

## 1. Product premise

- **Home** — a grid of game cards (downloadable games).
- **Game page** — tap a card → details, a Download / Play button, storage controls.
- **Play** — assets download on first play, then the game launches; exit returns to Home.
- **Storage hygiene** — a game's downloaded assets can be freed later to reclaim space
  ("un-download"); re-downloaded on next play. (MVP: manual. Auto-eviction: later — §5.4.)

## 2. Design principles (the reuse rules)

1. **The hub knows nothing about any specific game.** It talks to games only through a
   contract (§4). It must be possible to delete Trail Trap and have the hub still compile.
2. **One-way dependency layers** (same rule that keeps `NetKit` reusable):

   ```
   [Game modules]        →   [GameHub]              →   [NetKit]          →  [Unity + Addressables]
    TrailTrap, Game2…         home / catalogue /         Session facade
    (implement the            download mgr / nav /        (multiplayer)
     contract)                UI shell / IHubGame
   ```

   Arrows point one way only. `GameHub` may reference `NetKit`; **no game is ever
   referenced by `GameHub`**. Reverse that arrow and reuse dies.
3. **Config, not code, is what changes per project.** Branding, catalogue source, theme,
   and storage budget live in a `HubConfig` asset. Dropping the hub into a new project =
   new `HubConfig` + new game manifests, not new hub code (§11).
4. **Everything the games already assume stays true**: mobile-first / portrait, URP 2D,
   sim in `FixedUpdate`, pure-logic classes, pooling / no GC spikes in gameplay.

## 3. The five subsystems

| Subsystem | Responsibility |
|---|---|
| **Game contract** (`IHubGame` + `HubGameManifest`) | The seam every game implements; the only thing the hub knows about a game (§4). |
| **Content delivery** (`ContentService`) | Wraps Addressables: query size, download w/ progress, cached-state, free cache (§5). |
| **Catalogue** (`GameCatalogue`) | The list of available games that drives Home; local now, remote-CDN later (§5.5). |
| **Navigation** (`HubFlow`) | App state machine: Boot → Home → GamePage → Loading → InGame → Home (§6). |
| **UI shell** | Home grid, game page, download progress, settings — themed by `HubConfig` (§9). |

## 4. The game contract — the reuse seam

A game is described to the hub by **data**, and hands control back through **one small
component**. Nothing else crosses the boundary.

### 4.1 `HubGameManifest` (data — a ScriptableObject or JSON row)
```
id            : string        // stable unique key, e.g. "trailtrap"
title         : string
tagline       : string
coverCard     : Addressable sprite ref     // the tile art (hub-scoped art — see art-strategy)
sceneAddress  : string        // Addressables key of the game's entry scene
contentLabel  : string        // Addressables label grouping this game's downloadable assets
minHubVersion : string        // compatibility guard
supportsSolo / supportsMultiplayer : bool
```
Discovery, the Home grid, download-size queries, and cache-clearing all run off this data
alone — **the hub can show and manage a game it has never had code for.**

### 4.2 `GameEntryPoint` (behaviour — the launch handshake)
The game's entry scene contains exactly one component implementing:
```
void Launch(GameLaunchContext context);   // hub → game: "start, here's your context"
event Action OnExitToHub;                  // game → hub: "I'm done, tear me down"
```
`GameLaunchContext` carries `{ mode: Solo | Multiplayer, Session info / matchmaking result }`
(§8). This is the whole API surface. A game in *another project* implements this same
component and authors a manifest — done.

**Decision (recommended):** data-manifest for discovery + a `GameEntryPoint` MonoBehaviour
for the handshake. Games depend on the `GameHub` package to implement it (one-way, correct).

## 5. Content delivery & storage (Addressables)

Addressables gives us every primitive the premise needs, first-class:

### 5.1 Show the right button
```
size = await Addressables.GetDownloadSizeAsync(manifest.contentLabel);
button = size == 0 ? "PLAY" : $"DOWNLOAD ({Format(size)})";
```
`size == 0` means already cached locally → straight to Play.

### 5.2 Download with a progress bar
```
var op = Addressables.DownloadDependenciesAsync(manifest.contentLabel);
// poll op.GetDownloadStatus().Percent for the bar
```

### 5.3 Play
```
await Addressables.LoadSceneAsync(manifest.sceneAddress, LoadSceneMode.Additive);
// find GameEntryPoint in the loaded scene, call Launch(context)
```

### 5.4 Free memory ("un-download") — *this is the easy part*
```
await Addressables.ClearDependencyCacheAsync(manifest.contentLabel);
```
Removes that game's cached bundles → storage reclaimed; next play re-downloads. The user's
worry that this is hard is unfounded — it's one call. So we keep it.
- **MVP:** a manual "Remove downloaded data" button on the game page.
- **v2 (optional):** a storage budget in `HubConfig` + **LRU auto-eviction** — track
  `lastPlayedUtc` per game, and when total cache exceeds the budget, clear least-recently-
  played games until under. Purely a policy layer on top of the same one call.

### 5.5 Where games come from
- **MVP:** games shipped *inside* the hub build as local Addressables groups. Proves the
  download→play→free lifecycle end-to-end with no server. (Right now: only Trail Trap, no CDN.)
- **v2:** a **remote content catalogue** on a CDN (`LoadContentCatalogAsync`) so new games
  appear in Home without shipping a new hub build — the real "downloadable games store."

## 6. Navigation — the app state machine

```
Boot ──▶ Home ──▶ GamePage ──▶ Loading ──▶ InGame ──▶ (exit) ──▶ Home
                     │                                    ▲
                     └── Download / Remove ───────────────┘
                                        (Multiplayer: Loading routes via Matchmaking, §8)
```
`HubFlow` owns the current state; UI screens are dumb views driven by it (same view/sim
split the games use). One persistent **Hub scene** stays resident the whole time; games load
**additively** and unload on exit, so returning Home is instant (the shell never reloaded).

## 7. Scene & lifecycle model

- **Hub bootstrap scene** — loaded once at app start, never unloaded. Hosts `HubFlow`,
  `ContentService`, the UI shell, and a persistent audio/services root.
- **Game entry scene** — loaded additively on Play, holds the game (its own sim, its
  `GameEntryPoint`). On `OnExitToHub`: unload the scene, `Resources.UnloadUnusedAssets()`,
  optionally clear cache per policy → back to Home with a clean slate and freed memory.
- Isolation: each game runs in its own additive scene, so games can't leak state into the
  hub or each other.

## 8. Multiplayer handoff (NetKit tie-in)

Matchmaking belongs to the **hub**, not each game — so it's built once and reused (this
supersedes Trail Trap's in-game Host/Join dev menu). Flow for a multiplayer game:
```
GamePage START ▶ Hub matchmaking (NetKit + lobby/relay) ▶ session ready
             ▶ load game scene ▶ GameEntryPoint.Launch(context = { Multiplayer, session })
```
The game receives an already-connected session and just plays; it never implements lobby or
relay code. Solo games skip matchmaking and launch with `{ Solo }`. NetKit stays the
game-agnostic netcode layer it already is; the hub adds the lobby/matchmaking service above it.

## 9. UI shell

- **Home** — scrollable grid of game cards (cover art + title + Download/Play state badge).
- **Game page** — cover, tagline, size, primary button (Download → Play), "Remove data".
- **Download UI** — progress bar bound to §5.2; cancel-safe.
- **Settings** — storage usage list (per-game, with Remove), audio, about.
- Everything themed from `HubConfig` (colours, logo, font) — reskinnable per project. TMP +
  the same SDF-font approach Trail Trap already uses; procedural / low-art visuals throughout.

## 10. Performance budget — the "fast" guarantees

- **Tiny content** — low art is the premise; keep per-game download in the low single-digit
  MB so first-play is seconds. CI check: fail if a game's group exceeds a size ceiling.
- **Resident shell** — Home never reloads; transitions are additive scene load/unload only.
- **Warm cache** — keep recently-played games cached (don't over-evict); optionally prefetch.
- **No gameplay hitches** — pooling, no per-frame allocation, cheap procedural shaders
  (the Trail Trap grid/particle approach), URP 2D, portrait mobile targets.
- **Instant Home paint** — catalogue + cover cards are small and cached; Home shows before
  any game content is touched.

## 11. Reusing the hub in another project

The hub ships as a **bounded module** (start as an asmdef folder like `NetKit`; promote to an
embedded UPM package `com.rajeev.gamehub` when first extracted). To stand up a new hub:
1. Import the `GameHub` package (+ `NetKit` if the project has multiplayer games).
2. Create a `HubConfig` asset: app name, branding/theme, catalogue source URL, storage budget.
3. Author a `HubGameManifest` per game; mark each game's entry scene + assets Addressable.
4. Ship. Same hub core, different catalogue and skin.

Nothing in steps 1–4 edits hub source. That is the test of whether §2 was honoured.

## 12. Open decisions (need your call)

| # | Decision | Recommendation |
|---|---|---|
| D1 | Content tech: Addressables vs custom AssetBundles vs monolithic build | **Addressables** — gives download/size/clear for free (§5) |
| D2 | Catalogue: local-in-build first, or remote CDN from day one | **Local first** (MVP, no server) → remote when 2nd game + hosting exist |
| D3 | Un-download: manual only, or manual + LRU auto-eviction | **Manual MVP**; LRU as v2 policy layer |
| D4 | Hub packaging: asmdef folder vs UPM package now | **asmdef folder now** (like NetKit), package on first reuse |
| D5 | Game contract: data-manifest + `GameEntryPoint`, or richer `IHubGame` interface | **Manifest + GameEntryPoint** (most decoupled) |

## 13. Milestone roadmap (proposed)

- **H1 — Hub skeleton**: bootstrap scene, `HubFlow` state machine, `HubConfig`, a fake
  2-game catalogue, Home grid + Game page navigating with placeholder art. No downloads yet.
- **H2 — Content lifecycle**: `ContentService` over Addressables; make Trail Trap an
  addressable game; real Download → Play → Remove against local groups (§5.1–5.4).
- **H3 — Launch contract**: `HubGameManifest` + `GameEntryPoint`; Trail Trap launches via
  the hub with a `GameLaunchContext`; retire its dev Host/Join menu.
- **H4 — Matchmaking**: hub lobby/matchmaking over NetKit; multiplayer launch context; this
  also resolves Trail Trap's Lobby-phase redesign + its 3 deferred bugs.
- **H5 — Remote catalogue**: CDN-hosted catalogue + a 2nd game to prove reuse end-to-end.
- **H6 — Store polish**: hub app icon, splash, storage budget/LRU, store assets.

---

*Cross-refs: `TrailTrap-LLD.md` (game template), NetKit (netcode layer), art strategy
(low-art / presentation-first; app icon + cover cards are hub-scoped).*
