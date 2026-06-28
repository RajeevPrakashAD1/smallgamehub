# Trail Trap — Low-Level Design (LLD)

> Status: Draft v0.2 · Owner: Gokul · Last updated: 2026-06-18
> Companion to `TrailTrap-GDD.md`. The GDD says *what* and *why*; this doc says *how*, in code terms.
>
> We design one section at a time, in milestone order. Sections marked **TBD** are not designed yet — we fill them in as we reach them.
>
> **Approach decision (see chat):** host-authoritative multiplayer using **Netcode for GameObjects (NGO)**. We lean on NGO built-ins (`FixedUpdate` tick, `NetworkTransform`, `ServerRpc`/`NetworkVariable`) rather than a custom deterministic loop. M1 is **local, no network** — networking is added at M4.

---

## Milestone legend

"M1…M7" are build-order checkpoints (defined in GDD §9). Each LLD section is tagged
with the milestone where it gets built — i.e. *what to build now vs. later*.

| Tag | Milestone | What it means |
|---|---|---|
| **M1** | Local sim | Playable on one keyboard: movement, fading trails, collision, win. No network. |
| **M2** | Juice | Make it *feel* good: colors, screen shake, countdown, sound. |
| **M3** | Power-ups | Boost / Phase / Gap / Eraser, added one at a time. |
| **M4** | Netcode (host) | Run over the network as host only. |
| **M5** | Two clients | Two players on one PC (Multiplayer Play Mode). |
| **M6** | Relay | Play over the real internet. |
| **M7** | Hub module | Package into the launcher/hub. |

---

## 0. Architecture overview

Kept deliberately simple. Two cheap disciplines we follow throughout:

1. **Game logic lives in plain, testable methods** (e.g. `MovementStep(...)`), *called from* `FixedUpdate` — not smeared across MonoBehaviour callbacks. Keeps movement/trail/collision clean and unit-testable.
2. **`FixedUpdate` is the fixed tick.** All simulation (move, lay trail, fade, collision) runs there, never in `Update()`. `Update()` is for input gathering and visuals only.

```
Update()        → gather input, draw/visual only
FixedUpdate()   → THE TICK: move players, lay trail, fade, collide, check win
```

### Components (M1, local — no networking yet)
```
GameManager (MonoBehaviour)
    - owns match state (countdown / playing / over)
    - owns the two PlayerController refs + the trail/arena
    - drives the tick: calls each subsystem from its own FixedUpdate
PlayerController (MonoBehaviour)
    - holds PlayerState (pos, heading, speed, alive)
    - Update(): read this player's keys → store latest InputFrame
    - movement computed by a plain method, applied in the tick
SimConfig (ScriptableObject)
    - all tunables (tick-independent): speed, turnRate, trail fade, etc.
```

> At M4 we add `NetworkManager` and make `PlayerController` a `NetworkBehaviour`; the plain logic methods are reused unchanged. That's the payoff of discipline #1.

### Conventions
- Angles stored in **radians** internally; tunables authored in **degrees** and converted.
- Positions are `Vector2` in **world/sim units** (not pixels).
- Inside the tick, time step is `Time.fixedDeltaTime` (we alias it `dt`). Never use `Time.deltaTime` for simulation.

---

## 1. Simulation tick — `FixedUpdate`  *(M1 foundation)*

### 1.1 Responsibility
Advance the game by Unity's fixed timestep. No custom accumulator, no manual interpolation — `FixedUpdate` already gives a fixed tick, and (at M4) `NetworkTransform` handles remote interpolation for free.

### 1.2 The tick, driven by GameManager
One place owns tick order so it's explicit and deterministic-enough for host authority:

```csharp
public sealed class GameManager : MonoBehaviour
{
    [SerializeField] SimConfig config;
    [SerializeField] PlayerController p1, p2;
    TrailSystem trails;     // §3
    Arena arena;            // §4 (boundary)
    MatchState match;       // §5

    void FixedUpdate()
    {
        if (match.phase != Phase.Playing) return;
        float dt = Time.fixedDeltaTime;     // the fixed tick step

        // Fixed order matters (movement before trail before collision):
        p1.Tick(dt, config);                // 1. move          (§2)
        p2.Tick(dt, config);
        trails.Append(p1, p2, dt, config);  // 2. lay trail pts (§3)
        trails.Fade(dt, config);            // 3. fade old      (§3)
        CollisionStep(dt);                  // 4. who crashed   (§4)
        match.Step(p1, p2);                 // 5. win check     (§5)
    }
}
```
At M1 we wire up step 1 (+ stubs of 2 and 4); each subsystem is slotted in as we design it.

