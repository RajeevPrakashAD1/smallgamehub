# 01 — Unity project setup: templates & render pipelines

> Topic learned while deciding how to set up the SmallGameApp project (a hub that ships
> several small 2D **and** 3D games, mobile-first). First game = Trail Trap (2D).

## The concept (the "why")
"2D or 3D project?" is really **two** decisions, and the second matters far more:

### 1. Render pipeline (the costly-to-change decision)
The render pipeline is *how* a scene becomes pixels (lighting, shadows, post-processing
like bloom/glow). Three options:
- **Built-in** — legacy default. Deprecation began in Unity 6.5; *not* for new projects.
- **HDRP** — high-end 3D (PC/console, ray tracing). 3D-only, heavy, not for mobile.
- **URP (Universal Render Pipeline)** — lightweight, scalable, mobile→console, supports
  **both 2D and 3D**. Its 2D Renderer has 2D lights + Bloom (= the glow look we want).

➡ **Chosen: URP.** It's the only one that fits "mobile + 2D + 3D + glow".

### 2. 2D vs 3D *template* (NOT a lock-in)
A template only sets **starting defaults**:
- **Default Behavior Mode** (new scenes default to 2D orthographic / sprites, or 3D
  perspective / meshes).
- The **2D template preinstalls extra 2D tooling** (Sprite, Tilemap, 2D Animation).

You can **mix 2D and 3D in one URP project**: different scenes/cameras use a **2D Renderer**
or a **Universal (3D) Renderer**, assigned per scene. So pick the template matching what you
build *first*.

## How it applies here
- **Start from "Universal 2D" (URP)** — first game (Trail Trap) is 2D and wants 2D-lit glow;
  the hub is just UI (pipeline-agnostic).
- Add a **3D (Universal) Renderer + 3D scenes** later for the first 3D game — no pipeline
  switch needed (already URP).
- **Architecture:** ONE Unity project, ONE scene per game + a hub scene. Hub loads games via
  `SceneManager.LoadScene`, later via **Addressables** so games ship/update independently
  (matches GDD M7).
- Don't make a separate Unity project per game — Unity can't cleanly embed those.

## Interview questions
- **Built-in vs URP vs HDRP?** Built-in = legacy/flexible/deprecating; URP = lightweight,
  scalable, mobile→console, 2D+3D; HDRP = high-fidelity 3D (ray tracing, volumetrics), heavy.
- **Why URP for mobile?** Scalable, low-overhead forward rendering, broad device support,
  still supports PBR + post-processing.
- **Can one project use both 2D and 3D?** Yes — per-camera/scene 2D Renderer vs Universal
  Renderer; template only sets default behavior mode.
- **What does a project template actually change?** Default Behavior Mode + preinstalled
  packages — not a permanent constraint.
- **Why is switching render pipeline late costly?** Shaders/materials/lighting/post are
  pipeline-specific; converting means re-authoring shaders and re-tuning lighting.
