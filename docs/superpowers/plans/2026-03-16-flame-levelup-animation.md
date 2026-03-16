# Flame Level-Up Animation Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a grand, 6-stage animation sequence (flash → flame pulse → shockwave ring → hex cascade → rising embers → level badge) when the player levels up the Spark of Ara.

**Architecture:** A new static utility class `FlameLevelUpAnimator` orchestrates 6 timed stages using USS transitions and Painter2D procedural drawing. It is called from `CampsiteViewUI` after a successful upgrade, blocking input during the ~2.5s sequence and deferring grid rebuild to a completion callback.

**Tech Stack:** Unity UI Toolkit (VisualElement, USS transitions, Painter2D, `schedule.Execute()`), C#

**Spec:** `docs/superpowers/specs/2026-03-16-flame-levelup-animation-design.md`

---

## Chunk 1: USS Styles + Animator Core

### Task 1: Add USS Classes for Flame Level-Up

**Files:**
- Modify: `Assets/UI/Styles/Interaction.uss` (append after line 838)

- [ ] **Step 1: Add all flame level-up USS classes**

Append to the end of `Interaction.uss`:

```css
/* ── Flame Level-Up Animation ── */

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

.grid-cell--levelup-pulse {
    scale: 1.2 1.2;
    transition-property: scale;
    transition-duration: 600ms;
    transition-timing-function: ease-out;
}

.grid-cell--levelup-glow {
    background-color: rgba(255, 180, 50, 0.15);
    transition-property: background-color;
    transition-duration: 200ms;
    transition-timing-function: ease-in;
}

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

- [ ] **Step 2: Verify USS compiles**

Open Unity Editor, check console for USS parse errors. Run: `read_console` via Unity MCP filtered for errors.

- [ ] **Step 3: Commit**

```bash
git add Assets/UI/Styles/Interaction.uss
git commit -m "style: add USS classes for flame level-up animation"
```

### Task 2: Create FlameLevelUpAnimator — Ember Data + Painter2D Overlay

**Files:**
- Create: `Assets/Scripts/UI/FlameLevelUpAnimator.cs`

- [ ] **Step 1: Create the file with ember struct, animation state, and Painter2D drawing**

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public static class FlameLevelUpAnimator
    {
        public static bool IsPlaying { get; private set; }

        private struct Ember
        {
            public Vector2 position;
            public float speed;      // pixels per second upward
            public float drift;      // pixels per second horizontal
            public float lifetime;   // total seconds
            public float age;        // seconds elapsed
            public float radius;     // circle radius in px
        }

        private class AnimationState
        {
            public float elapsed;
            public float shockwaveRadius;
            public float shockwaveMaxRadius;
            public bool shockwaveDone;
            public List<Ember> embers = new();
            public bool embersDone;
        }

        private static void DrawOverlay(MeshGenerationContext ctx, AnimationState state)
        {
            var el = ctx.visualElement;
            float w = el.resolvedStyle.width;
            float h = el.resolvedStyle.height;
            if (float.IsNaN(w) || float.IsNaN(h) || w <= 0 || h <= 0) return;

            var painter = ctx.painter2D;
            float cx = w / 2f;
            float cy = h / 2f;

            // Shockwave ring
            if (!state.shockwaveDone && state.shockwaveRadius > 0)
            {
                float progress = Mathf.Clamp01(state.shockwaveRadius / state.shockwaveMaxRadius);
                float alpha = 1f - progress;
                float lineWidth = Mathf.Lerp(4f, 1f, progress);

                painter.BeginPath();
                painter.Arc(new Vector2(cx, cy), state.shockwaveRadius, 0f, 360f);
                painter.ClosePath();
                painter.strokeColor = new Color(1f, 0.71f, 0.2f, alpha);
                painter.lineWidth = lineWidth;
                painter.Stroke();
            }

            // Embers
            for (int i = 0; i < state.embers.Count; i++)
            {
                var e = state.embers[i];
                if (e.age >= e.lifetime) continue;
                float alpha = 1f - (e.age / e.lifetime);
                painter.BeginPath();
                painter.Arc(new Vector2(cx + e.position.x, cy + e.position.y),
                    e.radius, 0f, 360f);
                painter.ClosePath();
                painter.fillColor = new Color(1f, 0.71f, 0.2f, alpha);
                painter.Fill();
            }
        }
    }
}
```

