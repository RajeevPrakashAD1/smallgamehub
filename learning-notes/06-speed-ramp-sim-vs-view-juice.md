# Speed ramp — and "juice" that belongs in the sim, not the view

## The concept
Earlier juice (glow, screen shake, audio) was **presentation** — it changes how the game
*looks/sounds*, never what *happens*. The speed ramp is different: it changes movement, which
is **gameplay**. That single distinction decides where the code goes:

- **View juice** → runs in `Update`/`LateUpdate`, can use `Time.time`, lives in View/ scripts,
  may be skipped on a headless server. (CameraShake, audio.)
- **Sim juice** → runs in the fixed tick (`FixedUpdate`), uses the passed-in `dt`, lives in
  Gameplay/, and MUST run identically on the host/server later (M4). (Speed ramp.)

Putting the ramp in the sim keeps the game host-authoritative-ready: the server will run the
exact same `SpeedAt` each tick, so clients can't desync by speeding themselves up.

## How it was built (Trail Trap, M2)
- `SimConfig`: added `maxSpeed` and `speedRampDuration` (authored tunables).
- `GameManager`: tracks `_playElapsed` (seconds spent in the Playing phase; reset on
  start/rematch). Each Playing tick:
  ```csharp
  p1.State.speed = p2.State.speed = SpeedAt(_playElapsed, config);
  _playElapsed += dt;
  ```
- The pure ramp mirrors the existing trail-fade ramp exactly:
  ```csharp
  static float SpeedAt(float elapsed, SimConfig cfg)
      => Mathf.Lerp(cfg.baseSpeed, cfg.maxSpeed,
                    Mathf.Clamp01(elapsed / cfg.speedRampDuration));
  ```

### Why set `State.speed` instead of editing `MovementStep`?
`MovementStep` is a **pure function** (no globals, fully unit-tested) that already reads
`p.speed`. By only *setting* the field before the step, the ramp stays out of the math —
`MovementStep` needs no change, its tests still hold, and the server reuses it untouched.
This is the "open for extension, closed for modification" idea in practice.

### Why `Lerp` + `Clamp01`?
`Clamp01(elapsed / duration)` produces a 0→1 progress value that *stops* at 1, so
`Lerp(base, max, t)` rises linearly then **holds** at `maxSpeed` (it never overshoots).
Same shape as the trail fade — late game = faster *and* more crowded = rising tension.

## Interview questions
**Q: Why FixedUpdate (fixed timestep) for gameplay vs Update for visuals?**
Fixed timestep makes the simulation deterministic and frame-rate independent — the same
sequence of ticks produces the same result on any machine, which is essential for physics,
networking, and replays. Visuals can run per render frame since they don't affect outcomes.

**Q: What does "host-authoritative" mean and how did this change respect it?**
The server/host is the single source of truth for game state; clients send intent, the host
simulates. Keeping the ramp in the deterministic tick (not the view) means the host computes
speed and clients can't diverge.

**Q: Why pass `dt` in instead of reading `Time.deltaTime` inside the logic?**
It keeps the function pure and testable, and lets the same code run under a different clock
(server tick, replay, unit test) without depending on Unity's frame timing.

**Q: Lerp vs MoveTowards vs SmoothDamp?**
`Lerp(a,b,t)` blends by a 0–1 fraction (here, time progress). `MoveTowards` steps by a fixed
max delta per call. `SmoothDamp` eases with velocity for spring-like motion. We wanted a flat
linear climb to a cap, so clamped `Lerp` fits.

## Next topic to explore
Audio cues (deferred — will plug in downloaded clips), then a real countdown/winner UI to
replace the temporary OnGUI HUD. After that: M3 power-ups or M4 netcode.