### 1.3 Why no custom timing loop
We chose **host-authoritative** (one machine is the source of truth and broadcasts results), not **lockstep** (every machine runs a bit-perfect identical sim and only swaps inputs). Lockstep needs perfect determinism → a custom accumulator/replay layer. Host-authoritative doesn't, so that layer earns nothing here and is cut.

### 1.4 Tunable: tick rate
- Set Unity **Fixed Timestep** (Project Settings → Time); start at **0.04s (25Hz)** — smooth, not twitchy. Revisit after feel-test.
- [ ] Visually interpolate the local player in `Update()` only if 25Hz looks steppy. (At M4 the *remote* player is interpolated for free by `NetworkTransform`.)

---

## 2. Player movement  *(M1 foundation)*

### 2.1 Responsibility
Turn a steering input into a new position + heading each tick: constant forward speed, rate-limited turning (GDD §3.2).

### 2.2 Data
```csharp
public struct PlayerState
{
    public Vector2 position;   // sim units
    public float   heading;    // radians; 0 = +X, CCW positive
    public float   speed;      // units/sec (Boost/ramp may change later)
    public bool    alive;
}

public struct InputFrame
{
    public float turn;   // -1 = full right(CW) .. 0 = straight .. +1 = full left(CCW)
}
```
> `turn` is an **analog axis** in [-1,1] (aim-to-steer, §6): magnitude = how hard to turn this tick. `MovementStep` multiplies it by `turnRate * dt`, so `|turn| ≤ 1` keeps the capped turn rate.