- [ ] **Step 2: Verify compilation**

Check Unity console for errors via `read_console`.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/UI/FlameLevelUpAnimator.cs
git commit -m "feat: add FlameLevelUpAnimator with ember data and Painter2D drawing"
```

### Task 3: Add the Play() Method — Full Orchestration

**Files:**
- Modify: `Assets/Scripts/UI/FlameLevelUpAnimator.cs`

- [ ] **Step 1: Add the Play method to FlameLevelUpAnimator**

Add this method inside the `FlameLevelUpAnimator` class, after the `DrawOverlay` method:

```csharp
public static void Play(VisualElement root, VisualElement flameCell,
    VisualElement gridContainer, int newLevel, Action onComplete)
{
    if (IsPlaying) return;
    IsPlaying = true;

    var state = new AnimationState();
    IVisualElementScheduledItem updater = null;
    var createdElements = new List<VisualElement>();

    // ── Stage 1: Screen Flash (0.0s–0.3s) ──
    var flash = new VisualElement();
    flash.AddToClassList("flame-flash-overlay");
    flash.pickingMode = PickingMode.Position; // blocks input
    root.Add(flash);
    createdElements.Add(flash);

    // Trigger fade on next frame so transition runs
    flash.schedule.Execute(() => flash.AddToClassList("flame-flash-overlay--fade"));

    // ── Stage 2: Flame Pulse (0.0s–0.6s) ──
    // Add pulse class (scales to 1.2 via USS transition over 600ms)
    flameCell?.AddToClassList("grid-cell--levelup-pulse");
    // Remove pulse class at 600ms — the element reverts to base scale (1.0).
    // Since the transition is defined on the pulse class itself, the snap-back
    // is instant, but at this point the shockwave and cascade are drawing
    // attention away from the flame cell, so the instant revert is fine.
    flameCell?.schedule.Execute(() =>
    {
        flameCell.RemoveFromClassList("grid-cell--levelup-pulse");
    }).StartingIn(600);

    // ── Stage 3: Shockwave Ring (0.2s) + Stage 5: Embers (0.3s) — via Painter2D overlay ──
    var overlay = new VisualElement();
    overlay.pickingMode = PickingMode.Ignore;
    overlay.StretchToParentSize();
    overlay.generateVisualContent += ctx => DrawOverlay(ctx, state);
    gridContainer.Add(overlay);
    createdElements.Add(overlay);

    float containerWidth = gridContainer.resolvedStyle.width;
    state.shockwaveMaxRadius = float.IsNaN(containerWidth) ? 400f : containerWidth / 2f;

    // Init embers at 0.3s
    gridContainer.schedule.Execute(() =>
    {
        var rng = new System.Random();
        for (int i = 0; i < 20; i++)
        {
            state.embers.Add(new Ember
            {
                position = new Vector2(
                    (float)(rng.NextDouble() * 60 - 30),
                    (float)(rng.NextDouble() * 30 - 15)),
                speed = (float)(rng.NextDouble() * 80 + 60),   // 60–140 px/s
                drift = (float)(rng.NextDouble() * 40 - 20),   // -20 to +20 px/s
                lifetime = (float)(rng.NextDouble() * 1.0 + 1.0), // 1–2s
                age = 0f,
                radius = (float)(rng.NextDouble() * 3 + 3)     // 3–6 px
            });
        }
    }).StartingIn(300);

    // Start update loop at 0.2s (shockwave starts immediately within the loop)
    const float shockwaveDuration = 1.0f;

    gridContainer.schedule.Execute(() =>
    {
        updater = overlay.schedule.Execute(() =>
        {
            float dt = 0.016f; // ~60fps
            state.elapsed += dt;

            // Shockwave — starts immediately (the 0.2s delay is the StartingIn below)
            if (!state.shockwaveDone)
            {
                state.shockwaveRadius = (state.elapsed / shockwaveDuration)
                    * state.shockwaveMaxRadius;
                if (state.elapsed >= shockwaveDuration)
                    state.shockwaveDone = true;
            }

            // Embers
            bool anyAlive = false;
            for (int i = 0; i < state.embers.Count; i++)
            {
                var e = state.embers[i];
                if (e.age >= e.lifetime) continue;
                e.age += dt;
                e.position.y -= e.speed * dt; // rise upward (negative Y)
                e.position.x += e.drift * dt;
                state.embers[i] = e;
                if (e.age < e.lifetime) anyAlive = true;
            }
            if (state.embers.Count > 0 && !anyAlive)
                state.embersDone = true;

            overlay.MarkDirtyRepaint();
        }).Every(16);
    }).StartingIn(200);

    // ── Stage 4: Hex Cascade (0.4s–1.5s) ──
    if (gridContainer.childCount > 0)
    {
        // Collect cells by ring distance from center
        var cellsByRing = new Dictionary<int, List<VisualElement>>();
        foreach (var child in gridContainer.Children())
        {
            if (!child.ClassListContains("grid-cell")) continue;
            // Compute ring distance using element position relative to container center
            float childCX = child.resolvedStyle.left + child.resolvedStyle.width / 2f;
            float childCY = child.resolvedStyle.top + child.resolvedStyle.height / 2f;
            float contCX = gridContainer.resolvedStyle.width / 2f;
            float contCY = gridContainer.resolvedStyle.height / 2f;
            float dist = Vector2.Distance(
                new Vector2(childCX, childCY),
                new Vector2(contCX, contCY));
            // Approximate ring number: distance / hex spacing (~190px per ring at HexSize=220)
            int ring = Mathf.RoundToInt(dist / 190f);
            if (!cellsByRing.ContainsKey(ring))
                cellsByRing[ring] = new List<VisualElement>();
            cellsByRing[ring].Add(child);
        }

        foreach (var kvp in cellsByRing)
        {
            int ring = kvp.Key;
            long delayMs = 400 + ring * 200; // ring 0 at 0.4s, ring 1 at 0.6s, etc.
            foreach (var cell in kvp.Value)
            {
                var c = cell; // capture
                gridContainer.schedule.Execute(() =>
                {
                    c.AddToClassList("grid-cell--levelup-glow");
                    // Remove glow after 400ms
                    c.schedule.Execute(() =>
                    {
                        c.RemoveFromClassList("grid-cell--levelup-glow");
                    }).StartingIn(400);
                }).StartingIn(delayMs);
            }
        }
    }

    // ── Stage 6: Level Badge (1.0s–2.5s) ──
    var badge = new VisualElement();
    badge.AddToClassList("flame-level-badge");
    var badgeText = new Label($"Level {newLevel}");
    badgeText.AddToClassList("flame-level-badge__text");
    badge.Add(badgeText);
    root.Add(badge);
    createdElements.Add(badge);

    // Bounce in at 1.0s: scale 0 → 1.15 (250ms via USS transition), then 1.15 → 1.0 (200ms)
    root.schedule.Execute(() =>
    {
        badgeText.AddToClassList("flame-level-badge__text--visible");
        // Phase 1: USS transition animates scale from 0 → 1.15 over 250ms
        badgeText.style.scale = new Scale(new Vector2(1.15f, 1.15f));
        // Phase 2: settle to 1.0 after 250ms (USS transition animates 1.15 → 1.0)
        badgeText.schedule.Execute(() =>
        {
            badgeText.style.scale = new Scale(new Vector2(1f, 1f));
        }).StartingIn(250);
        // Fade out after 1.5s from badge appear
        badgeText.schedule.Execute(() =>
        {
            badgeText.RemoveFromClassList("flame-level-badge__text--visible");
            badgeText.AddToClassList("flame-level-badge__text--fade");
        }).StartingIn(1500);
    }).StartingIn(1000);

    // ── Cleanup at ~2.5s ──
    root.schedule.Execute(() =>
    {
        updater?.Pause();
        flameCell?.RemoveFromClassList("grid-cell--levelup-pulse");
        foreach (var el in createdElements)
            el.RemoveFromHierarchy();
        IsPlaying = false;
        onComplete?.Invoke();
    }).StartingIn(2500);
}
```

- [ ] **Step 2: Verify compilation**

Check Unity console for errors via `read_console`.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/UI/FlameLevelUpAnimator.cs
git commit -m "feat: add Play() orchestration for flame level-up animation"
```

