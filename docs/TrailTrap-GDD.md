# Trail Trap — Game Design Document

> Status: Draft v0.2 · Owner: Gokul · Last updated: 2026-06-18

---

## 1. One-line pitch
Two players duel in a single quick 1v1 match, each leaving a deadly fading light-trail behind them. Touch any trail and you die. First to crash loses. Grab quick power-ups to swing the duel.

## 2. Design pillars (the rules every decision must serve)
1. **Instantly understandable** — anyone gets it in 5 seconds: "don't crash."
2. **Fun comes from tension, not features** — fading trails + power-ups create drama, not complexity.
3. **Easy to build & finish** — flat 2D, simple shapes. If a feature is hard to net-sync, it gets cut.
4. **Quick play, instant rematch** — one match lasts ~1–2 min; you're always one tap from playing again. No rounds, no best-of.

If a feature fights pillar 3 or 4, it loses.

## 2a. Target platform — **mobile-first**
v1 ships **mobile-first** (touch, one-handed play). Desktop exists only for development/testing.
Every design and tech decision prioritizes mobile: controls, UI scale, readability at phone
size, performance/battery, and input. When a choice trades mobile feel for desktop convenience,
**mobile wins**. (Online 1v1 means each player is on their own device — so single-thumb controls
are the design target, not two-player split-screen input.)

---

## 3. Core gameplay

### 3.1 The loop
1. Two players spawn apart, moving forward automatically.
2. Players steer (free movement); a curved trail is laid behind them continuously.
3. Trails **fade over time**, and the fade time grows as the match goes on → the arena gets more dangerous (this is the tension engine).
4. First player to crash (into either trail, or the boundary) loses.
5. **Single quick match — sudden death.** No rounds. Winner screen → instant rematch or back to hub.

### 3.2 Movement model — **free movement**
- **Continuous 2D movement, no grid.** Player moves forward at constant speed; input rotates the heading at a capped **turn rate** (car / Asteroids-style steering). Produces smooth curved trails that look good.
- No instant 180° flips — turning is rate-limited, which is what makes it skillful and readable.
- Sim still runs on a **fixed tick** under the hood (for deterministic, net-friendly movement); rendering interpolates.

### 3.3 Trail & collision (the core mechanic)
- A trail is a **polyline**: an ordered list of points sampled along the player's path each tick, each point time-stamped.
- **Collision = distance check:** each tick, if the player's head is within `trailWidth/2 + grace` of any *live* trail segment → that player dies.
- **Self-trail immunity:** ignore the most recent ~0.3s of your *own* trail so tight turns don't self-kill.
- **Forgiving by design:** small collision grace, not pixel-perfect. Near-misses should feel close, not cheap.
- **Head-on tie** (both die same tick) → draw → instant replay.

### 3.4 Trail lifetime — **fading, with a growing fade time** ← main fun lever
- **Model chosen: time-based fading.** Each trail point vanishes `fadeTime` seconds after it's laid. The arena is a constantly-renewing maze rather than a board that clogs up.
- **Fade time ramps up over the match:** starts short/forgiving, grows long/claustrophobic. This automatically escalates tension and ends matches in ~1–2 min — it **replaces the shrinking arena**.
- **Erasing has two layers:**
  - *Background erase* = natural fade (keeps the board flowing, never permanently locks).  // lets go for this
  - *Burst erase* = **Trail Eraser** power-up clears trails in a radius (clutch escapes).
- **Tuning knobs for fun:** `fadeTime` (start/end), `trailWidth`, `eraser radius`. Short fade = chaotic/forgiving; long fade = tense/strategic.
- Optional: a simple static **boundary wall** around the arena (kills on contact). May not need an active shrink since the fade ramp does the tension work.

### 3.5 Win condition
- **Sudden death:** first to crash loses; the other wins immediately. Rematch button replays instantly.
- Match length is *emergent* (~30s–2min) from the fade ramp, not a hard timer.
- Both-die-same-tick = draw → replay.

---

## 4. Making it fun with *small* elements

Deliberately tiny — each is a few lines of logic + a glowing sprite. Add them **one at a time, only after the base duel is fun.**

### 4.1 Power-ups (pick 3–4 max for v1)
Spawn occasionally as a glowing pickup in open space; drive over to collect. Short effects (2–4s), instantly readable.

| Power-up | Effect | Why it's fun | Build cost |
|---|---|---|---|
| **Boost** | 2× speed for 3s | Risk/reward — speed kills if you misjudge | Tiny (speed multiplier) |
| **Phase** | Pass through trails for 2s | Dramatic escapes, clutch moments | Small (skip collision check) |
| **Gap** | Stop laying trail for 2s | Cut someone off cleanly, fake them out | Tiny (skip trail write) |
| **Trail Eraser** | Clears trails in a radius around you | Burst-erase to escape a closing trap | Small (remove nearby points) |

> Rule: a power-up should be explainable in 3 words. If it needs a tutorial, cut it.

