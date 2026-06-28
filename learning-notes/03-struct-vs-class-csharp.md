# 03 — struct vs class (C# value vs reference types)

> Learned while writing `PlayerState` and `InputFrame` for Trail Trap.

## The concept (the "why")
- **struct = value type.** Lives on the stack (or inline), and is **copied** on assignment /
  when passed to a method. Two variables hold two independent copies.
- **class = reference type.** Lives on the heap; variables hold a **reference** (pointer).
  Assigning copies the reference, so both point at the *same* object — mutating one mutates
  "both".

```csharp
struct P { public int x; }
class  C { public int x; }

var a = new P { x = 1 }; var b = a; b.x = 9;   // a.x still 1  (copy)
var c = new C { x = 1 }; var d = c; d.x = 9;   // c.x now 9    (same object)
```

Use a **struct** for small, short-lived data bundles you treat as values (positions, inputs).
Use a **class** for things with identity/behaviour that you share and mutate.

## How it applies here
- `PlayerState` (position, heading, speed, alive) is a **struct** so movement can be a *pure
  function*: `MovementStep(state, input, ...)` returns a **new** state instead of mutating a
  shared object. Predictable, testable, reused unchanged by the server at M4.
- `InputFrame` (just `turn`) is a separate **struct** = the player's intent, kept apart from
  state so it can be sent over the network later.

## Aside: why `sbyte` for `InputFrame.turn`
`sbyte` = signed 8-bit int (−128..127), 1 byte. `turn` only holds −1/0/+1, so it must be
*signed* (a `byte` is 0..255, can't be −1) and 1 byte is plenty. Main payoff: `InputFrame` is
sent to the server **every tick** at M4, so 1 byte vs `int`'s 4 bytes keeps bandwidth lean.
It does **not** come from Unity's new Input System (that emits `bool`/`float`/`Vector2`) — we
read the device, then **quantize** to −1/0/+1. Going analog later = switch to `float` and read
the Input System's float/Vector2 directly (math already supports it).

## Interview questions
- **byte vs sbyte?** byte = unsigned 0..255; sbyte = signed −128..127; both 1 byte.
- **Why a smaller int type?** Memory + serialization/bandwidth (many small networked values);
  not usually raw CPU speed (math often widens to int anyway).
- **struct vs class in C#?** struct = value type, copied on assignment, usually stack/inline;
  class = reference type, heap-allocated, shared by reference.
- **When prefer a struct?** Small (~16 bytes), immutable-ish, short-lived value-like data;
  avoids heap allocations/GC pressure.
- **Gotcha with mutable structs?** Modifying a struct returned by a property or stored in a
  readonly field changes a *copy* — a classic bug. Prefer immutable structs or mutate the
  stored variable directly.
- **Are structs always on the stack?** No — a struct that's a field of a class lives on the
  heap inside that object; "value type" is about copy semantics, not a fixed location.
- **Boxing?** Treating a struct as `object`/interface allocates a heap copy ("boxing"), which
  costs performance — watch for it in hot loops.