### 2.3 PlayerController
Input gathered in `Update()` (frame-timed), movement applied in the tick via a **plain method** (so it's reusable by netcode and unit tests later):

```csharp
public sealed class PlayerController : MonoBehaviour
{
    public PlayerState State;
    [SerializeField] KeyCode left, right;   // desktop test controls
    InputFrame _input;

    void Update()   // gather input only — NOT simulation
    {
        sbyte t = (sbyte)((Input.GetKey(left)  ? 1 : 0)
                        - (Input.GetKey(right) ? 1 : 0));
        _input = new InputFrame { turn = t };
    }

    public void Tick(float dt, SimConfig cfg)   // called from GameManager.FixedUpdate
    {
        State = MovementStep(State, _input, dt, cfg);
        transform.position = State.position;                  // visual
        transform.rotation = Quaternion.Euler(0, 0, State.heading * Mathf.Rad2Deg);
    }

    // Pure, testable: no Unity globals, no Time.deltaTime, no Input.*
    public static PlayerState MovementStep(PlayerState p, InputFrame inp, float dt, SimConfig cfg)
    {
        if (!p.alive) return p;

        float turnRate = cfg.turnRateDeg * Mathf.Deg2Rad;     // e.g. 180°/s
        p.heading = Mathf.Repeat(p.heading + inp.turn * turnRate * dt, Mathf.PI * 2f);

        Vector2 fwd = new Vector2(Mathf.Cos(p.heading), Mathf.Sin(p.heading));
        p.position += fwd * (p.speed * dt);
        return p;
    }
}
```
> `MovementStep` is a **pure function** (no Unity globals — same inputs always give same output). At M4 the server calls the *same* method; nothing about the math changes.

### 2.4 Spawn / setup
- P1 spawns at left edge facing +X; P2 at right edge facing −X — pointed at open space so neither immediately faces a wall.
- Initial `speed = cfg.baseSpeed`, `alive = true`.

### 2.5 Edge cases / decisions
- **180° self-kill is impossible by construction** — rate-limited turning can't reverse instantly.
- **Analog steering later:** only `InputFrame.turn` type changes.
- **Input timing model:**
  - *Held inputs* (steering): a **mailbox** — `Update()` writes the latest value into `_input` each frame, the tick reads it. Robust because every frame writes the same value; nothing to miss. Movement stays tick-rate independent via `* dt`.
  - *Tap inputs* (e.g. use power-up, if ever tap-based): **latch in `Update`, consume-and-clear in the tick** so a one-frame press isn't missed. A boolean latch intentionally **debounces** double-taps within a tick (collapses to one) — desirable here since a power-up should fire once. Only switch to a counter/queue if a mechanic must count every rapid tap (none planned).
- [ ] Turn rate value — start **180°/s**; feel-test 120–270.
- [ ] Boost: increase `speed` only, or speed + turn rate? (power-up milestone.)

---

## 3. Trail system  *(M1 — core mechanic)*

The trail is the core mechanic (GDD §3.3–3.4). It is **gameplay data first, visuals second**.

### 3.1 Responsibility
Drop a stamped point along each living player's path every tick, expire old points
(time-based fade with a fade time that ramps up over the match), and own the point
data that collision (§4), the Eraser power-up (§7), and netcode (§8) all read.

### 3.2 Data
```csharp
public struct TrailPoint
{
    public Vector2 pos;      // where it was dropped (sim units)
    public float   deathAt;  // sim time when it vanishes (locked at birth — see 3.4)
}
```
One **`List<TrailPoint>` per player** (each trail has its own color; self-immunity needs
to know which trail is "mine"). Points are appended at the end, so the list stays sorted
by `deathAt` → the oldest point is always at the front.

### 3.3 Truth vs. view — we do NOT use `TrailRenderer`
Mirror of §2's "`State` is truth, `transform` is the picture":

> The `List<TrailPoint>` is the **truth**. A **`LineRenderer`** per player is just a **view**
> we feed from the list each frame.

- **`LineRenderer`** = draw a ribbon through points *we* supply. We keep the data, so
  collision / Eraser / netcode can use it. Width = `trailWidth`; color gradient = fade
  to transparent; unlit/emissive material + Bloom = the glow look (GDD §7).
- **`TrailRenderer`** is rejected as the source of truth: it manages its points
  internally and won't hand them back, so gameplay can't "see" the trail. (Optional pure
  juice only.)

### 3.4 Fade model — **fade time fixed at birth** *(decided)*
Each point's lifespan is locked the instant it's laid:
```csharp
point.deathAt = now + CurrentFadeTime(matchElapsed);
```
- The ramp makes **fresher segments live longer**, so both tails grow over the match →
  escalating density. But every segment still dies on its own schedule.
- Because birth time and fade time both only increase, `deathAt` is monotonic down the
  list → list stays sorted → fading = remove from the front. (Rejected: recomputing
  lifespans every tick, which revives old trails, breaks the "forgiving early game", and
  scrambles ordering.)

**The ramp** — linear lerp then clamp (GDD appendix values):
```csharp
float CurrentFadeTime(float elapsed)
    => Mathf.Lerp(cfg.fadeStart, cfg.fadeEnd,            // 4s → 9s
                  Mathf.Clamp01(elapsed / cfg.fadeRampDuration));  // over ~90s
```

### 3.5 The two per-tick operations
```csharp
public sealed class TrailSystem
{
    readonly List<TrailPoint> _t1 = new(), _t2 = new();
    float _elapsed;   // match time, advanced each tick

    // Step 2 of the tick: drop a point per living player.
    public void Append(PlayerController p1, PlayerController p2, float dt, SimConfig cfg)
    {
        _elapsed += dt;
        float death = _elapsed + CurrentFadeTime(_elapsed, cfg);
        if (p1.State.alive && !p1.GapActive) _t1.Add(new TrailPoint { pos = p1.State.position, deathAt = death });
        if (p2.State.alive && !p2.GapActive) _t2.Add(new TrailPoint { pos = p2.State.position, deathAt = death });
    }

    // Step 3 of the tick: expired points are contiguous at the front (list is sorted).
    public void Fade(float dt, SimConfig cfg)
    {
        DropExpired(_t1); DropExpired(_t2);
        void DropExpired(List<TrailPoint> t)
        {
            int i = 0;
            while (i < t.Count && t[i].deathAt <= _elapsed) i++;
            if (i > 0) t.RemoveRange(0, i);
        }
    }
}
```
- **Gap power-up** = skip the `Add` while `GapActive` (the whole mechanic, §7).
- **Eraser power-up** (§7) marks points dead (`deathAt = _elapsed`) instead of deleting
  from the middle; they're then ignored by collision and cleaned up by `Fade`.

### 3.6 Sampling cadence — **one point per tick** *(decided)*
At 25Hz and ~4–5 u/s that's a point every ~0.2 units (smooth), and ≤ ~225 points/player
even at a 9s fade. Trivial cost → no distance-based decimation needed now (can add a
"only drop if moved > X" guard later as a micro-opt).

### 3.7 Hooks for later sections
- **Self-immunity (§4):** when testing a head against its *own* trail, skip the last
  `N = ceil(immunitySeconds / dt)` points (≈ 7–8 at 0.3s / 25Hz) — i.e. the newest tail
  by index, no birth-time field needed. Test the opponent's whole trail.
- **Netcode (§8):** server sends *new points + their `deathAt`*; clients append and fade
  locally — never resend the whole list.

### 3.8 Tunables (SimConfig)
| Field | Start | Note |
|---|---|---|
| `fadeStart` | 4s | early-game point lifespan |
| `fadeEnd` | 9s | late-game point lifespan |
| `fadeRampDuration` | ~90s | time to ramp start→end |
| `trailWidth` | 0.2u | also the LineRenderer width |

- [ ] Tune `fadeRampDuration` to hit the ~1–2 min match feel.
- [ ] Decide LineRenderer material / Bloom intensity (visual, M2).

## 4. Collision  *(M1 — core mechanic)*

Collision is a **distance check**, not point-vs-point (GDD §3.3). The head is a point; a
trail is a **stripe of width `trailWidth`** made of segments. A player crashes when its
head is inside any *live* stripe — i.e. the **shortest distance from the head to a trail
segment ≤ `trailWidth/2 + grace`**.

### 4.1 Closest point on a segment (the core math)
For head `P` and segment `A→B`, find the closest point via **projection**, then **clamp**
to the segment so we never measure to the segment's infinite extension (which would cause
phantom crashes in empty space past a trail's end):

```csharp
// Squared distance from point p to segment a-b. Squared = no sqrt (compare against r*r).
static float DistSqToSegment(Vector2 p, Vector2 a, Vector2 b)
{
    Vector2 ab = b - a, ap = p - a;
    float abLenSq = Vector2.Dot(ab, ab);              // |AB|²
    float t = abLenSq > 1e-6f                          // guard: A==B degenerate segment
              ? Vector2.Dot(ap, ab) / abLenSq          // projection fraction along AB
              : 0f;
    t = Mathf.Clamp01(t);                              // pin to the real segment [A..B]
    Vector2 c = a + t * ab;                            // closest point on the segment
    return (p - c).sqrMagnitude;
}
```
- `t = dot(AP,AB)/|AB|²` = where P's shadow lands along AB as a 0→1 fraction.
- `Clamp01` folds shadows that fall before A (t<0) or past B (t>1) back onto the endpoints
  — this is what prevents false hits with the line beyond the segment.
- Squared distance throughout: compare `DistSqToSegment ≤ r*r`, never call `sqrt`.

### 4.2 The collision step
```csharp
void CollisionStep(float dt)
{
    int immune = Mathf.CeilToInt(config.selfImmuneSeconds / dt);   // ≈8 at 0.3s / 25Hz
    float r = config.trailWidth * 0.5f + config.grace;
    float rSq = r * r;

    bool d1 = HitsTrail(p1, trails.T1, skipNewest: immune)   // own trail (skip newest N)
            | HitsTrail(p1, trails.T2, skipNewest: 0)        // opponent's whole trail
            | OutOfBounds(p1);
    bool d2 = HitsTrail(p2, trails.T2, skipNewest: immune)
            | HitsTrail(p2, trails.T1, skipNewest: 0)
            | OutOfBounds(p2);

    if (d1) p1.State.alive = false;
    if (d2) p2.State.alive = false;
    // both alive → play on · one dead → other wins · BOTH dead → draw (resolved in §5)

    bool HitsTrail(PlayerController pl, List<TrailPoint> t, int skipNewest)
    {
        Vector2 head = pl.State.position;
        int last = t.Count - 1 - skipNewest;             // segment is points[i], points[i+1]
        for (int i = 0; i < last; i++)
            if (DistSqToSegment(head, t[i].pos, t[i + 1].pos) <= rSq) return true;
        return false;
    }
}
```

### 4.3 Design points
- **Self-immunity:** against your *own* trail, skip the newest `N = ceil(selfImmuneSeconds/dt)`
  points by index (the §3.7 decision) so rate-limited tight turns don't self-kill. Against
  the opponent's trail, test everything.
- **Evaluate both players, then apply death** — note `|` (non-short-circuit), not `||`, so a
  genuine same-tick head-on collision sets *both* dead → §5 resolves it as a **draw** instead
  of letting check-order pick a "winner".
- **Eraser-marked points can't kill:** `Fade` runs before `CollisionStep` in the tick order
  (§1), so points the Eraser marked dead this tick are already removed. Free correctness.
- **Boundary** (`OutOfBounds`): head outside the arena rectangle = death. *Recommended:* keep
  a cheap static wall so players can't flee forever; GDD leaves this open (wall vs rely on the
  fade ramp).

