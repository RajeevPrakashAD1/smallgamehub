# SmallGameHub — LLD (file-by-file build plan)

Status: DRAFT for review — 2026-07-22. Author: Rajeev Prakash.
Implements `GameHub-Architecture.md` (HLD). Read that first for the *why*; this is the *how*,
in build order.

## How to read this

- **Features** = milestones (map to HLD H1–H6). **Steps** = one file / one approval unit each.
- We build **one Step at a time**: I show the file (or a chunk of it) + explain it, you say
  "go", I write it, we wire it, next Step. Small mechanical follow-ups (a `.meta`, a one-line
  scene edit) ride along without separate approval.
- **Pace marker** on every Step:
  - 🟢 **familiar** — a pattern you've already done (code-built UI, ScriptableObject, events). Fast.
  - 🟡 **one new idea** — I explain the single new concept as we write it.
  - 🔴 **new domain** — taught in visible stages, like the grid shader. Slower, on purpose.
- **Timeline** here means *approval units*, not calendar time. A "build session" for us ≈
  3–6 Steps with teaching. Estimates are per Feature at the bottom (§ Timeline).
- **Layer rule (locked):** all hub code lives in `Assets/Scripts/GameHub/`, namespace
  `GameHub`, its own asmdef. It **never** references a game. Games reference it. (HLD §2.)

---

## Feature 0 — Scaffolding  🟢

Goal: an empty, compiling hub module in the right place.

- **Step 0.1** 🟢 `Scripts/GameHub/GameHub.asmdef` — new assembly, references `NetKit` only
  (not TrailTrap). Add `GameHub` to the EditMode/PlayMode test asmdefs' references.
- **Step 0.2** 🟢 `Scenes/Hub.unity` — the persistent bootstrap scene (empty for now; becomes
  build scene 0 later). Camera + a single empty `HubRoot` GameObject.

---

## Feature 1 — Data & catalogue  (HLD §4, §5.5)

Goal: describe games as data, and hold the list. Pure classes, fully testable, no UI yet.

- **Step 1.1** 🟢 `Config/HubConfig.cs` — `ScriptableObject`, the per-project reskin knob.
  Fields: `appName`, `Color themePrimary/themeBg`, `TMP_FontAsset font`, `int storageBudgetMB`,
  `List<HubGameManifest> games` (MVP: catalogue is just this list). One asset in `Resources/`.
- **Step 1.2** 🟢 `Config/HubGameManifest.cs` — `ScriptableObject`, the game contract *data*.
  Fields: `string id, title, tagline`, `Sprite coverCard`, `string sceneAddress, contentLabel`,
  `string minHubVersion`, `bool supportsSolo, supportsMultiplayer`. Just data + `[CreateAssetMenu]`.
- **Step 1.3** 🟡 `Core/GameCatalogue.cs` — plain class (no Unity), testable. Wraps the list.
  - `IReadOnlyList<HubGameManifest> Games { get; }`
  - `HubGameManifest ById(string id)`  — null-safe lookup
  - ctor takes the list (DI seam, like `GameManager.Configure`) so tests pass a fake list.
  - *New idea:* keeping catalogue logic in a plain class so it's unit-testable without a scene.
- **Step 1.4** 🟢 `Tests/EditMode/GameCatalogueTests.cs` — 2–3 asserts (lookup hit, miss→null,
  count). Proves the pure-class seam, same as your existing sim tests.

---

## Feature 2 — Navigation (the app state machine)  (HLD §6)

Goal: one object owns "what screen are we on", raises events; screens are dumb views.

- **Step 2.1** 🟡 `Core/HubState.cs` — `enum HubState { Boot, Home, GamePage, Loading, InGame }`
  + `HubFlow.cs` plain class:
  - `HubState State { get; }`, `string CurrentGameId { get; }`
  - `event Action<HubState> StateChanged`
  - `void GoHome()`, `void OpenGame(string id)`, `void StartLoading()`, `void EnterGame()`, `void ExitToHub()`
  - Each transition sets state + fires `StateChanged` (views react). No scene work here — pure.
  - *New idea:* a state machine as the single source of "where am I", the same
    event-driven view/sim split your games use, applied to whole screens.