---

## Chunk 2: Integration + Manual Testing

### Task 4: Wire Animation into CampsiteViewUI

**Files:**
- Modify: `Assets/Scripts/UI/CampsiteViewUI.cs` (lines 135, 1493–1496)

- [ ] **Step 1: Gate the OnFlameUpgraded grid rebuild**

In `CampsiteViewUI.cs`, find the event subscription at line 135:

```csharp
FlameManager.Instance.OnFlameUpgraded += RebuildGrid;
```

Change it to defer the rebuild when the animation is playing:

```csharp
FlameManager.Instance.OnFlameUpgraded += () =>
{
    if (!FlameLevelUpAnimator.IsPlaying) RebuildGrid();
};
```

- [ ] **Step 2: Wire animation into upgrade button handler**

In `CampsiteViewUI.cs`, find the upgrade button lambda at lines 1493–1496:

```csharp
var upgradeBtn = new Button(() =>
{
    FlameManager.Instance.UpgradeFlame();
    CloseInteractionPanel();
})
```

Replace with:

```csharp
var upgradeBtn = new Button(() =>
{
    if (FlameLevelUpAnimator.IsPlaying) return;
    var flameCellEl = canvas?.Q(className: "grid-cell--flame");
    FlameManager.Instance.UpgradeFlame();
    CloseInteractionPanel();
    FlameLevelUpAnimator.Play(campRoot, flameCellEl, canvas, FlameManager.Instance.Level, RebuildGrid);
})
```