### 4.4 Performance
2 heads × ~450 segments × 25 ticks ≈ 22k checks/sec — trivial. **No spatial structure
(grid/quadtree) needed** for 2 players; revisit only if player count ever rises.

### 4.5 Tunables (SimConfig)
| Field | Start | Note |
|---|---|---|
| `grace` | 0.1u | forgiveness added to `trailWidth/2` |
| `selfImmuneSeconds` | 0.3s | newest own-trail window skipped |
| arena bounds | ~20×20u | for `OutOfBounds` |

- [ ] Keep boundary wall, or rely purely on the fade ramp? (GDD open decision.)
- [ ] Tune `grace` for "close but not cheap" feel.

## 5. Match flow & win condition  *(M1 — closes the loop)*

A small 3-phase state machine that gates the tick and drives countdown → duel → winner →
rematch (GDD §3.5, §8). Sudden death: first to crash loses; both-same-tick = draw.

### 5.1 Phases
```
StartMatch → [Countdown] --timer 0--> [Playing] --someone crashes--> [Over] --Rematch--> (back to Countdown)
              sim frozen               sim runs                       frozen, winner shown
```

### 5.2 MatchState
```csharp
public enum Phase { Countdown, Playing, Over }

public sealed class MatchState
{
    public Phase phase;
    public float countdown;   // seconds left in countdown
    public int   winner;      // 0 = none/draw, 1 = p1, 2 = p2

    public void StartMatch(SimConfig cfg)   // also used by Rematch
    {
        phase = Phase.Countdown;
        countdown = cfg.countdownSeconds;    // e.g. 3
        winner = 0;
    }

    public void TickCountdown(float dt)
    {
        countdown -= dt;
        if (countdown <= 0f) phase = Phase.Playing;
    }

    public void Step(PlayerController p1, PlayerController p2)   // called after CollisionStep
    {
        if (phase != Phase.Playing) return;
        bool a1 = p1.State.alive, a2 = p2.State.alive;
        if (a1 && a2) return;                          // still dueling
        phase  = Phase.Over;
        winner = (a1 == a2) ? 0 : (a1 ? 1 : 2);        // both dead → draw(0); else survivor
    }
}
```