- **Step 2.2** 🟢 `Tests/EditMode/HubFlowTests.cs` — assert legal transitions + event fires.
- **Step 2.3** 🟡 `View/HubBootstrap.cs` — MonoBehaviour on `HubRoot`, the composition root.
  `Awake`: load `HubConfig` from Resources, build `GameCatalogue`, create `HubFlow`, hand them
  to the views. This is the one place that wires everything (like a tiny DI container).
  - *New idea:* "composition root" — one object constructs the graph so nothing else needs refs.

---

## Feature 3 — Home & Game-page UI  (HLD §9)

Goal: see the catalogue on screen and navigate. Code-built uGUI/TMP, exactly like `HudController`.
Button actions are stubbed until Feature 4.

- **Step 3.1** 🟢 `View/GameCardView.cs` — one card: cover `Image`, title `TMP_Text`, a state
  badge. `Bind(HubGameManifest m, Action onClick)`. Built in code, themed from `HubConfig`.
- **Step 3.2** 🟢 `View/HomeView.cs` — subscribes to `HubFlow.StateChanged`; when `Home`, builds
  a scroll grid of `GameCardView`s from the catalogue; card click → `HubFlow.OpenGame(id)`.
  - Reuses the code-built-Canvas + `CanvasScaler` portrait pattern from `HudController`.
- **Step 3.3** 🟢 `View/GamePageView.cs` — shows when state `GamePage`: big cover, tagline,
  a primary button (`PLAY`/`DOWNLOAD` — text stubbed for now), a "Remove data" button, a back
  button → `HubFlow.GoHome()`. Exposes `SetPrimary(string label, Action)` for Feature 4 to drive.
- **Step 3.4** 🟢 (mechanical) wire `HomeView` + `GamePageView` onto `HubRoot` in `Hub.unity`.

*After Feature 3: you can browse a fake catalogue Home→GamePage→back, no downloads yet.*

---

## Feature 4 — Content lifecycle (Addressables)  🔴  (HLD §5)

Goal: real Download → Play → Remove. This Feature introduces **Addressables** — the one new
domain — so it's taught in stages, and hidden behind an interface so the rest of the hub
(and the tests) never touch Addressables directly.

- **Step 4.1** 🟡 `Core/IContentService.cs` — the seam that keeps Addressables at arm's length:
  - `Task<long> GetDownloadSizeAsync(string label)`
  - `bool IsDownloaded(string label)`  (size == 0)
  - `Task DownloadAsync(string label, Action<float> onProgress)`
  - `Task ClearAsync(string label)`   — the "un-download"
  - *New idea:* program to an interface so a `FakeContentService` can unit-test the UI flow
    with no real downloads (and so swapping Addressables later touches one file).
