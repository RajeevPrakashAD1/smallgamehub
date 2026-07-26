# GameHub Feature 6 — Matchmaking & Lobby (LLD)

Status: DRAFT for review — 2026-07-26. Author: Rajeev Prakash.
Expands Feature 6 of `GameHub-LLD.md`. Read `GameHub-Architecture.md` §8 for the *why*.
Same conventions as the hub LLD: **Steps** are one-file approval units, 🟢 familiar /
🟡 one new idea / 🔴 new domain.

## Goal

Online multiplayer launch as **hub infrastructure**, not a Trail Trap feature. A new game
declares `supportsMultiplayer = true` in its manifest and writes **one line of networking
code** (`Session.StartAs(Context.Role)`) — the hub finds the match, loads the scene, and tells
the game what role and seat it got. Discovery, queueing, the UI and (later) Relay are all
hub-side and reused as-is.

Secondary goal: this closes Trail Trap's three deferred bugs (self-starting match, dead
client-rematch, rematch trail streak) via the new `Lobby` phase — see §Step 6.9.

---

## §1 Layer rule (locked before we start)

Matchmaking splits into **mechanism** and **policy**. They live in different assemblies.

| Layer | Assembly | Knows about | Contents |
|---|---|---|---|
| Mechanism | `NetKit` | Netcode + UGS only | `IMatchmaker`, `MatchRequest`, `MatchResult`, `FakeMatchmaker`, `RelayMatchmaker` |
| Policy + UI | `GameHub` | NetKit, its own views | `HubState.Matchmaking`, `MatchmakingController`, `MatchmakingView`, launcher routing |
| Consumer | `TrailTrap` | GameHub, NetKit | reads `GameLaunchContext`; opens its own session via `Session.StartAs(Context.Role)` |

Reference direction stays one-way and compiler-enforced: `TrailTrap → GameHub → NetKit`.

**Why the mechanism goes in NetKit, not GameHub.** `Session.cs` already states its own
purpose: *"the connection layer (direct / Relay / matchmaking) can evolve without touching
gameplay."* This is that evolution. Keeping it there means (a) no UGS type ever appears in a
file that draws a button, (b) the matchmaker is unit-testable with no hub and no scene, and
(c) a future non-hub tool could reuse it. `GameHub.asmdef` already references `NetKit`, so
no asmdef change is needed on the hub side.

**Why the seam is an interface with a fake.** Identical to `IContentService` /
`FakeContentService` from Feature 4a, which is already proven in this codebase: the whole
flow gets built and play-tested against a fake, then the real implementation swaps in at
**one line in `HubBootstrap.Awake`**. Same de-risking as the Addressables plan.

---

## §2 Build order — fake first, cloud last

Steps 6.1–6.9 are buildable and play-testable **before UGS exists in the project**.
Step 6.10 is the only one gated on an external dependency.

> **External dependency (yours, not code):** Relay + Lobby need 4 packages
> (`com.unity.services.core`, `.authentication`, `.relay`, `.lobby`) and a **linked Unity
> Cloud project ID** set in Project Settings → Services. None are installed today —
> `manifest.json` currently has Netcode 2.12.0 but no `com.unity.services.*`. Do this only
> when we reach 6.10; everything before it runs offline.

---

## §3 Steps

### Step 6.1 🟡 `NetKit/Matchmaking/MatchTypes.cs`

> **Correction (2026-07-26, found in build):** an earlier draft of this doc said "on reaching
> Ready the session is already running". That is impossible — `NetworkManager` lives in the
> *game's* scene, which has not loaded while matchmaking runs, so `Session.StartHost()` returns
> false from the hub. The matchmaker **resolves** a match (role, seat, later the Relay join
> code); the **game opens** the session with `Session.StartAs(role)` from its entry point.
> Splitting it this way also matches Relay's real sequence: allocate and fetch a join code
> (no NetworkManager needed), then configure the transport and start.

The vocabulary. Plain structs/enums, no Unity, no UGS.

- `enum MatchRole { Host, Client }` — who you are in the session that gets created.
- `enum MatchPhase { Idle, Searching, Joining, Ready, Failed, Cancelled }` — ticket lifecycle.
- `struct MatchRequest { string gameId; int playersPerMatch; }` — what the hub asks for.
- `struct MatchResult { bool Success; MatchRole Role; int Seat; int PlayerCount; string FailureReason; }`

`Seat` is the deterministic 0..n-1 index a game uses to pick colours/spawn points. The host
assigns it; it is **not** the NGO `clientId` (which is transport-assigned and unstable).

### Step 6.2 🟡 `NetKit/Matchmaking/IMatchmaker.cs`

The seam. Deliberately shaped like `IContentService` so it reads familiar:

- `MatchPhase Phase { get; }` and `string StatusText { get; }` — polled per-frame by the view.
- `event Action<MatchPhase> PhaseChanged` — rare transitions, event-driven.
- `void BeginSearch(MatchRequest request)` / `void Cancel()`
- `MatchResult Result { get; }` — valid once `Phase == Ready`.
- `void Tick(float dt)` — on the interface because the Relay impl must pump async handles,
  exactly like the Addressables impl must poll download handles.

**Split-rhythm rationale (same as `IContentService`):** state transitions are rare and
event-worthy; status text changes every frame and is cheaper to poll than to broadcast.

### Step 6.3 🟢 `NetKit/Matchmaking/FakeMatchmaker.cs`