Note: `campRoot` is the root VisualElement field (set in `Initialize()` at line 100). `canvas` is the grid container (line 102). `FlameManager.Instance.Level` is read after `UpgradeFlame()` so it returns the new level.

- [ ] **Step 3: Verify compilation**

Check Unity console for errors via `read_console`.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/UI/CampsiteViewUI.cs
git commit -m "feat: wire flame level-up animation into upgrade flow"
```

### Task 5: Manual Testing in Unity Editor

- [ ] **Step 1: Enter Play Mode**

Use `manage_editor` tool to enter Play mode, or press Play in Unity Editor.

- [ ] **Step 2: Trigger a flame level-up**

Either use an existing save with enough resources, or temporarily set `CurrencyManager.FreeMode = true` in code to bypass cost checks. Tap the flame and hit "Level Up".

- [ ] **Step 3: Observe and verify all 6 stages**

Watch for:
1. White flash fading out (~0.3s)
2. Flame hex scales up then settles back (~0.6s)
3. Golden ring expanding outward from center (~1s)
4. Hex cells glowing golden in ring pattern (~1s)
5. Small golden dots rising upward (~2s)
6. "Level X" badge bouncing in then fading (~1.5s)

After ~2.5s, the grid should rebuild showing the new level.

- [ ] **Step 4: Test input blocking**

During the animation, try tapping hex cells. Nothing should respond (flash overlay blocks input).

- [ ] **Step 5: Test double-trigger**

Try triggering two upgrades in quick succession. The `IsPlaying` guard should prevent the second animation from starting while the first is running.

- [ ] **Step 6: Check console for errors**

Use `read_console` to verify no errors or warnings during the animation.

- [ ] **Step 7: Final commit if any fixes were needed**

```bash
git add -u
git commit -m "fix: address flame level-up animation issues from manual testing"
```
