# Screen shake + C# events (sim → view decoupling)

## Two concepts landed here.

### 1. Decoupling sim from view with a C# `event`
The crash is detected deep in the simulation (`GameManager.CollisionStep`), but the
*reaction* (shake the camera) is a view concern. We don't want the sim to know cameras
exist — that would tangle gameplay with presentation and break the host-authoritative plan
(the server runs the sim with no camera at all).

Solution: the sim **publishes** an event; view objects **subscribe**.

```csharp
// GameManager (publisher)
public event System.Action OnCrash;          // "a crash happened" — no idea who listens
...
if (d1 || d2) OnCrash?.Invoke();             // ?. = only fire if someone subscribed

// CameraShake (subscriber)
void OnEnable()  { game.OnCrash += AddCrashTrauma; }
void OnDisable() { game.OnCrash -= AddCrashTrauma; }   // ALWAYS unsubscribe
```

- **Why subscribe in `OnEnable` / unsubscribe in `OnDisable`?** A subscription is a reference
  from the publisher (GameManager) to the subscriber (CameraShake). If you never remove it,
  the GameManager keeps the destroyed camera alive (a **managed memory leak**) and may invoke
  a method on a dead object. Pairing `+=`/`-=` with enable/disable keeps it clean.
- The sim raises the event inside `FixedUpdate` (the tick); the view just stores a number and
  animates it later in `Update`/`LateUpdate`. Event = a one-line, instantaneous notification.

### 2. The "trauma" screen-shake model (Squirrel Eiserloh, GDC)
Naive shake = "offset camera by random for N frames" — feels mechanical and can stack badly.
Better:
- Keep a single `trauma` value in `[0,1]`.
- Callers **add** trauma on impact (`crashTrauma = 0.7`); it **clamps** at 1, so multiple
  hits don't explode.
- Felt shake = `trauma * trauma` (squared) → small trauma barely moves, big trauma punches,
  and it tapers smoothly.
- Trauma **drains** every frame (`trauma -= decayPerSec * dt`) so the shake auto-settles.
- Offset uses **Perlin noise** sampled over time, not `Random` per frame, so it's a smooth
  vibration instead of harsh static. Different noise seeds per axis (x, y, roll) = independent
  motion.

```csharp
float shake = _trauma * _trauma;
float t = Time.time * frequency;
float ox = (Mathf.PerlinNoise(seed,      t) * 2 - 1) * maxOffset * shake; // [-1,1] * range
```

Runs in `LateUpdate` so it happens after anything else that moves the camera, and it always
returns to the exact rest position when trauma hits 0.

## How it was used here (Trail Trap, M2)
- `GameManager`: added `public event Action OnCrash;`, fired once on the crash tick.
- `CameraShake` (new, on Main Camera): subscribes to `OnCrash`, adds trauma, shakes camera.
- Wired in the scene: component added to Main Camera with its `game` field = the GameManager.

## Interview questions
**Q: Difference between a C# `event` and a plain `delegate`/`Action` field?**
An `event` is a delegate with restricted access: outside classes can only `+=`/`-=`, not
invoke it or overwrite the whole list. It protects the publisher's invocation rights.

**Q: How do event subscriptions cause memory leaks?**
The publisher holds a reference to each subscriber's method (and thus its object). If the
subscriber is "destroyed" but never unsubscribes, the publisher keeps it reachable, so the
GC can't collect it. Fix: unsubscribe (here, in `OnDisable`).

**Q: Why `OnCrash?.Invoke()` instead of `OnCrash.Invoke()`?**
If no one has subscribed, the event is `null`; invoking null throws. `?.` invokes only when
non-null.

**Q: Why FixedUpdate for the sim but LateUpdate for the shake?**
FixedUpdate is the fixed-timestep tick (deterministic gameplay). Visual-only effects belong
in Update/LateUpdate, tied to render frames; LateUpdate specifically runs after other
movement so the camera adjustment is applied last.

**Q: Why Perlin noise over Random.Range for shake?**
Perlin is continuous — adjacent samples are close, giving smooth motion. Per-frame Random is
discontinuous, producing harsh jitter.

## Next topic to explore
Audio cues (countdown beeps + crash SFX) — reuse the same `OnCrash` event to also trigger a
sound, reinforcing the event-driven view pattern. Then a match speed-ramp for rising tension.