Local, deterministic, no network. Configurable `searchSeconds`, and a forced outcome
(succeed-as-host / succeed-as-client / fail / never-find) so we can drive every UI branch
without a second device. On success it calls `Session.StartHost()` or `StartClient()` so the
downstream flow is genuinely exercised.

### Step 6.4 🟡 `GameHub/Launch/MatchmakingController.cs`

**Pure static `Describe(MatchPhase, string statusText, float elapsed) → MatchmakingPresentation`.**
Same pattern as `GamePageController.Describe`, which is already the most-tested code in the
hub. All decisions live here — headline text, spinner on/off, Cancel enabled, whether to show
Retry, and a `MatchAction` enum the view dispatches on. No `MonoBehaviour`, so every branch is
EditMode-testable with zero scene setup.

### Step 6.5 🟢 `GameHub/Core/HubState.cs` — add `Matchmaking`

One enum value between `GamePage` and `Loading`, plus `HubFlow.StartMatchmaking()`. `HubFlow`
stays a plain class raising `StateChanged`; views react. No other logic changes.

### Step 6.6 🟢 `GameHub/View/MatchmakingView.cs`

Full-screen panel: headline, animated status line, elapsed timer, Cancel. Implements
`IHubView`, gets deps pushed via `Init(HubBootstrap)` in `Start` — never `FindObjectOfType`,
per the composition-root rule. Built with `HubUi` helpers, themed from `HubConfig`.
Portrait-first layout (MOBILE-FIRST is locked).

### Step 6.7 🟢 `GameHub/Config/HubGameManifest.cs` — add `playersPerMatch`

One field, default 2. This is what makes matchmaking generic: the hub learns the shape of a
match from **data**, not from game code. `supportsSolo` / `supportsMultiplayer` already exist.

### Step 6.8 🟡 `GameHub/Launch/GameLaunchContext.cs` + `GameLauncher.cs`

- `GameLaunchContext` gains `MatchRole Role`, `int Seat`, `int PlayerCount`. Still plain data
  plus the single `ExitToHub` callback — the game's only capability.
- `GameLauncher.Launch` branches on mode: **Solo** → today's path unchanged. **Multiplayer** →
  `HubFlow.StartMatchmaking()`, `matchmaker.BeginSearch(...)`, and only on `Ready` proceed to
  `LoadRoutine` with the result folded into the context.
- **Ordering fix:** the session is started by the matchmaker **before** the scene loads. This
  is what removes the race noted in Feature 5 — `GameManager.Start()` currently runs before
  `Launch` is called, so today `OnLaunch` can't influence spawn setup.
- Cancel and failure both unwind to the game page with the session shut down.

### Step 6.9 🔴 `TrailTrap` — `Phase.Lobby`

`MatchState.Phase` becomes `{ Lobby, Countdown, Playing, Over }`. The match sits in `Lobby`
until `PlayerCount` seats are filled, then advances to `Countdown`. `TrailTrapEntry` deletes
its `Session.StartHost()` call (the hub already started the session) and reads `Context.Seat`
to pick its player slot. `NetBootstrap`'s dev OnGUI menu is retired.

Existing EditMode sim tests must stay green — they start at `Countdown`, so `Configure()`
keeps an explicit auto-start seam that skips `Lobby`.

**This step is where the three deferred bugs die:** the match can no longer self-start
(it waits in `Lobby`), rematch returns to `Lobby` rather than restarting blind (fixing the
dead client-rematch), and the trail buffer clears on `Lobby` entry (fixing the streak).

### Step 6.10 🔴 `NetKit/Matchmaking/RelayMatchmaker.cs`

Real backend. Anonymous auth on hub boot → Lobby query for an open lobby with matching
`gameId` → join it, or create one and a Relay allocation if none found → exchange join code
via lobby data → set `UnityTransport` to the Relay allocation → `StartHost`/`StartClient`.
Taught in visible stages (auth, then lobby, then relay, then wiring), like the grid shader.

Swap-in is **one line in `HubBootstrap.Awake`**, same as the planned Addressables swap.

---

## §4 Testing

- `MatchmakingController.Describe` — EditMode, every phase and both failure paths. No scene.
- `FakeMatchmaker` — EditMode, drives `IMatchmaker` contract (cancel mid-search, fail, ready).
- Trail Trap `Lobby` phase — extends the existing sim tests; seat-filling is pure logic.
- Views and Relay — verified in play, not unit-tested (house rule).
- Two-instance testing uses **MPPM** (`com.unity.multiplayer.playmode` is already installed).

## §5 Open questions

1. **Session lifetime.** Session-per-match (shut down on exit to hub) vs. persistent hub
   session. Recommend per-match — simpler, and `TrailTrapEntry.OnExitToHub` already shuts down.
2. **Solo path.** Does Solo still create a 1-player local session for code uniformity, or stay
   fully offline? Recommend fully offline (today's behaviour, zero risk).
3. **Reconnect.** Out of scope for Feature 6. A dropped opponent ends the match.

## §6 Timeline

| Steps | Content | Est. build sessions |
|---|---|---|
| 6.1–6.3 | NetKit seam + fake | ~1 |
| 6.4–6.8 | Hub UI, flow, launcher routing | ~1 |
| 6.9 | Trail Trap Lobby phase (+ 3 bug fixes) | ~1 |
| 6.10 | Relay + Lobby backend | ~1–2 |

Playable multiplayer flow (on the fake) lands at **6.9**, with no cloud dependency.

---

*Cross-refs: `GameHub-LLD.md` (Feature 6 summary), `GameHub-Architecture.md` §8,
`TrailTrap-LLD.md` (match phases), `NetKit/Session.cs`.*