### 4.2 Other cheap "juice" (feel > features)
- **Screen shake + flash** on death.
- **Color-coded players** (e.g. cyan vs orange) — trail = player color, fades toward transparent.
- **Countdown "3…2…1…GO"** before the match.
- **Near-miss feedback** — brief glow when you skim a trail.
- **Speed ramp** — both players speed up slightly over the match = rising intensity for free.
- **Sound:** hum while alive, sharp crash on death, ping on power-up. 3 sounds carry the whole game.

### 4.3 Explicitly OUT of v1 (scope guard)
- More than 2 players (architect for it, ship 2).
- Rounds / best-of / scoring systems.
- Maps/obstacles/themes, cosmetics, progression, accounts.
- Anything requiring a backend beyond matchmaking.

---

## 5. Multiplayer requirements
*(See companion networking notes; summary here.)*
- **Engine:** Unity + Netcode for GameObjects (NGO) + Relay (Relay added last).
- **Authority:** **Host-authoritative.** Host runs the whole simulation on a fixed tick — movement, trails, collision, fade, power-up spawns.
- **Client → Server:** sends only **steering input** (ServerRpc, e.g. `SetTurnInput(turnAxis, tick)`).
- **Server → Clients:** authoritative state via NetworkVariables / NetworkList — player position/heading, alive flags, match state, power-up positions.
- **Trail data:** server owns trails; clients render from synced trail-point additions (sync new points + their timestamps; clients fade them locally — don't resend the whole polyline each tick).
- **Tick rate:** ~20–30 ticks/sec (movement is smooth but not twitchy).
- **Testing order:** Host-only (one instance) → 2 clients via Multiplayer Play Mode locally → Relay for internet.
- **Versioning (for hub):** both players must run the same game-module version; check before match start.

---

## 6. Controls  *(mobile-first)*
The **movement model is identical on every platform**: constant forward speed, capped turn
rate. Only *how you aim* differs by device.

- **Mobile (primary) — floating joystick, aim-to-steer.** Touch anywhere and drag toward a
  direction; the player turns *toward* that direction at the capped turn rate (slither.io /
  worms.io style). One thumb, analog, comfortable. No grid, no on/off buttons.
- **Desktop (dev/testing) — mouse aim-to-steer** (same turn-toward model), plus **A/D or
  arrows** as a quick keyboard fallback.
- **Why aim-to-steer instead of hold-left/right:** pointing "where I want to go" (absolute) is
  far more comfortable than relative turn buttons and is the natural joystick model for mobile.
  The capped turn rate (can't snap 180°) is preserved — that constraint *is* the skill.
- Input is **analog** (`turn` is a float in [-1, 1]); the movement math already scales (LLD §2.2).

## 7. Art & audio (deliberately minimal)
- **Art:** solid background, bright curved trail lines (fading to transparent), glowing power-up pickups, simple player head sprite. Two accent colors.
- **Audio:** background hum, crash, power-up ping, countdown beeps.
- No animations beyond trail growth/fade, death particle burst, power-up pulse.

## 8. Screens / flow
1. **(In hub)** select Trail Trap → download/launch module.
2. **Game menu:** Play (matchmake) · How to play · Back to hub.
3. **Matchmaking:** find opponent → countdown.
4. **Match (sudden death) → winner screen.**
5. **Winner screen:** rematch or back to hub.

## 9. Build milestones
- **M1 — Local sim:** free movement, two players (shared keyboard), fading trails, distance collision, sudden-death win. *No network.*
- **M2 — Juice:** colors, shake, countdown, sound, speed ramp. Make it *feel* good.
- **M3 — Power-ups:** add 1, tune, then add the rest one at a time.
- **M4 — Netcode (host-only):** move sim into NetworkBehaviours, prove as host.
- **M5 — Two clients local:** Multiplayer Play Mode, sync inputs/state.
- **M6 — Relay:** play over the internet.
- **M7 — Hub module:** package as Addressables, version check, launch/return.

## 10. Open decisions (to resolve next)
- [x] Steering: **aim-to-steer (turn-toward, analog)** — resolved (mobile-first, §6). Turn-rate value still to feel-test (start 180°/s).
- [ ] Keep a boundary wall? Active shrink at all, or rely purely on the fade ramp?
- [ ] Power-up set for v1 (which 3–4 from §4.1).
- [x] Target platform: **mobile-first** — resolved (§2a). Desktop is dev/testing only.
- [ ] Tick rate & trail point-sampling rate.

---

### Appendix A — Tunable values (first guesses, expect to change)
| Param | Start value |
|---|---|
| Arena size | ~20 × 20 units (camera-framed) |
| Tick rate | 20–30 /sec |
| Player speed | constant (tune for ~1 arena-width in ~4–5s) |
| Turn rate | ~180°/sec |
| Trail fade time | 4s at start → 9s by end of match |
| Trail width | ~0.2 units |
| Collision grace | ~0.1 units |
| Self-trail immunity | last 0.3s of own trail |
| Eraser power-up radius | ~2 units |
| Power-up spawn | every ~6–10s, max 2 on board |
| Match length | emergent, ~1–2 min (no hard timer) |
