# Flame Level-Up Animation Design

## Overview

Add a grand, triumphant animation sequence when the player levels up their flame (Spark of Ara). The flame is the heart of the camp and leveling it is a pivotal progression moment — currently it just plays a sound effect and rebuilds the grid with no visual celebration.

## Animation Sequence (6 stages, ~2.5s total)

### Stage 1 — Screen Flash (0.0s–0.3s)
A full-screen white overlay fades from 40% opacity to 0. Implemented as a `VisualElement` covering the root, added on upgrade trigger, removed after fade completes. Punctuates the moment of transformation.

### Stage 2 — Flame Pulse & Grow (0.0s–0.6s)
The flame hex cell scales to 120% then eases back to 100%. Border thickens from 3px to 5px with golden color (`#FFB432`). Pure USS transitions on `scale` and `border-color`. Gives the flame a physical "power surge" feel.

### Stage 3 — Shockwave Ring (0.2s–1.2s)
A single golden ring expands outward from the flame center, drawn on a Painter2D overlay element. Starts at flame hex radius (~110px), expands to cover the full grid. Stroke width interpolates from 4px to 1px, opacity from 1.0 to 0.0. Animated via `schedule.Execute().Every(16)` on the overlay element.

### Stage 4 — Hex Cascade (0.4s–1.5s)
All hex cells (occupied and empty) briefly glow golden in a ripple pattern outward from (0,0). Ring distance 1 highlights at 0.4s, ring 2 at 0.6s, ring 3 at 0.8s, etc. Each cell gets a temporary USS class adding a golden `background-color` tint, removed after 400ms. Staggered via `schedule.Execute().StartingIn()`. Note: hex borders are drawn via Painter2D in `DrawHexCell`, so the cascade glow uses `background-color` only (not `border-color`), which is USS-controlled and independent of Painter2D borders.

### Stage 5 — Rising Embers (0.3s–2.5s)
~20 small circles (3–6px radius) spawn near the flame center and float upward with slight horizontal drift. Drawn on the same Painter2D overlay as the shockwave ring. Each ember has randomized: start position (within flame hex bounds), rise speed, horizontal drift, lifetime (1–2s), and size. Color: golden (`#FFB432`) fading to transparent over lifetime.

### Stage 6 — Level Badge (1.0s–2.5s)
A "Level X" label appears at screen center. Gold text on a dark semi-transparent rounded-rect background. Bounce effect implemented procedurally: scheduled callback scales from 0 → 1.15 over 250ms (ease-out), then a second phase scales from 1.15 → 1.0 over 200ms (ease-in-out). Opacity fades in via USS transition. Fades out after 1.5s via USS opacity transition.

## Architecture

### New File: `Assets/Scripts/UI/FlameLevelUpAnimator.cs`

A static utility class that orchestrates the full animation sequence. Entry point:

```csharp
public static void Play(VisualElement root, VisualElement flameCell,
                         VisualElement gridContainer, int newLevel,
                         System.Action onComplete)
```

Parameters:
- `root`: The root VisualElement (for full-screen flash overlay and level badge)
- `flameCell`: The flame hex cell element (for pulse/scale animation). Caller identifies this by the `grid-cell--flame` CSS class already applied in `PopulateOccupiedCell`.
- `gridContainer`: The grid container (for Painter2D overlay and hex cascade)
- `newLevel`: The new flame level (for badge text)
- `onComplete`: Callback when animation finishes (~2.5s), triggers grid rebuild

Internal structure:
- Creates the flash overlay, Painter2D overlay, and level badge elements
- Uses `schedule.Execute()` for timing stages
- Painter2D overlay uses `generateVisualContent` callback; a scheduled updater at ~60fps drives shockwave expansion and ember particle positions
- Flash overlay uses `pickingMode = PickingMode.Position` to block all input during the animation sequence. Other created elements use `PickingMode.Ignore`.
- A static `IsPlaying` flag prevents re-triggering during animation
- Cleans up all created elements in `onComplete`

**Audio:** The existing `"flame_upgrade"` SFX continues to play as-is (fired from `FlameManager.UpgradeFlame()`). No additional audio changes.

### Painter2D Overlay Element

