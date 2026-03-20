# ParticleSystem Weather VFX Design

## Overview

Replace the Painter2D-based weather particle rendering in `WeatherVFXOverlay` with Unity ParticleSystem components rendered to a RenderTexture displayed on a UI Toolkit VisualElement. This solves anti-aliasing issues, provides GPU-accelerated rendering, and handles high particle counts efficiently.

## Architecture

### New Scene Objects

A `WeatherVFX` root GameObject in the scene with children:

- **`WeatherVFXCamera`** — Orthographic Camera, culling mask set to a dedicated layer (layer index 6, named "WeatherVFX" — created at runtime via `TagManager` or manually in Project Settings). Renders to a RenderTexture with transparent clear color. Depth -1 (doesn't render to screen). Camera size matched to half the canvas height in world units.
- **`RainParticles`** — ParticleSystem for rain ground impacts. Assigned to "WeatherVFX" layer.
- **`SnowParticles`** — ParticleSystem for falling snowflakes. Assigned to "WeatherVFX" layer.

All three are children of the root so they can be found/managed together. Created at runtime by `WeatherVFXOverlay.Initialize()`.

**Layer setup**: The implementation must assign all VFX GameObjects to layer 6 and set the camera culling mask to `1 << 6`. If layer 6 is unnamed, set its name to "WeatherVFX" via code or verify it exists. The main camera (if any) should exclude this layer to avoid double-rendering.

### Modified Files

- **`WeatherVFXOverlay.cs`** — Rewritten. Removes all Painter2D code, particle structs, and simulation logic (including `generateVisualContent` callback). New responsibilities:
  - Creates/manages RenderTexture and VisualElement displaying it
  - Creates camera and ParticleSystem GameObjects at runtime
  - Controls emission rates based on weather condition
  - Syncs camera position with canvas pan offset
  - Retains lightning flash logic (VisualElement opacity pulse, unchanged)
- **`CampsiteViewUI.cs`** — No changes. Already calls `weatherVFXOverlay.Initialize(canvas)`.

### Coordinate System

The ParticleSystem operates in world space. One world unit = one pixel of canvas space.

- Camera orthographic size = `canvasHeight / 2`
- Camera position tracks canvas pan offset. The translate from `canvas.resolvedStyle.translate` gives the CSS translate (negative X = panned right, negative Y = panned down). Camera position in world coords:
  ```
  float cx = canvasWidth / 2f + translate.x;   // center of visible area in canvas X
  float cy = -(canvasHeight / 2f + translate.y); // flipped Y: UI down = world up
  camera.transform.position = new Vector3(cx, cy, -10f);
  ```
  Example: canvas is 2000x3000, viewport is 500x800, translate is (-200, -100) (panned 200px right, 100px down). Camera center = (2000/2 + (-200), -(3000/2 + (-100))) = (800, -1400). The camera's ortho size shows 400px in each direction vertically, so it sees Y range -1000 to -1800 in world, which maps to canvas Y 1000-1800 — correct visible area.
- Particles spawn in world-space coordinates: X = canvas pixel X, Y = -(canvas pixel Y)

### RenderTexture

- Resolution: viewport layout dimensions multiplied by `Screen.dpi / 96f` (clamped to 1-3x) for proper high-DPI rendering. Fallback to `viewport.resolvedStyle.width/height` if DPI unavailable.
- Format: `RenderTextureFormat.ARGB32` with transparent clear
- Recreated if viewport size changes significantly (>10% difference)
- Displayed via `Background.FromRenderTexture(rt)` on a VisualElement child of the viewport (available in Unity 6). The VisualElement is absolute-positioned to fill the viewport, `pickingMode = Ignore`.
- Camera aspect ratio set to match the RenderTexture dimensions

### Resource Cleanup

In `OnDestroy()`:
- Call `renderTexture.Release()` and `Destroy(renderTexture)`
- Destroy the runtime-created root GameObject (camera + particle systems)
- Remove the VisualElement from the viewport
- Unsubscribe from `WeatherService.OnWeatherUpdated`

## Rain ParticleSystem

Top-down ground impacts — circles appear at random positions and expand outward as fading filled circles.

### Configuration
- **Simulation Space**: World
- **Shape Module**: Box, size = `(canvasWidth, 1, canvasHeight)` in world coords (XZ plane mapped to XY since we're in 2D). Position at canvas center.
- **Start Lifetime**: 0.5–0.7s (randomized between two constants)
- **Start Size**: 6–14px (randomized)
- **Start Speed**: 0 (impacts are stationary)
- **Start Color**: `rgba(180, 200, 255, 165)` — cool blue-white
- **Emission**: Rate = `canvasArea / viewportArea * 80`. For a canvas 4x viewport area, that's 320 impacts/sec.
- **Size over Lifetime**: Curve from 0.2 → 1.0 (small dot expands to full size)
- **Color over Lifetime**: Alpha gradient 1.0 → 0.0 over lifetime
- **Renderer**: Billboard mode, `raindrop.png` sprite, material using `Particles/Standard Unlit` shader with **alpha blending**
- **Max Particles**: 1000

### Storm Variant
When `WeatherCondition.Storm`, multiply emission rate by 1.25 and increase start size range to 8–18px.

## Snow ParticleSystem

Falling snowflakes drifting from top to bottom of the canvas with organic sway.

### Configuration
- **Simulation Space**: World
- **Shape Module**: Edge, width = canvas width, positioned at Y = 0 (top of canvas in world space, since Y is flipped)
- **Start Lifetime**: `canvasHeight / 60f` (based on average speed of 60 px/s). For a 3000px canvas, ~50s.
- **Start Size**: 6–12px (randomized)
- **Start Speed**: 40–80 px/s downward (in world space: positive Y since world Y is flipped... actually negative Y in world since down = -Y in world). Set via `startSpeed` + gravity or velocity over lifetime pointing in -Y world direction.
- **Start Color**: `rgba(230, 235, 255, 140)` — soft white with blue tint
- **Emission**: Rate = `canvasWidth / viewportWidth * 4` flakes/sec. Scales with canvas width so the visible density stays consistent.
- **Noise Module**: Enabled
  - Strength: 15 (X axis), 0 (Y axis)
  - Frequency: 1.0
  - Scroll speed: 0.1
  - Octaves: 2
- **Rotation over Lifetime**: Angular velocity 15–30 deg/sec (randomized, both directions via `startRotation` random between two constants)
- **Color over Lifetime**: Alpha gradient: 0 at 0%, 1 at 5%, 1 at 90%, 0 at 100%
- **Renderer**: Billboard mode, `snowflake.png` sprite, material using `Particles/Standard Unlit` shader with **alpha blending**
- **Max Particles**: 1000

### Pre-seeding on Initialize
If weather is already snowing at startup, call `snowParticleSystem.Simulate(canvasHeight / 60f, true, true)` to fast-forward the simulation so particles are already distributed across the canvas. Then call `Play()`.

## Lightning Flash

Unchanged from current implementation. A VisualElement with opacity pulsed via code:
- 2–3 rapid pulses (150ms on, 100ms gap) at random intervals (4–10s)
- 30% chance of a follow-up cluster strike within 1s
- Only active during `WeatherCondition.Storm`
- Element is a child of the viewport, `pickingMode = Ignore`

## State Transitions

- Subscribe to `WeatherService.Instance.OnWeatherUpdated`
- On weather change, set emission rate on the appropriate ParticleSystem:
  - `Rain`/`Storm`: enable rain emission, disable snow emission
  - `Snow`: enable snow emission, disable rain emission
  - `Clear`/`Cloudy`: disable both emissions (existing particles finish their lifetime naturally)
- Lightning stops immediately on non-Storm (same as current)
- No manual spawn/despawn logic — ParticleSystem handles lifecycle automatically
- When switching to snow, call `Simulate()` to pre-fill if no snow particles are alive

## Pan Synchronization

Each frame in `Update()`:
1. Read `canvas.resolvedStyle.translate` for pan offset
2. Compute camera world position from translate + canvas dimensions (see Coordinate System section)
3. Also update camera orthographic size if canvas dimensions changed (grid expansion)

The camera reads the translate at the same time as rendering, ensuring perfect sync with no frame lag.

## Performance

- GPU-accelerated rendering — particles rendered by the GPU, not CPU Painter2D
- Proper anti-aliasing from the particle renderer and sprite texture filtering
- RenderTexture is a single GPU blit per frame
- Camera culling means off-screen particles are not rendered (automatic frustum culling)
- Max 1000 particles per system, trivial for GPU

## Interaction with CampsiteLightingOverlay

No changes. Both overlays coexist independently:
- `CampsiteLightingOverlay` provides color tinting (canvas child, Texture2D lightmap)
- `WeatherVFXOverlay` provides particle effects (viewport child, RenderTexture from camera)
- Z-order: weather VFX VisualElement is a child of viewport; lighting overlay is a child of canvas. Viewport children render after canvas content, so weather particles appear on top. This is the same ordering as the current implementation.

## Sprite Assets

Two simple sprite textures in `Assets/Resources/VFX/`:
- `raindrop.png` — soft white filled circle, 32x32px, alpha-feathered edges for smooth anti-aliasing
- `snowflake.png` — soft white filled circle, 32x32px, alpha-feathered edges

Both are plain white circles. The ParticleSystem's color, size, noise, and rotation modules create all visual variation. A single sprite per system keeps draw calls minimal (single material = batched).