### 5.3 GameManager dispatch (supersedes the simplified guard in §1.2)
The countdown timer must advance even though the sim is paused, so the tick dispatches on
phase instead of a single early-return:
```csharp
void FixedUpdate()
{
    float dt = Time.fixedDeltaTime;
    switch (match.phase)
    {
        case Phase.Countdown: match.TickCountdown(dt); return;   // count "3..2..1", no sim
        case Phase.Over:      return;                            // frozen; await rematch
        case Phase.Playing:   break;
    }

    p1.Tick(dt, config); p2.Tick(dt, config);   // §2
    trails.Append(p1, p2, dt, config);          // §3
    trails.Fade(dt, config);                    // §3
    CollisionStep(dt);                          // §4
    match.Step(p1, p2);                         // §5 — resolve win/draw
}
```

### 5.4 Rematch (instant)
Rematch resets and re-enters Countdown — no scene reload (keeps it "one tap to replay",
GDD §4/§8):
```csharp
public void Rematch()
{
    p1.Respawn(config); p2.Respawn(config);   // reset PlayerState to spawn (§2.4)
    trails.Clear();                            // empty both point lists + reset elapsed
    match.StartMatch(config);
}
```
- Rematch is triggered by a UI button or a tap on the winner screen. A tap uses the
  §2.5 **latch-and-clear** pattern (read in `Update`, consume in the tick) — or just wire
  a Unity UI `Button.onClick` straight to `Rematch()`.

### 5.5 Hooks for later
- **Netcode (§8):** `phase`/`winner`/`countdown` become a `NetworkVariable` owned by the
  server (clients read to drive UI); `Rematch()` becomes a `ServerRpc`. Both players must
  agree before a rematch starts.
- **Juice (M2):** countdown beeps + "GO!", winner-screen flash, screen shake on the death
  that triggers `Over`.

### 5.6 Tunables (SimConfig)
| Field | Start | Note |
|---|---|---|
| `countdownSeconds` | 3 | length of the "3..2..1..GO" |

- [ ] Auto-rematch vs. require a tap? (GDD leans "instant rematch" — default to a tap.)

## 6. Input layer  *(mobile-first — GDD §2a, §6)*

One job: produce `InputFrame.turn` (a **float in [-1,1]**) per player, device-agnostic.
Everything downstream (`MovementStep` §2, netcode §8) only sees `turn`, so the device never
leaks past here.

**Control model = aim-to-steer (turn-toward).** The player turns *toward* a pointed direction
at the capped turn rate — mobile floating joystick, desktop mouse. This is **absolute** intent
("go that way"), converted to a **relative** turn axis by a shared bit of vector math. The old
relative hold-left/right keyboard scheme survives only as a dev fallback.

