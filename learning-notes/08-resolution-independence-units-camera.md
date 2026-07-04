# 08 — World units, the camera, and resolution independence

## World units are an abstract ruler

A Unity unit has no inherent physical size (physics assumes 1 u = 1 m; we don't use it).
Only **ratios** between game measurements are real: our arena is "100 trail-widths wide",
crossing it takes `width / speed` seconds. Doubling every length and speed in SimConfig
would change nothing visible.

## Units → screen pixels

Orthographic `size` = **half the visible height in world units**. So:

```
pixels-per-unit on screen = screenHeight / (2 × orthographicSize)
visible half-width        = orthographicSize × aspect
```

At size 10 on 1080p: 54 px/unit → trailWidth 0.2 ≈ 11 px, speed 5 u/s ≈ 270 px/s.
A sharper phone gives more px/unit = sharper, not bigger — same screen fraction.
Separately, a sprite's **PPU import setting** maps *texture* pixels to world units.

## The aspect-ratio problem & the four industry policies

Screens range 4:3 (iPad) to 21:9 — visible width varies ~75% at fixed ortho size.
A hand-tuned camera/arena pair that fits one aspect clips on another (invisible death
walls = unshippable). Standard policies:

1. **Fit + decorate** (mobile standard — Clash Royale, Brawl Stars): zoom so the
   gameplay-critical rect always fits; extra space shows cosmetic background only.
   Fair by construction — everyone sees 100% of the play area. ← **Trail Trap uses this.**
2. **Extend view**: wider screen = more world. Immersive single-player; an information
   advantage in competitive (why esports titles cap FOV / restrict ultrawide).
3. **Letterbox**: black bars. Always correct, looks cheap on mobile.
4. **Crop**: fill and cut edges. Only when edges are decorative — lethal for us.

## How it's used here

`CameraFit.cs` (on Main Camera) recomputes every frame from the sim's truth:

```csharp
Vector2 need = config.arenaHalfSize + Vector2.one * margin;
cam.orthographicSize = Mathf.Max(need.y, need.x / cam.aspect);
```

Height-needed vs width-needed (width converted via aspect), take the binding constraint.
Same "derive the view from the sim's config" principle as ArenaView drawing the death
rectangle from `arenaHalfSize` — rule, picture, and framing can never disagree.

UI is a separate layer: Canvas Scaler (Scale With Screen Size + reference resolution),
anchors instead of absolute pixels, `Screen.safeArea` for notches. Preview tools:
Game-view aspect dropdown + Device Simulator (built into Unity 6).

## Interview questions

- *Ortho size vs FOV?* Half view-height in units (2D) vs vertical angle (3D); horizontal
  is derived from vertical × aspect in both.
- *Guarantee a fixed play area is always visible?* `size = max(halfH, halfW / aspect)`,
  recomputed on resolution change.
- *Why do competitive games restrict ultrawide?* Wider aspect = more visible world =
  information advantage; cap FOV, letterbox ranked, or design so everyone sees the whole
  competitive space.
- *Make UI work across screen sizes?* Canvas Scaler + reference resolution + Match,
  anchor-based layout, safe area.
- *What is sprite PPU?* Texture pixels per world unit; sets world size of a sprite and,
  with camera size + target resolution, its on-screen sharpness.
