# 09 — Power-ups as tick modifiers; the mark-dead pattern

## Power-ups are modifiers, not systems (LLD §7)

Each power-up is a small hook into the existing fixed tick, not new machinery:

| Power-up | Hook | Implementation |
|---|---|---|
| Boost (3 s) | movement | `speed = ramp × (BoostActive ? boostMul : 1)` |
| Phase (2 s) | collision | `HitsTrail` returns false while `PhaseActive` (walls still kill) |
| Eraser (instant) | trail data | `EraseAround`: mark points dead in BOTH trails |
| Gap (2 s) | trail append | skipped for v1 (GDD allows 3 of 4) |

Payoff of the scaffolding: Boost cost a system (`PowerUpSystem` + `ActiveEffects` +
spawner + view); Phase cost 6 small edits, zero new files.

**Effects lifecycle** (`ActiveEffects` struct on `PlayerController`): written by
`PowerUpSystem.Apply` on collect → drained by `Effects.Tick(dt)` each tick → read by the
relevant hook → wiped by `Effects = default` on respawn. Data lives on the player;
rules live in the systems (same split as `PlayerState`).

## The mark-dead pattern (Eraser)

Don't remove erased points — set `deathAt = elapsed` ("you expired just now"):

- Reuses the existing "dead when `deathAt <= now`" rule; `Fade` needs zero changes.
- Preserves the sorted-by-deathAt list invariant (removal only from the front).
- O(1) per point vs O(n) mid-list removal; replicates at M4 as a tiny "erase at (x, r)"
  event instead of resyncing whole lists.
- Consequence (§7.5): every consumer must now skip dead points — collision ignores
  segments with a dead endpoint; the view must not draw dead points at full glow.

**C# struct gotcha met here:** `List<T>`'s indexer returns a *copy* for structs —
`t[i].deathAt = x` won't compile. Copy out, mutate, write back:
`var p = t[i]; p.deathAt = _elapsed; t[i] = p;`
(Same photocopy rule explains why `pl.Effects.boost = ...` works only because `Effects`
is a public *field*, and why `transform.position.x = 1` is illegal — classic interview q.)

## Rendering a trail with holes (design decided; view pending)

A LineRenderer is one continuous ribbon with one gradient (~8 keys) — it can't skip or
recolor an arbitrary interior stretch. Options weighed:

1. **Dots (sprite per point):** per-point color trivial, but dot spacing = `speed × dt`
   (0.2 u base → 0.72 u boosted vs 0.2 u width) → the line dissolves into beads exactly
   when the game is fastest. Prototype-only.
2. **Split into runs of pooled LineRenderers** ← chosen: cut the point list at live/dead
   boundaries; each run rents a renderer cloned from the authored template; live runs get
   the glow gradient, dead runs a dim ghost color (readability: a visible "broken wall"
   beats an invisible hole). No bites → 1 run → identical to today.
3. **Procedural mesh with vertex colors:** the senior answer — one draw call, arbitrary
   per-point effects; most code. LineRenderer generates such a mesh internally anyway.

## Interview questions

- *Buff/debuff system design?* Timed effects as data on the affected entity, applied by
  systems in a fixed tick order; instant effects mutate state directly at pickup time.
- *Delete from the middle of a hot list?* Consider tombstoning (mark dead) to preserve
  invariants and O(1) cost; lazily compact later (our front-fade does this for free).
- *Why can't you set `transform.position.x`?* Value-type property returns a copy;
  mutate a local copy and assign back.
- *Color part of a line differently in Unity?* Split renderers per region or build a
  vertex-colored mesh; one gradient can't target arbitrary interior ranges.
- *LineRenderer vs TrailRenderer?* Same rendering core; LineRenderer takes explicit
  points (data you own — needed for collision/netcode), TrailRenderer auto-lays and
  hides them.