A single `VisualElement` added as a child of `gridContainer`, sized via `StretchToParentSize()` to cover the full grid area. Uses `generateVisualContent` for Painter2D drawing of:
- The expanding shockwave ring (stage 3)
- Rising ember particles (stage 5)

An animation state object tracks current time, shockwave radius, and ember positions. A scheduled callback (`Every(16)`) updates state and calls `MarkDirtyRepaint()`. Shockwave max radius is calculated dynamically from `gridContainer.resolvedStyle.width / 2` so it scales with grid size at any flame level. The overlay is removed when both shockwave and embers have completed.

### USS Additions (in `Interaction.uss`)

```css
/* Stage 1: Screen flash */
.flame-flash-overlay {
    position: absolute;
    left: 0;
    top: 0;
    right: 0;
    bottom: 0;
    background-color: rgba(255, 255, 255, 0.4);
    transition-property: opacity;
    transition-duration: 300ms;
    transition-timing-function: ease-out;
}
.flame-flash-overlay--fade {
    opacity: 0;
}

/* Stage 2: Flame pulse (applied to flame hex cell) */
.grid-cell--levelup-pulse {
    scale: 1.2 1.2;
    transition-property: scale;
    transition-duration: 600ms;
    transition-timing-function: ease-out;
}

/* Stage 4: Hex cascade glow (background-color only — borders are Painter2D) */
.grid-cell--levelup-glow {
    background-color: rgba(255, 180, 50, 0.15);
    transition-property: background-color;
    transition-duration: 200ms;
    transition-timing-function: ease-in;
}

/* Stage 6: Level badge — scale is driven procedurally for bounce effect */
.flame-level-badge {
    position: absolute;
    left: 0;
    top: 0;
    right: 0;
    bottom: 0;
    -unity-text-align: middle-center;
    justify-content: center;
    align-items: center;
    background-color: rgba(0, 0, 0, 0);
    picking-mode: ignore;
}

.flame-level-badge__text {
    background-color: rgba(20, 15, 10, 0.85);
    color: #FFB432;
    font-size: 36px;
    -unity-font-style: bold;
    padding: 12px 32px;
    border-radius: 24px;
    scale: 0 0;
    opacity: 0;
    transition-property: opacity, scale;
    transition-duration: 300ms, 250ms;
    transition-timing-function: ease-out, ease-out;
}
.flame-level-badge__text--visible {
    opacity: 1;
}
.flame-level-badge__text--fade {
    opacity: 0;
    transition-property: opacity;
    transition-duration: 500ms;
    transition-timing-function: ease-out;
}
```

Note: USS uses longhand transition properties to match existing codebase conventions. The level badge bounce is procedural (scheduled scale interpolation), not USS `ease-out-back` which Unity USS does not support. Hex cascade uses `background-color` only since hex borders are drawn via Painter2D. The glow class is simply removed (no `initial` keyword needed) — removing the class reverts `background-color` to the element's base style.

### Integration Point

In `CampsiteViewUI`, the upgrade button click handler currently:
1. Calls `FlameManager.Instance.UpgradeFlame()`
2. Closes the interaction panel
3. Rebuilds the grid immediately

Modified flow:
1. Calls `FlameManager.Instance.UpgradeFlame()`
2. Closes the interaction panel
3. Calls `FlameLevelUpAnimator.Play(root, flameCell, gridContainer, newLevel, onComplete: RebuildGrid)`
4. Grid rebuild happens in the `onComplete` callback after ~2.5s

The `OnFlameUpgraded` event subscription in `CampsiteViewUI` that currently triggers an immediate grid rebuild should be gated — if the animation is playing, defer the rebuild to the animation's completion callback.

## Files Changed

| File | Change |
|------|--------|
| `Assets/Scripts/UI/FlameLevelUpAnimator.cs` | **New** — animation orchestrator |
| `Assets/UI/Styles/Interaction.uss` | Add flame level-up CSS classes |
| `Assets/Scripts/UI/CampsiteViewUI.cs` | Wire animation into upgrade flow, defer grid rebuild |

## Testing

Manual verification in Unity Editor:
- Trigger flame upgrade → observe full 6-stage sequence
- Verify grid rebuilds correctly after animation completes
- Verify animation doesn't break if player taps during it
- Check performance — Painter2D overlay at 60fps with 20 embers should be lightweight
