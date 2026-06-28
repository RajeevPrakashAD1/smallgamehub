# Bloom + HDR colors (neon glow)

## The concept (the "why")
A normal screen pixel maxes at **1.0** per color channel — pure cyan is `(0, 1, 1)`.
**Bloom** is a post-processing effect: after the scene is rendered, it scans for pixels
*brighter than a threshold*, then blurs/bleeds that brightness into neighboring pixels.
That bleed is what your eye reads as "glow."

For a pixel to glow convincingly it should be **brighter than 1.0**, and only **HDR**
(High Dynamic Range) colors can hold values above 1.0. So the glow chain has three links:

1. **HDR color (value > 1.0)** — in code, mark the field `[ColorUsage(true, true)]`; the
   second `true` enables the inspector's HDR **Intensity** slider (an exposure multiplier).
2. **Camera renders post-processing** — the camera must have *Post Processing* enabled,
   and the URP renderer must have Post Process Data assigned.
3. **Bloom is active in a Volume** — a Volume Profile with a Bloom override whose
   `intensity > 0`. URP's *Default Volume Profile* is a global fallback applied everywhere.

If any link is missing, no glow.

## How it was used here (Trail Trap, M2)
- `TrailView.cs`: trail colors are `[ColorUsage(true, true)] Color color1/color2` (HDR), and
  a `Gradient` ramps alpha tail→head so the trail fades out. The gradient sets the
  LineRenderer's **vertex colors**.
- `SampleScene.unity`: Main Camera `m_RenderPostProcessing` flipped `0 → 1`.
- `DefaultVolumeProfile.asset`: Bloom `intensity 0 → 1`, `threshold 0.9 → 0.8`.
- The LineRenderer **material** must be unlit and pass vertex colors through
  (`Sprites/Default`) — a lit material would be dimmed by 2D lights, and a null material
  renders pink and can't glow.

Bloom knobs worth knowing:
- **Threshold** — brightness a pixel must exceed to glow (lower = more things glow).
- **Intensity** — how strong the glow is.
- **Scatter** — how far the glow spreads.

## Interview questions
**Q: What is HDR rendering and why does Bloom need it?**
HDR lets color values exceed 1.0, preserving "how bright" beyond what a display shows.
Bloom uses those super-bright values to decide what glows and how much; without HDR
everything clamps at 1.0 and the effect is flat/uniform.

**Q: Why is Bloom a post-process (screen-space) effect rather than per-object?**
It operates on the finished frame buffer, sampling neighboring pixels to spread light.
That's inherently a 2D image operation done after the scene is rendered, so it's cheap
relative to simulating real light bleed in the 3D scene and works regardless of geometry.

**Q: Difference between tone mapping and Bloom?**
Tone mapping *maps* HDR values down into the displayable 0–1 range (how bright becomes how
displayed); Bloom *spreads* bright pixels to fake glow. They're separate stages; tone
mapping usually runs after Bloom.

**Q: Forward vs deferred — does it affect post-processing like Bloom?**
Bloom is screen-space and runs the same way regardless of the lighting path; it just needs
an HDR color target to read from.

## Next topic to explore
Screen shake / camera juice (a small, time-decaying positional offset driven by an event
like a crash), then audio cues (countdown beeps). These complete the M2 "juice" pass.