### 6.1 The abstraction
A one-method interface so swapping device = swapping one object. It takes the current state
because aim-to-steer needs the player's heading to know which way to turn:
```csharp
public interface ITurnInput { float Read(in PlayerState state); }   // -1 = right(CW)..+1 = left(CCW)
```
`PlayerController` holds an `ITurnInput` (supersedes the inline `KeyCode` read in §2.3):
```csharp
ITurnInput _turnSource;   // mouse on desktop, joystick on mobile
void Update() { _input = new InputFrame { turn = _turnSource.Read(in State) }; }   // mailbox (§2.5)
```

### 6.2 The turn-toward math (shared by mouse + joystick)
Given a desired world direction, return the analog turn axis. The **cross product's sign**
says left/right; `Atan2(cross, dot)` is the signed angle error; dividing by a small smoothing
band gives **full turn when far, eased turn when nearly aligned** (no overshoot/jitter):
```csharp
public static class SteerMath
{
    // turn in [-1,1] to rotate `headingRad` toward `desiredDir`. smoothBandRad ~ 15°.
    public static float TurnToward(float headingRad, Vector2 desiredDir, float smoothBandRad)
    {
        if (desiredDir.sqrMagnitude < 1e-6f) return 0f;          // no input → straight
        Vector2 fwd = new Vector2(Mathf.Cos(headingRad), Mathf.Sin(headingRad));
        Vector2 des = desiredDir.normalized;
        float cross = fwd.x * des.y - fwd.y * des.x;             // + = target is to my left
        float dot   = fwd.x * des.x + fwd.y * des.y;
        float err   = Mathf.Atan2(cross, dot);                   // signed radians toward target
        return Mathf.Clamp(err / smoothBandRad, -1f, 1f);
    }
}
```

### 6.3 Mouse aim (M1 desktop dev — same model as mobile)
```csharp
public sealed class MouseAimTurnInput : ITurnInput
{
    readonly Camera _cam; readonly float _band;
    public MouseAimTurnInput(Camera cam, float smoothBandDeg = 15f)
        { _cam = cam; _band = smoothBandDeg * Mathf.Deg2Rad; }
    public float Read(in PlayerState p)
    {
        Vector2 world = _cam.ScreenToWorldPoint(Input.mousePosition);
        return SteerMath.TurnToward(p.heading, world - p.position, _band);   // aim from player to cursor
    }
}
```

### 6.4 Joystick (mobile primary — M-mobile)
Floating joystick: first touch sets an anchor; the drag vector from anchor → finger is the
desired direction, fed into the **same** `SteerMath.TurnToward`. No new movement code.
```csharp
// Sketch: anchor = first-touch pos; desired = (touch.pos - anchor); turn = TurnToward(p.heading, desired, band)
```
Held touch = level input = robust mailbox model (no latch, §2.5).

### 6.5 Keyboard fallback (dev only)
```csharp
public sealed class KeyboardTurnInput : ITurnInput
{
    readonly KeyCode _left, _right;
    public KeyboardTurnInput(KeyCode left, KeyCode right) { _left = left; _right = right; }
    public float Read(in PlayerState _) => (Input.GetKey(_left) ? 1f : 0f) - (Input.GetKey(_right) ? 1f : 0f);
}
```

### 6.6 Notes & decisions
- **Analog by default now** (`turn` is `float`); `MovementStep` already scales (§2.2). Capped
  turn rate is preserved — `|turn| ≤ 1` and per-tick rotation ≤ `turnRate * dt`.
- **Netcode (§8):** only the *owner* reads its `ITurnInput`; the resulting float `turn` is sent
  to the server. The server never touches input devices.
- [ ] Feel-test `smoothBandDeg` (start 15°) and turn rate together — band too big = sluggish,
  too small = twitchy near the target.

## 7. Power-ups  *(M3 — add one at a time)*

Each power-up is a **small modifier on the existing tick**, not a new system. Add them one
at a time after the base duel is fun (GDD §4.1). Three are timed; Eraser is instant.

### 7.1 How each hooks in
| Power-up | Effect | Hook | Mechanic |
|---|---|---|---|
| **Boost** | 2× speed, 3s | §2 movement | effective speed ×`boostMul` while active |
| **Phase** | pass trails, 2s | §4 collision | skip trail check for that player |
| **Gap** | no trail, 2s | §3 `Append` | skip the point write (already hooked) |
| **Eraser** | clear in radius | §3 data | **instant**: mark nearby points dead |

### 7.2 Data
```csharp
public enum PowerUpType { Boost, Phase, Gap, Eraser }

public struct PowerUp { public Vector2 pos; public PowerUpType type; }   // a pickup on the board

public struct ActiveEffects   // per player; timed effects only
{
    public float boost, phase, gap;   // seconds remaining
    public void Tick(float dt)
    {
        boost = Mathf.Max(0, boost - dt);
        phase = Mathf.Max(0, phase - dt);
        gap   = Mathf.Max(0, gap   - dt);
    }
    public bool BoostActive => boost > 0;
    public bool PhaseActive => phase > 0;
    public bool GapActive   => gap   > 0;   // §3.5 reads this
}
```

