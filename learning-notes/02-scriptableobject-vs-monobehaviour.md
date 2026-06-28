# 02 — ScriptableObject vs MonoBehaviour

> Learned while creating `SimConfig` (Trail Trap's tunables asset).

## The concept (the "why")
Both derive from `UnityEngine.Object`, but they live in different places and do different jobs:

| | MonoBehaviour | ScriptableObject |
|---|---|---|
| Lives | **on a GameObject** in a scene | **as an asset** in `Assets/` |
| Purpose | *behaviour* — runs logic each frame (`Update`, `FixedUpdate`...) | *data container* — just stores values |
| Lifecycle callbacks | `Awake/Start/Update/OnCollision...` | `OnEnable/OnDisable/OnValidate` (no per-frame loop) |
| Instances | one per GameObject it's attached to | one shared asset many objects can reference |

**Why use a ScriptableObject for config:** keep all gameplay numbers in one asset you tune in
the Inspector (no code edits, no Play mode needed), shared by every system that references it.
It also avoids duplicating the same values across many MonoBehaviours and survives outside any
single scene.

## How it applies here
`SimConfig` is a ScriptableObject holding tunables (`baseSpeed`, `turnRateDeg`, ...). Created
via `[CreateAssetMenu(...)]` → right-click `Create > TrailTrap > Sim Config`. GameManager and
PlayerController will reference the same `SimConfig` asset, so balancing the game = editing one
asset.

```csharp
[CreateAssetMenu(menuName = "TrailTrap/Sim Config", fileName = "SimConfig")]
public sealed class SimConfig : ScriptableObject { public float baseSpeed = 5f; /* ... */ }
```

## Interview questions
- **ScriptableObject vs MonoBehaviour?** MonoBehaviour attaches to a GameObject and runs
  per-frame logic; ScriptableObject is an asset that stores shared data with no per-frame loop.
- **Why use ScriptableObjects?** Centralized, Inspector-editable, reusable data; less memory
  duplication; decouples data from scene objects; good for configs, events, and data tables.
- **Can a ScriptableObject have Update()?** No per-frame `Update` callback — it's not on a
  GameObject. It has `OnEnable`/`OnDisable`/`OnValidate`.
- **Where does ScriptableObject data persist?** As an asset in the project (edit-time changes
  saved); runtime changes to the asset are NOT auto-persisted in builds.
- **Common ScriptableObject patterns?** Config/settings, ScriptableObject-based events, and
  data-driven design (enemy/item definitions).
