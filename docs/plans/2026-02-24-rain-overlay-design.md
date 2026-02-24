# Rain Overlay — Design

**Goal:** Replace the broken world-space particle rain with a screen-space Painter2D rain drawn inside a UI Toolkit VisualElement.

**Why:** World-space particles in a 2D orthographic scene require exact world-unit tuning per camera zoom. A VisualElement rain is always screen-sized, needs no sizing math, and integrates naturally with the existing UI Toolkit stack.

---

## Architecture

A new `RainOverlay` MonoBehaviour on the existing `WeatherOverlay` GameObject:

- Finds `#rain-overlay` in the UIDocument on `Start()`.
- Registers a `generateVisualContent` callback that draws all active drops using `Painter2D.LineTo`.
- Updates drop positions every `Update()` and calls `MarkDirtyRepaint()` when active.
- Exposes `Show(intensity)` / `Hide()` so `WeatherOverlay` can drive it.

`WeatherOverlay` is simplified — it drops all `ParticleSystem` fields and calls `RainOverlay` instead.

## Visual Design

| Property | Rain | Storm |
|---|---|---|
| Active drops | 80 | 180 |
| Drop width | 1.5 px | 2 px |
| Drop length | 5–9 % screen height | 7–12 % screen height |
| Color | `rgba(200,225,255, 0.15–0.40)` | `rgba(180,210,255, 0.20–0.55)` |
| Fall speed | 0.7–1.1 screen/s | 1.0–1.6 screen/s |
| Wind angle | 0.12 (x offset / length) | 0.22 |

Drops are initialized at random positions across the full screen (not just the top) so the effect is instant on first frame.

## UXML Placement

`#rain-overlay` is added to `GardenRoot.uxml` just **before** `#location-gate` (after the page-content elements, before modal overlays). This puts it above page content but below debug/satchel panels.

```xml
<ui:VisualElement name="rain-overlay" picking-mode="Ignore"
    style="position: absolute; left: 0; right: 0; top: 0; bottom: 0; display: none;" />
```

## Data Model

```csharp
private struct RainDrop {
    public float x, y;      // normalized 0..1 screen coords
    public float speed;      // screen heights per second
    public float alpha;
    public float length;     // fraction of screen height
}
```

When a drop scrolls off the bottom it wraps back to y ≈ -length with a new random x.

## Integration

`WeatherOverlay` keeps its `WeatherService` subscription pattern unchanged. It gains a `[SerializeField] RainOverlay rainOverlay` field and replaces particle calls with `rainOverlay.Show/Hide`.

Old particle system children (`RainEffect`, `SnowEffect`, `WindLines`) are disabled, not deleted, in case they're needed later.
