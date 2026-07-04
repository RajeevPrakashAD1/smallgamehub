# 07 — Delta time (`dt`) & the fixed timestep

## The concept

`dt` (delta time, Δt) = **how many seconds of game time one simulation step represents**.
A game world is simulated as a flipbook of discrete steps; `dt` is the time-thickness of
one page. Everything the sim does per step is scaled by it:

```csharp
p.position += fwd * (p.speed * dt);   // move: 5 u/s × 0.04 s = 0.2 units this tick
boost -= dt;                          // timer: consume 0.04 s of the remaining 3 s
```

**Why scale by dt?** Rate independence. If the tick rate changes (25 Hz → 50 Hz), `dt`
halves, ticks double, and speeds/durations stay identical in real seconds. Code that
moves "0.2 per tick" or subtracts "0.04 per tick" silently changes behavior with the rate.

**Fixed vs variable dt:**
- `Update()` → `Time.deltaTime` is *variable* (0.016 s at 60 fps, spikes on hitches).
  Fine for visuals; bad for simulation — variable steps make physics/collision behave
  differently per machine (subtle "feel" changes up to tunneling through walls).
- `FixedUpdate()` → `Time.fixedDeltaTime` is *constant*. Unity's loop is "semi-fixed":
  render at any fps, sim catches up with 0..N fixed steps per frame.
- Canonical essay: Gaffer on Games, "Fix Your Timestep!"
  (https://gafferongames.com/post/fix_your_timestep/) — includes the "spiral of death"
  (a fixed step that costs more than a step's worth of time never catches up).

## How it's used here

- Locked tick: 25 Hz → `dt = 0.04`. All sim (move → trail → fade → collect → collide →
  resolve → spawn) runs in `FixedUpdate`; `Update` is input/visuals only.
- `dt` is a **parameter** everywhere (`MovementStep`, `Fade`, `Effects.Tick`) instead of
  reading `Time.fixedDeltaTime` inside — keeps the methods pure: unit tests pass any dt,
  and the M4 server reuses them with no Unity clock.
- Boost timer trace: 3.0 s at dt 0.04 → exactly 75 ticks to expiry.

## Alternatives for "expires after N seconds" (both live in this codebase!)

| Approach | How | Used here | Trade-off |
|---|---|---|---|
| Count down float | `t -= dt` each tick | `ActiveEffects` (boost/phase) | mutation per tick; "remaining" is directly readable |
| Expiry timestamp | `deathAt = now + N`, compare to clock | `TrailPoint.deathAt` | set once, zero per-tick cost; needs a shared clock |
| Tick counting (int) | `ticksLeft--` | no | couples durations to tick rate |
| Coroutines/Invoke | `WaitForSeconds` | no (view-only tool) | frame-based, non-deterministic, untestable as sim truth |
| Wall clock | `Time.time`, `DateTime` | no | breaks pause/slow-mo/headless server |

Rule of thumb: **few things that report remaining time → count down; many things that
just expire → timestamp.**

## Interview questions

- *What is deltaTime and why multiply movement by it?* Seconds this step represents;
  scaling makes speed frame/tick-rate independent (units per second, not per frame).
- *Update vs FixedUpdate?* Update: once per rendered frame, variable dt, input/visuals.
  FixedUpdate: fixed-rate sim steps, may run 0..N times per frame.
- *Fixed vs variable timestep trade-offs?* Variable: smooth/simple but nondeterministic.
  Fixed: stable/reproducible but needs catch-up; risk of spiral of death.
- *Implement a 3-second buff?* Countdown minus dt in the fixed tick, or expiry timestamp
  vs the sim clock — never wall clock or coroutines for authoritative state.
- *What do you need for identical replays on every machine?* Fixed timestep, fixed step
  order, no wall-clock/frame-rate inputs, seeded RNG.
