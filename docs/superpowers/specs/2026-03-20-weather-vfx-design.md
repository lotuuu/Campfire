# Weather VFX Overlay Design

## Overview

Add visual weather effects (rain, snow, thunderstorm lightning) to the campsite view. A new `WeatherVFXOverlay` MonoBehaviour renders weather particles via Painter2D on a viewport-level overlay element that sits above the hex grid. Subscribes to `WeatherService.OnWeatherUpdated` and smoothly transitions between weather states.

## Architecture

### New File
- `Assets/Scripts/UI/WeatherVFXOverlay.cs` — single MonoBehaviour, no new USS

### Integration
- Initialized in `CampsiteViewUI.Initialize()` alongside `CampsiteLightingOverlay`
- Attached to the same GameObject (`--- UI ---`)
- `GetComponent<WeatherVFXOverlay>()` with `AddComponent` fallback (same pattern as lighting overlay)
- Method signature: `public void Initialize(VisualElement viewport)` — takes the viewport directly (unlike the lighting overlay which takes canvas and derives viewport via `.parent`)

### Overlay Element
- Absolute-positioned `VisualElement` child of `#campsite-viewport` (not `#campsite-canvas`)
- `pickingMode = PickingMode.Ignore`
- Uses `generateVisualContent` callback with `Painter2D` for all particle rendering
- `MarkDirtyRepaint()` called each frame from `Update()` when active
- Hidden (`DisplayStyle.None`) when no weather effect is active

### Why Viewport (Not Canvas)
Rain/snow fall from the sky — they should not pan with the grid. Viewport-level means particles fall in fixed screen-space regardless of `CampsitePanController` offset. Simpler implementation, more natural appearance.

## Rain (WeatherCondition.Rain)

### Particles
- **Count**: ~80 active particles
- **Types**: mix of thin angled streaks (~60%) and elongated drop ellipses (~40%)
- **Angle**: slight diagonal (~10 degrees from vertical), varied per particle with small random offset
- **Color**: `rgba(180, 200, 255, alpha)` — cool blue-white

### Depth Layers
Two layers for parallax depth:
- **Foreground** (~40%): alpha 0.4–0.55, length 40–60px (streaks) or rx=2.5/ry=7 (drops), fall speed 800–1000 px/s
- **Background** (~60%): alpha 0.2–0.35, length 25–40px (streaks) or rx=1.5/ry=5 (drops), fall speed 500–700 px/s

### Splash Ripples
- When a particle crosses the bottom ~20% of the viewport, it despawns and spawns a splash
- Splash: small expanding circle, radius 0 → 6px over ~300ms, alpha fading from 0.3 → 0
- Max ~10 active splashes at a time
- Drawn as `painter2D.Arc()` with stroke, no fill

### Spawning
- Continuous from top edge, random horizontal position across viewport width
- Slight spawn offset above visible area (-10 to -30px) so particles don't pop in

## Snow (WeatherCondition.Snow)

### Particles
- **Count**: ~60 active particles
- **Types**: soft filled circles (~85%) and 6-line crystalline flakes (~15%)
- **Color**: `rgba(230, 235, 255, alpha)` — soft white with slight blue tint

### Soft Dots
- Filled circles via `painter2D.Arc()` + `Fill()`
- Radius: 2–4px (foreground), 1–3px (background)
- Fall speed: 40–80 px/s (much slower than rain)

### Crystalline Flakes
- 3 crossed lines through center (6-pointed star shape), drawn with `painter2D.MoveTo/LineTo`
- Radius: 4–7px
- Slowly rotate as they fall: rotation speed 15–30 degrees/sec, random direction
- ~15% of spawned particles are crystalline

### Motion
- Sinusoidal horizontal sway: `x += sin(time * swayFreq + phase) * swayAmplitude`
- Each particle has random `swayFreq` (0.5–1.5), `swayAmplitude` (10–25px), and `phase`
- Creates gentle drifting, non-uniform motion

### Depth Layers
- **Foreground** (~35%): alpha 0.45–0.6, larger, slightly faster
- **Background** (~65%): alpha 0.25–0.4, smaller, slower