- **Step 4.2** 🔴 `Core/AddressablesContentService.cs` — the real implementation. Built in
  **stages** so each Addressables call is understood before the next:
  - stage a: `GetDownloadSizeAsync` → wire a card badge to real "DOWNLOAD (x MB)" / "PLAY".
  - stage b: `DownloadAsync` with a progress callback → a working progress bar.
  - stage c: `ClearAsync` → the Remove button frees storage.
  - (This mirrors how the shader was built 4 visible stages; you'll *see* each call work.)
- **Step 4.3** 🟡 `Core/GamePageController.cs` — the brain behind `GamePageView`: picks button
  label from `IsDownloaded`, runs `DownloadAsync` (feeding the bar), then flips to Play; wires
  "Remove data" → `ClearAsync`. Pure-ish (takes `IContentService`), so it's testable with the fake.
- **Step 4.4** 🟢 `Tests/EditMode/GamePageControllerTests.cs` — with `FakeContentService`:
  not-downloaded→"DOWNLOAD", after download→"PLAY", remove→back to "DOWNLOAD".
- **Step 4.5** 🟡 (Unity config) Install the **Addressables** package; make Trail Trap's scene
  + assets an addressable group with a `contentLabel`; author Trail Trap's `HubGameManifest`
  asset. (Editor/asset work — I do the YAML/settings.)

*After Feature 4: Trail Trap really downloads, plays from cache, and its data can be freed.*

---

## Feature 5 — Launch contract & Trail Trap integration  (HLD §4.2, §7, §8-solo)

Goal: the hub launches a game generically and gets control back. Retire Trail Trap's dev menu.

- **Step 5.1** 🟢 `Core/GameLaunchContext.cs` — data struct: `enum LaunchMode { Solo, Multiplayer }`
  `LaunchMode mode`; (session fields added in Feature 6). What the hub hands a game.
- **Step 5.2** 🟡 `Core/GameEntryPoint.cs` — `abstract MonoBehaviour` the game's entry scene has one of:
  - `abstract void Launch(GameLaunchContext ctx);`
  - `event Action OnExitToHub;`  + protected `RaiseExit()`
  - *New idea:* the contract is a base class in the hub package; the *game* subclasses it, so
    the dependency points game→hub (never the reverse).
- **Step 5.3** 🟡 `Core/GameLauncher.cs` — MonoBehaviour: on `HubFlow` entering `Loading`,
  `Addressables.LoadSceneAsync(sceneAddress, Additive)`, find the `GameEntryPoint`, call
  `Launch(ctx)`, subscribe `OnExitToHub` → unload scene + `HubFlow.ExitToHub()`.
- **Step 5.4** 🟢 `TrailTrap/View/TrailTrapEntry.cs` — *lives in the TrailTrap module*, subclasses
  `GameEntryPoint`; `Launch` starts the match (solo for now); a HOME button calls `RaiseExit()`.
  Delete the `NetBootstrap` dev Host/Join OnGUI menu.

*After Feature 5: Home → tap Trail Trap → Download → Play → HOME → back to Home. The loop closes.*

---

## Feature 6 — Matchmaking + Trail Trap Lobby redesign  (HLD §8)  🔴

Goal: multiplayer launch. This is its own milestone and also fixes Trail Trap's 3 deferred bugs
(self-starting match, dead client-rematch, rematch streak) via the Lobby phase. Coarse for now;
gets its own LLD detail before we build.

- **Step 6.1** design: add `Lobby` to Trail Trap's `Phase`; match waits until both seats filled;
  `Configure()` auto-start seam kept so tests stay green.
- **Step 6.2** 🔴 `GameHub/Net/` lobby+matchmaking over NetKit (Relay/lobby service): find/host a
  session, then hand the game a ready `GameLaunchContext { Multiplayer, session }`.
- **Step 6.3** wire `GameLauncher` to route multiplayer games through matchmaking before load.
- **Step 6.4** Trail Trap consumes the session instead of its own Host/Join; session-per-match.

---

## Feature 7 — Remote catalogue & store polish  (HLD §5.5, §10, §12-D3)

- **Step 7.1** remote content catalogue (`LoadContentCatalogAsync`) so new games appear without a
  hub rebuild; a 2nd tiny game to prove reuse end-to-end.
- **Step 7.2** storage screen: per-game usage list + Remove; optional LRU auto-eviction against
  `HubConfig.storageBudgetMB`.
- **Step 7.3** hub app icon + splash + store assets (the hub-scoped art, per art strategy).

---

## Testing strategy

- Pure classes (`GameCatalogue`, `HubFlow`, `GamePageController`) get EditMode tests with fakes —
  no scene, no Addressables. Same discipline as the Trail Trap sim tests.
- `IContentService` interface = the mock seam; UI-flow logic is tested with `FakeContentService`.
- Views (Home/GamePage) are dumb and reactive; verified in play, not unit-tested.

## Timeline (approval units, not calendar)

| Feature | Steps | New concepts | Est. build sessions |
|---|---|---|---|
| 0 Scaffolding | 2 | none | ¼ (bundled into F1) |
| 1 Data & catalogue | 4 | plain-class testable seam | ~1 |
| 2 Navigation | 3 | state machine, composition root | ~1 |
| 3 Home & Game-page UI | 4 | (reuses HudController patterns) | ~1 |
| 4 Content lifecycle | 5 | **Addressables (taught in stages)** | ~2 |
| 5 Launch contract | 4 | entry-point contract, additive load | ~1 |
| 6 Matchmaking + Lobby | 4 | **Relay/lobby netcode** (own LLD) | ~3 |
| 7 Remote + polish | 3 | remote catalogue, LRU | ~2 |

Suggested first build target: **Features 0–3** (a browsable hub over a fake catalogue) — all
🟢/🟡, no new domains, and it makes the product visible fast. Addressables (🔴) starts at F4.

---

*Cross-refs: `GameHub-Architecture.md` (HLD), `TrailTrap-LLD.md` (game template), NetKit.*