### 7.3 Tick integration (extends §1 / §5.3 order)
```
effects.Tick(dt)     // 0. decrement timers
move                 // 1. effective speed = baseSpeed * rampFactor * (BoostActive ? boostMul : 1)
append trail         // 2. skip if GapActive (§3.5)
fade                 // 3.
pickups + eraser     // 4. NEW — collect pickups; Eraser marks points dead  (BEFORE collision)
collision            // 5. PhaseActive skips trail check; dead points skipped (see 7.5)
match.Step           // 6.
spawner.Update(dt)   // 7. NEW — maybe spawn a pickup
```
Boost is applied by computing effective speed each tick before `MovementStep` (keeps
`baseSpeed` in config untouched). Pickups run **before** collision so an Eraser grabbed
this tick saves you this tick.

### 7.4 Pickup detection & apply
```csharp
void PickupStep(PlayerController pl, List<PowerUp> board, SimConfig cfg)
{
    Vector2 head = pl.State.position;
    for (int i = board.Count - 1; i >= 0; i--)
        if ((head - board[i].pos).sqrMagnitude <= cfg.pickupRadius * cfg.pickupRadius)
        {
            Apply(pl, board[i].type, head, cfg);
            board.RemoveAt(i);
        }
}

void Apply(PlayerController pl, PowerUpType type, Vector2 head, SimConfig cfg)
{
    switch (type)
    {
        case PowerUpType.Boost:  pl.Effects.boost = cfg.boostDur; break;
        case PowerUpType.Phase:  pl.Effects.phase = cfg.phaseDur; break;
        case PowerUpType.Gap:    pl.Effects.gap   = cfg.gapDur;   break;
        case PowerUpType.Eraser: EraseAround(head, cfg.eraserRadius); break;  // instant
    }
}

// Eraser: mark points within radius dead in BOTH trails; Fade cleans them when they reach front.
void EraseAround(Vector2 c, float radius)
{
    float rSq = radius * radius;
    foreach (var list in new[] { trails.T1, trails.T2 })
        for (int i = 0; i < list.Count; i++)
            if ((list[i].pos - c).sqrMagnitude <= rSq)
            {
                var p = list[i]; p.deathAt = trails.Elapsed; list[i] = p;   // mark dead
            }
}
```

### 7.5 Required refinement to §4
Eraser marks *middle* points dead, but §3 `Fade` only removes from the *front*. So
`HitsTrail` (§4.2) must **skip dead points**: ignore any segment whose endpoint has
`deathAt <= now`. This also makes erased points instantly non-lethal.

### 7.6 Spawning
```csharp
public sealed class PowerUpSpawner
{
    readonly List<PowerUp> _board = new();
    float _nextIn;
    public void Update(float dt, SimConfig cfg, /*for open-space test*/ TrailSystem trails)
    {
        if (_board.Count >= cfg.maxPickups) return;
        _nextIn -= dt;
        if (_nextIn <= 0f)
        {
            if (TryFindOpenSpot(cfg, trails, out var pos))
                _board.Add(new PowerUp { pos = pos, type = RandomType(cfg) });
            _nextIn = Random.Range(cfg.spawnMin, cfg.spawnMax);
        }
    }
}
```
`TryFindOpenSpot` = a few random points in-arena, reject ones too close to a live trail or
player (so pickups appear in open space, GDD §4.1). `RandomType` draws from the v1 set.

### 7.7 Decisions
- **Phase + boundary:** Phase passes through *trails* only; the boundary wall still kills
  (otherwise you could leave the arena). [ ] confirm.
- **v1 power-up set:** GDD open — pick 3–4 of Boost/Phase/Gap/Eraser. Default: all four
  (each is tiny). Add one at a time and tune.

### 7.8 Tunables (SimConfig)
| Field | Start | Note |
|---|---|---|
| `boostMul` | 2× | Boost speed multiplier |
| `boostDur`/`phaseDur`/`gapDur` | 3 / 2 / 2 s | effect durations |
| `eraserRadius` | 2u | Eraser clear radius |
| `pickupRadius` | ~0.4u | collect distance (head to pickup) |
| `spawnMin`/`spawnMax` | 6 / 10 s | time between spawns |
| `maxPickups` | 2 | max on board |

## 8. Netcode integration  *(M4+ — host-authoritative)*