### Despawn
- Particles fade out (alpha → 0) over the bottom 10% of viewport, then recycle. No splash.

## Storm (WeatherCondition.Storm)

Storm is rain + lightning. Uses the same rain particle system with modifications:

### Enhanced Rain
- **Count**: ~100 particles (denser than normal rain)
- **Speed**: 20% faster than normal rain
- **Splash rate**: slightly higher (more splashes active)

### Lightning Flash
- Separate `VisualElement` overlaid on the viewport (sibling of the particle overlay)
- Background color: `rgba(200, 210, 255, alpha)`
- **Flash sequence**: opacity pulses 0 → 0.3 → 0 over ~150ms per pulse, 2–3 rapid pulses per strike with ~100ms gap between pulses
- **Timing**: random interval between strikes, 4–10 seconds
- **Clustering**: 30% chance of a second strike within 1 second of the first
- Flash element uses `pickingMode = PickingMode.Ignore`

### Lightning Implementation
- Driven from `Update()` with countdown timers (same pattern as `CampsiteLightingOverlay.nextFlareTime`)
- No USS transitions — animate opacity directly in code for precise multi-pulse control

## State Transitions

### Weather Change Handling
- Subscribe to `WeatherService.Instance.OnWeatherUpdated` in `Initialize()`
- On condition change, set `targetParticleCount` and `targetWeatherType`
- **Fade in**: gradually increase spawn rate over ~2 seconds until target count reached
- **Fade out**: stop spawning, let existing particles fall off-screen naturally (~1-2 seconds for rain, ~3-4 seconds for snow)
- Lightning stops immediately on transition away from Storm (no lingering flashes)

### Condition Mapping
| WeatherCondition | Effect | Particle Count |
|---|---|---|
| Clear | None (overlay hidden) | 0 |
| Cloudy | None (overlay hidden) | 0 |
| Rain | Rain particles + splashes | ~80 |
| Storm | Dense rain + splashes + lightning flashes | ~100 |
| Snow | Snow particles (dots + rare flakes) | ~60 |

### Overlay Visibility
- `DisplayStyle.None` when no effect active and all particles have despawned
- `DisplayStyle.Flex` when any weather effect is active or particles are still fading out

## Particle Data Structure

```csharp
private enum ParticleType { RainStreak, RainDrop, SnowDot, SnowFlake }

private struct WeatherParticle
{
    public Vector2 position;       // viewport-relative pixels
    public float speed;            // px/sec downward
    public float angle;            // radians from vertical (rain only)
    public float alpha;            // 0-1
    public float size;             // length for streaks, radius for dots/flakes
    public float swayPhase;        // snow sway offset
    public float swayFreq;         // snow sway frequency
    public float swayAmplitude;    // snow sway amplitude
    public float rotation;         // current rotation (snowflakes)
    public float rotationSpeed;    // degrees/sec (snowflakes)
    public ParticleType type;
    public bool isForeground;      // depth layer
}
```

Splashes stored separately:
```csharp
private struct Splash
{
    public Vector2 position;
    public float age;              // seconds since spawn
    public float lifetime;         // ~0.3s
}
```

## Performance

- Particle list pre-allocated at max capacity (120 particles + 10 splashes) to cover Storm density, recycled by overwriting despawned entries (no alloc during gameplay)
- `Update()` drives simulation; `MarkDirtyRepaint()` triggers `generateVisualContent` callback
- Skip `Update()` entirely when overlay is hidden and no particles remain
- Painter2D draw calls: ~80-100 for rain, ~60 for snow — well within UI Toolkit budget
- Lightning flash is a simple opacity change on a single element, negligible cost

## Interaction with CampsiteLightingOverlay

Both overlays coexist:
- `CampsiteLightingOverlay` provides the color tint (blue for rain, grey for snow) on the canvas
- `WeatherVFXOverlay` provides the particle effects on the viewport
- They subscribe to the same `OnWeatherUpdated` event independently
- No direct coupling between the two — they react to weather state in parallel
- Z-order: weather particles render above the lighting overlay (viewport child is above canvas children)