Built on **Netcode for GameObjects (NGO)**. The whole M1 sim moves onto the **server**;
clients send input and render. The §0/§2 discipline pays off here: `MovementStep`,
`DistSqToSegment`, the whole tick are **reused unchanged** server-side.

### 8.0 The governing principle
The **server owns everything that decides who wins**: the real trails, collision, alive
flags, match state, power-up effects. Clients never decide death. Everything else is just
"feel good + cheap bandwidth".

### 8.1 Decisions (locked)
- **Movement = pure server-authoritative.** Client sends turn input via `ServerRpc`; server
  runs `MovementStep` for both; `NetworkTransform` (server authority) streams positions;
  clients render. Accepts ~50–150ms input lag on own steering (tolerable at 25Hz).
  *Upgrade path:* add owner prediction/reconciliation only if play-testing feels mushy.
- **Trails = derived on client (visual only).** The server keeps the real `List<TrailPoint>`
  for collision. Clients append a local trail point each tick at the synced position and
  fade locally — ~zero extra bandwidth (rides on `NetworkTransform`). Cosmetic drift is fine.
  *Fallback:* explicit point sync (`NetworkList`/RPC) if drift ever shows.

### 8.2 What syncs, and how
| State | Mechanism | Authority |
|---|---|---|
| Player position / heading | `NetworkTransform` | server |
| Turn input | `SetTurnInput(sbyte)` `ServerRpc` | owner → server |
| `phase` / `countdown` / `winner` | `NetworkVariable` | server |
| per-player `alive` + effect flags | `NetworkVariable` | server |
| power-up pickups on board | `NetworkList<PowerUp>` (or NetworkObjects) | server |
| Eraser burst | `ClientRpc(center, radius)` | server → clients |
| Gap (suppress local trail) | 1-bit in a `NetworkVariable` | server |

### 8.3 PlayerController becomes a NetworkBehaviour
```csharp
public sealed class PlayerController : NetworkBehaviour
{
    public PlayerState State;
    ITurnInput _turnSource;          // §6 — only constructed for the owner
    sbyte _serverTurn;               // latest input the server holds for this player

    void Update()   // OWNER only: read input and send to server
    {
        if (!IsOwner) return;
        SetTurnInputServerRpc(_turnSource.Read());
    }

    [ServerRpc] void SetTurnInputServerRpc(sbyte turn) => _serverTurn = turn;

    // SERVER only: called from GameManager.FixedUpdate (the tick)
    public void ServerTick(float dt, SimConfig cfg)
    {
        State = MovementStep(State, new InputFrame { turn = _serverTurn }, dt, cfg);  // §2, unchanged
        transform.position = State.position;          // NetworkTransform streams this out
        transform.rotation = Quaternion.Euler(0, 0, State.heading * Mathf.Rad2Deg);
    }
}
```
Only the **owner** reads a device (`ITurnInput`); the **server** simulates. `MovementStep`
is byte-for-byte the same method from §2.

### 8.4 Where the tick runs
- **Server:** `GameManager.FixedUpdate` runs the full §5.3/§7.3 tick (move → trail → fade →
  pickups → collision → match → spawn) for *both* players. This is the truth.
- **Client:** `FixedUpdate` only **derives the visual trail** — append a point at each
  player's synced position unless that player's Gap flag is set; fade using the synced
  match-elapsed. No movement, no collision, no win logic on clients.

### 8.5 Power-ups over the network
Server spawns pickups, detects pickup against the head it simulates, applies effects
server-side, and exposes effect state via `NetworkVariable` so clients show boost glow /
phase shimmer. Eraser additionally fires `EraseClientRpc(center, radius)` so clients clear
their *local* visual trails to match the server's erase.

### 8.6 Match flow & rematch
`phase`/`countdown`/`winner` are `NetworkVariable`s the server writes; clients read them to
drive countdown UI and the winner screen. `Rematch()` becomes a `ServerRpc`; **both players
must agree** before the server resets and re-enters Countdown.

### 8.7 Build & test order (GDD §5)
1. **M4 — host-only:** one instance as host; prove the server tick + NetworkTransform.
2. **M5 — two clients local:** Unity **Multiplayer Play Mode**; sync input + state, 2 players.
3. **M6 — Relay:** add Unity Relay for internet play (last — don't let it block earlier steps).

### 8.8 Open items
- [ ] Player spawning: NetworkManager player-prefab vs. server spawns 2 objects + assigns ownership.
- [ ] Send turn input every tick vs. only on change (both cheap; on-change saves a little).
- [ ] Revisit movement prediction only if Relay steering feels mushy.
- [ ] Hub/versioning (M7): both players must run the same module version before match start (GDD §5).
