# Visual Polish Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add interaction juice, UI panel polish, ambient life, smooth transitions, and reward celebrations to make Camp Fire feel alive and satisfying.

**Architecture:** A shared `VFXToolkit.cs` static utility provides reusable animation primitives (easing, tweens, glow pulses, ripple cascades, sparkles). All five visual areas build on these primitives. Effects are cosmetic overlays — game state updates immediately, animations are non-blocking.

**Tech Stack:** Unity UI Toolkit (VisualElement.schedule for tweens, CSS transitions for panels, Painter2D for particles), C# with existing singleton pattern.

**Spec:** `docs/superpowers/specs/2026-03-26-visual-polish-design.md`

---

## File Structure

### New Files
- `Assets/Scripts/Utils/VFXToolkit.cs` — static utility with easing functions, tween scheduler, glow pulse, ripple cascade, number tween, scale bounce, sparkles, vignette, micro-shake

### Modified Files
- `Assets/Scripts/UI/CampsiteViewUI.cs` — interaction juice hooks (plant/water/harvest/craft effects, mana pulse, plant shimmer, bird burst, growth crossfade)
- `Assets/Scripts/UI/ResourceDisplayUI.cs` — number tween integration for water/gems
- `Assets/Scripts/UI/CampFireUI.cs` — panel open/close rewrite with CSS class toggling + TransitionEndEvent
- `Assets/Scripts/UI/BottomNavUI.cs` — PulseButton method for notification pulses
- `Assets/Scripts/UI/ApothekeUI.cs` — mix animation (ingredient shrink, result bounce)
- `Assets/Scripts/UI/DialogueUI.cs` — entrance/exit animation via class toggle
- `Assets/Scripts/UI/TutorialUI.cs` — smooth pulse via CSS transitions
- `Assets/Scripts/UI/WeatherBarUI.cs` — crossfade on weather update
- `Assets/Scripts/UI/RewardRevealUI.cs` — tier-specific celebrations per card
- `Assets/Scripts/UI/FlameLevelUpAnimator.cs` — post-celebration nav button pulses
- `Assets/Scripts/UI/CampsiteLightingOverlay.cs` — magical motes + smoke wisps
- `Assets/Scripts/Managers/FlameManager.cs` — OnManaTick event with 1s throttle
- `Assets/Resources/UI/Templates/GridCell.uxml` — add second sprite layer for crossfade
- `Assets/UI/Styles/Overlay.uss` — `.panel--open`, `.scrim--visible` transition classes
- `Assets/UI/Styles/Common.uss` — `.item--visible` stagger, `:active` press states
- `Assets/UI/Styles/Dialogue.uss` — entrance/exit transitions
- `Assets/UI/Styles/Tutorial.uss` — smooth pulse transitions
- `Assets/UI/Styles/CampsiteGrid.uss` — two-layer sprite cell styles
- `Assets/UI/Styles/RewardReveal.uss` — tier-specific glow styles
- `Assets/Tests/EditMode/VFXToolkitTests.cs` — unit tests for easing functions

---

## Task 1: VFXToolkit — Easing Functions

**Files:**
- Create: `Assets/Scripts/Utils/VFXToolkit.cs`
- Create: `Assets/Tests/EditMode/VFXToolkitTests.cs`

- [ ] **Step 1: Write failing tests for easing functions**

```csharp
// Assets/Tests/EditMode/VFXToolkitTests.cs
using NUnit.Framework;
using Garden;

[TestFixture]
public class VFXToolkitTests
{
    [Test]
    public void EaseOutQuad_AtBoundaries()
    {
        Assert.AreEqual(0f, VFXToolkit.EaseOutQuad(0f), 0.001f);
        Assert.AreEqual(1f, VFXToolkit.EaseOutQuad(1f), 0.001f);
    }

    [Test]
    public void EaseOutQuad_AtMidpoint_IsAboveLinear()
    {
        float result = VFXToolkit.EaseOutQuad(0.5f);
        Assert.Greater(result, 0.5f, "Ease-out should be above linear at midpoint");
        Assert.AreEqual(0.75f, result, 0.001f);
    }

    [Test]
    public void EaseOutBack_AtBoundaries()
    {
        Assert.AreEqual(0f, VFXToolkit.EaseOutBack(0f), 0.001f);
        Assert.AreEqual(1f, VFXToolkit.EaseOutBack(1f), 0.001f);
    }

    [Test]
    public void EaseOutBack_Overshoots()
    {
        // EaseOutBack should exceed 1.0 at some point
        float max = 0f;
        for (float t = 0f; t <= 1f; t += 0.01f)
            max = UnityEngine.Mathf.Max(max, VFXToolkit.EaseOutBack(t));
        Assert.Greater(max, 1f, "EaseOutBack should overshoot past 1.0");
    }

    [Test]
    public void EaseOutElastic_AtBoundaries()
    {
        Assert.AreEqual(0f, VFXToolkit.EaseOutElastic(0f), 0.001f);
        Assert.AreEqual(1f, VFXToolkit.EaseOutElastic(1f), 0.001f);
    }

    [Test]
    public void Smoothstep_AtBoundaries()
    {
        Assert.AreEqual(0f, VFXToolkit.Smoothstep(0f), 0.001f);
        Assert.AreEqual(1f, VFXToolkit.Smoothstep(1f), 0.001f);
    }

    [Test]
    public void Smoothstep_AtMidpoint()
    {
        Assert.AreEqual(0.5f, VFXToolkit.Smoothstep(0.5f), 0.001f);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run via Unity Test Runner (EditMode) or `run_tests` MCP tool with `mode: "EditMode"`.
Expected: FAIL — `VFXToolkit` class does not exist.

- [ ] **Step 3: Implement easing functions**

```csharp
// Assets/Scripts/Utils/VFXToolkit.cs
using UnityEngine;
using UnityEngine.UIElements;
using System;

namespace Garden
{
    public static class VFXToolkit
    {
        // --- Easing Functions ---

        public static float EaseOutQuad(float t)
        {
            return 1f - (1f - t) * (1f - t);
        }

        public static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        public static float EaseOutElastic(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * (2f * Mathf.PI / 3f)) + 1f;
        }

        public static float Smoothstep(float t)
        {
            return t * t * (3f - 2f * t);
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run via Unity Test Runner (EditMode).
Expected: All 6 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Utils/VFXToolkit.cs Assets/Tests/EditMode/VFXToolkitTests.cs
git commit -m "feat: add VFXToolkit with easing functions and tests"
```

---

## Task 2: VFXToolkit — Tween Scheduler

**Files:**
- Modify: `Assets/Scripts/Utils/VFXToolkit.cs`

- [ ] **Step 1: Add Tween method to VFXToolkit**

Add inside the `VFXToolkit` class, after the easing functions:

```csharp
        // --- Tween Scheduler ---

        public static IVisualElementScheduledItem Tween(
            VisualElement element,
            float durationMs,
            Action<float> onUpdate,
            Func<float, float> easing = null,
            float delayMs = 0f)
        {
            easing ??= EaseOutQuad;
            float elapsed = -delayMs;
            var item = element.schedule.Execute(() =>
            {
                elapsed += 16f;
                if (elapsed < 0f) return;
                float t = Mathf.Clamp01(elapsed / durationMs);
                float eased = easing(t);
                onUpdate(eased);
            }).Every(16).Until(() => elapsed >= durationMs);
            return item;
        }
```

- [ ] **Step 2: Verify in Unity Editor**

Open Unity, confirm no compilation errors. VFXToolkit should appear in code completion.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Utils/VFXToolkit.cs
git commit -m "feat: add VFXToolkit.Tween scheduler"
```

---

## Task 3: VFXToolkit — Scale Bounce, Number Tween, Glow Pulse

**Files:**
- Modify: `Assets/Scripts/Utils/VFXToolkit.cs`

- [ ] **Step 1: Add ScaleBounce method**

Add inside the `VFXToolkit` class:

```csharp
        // --- Scale Bounce ---

        public static IVisualElementScheduledItem ScaleBounce(
            VisualElement element,
            float peakScale = 1.12f,
            float durationMs = 400f,
            Func<float, float> easing = null)
        {
            easing ??= EaseOutElastic;
            return Tween(element, durationMs, t =>
            {
                // Up phase (first 30%), then settle (remaining 70%)
                float scale;
                if (t < 0.3f)
                {
                    float upT = t / 0.3f;
                    scale = Mathf.Lerp(1f, peakScale, EaseOutQuad(upT));
                }
                else
                {
                    float downT = (t - 0.3f) / 0.7f;
                    scale = Mathf.Lerp(peakScale, 1f, easing(downT));
                }
                element.style.scale = new Scale(new Vector2(scale, scale));
            });
        }
```

- [ ] **Step 2: Add TweenNumber method**

```csharp
        // --- Number Tween ---

        public static IVisualElementScheduledItem TweenNumber(
            Label label,
            float fromValue,
            float toValue,
            float durationMs = 300f,
            string format = "F0")
        {
            return Tween(label, durationMs, t =>
            {
                float current = Mathf.Lerp(fromValue, toValue, t);
                label.text = current.ToString(format);
            });
        }
```

- [ ] **Step 3: Add GlowPulse method**

This writes to `CampsiteViewUI.GlowColor` (the existing static dictionary) and schedules a fade-in → hold → fade-out lifecycle:

```csharp
        // --- Glow Pulse ---

        public static void GlowPulse(
            VisualElement cell,
            Color rgb,
            float peakAlpha = 0.25f,
            float fadeInMs = 100f,
            float holdMs = 150f,
            float fadeOutMs = 300f)
        {
            float totalMs = fadeInMs + holdMs + fadeOutMs;
            float elapsed = 0f;
            cell.schedule.Execute(() =>
            {
                elapsed += 16f;
                float alpha;
                if (elapsed < fadeInMs)
                {
                    alpha = peakAlpha * (elapsed / fadeInMs);
                }
                else if (elapsed < fadeInMs + holdMs)
                {
                    alpha = peakAlpha;
                }
                else
                {
                    float fadeT = (elapsed - fadeInMs - holdMs) / fadeOutMs;
                    alpha = peakAlpha * (1f - Mathf.Clamp01(fadeT));
                }
                CampsiteViewUI.GlowColor[cell] = new Color(rgb.r, rgb.g, rgb.b, alpha);
                cell.MarkDirtyRepaint();
            }).Every(16).Until(() =>
            {
                if (elapsed >= totalMs)
                {
                    CampsiteViewUI.GlowColor.Remove(cell);
                    cell.MarkDirtyRepaint();
                    return true;
                }
                return false;
            });
        }
```

- [ ] **Step 4: Verify in Unity Editor**

Open Unity, confirm no compilation errors. All three methods should compile cleanly.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Utils/VFXToolkit.cs
git commit -m "feat: add ScaleBounce, TweenNumber, GlowPulse to VFXToolkit"
```

---

## Task 4: VFXToolkit — Ripple Cascade

**Files:**
- Modify: `Assets/Scripts/Utils/VFXToolkit.cs`

- [ ] **Step 1: Add RippleCascade method**

This needs to look up hex neighbors by ring distance. CampsiteViewUI already groups cells by grid position. The method accepts a lookup function so it stays decoupled:

```csharp
        // --- Ripple Cascade ---

        public static void RippleCascade(
            VisualElement centerCell,
            Color rgb,
            float ringDelayMs,
            int maxRings,
            float peakAlpha,
            Func<VisualElement, int, List<VisualElement>> getCellsByRing)
        {
            for (int ring = 1; ring <= maxRings; ring++)
            {
                var cells = getCellsByRing(centerCell, ring);
                if (cells == null) continue;
                float delay = ring * ringDelayMs;
                float ringAlpha = peakAlpha * (1f - (float)ring / (maxRings + 1));
                foreach (var cell in cells)
                {
                    var capturedCell = cell;
                    var capturedAlpha = ringAlpha;
                    centerCell.schedule.Execute(() =>
                    {
                        GlowPulse(capturedCell, rgb, capturedAlpha, 80f, 60f, 200f);
                    }).ExecuteLater((long)delay);
                }
            }
        }
```

- [ ] **Step 2: Verify compilation**

Open Unity, confirm no errors.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Utils/VFXToolkit.cs
git commit -m "feat: add RippleCascade to VFXToolkit"
```

---

## Task 5: VFXToolkit — Sparkles, Vignette, Micro-Shake

**Files:**
- Modify: `Assets/Scripts/Utils/VFXToolkit.cs`

- [ ] **Step 1: Add SpawnSparkles method**

Uses Painter2D to draw sparkle particles on an overlay element:

```csharp
        // --- Sparkle Particles ---

        private struct Sparkle
        {
            public Vector2 position;
            public Vector2 velocity;
            public float age;
            public float lifetime;
            public float radius;
            public Color color;
        }

        public static void SpawnSparkles(
            VisualElement overlay,
            Vector2 center,
            int count,
            Color color,
            float spread = 100f,
            float lifetimeMs = 400f)
        {
            var sparkles = new Sparkle[count];
            var rng = new System.Random();
            for (int i = 0; i < count; i++)
            {
                float angle = (float)(rng.NextDouble() * Mathf.PI * 2f);
                float speed = 80f + (float)rng.NextDouble() * 70f;
                sparkles[i] = new Sparkle
                {
                    position = center,
                    velocity = new Vector2(Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed),
                    age = 0f,
                    lifetime = lifetimeMs * (0.7f + (float)rng.NextDouble() * 0.6f),
                    radius = 3f + (float)rng.NextDouble() * 3f,
                    color = color
                };
            }

            bool done = false;
            Action<MeshGenerationContext> painter = ctx =>
            {
                var p = ctx.painter2D;
                foreach (ref var s in sparkles.AsSpan())
                {
                    float t = s.age / s.lifetime;
                    if (t >= 1f) continue;
                    float alpha = t < 0.1f ? t / 0.1f : 1f - EaseOutQuad((t - 0.1f) / 0.9f);
                    p.fillColor = new Color(s.color.r, s.color.g, s.color.b, s.color.a * alpha);
                    p.BeginPath();
                    p.Arc(s.position, s.radius * (1f - t * 0.5f), 0f, 360f);
                    p.Fill();
                }
            };

            overlay.generateVisualContent += painter;
            overlay.schedule.Execute(() =>
            {
                for (int i = 0; i < sparkles.Length; i++)
                {
                    var s = sparkles[i];
                    s.age += 16f;
                    s.position += s.velocity * 0.016f;
                    s.velocity.y += 40f * 0.016f; // slight gravity
                    sparkles[i] = s;
                }
                overlay.MarkDirtyRepaint();
            }).Every(16).Until(() =>
            {
                bool allDead = true;
                foreach (var s in sparkles)
                    if (s.age < s.lifetime) { allDead = false; break; }
                if (allDead)
                {
                    overlay.generateVisualContent -= painter;
                    overlay.MarkDirtyRepaint();
                    done = true;
                }
                return done;
            });
        }
```

- [ ] **Step 2: Add ScreenVignettePulse method**

```csharp
        // --- Screen Vignette Pulse ---

        public static void ScreenVignettePulse(
            VisualElement fullscreenOverlay,
            Color color,
            float peakAlpha = 0.1f,
            float durationMs = 300f)
        {
            Tween(fullscreenOverlay, durationMs, t =>
            {
                // Triangle wave: 0 → peak at 0.3 → 0
                float alpha;
                if (t < 0.3f)
                    alpha = peakAlpha * (t / 0.3f);
                else
                    alpha = peakAlpha * (1f - (t - 0.3f) / 0.7f);
                fullscreenOverlay.style.borderTopColor = new Color(color.r, color.g, color.b, alpha);
                fullscreenOverlay.style.borderBottomColor = new Color(color.r, color.g, color.b, alpha);
                fullscreenOverlay.style.borderLeftColor = new Color(color.r, color.g, color.b, alpha);
                fullscreenOverlay.style.borderRightColor = new Color(color.r, color.g, color.b, alpha);
                fullscreenOverlay.style.borderTopWidth = 60f * alpha / peakAlpha;
                fullscreenOverlay.style.borderBottomWidth = 60f * alpha / peakAlpha;
                fullscreenOverlay.style.borderLeftWidth = 60f * alpha / peakAlpha;
                fullscreenOverlay.style.borderRightWidth = 60f * alpha / peakAlpha;
            });
        }
```

- [ ] **Step 3: Add ViewportMicroShake method**

```csharp
        // --- Viewport Micro-Shake ---

        public static void ViewportMicroShake(
            VisualElement viewport,
            float amplitude = 3f,
            float durationMs = 300f,
            float decay = 0.92f)
        {
            float shakeAmount = amplitude;
            int seed = 42;
            Tween(viewport, durationMs, t =>
            {
                shakeAmount *= decay;
                if (shakeAmount < 0.5f)
                {
                    viewport.style.translate = new Translate(0f, 0f);
                    return;
                }
                float x = (Mathf.PerlinNoise(seed + t * 30f, 0f) - 0.5f) * 2f * shakeAmount;
                float y = (Mathf.PerlinNoise(0f, seed + t * 30f) - 0.5f) * 2f * shakeAmount;
                viewport.style.translate = new Translate(x, y);
            }, t => t); // linear — decay handles the easing
        }
```

- [ ] **Step 4: Verify compilation**

Open Unity, confirm no errors.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Utils/VFXToolkit.cs
git commit -m "feat: add SpawnSparkles, ScreenVignettePulse, ViewportMicroShake to VFXToolkit"
```

---

## Task 6: Interaction Juice — Plant, Water, Harvest Effects

**Files:**
- Modify: `Assets/Scripts/UI/CampsiteViewUI.cs`

This task adds glow/ripple/bounce effects when the player plants, waters, or harvests. CampsiteViewUI already subscribes to `PlotManager.Instance.OnPlotChanged` via `OnPlotChangedRebuild` (line 189). We need to detect *what kind* of change happened and play the appropriate effect.

- [ ] **Step 1: Add a helper to get neighboring cells by ring distance**

Add this method to CampsiteViewUI. It looks up cells by hex distance from a center cell using the existing `cellLookup` dictionary (which maps `(gridX, gridY)` to VisualElement). Find the `cellLookup` dictionary or equivalent grid-position-to-cell mapping in CampsiteViewUI and use it.

```csharp
        // Add near PlayGlowPulse method
        internal List<VisualElement> GetCellsByRing(VisualElement centerCell, int ring)
        {
            // Find center cell's grid position
            (int cq, int cr) = (0, 0);
            bool found = false;
            foreach (var kvp in cellLookup)
            {
                if (kvp.Value == centerCell) { (cq, cr) = kvp.Key; found = true; break; }
            }
            if (!found) return null;

            var result = new List<VisualElement>();
            // Axial hex neighbors at distance == ring
            for (int q = -ring; q <= ring; q++)
            {
                for (int r = Mathf.Max(-ring, -q - ring); r <= Mathf.Min(ring, -q + ring); r++)
                {
                    int dist = (Mathf.Abs(q) + Mathf.Abs(r) + Mathf.Abs(-q - r)) / 2;
                    if (dist != ring) continue;
                    if (cellLookup.TryGetValue((cq + q, cr + r), out var cell))
                        result.Add(cell);
                }
            }
            return result;
        }
```

Note: If `cellLookup` doesn't exist by that name, find the equivalent dictionary that maps grid coordinates to VisualElements and use it. Check the grid rebuilding loop around lines 445-583 for how cells are tracked.

- [ ] **Step 2: Add interaction effect methods**

Add these methods to CampsiteViewUI:

```csharp
        private void PlayPlantEffect(VisualElement cell)
        {
            VFXToolkit.ScaleBounce(cell, 1.12f, 400f);
            VFXToolkit.GlowPulse(cell, new Color(0.3f, 0.8f, 0.2f), 0.25f, 100f, 100f, 250f);
            VFXToolkit.RippleCascade(cell, new Color(0.3f, 0.8f, 0.2f), 100f, 1, 0.12f, GetCellsByRing);
        }

        private void PlayWaterEffect(VisualElement cell)
        {
            // Existing blue glow pulse is kept (already at line ~892)
            VFXToolkit.RippleCascade(cell, new Color(0.4f, 0.65f, 1f), 80f, 2, 0.15f, GetCellsByRing);
            // Subtle squash
            VFXToolkit.Tween(cell, 200f, t =>
            {
                float scale = Mathf.Lerp(0.95f, 1f, VFXToolkit.EaseOutQuad(t));
                cell.style.scale = new Scale(new Vector2(scale, scale));
            });
        }

        private void PlayHarvestEffect(VisualElement cell, float quality)
        {
            VFXToolkit.ScaleBounce(cell, 1.15f, 400f);
            var gold = new Color(1f, 0.72f, 0.2f);
            int rings;
            float alpha;
            if (quality >= 1f) { rings = 3; alpha = 0.30f; }
            else if (quality > 0.7f) { rings = 3; alpha = 0.25f; }
            else if (quality > 0.3f) { rings = 2; alpha = 0.18f; }
            else { rings = 1; alpha = 0.12f; }
            VFXToolkit.RippleCascade(cell, gold, 80f, rings, alpha, GetCellsByRing);
            if (quality >= 1f)
                VFXToolkit.GlowPulse(cell, gold, 0.35f, 100f, 200f, 400f);
        }
```

- [ ] **Step 3: Hook effects into existing event handlers**

Find where `OnPlotChangedRebuild` is called and the plot interaction handlers (`OnCellTapped` for plot actions). The effects need to be triggered *before* the grid rebuild clears the cell references. The best approach: store the cell reference and grid coords before the action, then play the effect after the action completes.

Specifically, in the tap handler flow:
- After `PlotManager.Instance.Plant()` succeeds, call `PlayPlantEffect(cell)` on the rebuilt cell
- After `PlotManager.Instance.Water()` succeeds, call `PlayWaterEffect(cell)`
- After `PlotManager.Instance.Harvest()` succeeds, call `PlayHarvestEffect(cell, result.recipeScore)`

The exact integration depends on how the tap handler chains actions — look at `OnCellTapped` (line ~878) and the interaction panel flow. The key constraint: effects must play on the *new* cell after grid rebuild, so either delay the effect or look up the cell by grid coords after rebuild.

- [ ] **Step 4: Add craft flash enhancement**

In the existing `PlayCraftAnimation` method (line ~624), after the existing scale bounce and neighbor ripple, add a brief warm flash:

```csharp
            // Add after existing craft animation code:
            VFXToolkit.Tween(cell, 300f, t =>
            {
                float alpha = 0.4f * (1f - VFXToolkit.EaseOutQuad(t));
                cell.style.unityBackgroundImageTintColor = Color.Lerp(Color.white, new Color(1f, 1f, 1f, alpha), 1f - t);
            });
```

- [ ] **Step 5: Verify in Unity Editor**

Play the game, plant a seed, water it, harvest. Verify:
- Planting shows green glow + 1-ring ripple + scale bounce
- Watering shows blue ripple (2 rings) + squash settle
- Harvesting shows gold ripple (ring count varies by quality) + scale bounce
- Crafting shows warm flash on top of existing animation

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/UI/CampsiteViewUI.cs
git commit -m "feat: add interaction juice for plant, water, harvest, craft actions"
```

---

## Task 7: Interaction Juice — Mana Collection & Resource Counters

**Files:**
- Modify: `Assets/Scripts/Managers/FlameManager.cs`
- Modify: `Assets/Scripts/UI/CampsiteViewUI.cs`
- Modify: `Assets/Scripts/UI/ResourceDisplayUI.cs`

- [ ] **Step 1: Add OnManaTick event to FlameManager**

In `FlameManager.cs`, add a throttled event that fires at most once per second:

```csharp
        // Add to class fields (near line 30, alongside OnFlameUpgraded)
        public event Action OnManaTick;
        private float _manaTickTimer;

        // Add to Update() method, after mana accumulation (after line 52):
        _manaTickTimer += Time.deltaTime;
        if (_manaTickTimer >= 1f)
        {
            _manaTickTimer = 0f;
            OnManaTick?.Invoke();
        }
```

- [ ] **Step 2: Add mana pulse to CampsiteViewUI**

Subscribe to OnManaTick and pulse the flame cell. Find where PlotManager events are subscribed (line ~189) and add:

```csharp
        // In Initialize or wherever events are subscribed:
        FlameManager.Instance.OnManaTick += OnManaTick;

        // Add method:
        private void OnManaTick()
        {
            // Find the flame cell — it's the cell at grid position (0,0)
            if (cellLookup != null && cellLookup.TryGetValue((0, 0), out var flameCell))
            {
                float level = SaveManager.Instance?.Data?.flameLevel ?? 1;
                float alpha = Mathf.Min(0.12f + level * 0.005f, 0.20f);
                VFXToolkit.GlowPulse(flameCell, new Color(1f, 0.55f, 0.1f), alpha, 80f, 60f, 200f);
            }
        }

        // Don't forget to unsubscribe in cleanup/OnDestroy:
        // FlameManager.Instance.OnManaTick -= OnManaTick;
```

- [ ] **Step 3: Add number tween to ResourceDisplayUI**

In `ResourceDisplayUI.cs`, replace snap updates for water and gems with NumberTween. Modify the `OnCurrencyChanged` callback (line ~41):

```csharp
        private void OnCurrencyChanged(CurrencyType type, float oldVal, float newVal)
        {
            if (type == CurrencyType.Water && waterDisplay != null)
            {
                VFXToolkit.TweenNumber(waterDisplay, oldVal, newVal,
                    Mathf.Abs(newVal - oldVal) > oldVal * 0.2f ? 600f : 300f, "F0");
                // Brief blue color flash
                VFXToolkit.Tween(waterDisplay, 400f, t =>
                {
                    float a = 0.15f * (1f - t);
                    waterDisplay.style.backgroundColor = new Color(0.4f, 0.65f, 1f, a);
                });
            }
            else if (type == CurrencyType.Gems && gemsDisplay != null)
            {
                VFXToolkit.TweenNumber(gemsDisplay, oldVal, newVal,
                    Mathf.Abs(newVal - oldVal) > oldVal * 0.2f ? 600f : 300f, "F0");
                VFXToolkit.Tween(gemsDisplay, 400f, t =>
                {
                    float a = 0.15f * (1f - t);
                    gemsDisplay.style.backgroundColor = new Color(0.6f, 0.3f, 0.8f, a);
                });
            }
            else
            {
                UpdateDisplay(); // fallback for other types
            }
        }
```

Note: Check if there's a `gemsDisplay` label reference. If gems are displayed via a different label name or mechanism, adapt accordingly. Also check if `OnCurrencyChanged` provides `oldVal` — if not, cache the previous value before the update.

- [ ] **Step 4: Verify in Unity Editor**

- Watch flame cell: should pulse amber every ~1 second
- Spend water: counter should tween down with blue flash
- Collect gems: counter should tween with purple flash

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Managers/FlameManager.cs Assets/Scripts/UI/CampsiteViewUI.cs Assets/Scripts/UI/ResourceDisplayUI.cs
git commit -m "feat: add mana heartbeat pulse and resource counter animations"
```

---

## Task 8: Interaction Juice — Apotheke Mix Animation

**Files:**
- Modify: `Assets/Scripts/UI/ApothekeUI.cs`

- [ ] **Step 1: Add mix animation**

Find the mix success handler in ApothekeUI.cs (where the mix result is displayed after a successful `ApothekeManager.Instance.Mix()` call). Add animation for consumed ingredients shrinking out and result item bouncing in.

```csharp
        // After successful mix, before refreshing the UI:
        // 1. Animate consumed ingredient elements shrinking out
        foreach (var ingredientEl in consumedIngredientElements)
        {
            VFXToolkit.Tween(ingredientEl, 200f, t =>
            {
                float scale = 1f - VFXToolkit.EaseOutQuad(t);
                ingredientEl.style.scale = new Scale(new Vector2(scale, scale));
                ingredientEl.style.opacity = 1f - t;
            });
        }

        // 2. After 200ms delay, bounce in the result
        var resultElement = /* the element showing the mix result */;
        resultElement.style.scale = new Scale(Vector2.zero);
        resultElement.schedule.Execute(() =>
        {
            VFXToolkit.ScaleBounce(resultElement, 1.15f, 350f, VFXToolkit.EaseOutBack);
        }).ExecuteLater(200);
```

The exact element references depend on how ApothekeUI structures its mix result display. Read the mix handler to find the ingredient list elements and result display element, then apply the animation to those specific references.

- [ ] **Step 2: Verify in Unity Editor**

Open Apotheke, mix a recipe. Verify ingredients shrink out, result bounces in.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/UI/ApothekeUI.cs
git commit -m "feat: add mix animation to Apotheke (ingredient shrink, result bounce)"
```

---

## Task 9: UI Panel Polish — Panel Entrance/Exit CSS

**Files:**
- Modify: `Assets/UI/Styles/Overlay.uss`
- Modify: `Assets/UI/Styles/Common.uss`

- [ ] **Step 1: Add panel transition classes to Overlay.uss**

Add these classes to the end of `Overlay.uss`:

```css
/* --- Panel Entrance/Exit Transitions --- */

.overlay-panel {
    translate: 0 100%;
    opacity: 0;
    transition-property: translate, opacity;
    transition-duration: 350ms, 250ms;
    transition-timing-function: ease-out-back, ease;
}

.overlay-panel.panel--open {
    translate: 0 0;
    opacity: 1;
}

.overlay-panel .panel-content {
    opacity: 0;
    translate: 0 12px;
    transition-property: opacity, translate;
    transition-duration: 250ms, 250ms;
    transition-timing-function: ease, ease-out;
    transition-delay: 150ms, 150ms;
}

.overlay-panel.panel--open .panel-content {
    opacity: 1;
    translate: 0 0;
}

/* Scrim fade */
.overlay-scrim {
    opacity: 0;
    transition-property: opacity;
    transition-duration: 250ms;
    transition-timing-function: ease;
}

.overlay-scrim.scrim--visible {
    opacity: 1;
}
```

- [ ] **Step 2: Add press feedback and staggered item classes to Common.uss**

Add to the end of `Common.uss`:

```css
/* --- Card Press Feedback --- */

.press-feedback {
    transition-property: scale;
    transition-duration: 200ms;
    transition-timing-function: ease-out;
}

.press-feedback:active {
    scale: 0.97 0.97;
    transition-duration: 100ms;
    transition-timing-function: ease;
}

/* --- Staggered Item Appearance --- */

.stagger-item {
    opacity: 0;
    translate: 0 8px;
    transition-property: opacity, translate;
    transition-duration: 200ms, 200ms;
    transition-timing-function: ease, ease-out;
}

.stagger-item.item--visible {
    opacity: 1;
    translate: 0 0;
}
```

- [ ] **Step 3: Verify USS compiles in Unity**

Open Unity, check Console for USS parse errors.

- [ ] **Step 4: Commit**

```bash
git add Assets/UI/Styles/Overlay.uss Assets/UI/Styles/Common.uss
git commit -m "feat: add CSS transition classes for panel entrance, press feedback, staggered items"
```

---

## Task 10: UI Panel Polish — Panel Open/Close Rewrite

**Files:**
- Modify: `Assets/Scripts/UI/CampFireUI.cs`

- [ ] **Step 1: Rewrite OpenOverlay**

Replace the current `OpenOverlay` method (lines ~569-577) with CSS class-driven animation:

```csharp
        public void OpenOverlay(string title, VisualElement panel)
        {
            AudioManager.Instance?.PlaySFX("ui_panel_open");
            HideAllPanels();
            overlayTitle.text = title;
            panel.style.display = DisplayStyle.Flex;
            overlayContainer.style.display = DisplayStyle.Flex;
            overlayContainer.BringToFront();

            // Animate scrim
            overlayBackdrop?.AddToClassList("scrim--visible");

            // Animate panel (next frame so initial state is captured)
            panel.schedule.Execute(() =>
            {
                panel.AddToClassList("panel--open");
                // Stagger list items inside the panel
                StaggerChildren(panel);
            }).ExecuteLater(16);
        }

        private void StaggerChildren(VisualElement panel)
        {
            var items = panel.Query(className: "stagger-item").ToList();
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                item.RemoveFromClassList("item--visible");
                item.schedule.Execute(() => item.AddToClassList("item--visible"))
                    .ExecuteLater(40 * i);
            }
        }
```

- [ ] **Step 2: Rewrite CloseOverlay**

Replace `CloseOverlay` (lines ~633-638) with animated close:

```csharp
        public void CloseOverlay(bool silent = false)
        {
            if (!silent) AudioManager.Instance?.PlaySFX("ui_panel_close");

            // Animate scrim out
            overlayBackdrop?.RemoveFromClassList("scrim--visible");

            // Find the currently visible panel and animate it out
            var visiblePanel = overlayBody?.Query(className: "panel--open").First();
            if (visiblePanel != null)
            {
                visiblePanel.RemoveFromClassList("panel--open");
                visiblePanel.RegisterCallbackOnce<TransitionEndEvent>(evt =>
                {
                    HideAllPanels();
                    overlayContainer.style.display = DisplayStyle.None;
                });
            }
            else
            {
                HideAllPanels();
                overlayContainer.style.display = DisplayStyle.None;
            }
        }
```

- [ ] **Step 3: Add `overlay-panel` class to panel elements**

The overlay panels (build, apotheke, quest, letters, etc.) need the `overlay-panel` CSS class. Find where these panels are queried in CampFireUI (e.g., `root.Q("build-panel")`) and add the class:

```csharp
        // In Initialize, after querying panel elements:
        buildPanel?.AddToClassList("overlay-panel");
        apothekePanel?.AddToClassList("overlay-panel");
        questPanel?.AddToClassList("overlay-panel");
        lettersPanel?.AddToClassList("overlay-panel");
        // Add for any other overlay panels
```

Alternatively, add `class="overlay-panel"` directly in the UXML if these panels are defined there.

- [ ] **Step 4: Verify in Unity Editor**

Open each panel (Build, Apotheke, Quest, Letters). Verify:
- Panel slides up from bottom with spring feel
- Content fades in with slight delay
- Scrim fades in behind
- Closing reverses the animation smoothly
- Panel reaches display:none after close animation completes

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/UI/CampFireUI.cs
git commit -m "feat: rewrite panel open/close with CSS transition animations"
```

---

## Task 11: UI Panel Polish — Dialogue Entrance

**Files:**
- Modify: `Assets/Scripts/UI/DialogueUI.cs`
- Modify: `Assets/UI/Styles/Dialogue.uss`

- [ ] **Step 1: Add dialogue transition CSS**

Add to `Dialogue.uss`:

```css
/* --- Dialogue Entrance/Exit --- */

.dialogue-container {
    scale: 0.9 0.9;
    opacity: 0;
    transition-property: scale, opacity;
    transition-duration: 250ms, 250ms;
    transition-timing-function: ease-out-back, ease;
}

.dialogue-container.dialogue--open {
    scale: 1 1;
    opacity: 1;
}

.dialogue-portrait {
    translate: -20px 0;
    opacity: 0;
    transition-property: translate, opacity;
    transition-duration: 200ms, 200ms;
    transition-timing-function: ease-out, ease;
    transition-delay: 100ms, 100ms;
}

.dialogue-container.dialogue--open .dialogue-portrait {
    translate: 0 0;
    opacity: 1;
}
```

- [ ] **Step 2: Update DialogueUI.Show to use class toggle**

In the `Show` method (line ~63), after setting display to Flex, add the open class on next frame:

```csharp
        // In Show(), after setting display to Flex:
        dialogueContainer.RemoveFromClassList("dialogue--open");
        dialogueContainer.schedule.Execute(() =>
        {
            dialogueContainer.AddToClassList("dialogue--open");
        }).ExecuteLater(16);
```

- [ ] **Step 3: Update DialogueUI.Hide to animate out**

In the `Hide` method (line ~101):

```csharp
        public void Hide(bool silent = false)
        {
            if (!silent) AudioManager.Instance?.PlaySFX("ui_panel_close");
            dialogueContainer.RemoveFromClassList("dialogue--open");
            dialogueContainer.RegisterCallbackOnce<TransitionEndEvent>(evt =>
            {
                dialogueContainer.style.display = DisplayStyle.None;
            });
        }
```

- [ ] **Step 4: Verify**

Trigger dialogue in game. Verify container scales in, portrait slides from left, typewriter plays normally. Closing animates out.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/UI/DialogueUI.cs Assets/UI/Styles/Dialogue.uss
git commit -m "feat: add dialogue box entrance/exit animation"
```

---

## Task 12: Smooth Transitions — Growth Stage Crossfade

**Files:**
- Modify: `Assets/Resources/UI/Templates/GridCell.uxml`
- Modify: `Assets/UI/Styles/CampsiteGrid.uss`
- Modify: `Assets/Scripts/UI/CampsiteViewUI.cs`

- [ ] **Step 1: Add second sprite layer to GridCell.uxml**

Update `GridCell.uxml` to include a back sprite layer:

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <ui:VisualElement class="grid-cell">
        <ui:VisualElement class="cell-sprite-back" />
        <ui:VisualElement class="cell-sprite-front" />
        <ui:VisualElement class="cell-icon" />
        <ui:Label class="cell-label" text="" />
        <ui:VisualElement class="cell-progress">
            <ui:VisualElement class="cell-progress-fill" />
        </ui:VisualElement>
        <ui:Label class="cell-status" text="" />
    </ui:VisualElement>
</ui:UXML>
```

- [ ] **Step 2: Add sprite layer CSS to CampsiteGrid.uss**

```css
/* --- Two-Layer Sprite Crossfade --- */

.cell-sprite-back,
.cell-sprite-front {
    position: absolute;
    width: 100%;
    height: 100%;
    -unity-background-scale-mode: scale-to-fit;
}

.cell-sprite-front {
    transition-property: opacity;
    transition-duration: 400ms;
    transition-timing-function: ease;
}
```

- [ ] **Step 3: Update sprite assignment in CampsiteViewUI**

Find the `TrySetHexSpriteByPercent` method (line ~848) and `TrySetHexSprite` (line ~811). These currently set `cell.style.backgroundImage`. Change them to set sprites on the front layer, and add a crossfade method.

Modify the cell setup code to use the front sprite layer for all sprite assignments. Add a crossfade method:

```csharp
        internal static void CrossfadeSprite(VisualElement cell, Texture2D newSprite)
        {
            var front = cell.Q(className: "cell-sprite-front");
            var back = cell.Q(className: "cell-sprite-back");
            if (front == null || back == null) return;

            // Set new sprite on back layer
            back.style.backgroundImage = new StyleBackground(newSprite);
            back.style.opacity = 1f;

            // Fade out front to reveal back
            front.style.opacity = 0f;

            // After transition completes, swap layers
            front.RegisterCallbackOnce<TransitionEndEvent>(evt =>
            {
                front.style.backgroundImage = new StyleBackground(newSprite);
                front.style.opacity = 1f;
                back.style.backgroundImage = StyleKeyword.None;
            });

            // Small scale pop to draw attention
            VFXToolkit.ScaleBounce(cell, 1.05f, 300f, VFXToolkit.EaseOutQuad);
        }
```

Then, where sprites are updated for growing plots (in the grid rebuild or update loop), detect when the growth stage changes and call `CrossfadeSprite` instead of directly setting the background image. This requires tracking the previous growth stage per cell — store it alongside the cell reference (e.g., in a `Dictionary<VisualElement, int> cellGrowthStage`).

- [ ] **Step 4: Verify**

Plant a seed, wait for growth to cross 50%. Verify sprite crossfades smoothly with a scale pop, instead of snapping.

- [ ] **Step 5: Commit**

```bash
git add Assets/Resources/UI/Templates/GridCell.uxml Assets/UI/Styles/CampsiteGrid.uss Assets/Scripts/UI/CampsiteViewUI.cs
git commit -m "feat: add two-layer sprite crossfade for growth stage transitions"
```

---

## Task 13: Smooth Transitions — Weather Crossfade & Tutorial Pulse

**Files:**
- Modify: `Assets/Scripts/UI/WeatherBarUI.cs`
- Modify: `Assets/Scripts/UI/TutorialUI.cs`
- Modify: `Assets/UI/Styles/Tutorial.uss`

- [ ] **Step 1: Add weather crossfade to WeatherBarUI**

Find where WeatherBarUI updates its display on weather changes (it subscribes to `WeatherService.OnWeatherUpdated`). Wrap the update in a crossfade:

```csharp
        private void OnWeatherUpdated()
        {
            // Fade out current content
            VFXToolkit.Tween(weatherContent, 150f, t =>
            {
                weatherContent.style.opacity = 1f - t;
            }, null, 0f);

            // After fade out, update content and fade in
            weatherContent.schedule.Execute(() =>
            {
                UpdateWeatherDisplay(); // existing method that sets icon, temp, description
                VFXToolkit.Tween(weatherContent, 150f, t =>
                {
                    weatherContent.style.opacity = t;
                });
            }).ExecuteLater(250);
        }
```

Adapt `weatherContent` to whatever the actual container element is named.

- [ ] **Step 2: Smooth tutorial pulse**

Update `Tutorial.uss` — replace the binary highlight classes with smooth transitions:

```css
.tutorial-highlight {
    border-color: rgba(255, 210, 70, 0.8);
    border-width: 3px;
    scale: 1.02 1.02;
    transition-property: border-color, border-width, scale;
    transition-duration: 600ms, 600ms, 600ms;
    transition-timing-function: ease-in-out, ease-in-out, ease-in-out;
}

.tutorial-highlight-dim {
    border-color: rgba(255, 210, 70, 0.3);
    border-width: 2px;
    scale: 1 1;
    transition-property: border-color, border-width, scale;
    transition-duration: 600ms, 600ms, 600ms;
    transition-timing-function: ease-in-out, ease-in-out, ease-in-out;
}
```

No C# changes needed — the existing class toggle in TutorialUI.cs (lines 105-120) will now produce smooth transitions instead of binary snaps because the CSS transitions handle the interpolation.

- [ ] **Step 3: Verify**

- Change weather in debug mode. Verify weather bar fades out/in smoothly.
- Start tutorial. Verify highlight pulse is a smooth breathe, not a blink.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/UI/WeatherBarUI.cs Assets/Scripts/UI/TutorialUI.cs Assets/UI/Styles/Tutorial.uss
git commit -m "feat: add weather crossfade and smooth tutorial highlight pulse"
```

---

## Task 14: Ambient Life — Magical Motes & Smoke Wisps

**Files:**
- Modify: `Assets/Scripts/UI/CampsiteLightingOverlay.cs`

- [ ] **Step 1: Add Mote struct and pool**

Add alongside the existing Firefly struct (line ~64):

```csharp
        private struct Mote
        {
            public Vector2 position;   // normalized 0-1
            public Vector2 velocity;   // normalized/sec
            public float age;
            public float lifetime;
            public Color color;
        }

        private readonly List<Mote> motes = new();
        private float nextMoteSpawn;
        const int MaxMotes = 15;
        const float MoteSpawnInterval = 0.4f;
        const float MoteDriftSpeed = 0.005f;
        const float MoteRadius = 0.02f;
        const float MoteIntensity = 0.3f;
```

- [ ] **Step 2: Add Smoke Wisp struct and pool**

```csharp
        private struct SmokeWisp
        {
            public Vector2 position;
            public Vector2 velocity;
            public float age;
            public float lifetime;
            public float radius;
        }

        private readonly List<SmokeWisp> smokeWisps = new();
        private float nextWispSpawn;
        const int MaxWisps = 3;
        const float WispSpawnInterval = 2.5f;
        const float WispRiseSpeed = 0.008f;
        const float WispIntensity = 0.10f;
```

- [ ] **Step 3: Add UpdateMotes method**

Add alongside `UpdateFireflies` (line ~342):

```csharp
        private void UpdateMotes(float dt, bool isNight, bool isRaining)
        {
            // Don't spawn in rain/storm
            float spawnRate = isRaining ? 0f : (isNight ? 1f : 0.5f);

            for (int i = motes.Count - 1; i >= 0; i--)
            {
                var m = motes[i];
                m.age += dt;
                m.position += m.velocity * dt;
                if (m.age >= m.lifetime)
                {
                    motes.RemoveAt(i);
                    continue;
                }
                motes[i] = m;
            }

            nextMoteSpawn -= dt;
            if (nextMoteSpawn <= 0f && motes.Count < MaxMotes * spawnRate && spawnRate > 0f)
            {
                var m = new Mote
                {
                    position = new Vector2(Random.Range(0.1f, 0.9f), Random.Range(0.1f, 0.9f)),
                    velocity = new Vector2(
                        Random.Range(-MoteDriftSpeed, MoteDriftSpeed),
                        Random.Range(-MoteDriftSpeed, MoteDriftSpeed * 0.5f) - MoteDriftSpeed * 0.3f),
                    age = 0f,
                    lifetime = Random.Range(4f, 6f),
                    color = isNight
                        ? new Color(0.7f, 0.8f, 1f)
                        : new Color(1f, 0.9f, 0.6f)
                };
                motes.Add(m);
                nextMoteSpawn = MoteSpawnInterval + Random.Range(-0.1f, 0.1f);
            }
        }
```

- [ ] **Step 4: Add UpdateSmokeWisps method**

```csharp
        private void UpdateSmokeWisps(float dt, bool isRaining, float windX)
        {
            for (int i = smokeWisps.Count - 1; i >= 0; i--)
            {
                var w = smokeWisps[i];
                w.age += dt;
                w.position += w.velocity * dt;
                w.position.x += windX * 0.002f * dt; // wind drift
                if (w.age >= w.lifetime)
                {
                    smokeWisps.RemoveAt(i);
                    continue;
                }
                smokeWisps[i] = w;
            }

            nextWispSpawn -= dt;
            if (nextWispSpawn <= 0f && smokeWisps.Count < MaxWisps)
            {
                float lifetimeMul = isRaining ? 0.5f : 1f;
                var w = new SmokeWisp
                {
                    position = fireNormalized + new Vector2(Random.Range(-0.02f, 0.02f), 0f),
                    velocity = new Vector2(
                        isRaining ? windX * 0.004f : 0f,
                        -WispRiseSpeed * (isRaining ? 1f : 1f)), // upward in screen space (negative Y if Y goes down)
                    age = 0f,
                    lifetime = Random.Range(5f, 8f) * lifetimeMul,
                    radius = Random.Range(0.06f, 0.10f)
                };
                smokeWisps.Add(w);
                nextWispSpawn = WispSpawnInterval + Random.Range(-0.5f, 0.5f);
            }
        }
```

Note: `fireNormalized` refers to the flame's normalized (0-1) position on the lightmap canvas. In CampsiteLightingOverlay, the fire position is tracked and converted to normalized coordinates for the lightmap. Search for where the fire light source position is calculated (typically derived from the flame hex cell's world position mapped to the 0-1 lightmap space) and use the same variable. The lightmap Y-axis follows screen convention (0=top, 1=bottom), so rising smoke uses negative Y velocity (upward).

- [ ] **Step 5: Render motes and wisps in the lightmap**

In the `RenderLightmap` method (line ~392), find where fireflies are rendered (they're drawn as additive circles). Add mote and wisp rendering in the same loop:

```csharp
            // After firefly rendering:
            // Render motes
            foreach (var m in motes)
            {
                float fadeIn = Mathf.Clamp01(m.age / 1.5f);
                float fadeOut = Mathf.Clamp01((m.lifetime - m.age) / 1.5f);
                float alpha = Mathf.Min(fadeIn, fadeOut) * MoteIntensity;
                if (alpha < 0.01f) continue;
                DrawLight(pixels, w, h, m.position, MoteRadius, m.color, alpha);
            }

            // Render smoke wisps (only when lightmap is active — night/golden hour)
            if (baseAlpha > 0.05f)
            {
                foreach (var sw in smokeWisps)
                {
                    float fadeIn = Mathf.Clamp01(sw.age / 2f);
                    float fadeOut = Mathf.Clamp01((sw.lifetime - sw.age) / 2f);
                    float alpha = Mathf.Min(fadeIn, fadeOut) * WispIntensity;
                    if (alpha < 0.01f) continue;
                    DrawLight(pixels, w, h, sw.position, sw.radius, new Color(0.9f, 0.85f, 0.75f), alpha);
                }
            }
```

Adapt `DrawLight` to whatever the existing method name is for drawing a light source circle on the pixel array. If it's inline pixel manipulation, follow the same pattern used for fireflies.

- [ ] **Step 6: Call update methods from the main Update/render loop**

Find where `UpdateFireflies(dt)` is called and add:

```csharp
            bool isRaining = currentCondition == "Rain" || currentCondition == "Storm";
            float windX = WeatherService.Instance?.CurrentWeather.windSpeed ?? 0f;
            UpdateMotes(dt, isNight, isRaining);
            if (baseAlpha > 0.05f) // only update wisps when lightmap is visible
                UpdateSmokeWisps(dt, isRaining, windX);
```

- [ ] **Step 7: Verify in Unity Editor**

- Set debug weather to night + clear. Verify golden motes drift across grid, smoke wisps rise from flame area.
- Set weather to rain. Verify motes disappear, wisps blow sideways.
- Set daytime. Verify fewer motes, no smoke wisps.

- [ ] **Step 8: Commit**

```bash
git add Assets/Scripts/UI/CampsiteLightingOverlay.cs
git commit -m "feat: add magical motes and smoke wisps to ambient lighting"
```

---

## Task 15: Ambient Life — Growing Plant Shimmer

**Files:**
- Modify: `Assets/Scripts/UI/CampsiteViewUI.cs`

- [ ] **Step 1: Add shimmer timer to CampsiteViewUI**

Add fields and Update logic:

```csharp
        // Add to class fields:
        private float shimmerTimer;
        private float shimmerInterval = 4f;
        private bool shimmerActive;

        // Add to Update() method (or wherever periodic updates run):
        shimmerTimer += Time.deltaTime;
        if (shimmerTimer >= shimmerInterval && !shimmerActive)
        {
            shimmerTimer = 0f;
            shimmerInterval = Random.Range(3f, 5f);
            TryShimmerGrowingPlot();
        }
```

- [ ] **Step 2: Implement TryShimmerGrowingPlot**

```csharp
        private void TryShimmerGrowingPlot()
        {
            if (SaveManager.Instance?.Data?.plots == null) return;
            var growingPlots = new List<int>();
            var plots = SaveManager.Instance.Data.plots;
            for (int i = 0; i < plots.Count; i++)
            {
                if (!string.IsNullOrEmpty(plots[i].seedName) && !plots[i].isHarvested)
                    growingPlots.Add(i);
            }
            if (growingPlots.Count == 0) return;

            int plotIndex = growingPlots[Random.Range(0, growingPlots.Count)];
            var plot = plots[plotIndex];
            // Look up the cell for this plot by grid coords
            if (cellLookup != null && cellLookup.TryGetValue((plot.gridX, plot.gridY), out var cell))
            {
                shimmerActive = true;
                VFXToolkit.GlowPulse(cell, new Color(0.3f, 0.8f, 0.2f), 0.10f, 50f, 0f, 150f);
                cell.schedule.Execute(() => shimmerActive = false).ExecuteLater(200);
            }
        }
```

- [ ] **Step 3: Verify**

Plant seeds, watch for a few seconds. Verify occasional green shimmer on random growing plots.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/UI/CampsiteViewUI.cs
git commit -m "feat: add subtle green shimmer on growing plants"
```

---

## Task 16: Reward Moments — Tiered Celebration System

**Files:**
- Modify: `Assets/Scripts/Utils/VFXToolkit.cs`
- Modify: `Assets/Scripts/UI/RewardRevealUI.cs`
- Modify: `Assets/UI/Styles/RewardReveal.uss`

- [ ] **Step 1: Add CelebrateTier method to VFXToolkit**

```csharp
        // --- Tiered Celebration ---

        public static void CelebrateTier(
            VisualElement card,
            int tier,
            VisualElement sparkleOverlay = null,
            VisualElement vignetteOverlay = null,
            VisualElement viewport = null)
        {
            var tierColors = new[]
            {
                new Color(0.8f, 0.7f, 0.5f),  // tier 1: warm gray
                new Color(0.3f, 0.8f, 0.3f),  // tier 2: green
                new Color(0.3f, 0.5f, 1f),    // tier 3: blue
                new Color(1f, 0.75f, 0.2f),   // tier 4: gold
            };
            int idx = Mathf.Clamp(tier - 1, 0, 3);
            var color = tierColors[idx];

            switch (tier)
            {
                case 1:
                    // Warm glow ring only (CSS handles glow via tier class)
                    break;
                case 2:
                    if (sparkleOverlay != null)
                        SpawnSparkles(sparkleOverlay, card.worldBound.center, 7, color, 80f, 400f);
                    break;
                case 3:
                    ScaleBounce(card, 1.1f, 400f, EaseOutBack);
                    if (sparkleOverlay != null)
                        SpawnSparkles(sparkleOverlay, card.worldBound.center, 13, color, 120f, 500f);
                    if (vignetteOverlay != null)
                        ScreenVignettePulse(vignetteOverlay, color, 0.1f, 300f);
                    break;
                case >= 4:
                    ScaleBounce(card, 1.2f, 500f, EaseOutElastic);
                    if (sparkleOverlay != null)
                        SpawnSparkles(sparkleOverlay, card.worldBound.center, 22, color, 160f, 600f);
                    if (vignetteOverlay != null)
                        ScreenVignettePulse(vignetteOverlay, color, 0.15f, 500f);
                    if (viewport != null)
                        ViewportMicroShake(viewport, 3f, 300f);
                    break;
            }
        }
```

- [ ] **Step 2: Add tier-specific CSS glow to RewardReveal.uss**

Add to `RewardReveal.uss`:

```css
/* --- Tier-Specific Glows --- */

.reward-card--tier2 .reward-glow {
    border-color: rgba(80, 200, 80, 0.4);
}

.reward-card--tier3 .reward-glow {
    border-color: rgba(80, 130, 255, 0.5);
    border-width: 3px;
}

.reward-card--tier4 .reward-glow {
    border-color: rgba(255, 190, 50, 0.6);
    border-width: 4px;
}
```

- [ ] **Step 3: Integrate celebrations into RewardRevealUI**

In `RewardRevealUI.cs`, find the `RevealCards` coroutine (line ~197) where cards are created and revealed with stagger. After each card is made visible, call the tier celebration:

```csharp
            // Inside the reveal loop, after card becomes visible:
            // Determine tier from the reward card's existing CSS tier class
            // RewardRevealUI already adds "reward-card--tierN" classes (clamped 1-4)
            // Parse tier from the class name:
            int tier = 1;
            for (int t = 4; t >= 1; t--)
            {
                if (cardElement.ClassListContains($"reward-card--tier{t}")) { tier = t; break; }
            }
            VFXToolkit.CelebrateTier(cardElement, tier, sparkleOverlay, vignetteOverlay, viewport);
```

For the overlay elements, add a sparkle overlay and vignette overlay to the reward reveal container during initialization:

```csharp
        // In RewardRevealUI initialization:
        var sparkleOverlay = new VisualElement();
        sparkleOverlay.style.position = Position.Absolute;
        sparkleOverlay.style.width = Length.Percent(100);
        sparkleOverlay.style.height = Length.Percent(100);
        sparkleOverlay.pickingMode = PickingMode.Ignore;
        rewardContainer.Add(sparkleOverlay);

        var vignetteOverlay = new VisualElement();
        vignetteOverlay.style.position = Position.Absolute;
        vignetteOverlay.style.width = Length.Percent(100);
        vignetteOverlay.style.height = Length.Percent(100);
        vignetteOverlay.pickingMode = PickingMode.Ignore;
        rewardContainer.Add(vignetteOverlay);
```

- [ ] **Step 4: Verify**

Use admin tools to grant items of different rarities. Verify:
- Common: simple glow
- Uncommon: green sparkles
- Rare: blue sparkles + screen vignette
- Epic: gold sparkles + vignette + shake

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Utils/VFXToolkit.cs Assets/Scripts/UI/RewardRevealUI.cs Assets/UI/Styles/RewardReveal.uss
git commit -m "feat: add tiered celebration system for reward reveals"
```

---

## Task 17: Reward Moments — Bird Collection & Nav Button Pulse

**Files:**
- Modify: `Assets/Scripts/UI/CampsiteViewUI.cs`
- Modify: `Assets/Scripts/UI/BottomNavUI.cs`

- [ ] **Step 1: Add bird collection effect to CampsiteViewUI**

Subscribe to `BirdManager.OnBirdCollected` (where other events are subscribed, line ~189):

```csharp
        // In Initialize:
        BirdManager.Instance.OnBirdCollected += OnBirdCollected;

        // Method:
        private void OnBirdCollected(BirdSave bird)
        {
            if (cellLookup != null && cellLookup.TryGetValue((bird.gridX, bird.gridY), out var cell))
            {
                // Feather burst
                var overlay = cell.panel?.visualTree; // use root as sparkle overlay
                if (overlay != null)
                    VFXToolkit.SpawnSparkles(overlay, cell.worldBound.center, 5,
                        new Color(1f, 0.95f, 0.85f), 60f, 500f);

                // Cell shrinks away
                VFXToolkit.Tween(cell, 300f, t =>
                {
                    float scale = Mathf.Lerp(1f, 0f, VFXToolkit.EaseOutQuad(t));
                    cell.style.scale = new Scale(new Vector2(scale, scale));
                });
            }
        }

        // Unsubscribe in cleanup:
        // BirdManager.Instance.OnBirdCollected -= OnBirdCollected;
```

- [ ] **Step 2: Add PulseButton method to BottomNavUI**

```csharp
        // Add field refs for buttons (store them during Initialize):
        private Button seedsButton;
        private Button questButton;
        private Button mailButton;

        // In Initialize (adapt from existing code at line ~22):
        seedsButton = root.Q<Button>("btn-seeds");
        questButton = root.Q<Button>("btn-quest");
        mailButton = root.Q<Button>("btn-mail");

        // Add public method:
        public void PulseButton(string buttonName, int repetitions = 2)
        {
            Button btn = buttonName switch
            {
                "seeds" => seedsButton,
                "quest" => questButton,
                "mail" => mailButton,
                _ => null
            };
            if (btn == null) return;

            int count = 0;
            void DoPulse()
            {
                if (count >= repetitions) return;
                count++;
                VFXToolkit.ScaleBounce(btn, 1.15f, 400f);
                // Use background-color tween for glow (not GlowPulse which is hex-cell-only)
                VFXToolkit.Tween(btn, 400f, t =>
                {
                    float alpha;
                    if (t < 0.25f) alpha = 0.20f * (t / 0.25f);
                    else alpha = 0.20f * (1f - (t - 0.25f) / 0.75f);
                    btn.style.backgroundColor = new Color(1f, 0.75f, 0.2f, alpha);
                });
                if (count < repetitions)
                    btn.schedule.Execute(DoPulse).ExecuteLater(700);
            }
            DoPulse();
        }
```

- [ ] **Step 3: Wire nav pulse to reward collection**

In RewardRevealUI.cs, after the fly-out collection animation completes, call:

```csharp
        // After all cards have been collected:
        BottomNavUI.Instance?.PulseButton("seeds");
```

Similarly in the bird collection handler, after grid rebuilds:

```csharp
        BottomNavUI.Instance?.PulseButton("seeds");
```

- [ ] **Step 4: Verify**

- Collect a bird. Verify feather burst + cell shrink + seeds button pulse.
- Complete a quest and collect rewards. Verify seeds button pulses after fly-out.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/UI/CampsiteViewUI.cs Assets/Scripts/UI/BottomNavUI.cs Assets/Scripts/UI/RewardRevealUI.cs
git commit -m "feat: add bird collection feather burst and nav button notification pulse"
```

---

## Task 18: Reward Moments — Flame Level-Up Enhancement

**Files:**
- Modify: `Assets/Scripts/UI/FlameLevelUpAnimator.cs`

- [ ] **Step 1: Add post-celebration nav button pulses**

Find the end of the flame level-up animation sequence (after the level badge fades at ~3.5s). Add capability pulses:

```csharp
        // After the level badge fade-out (around the 3.5s mark in the animation timeline):
        // Pulse nav buttons to show what was unlocked
        element.schedule.Execute(() =>
        {
            // Entity cap increased — pulse Build button
            BottomNavUI.Instance?.PulseButton("seeds", 2);

            // If quest tier unlocked at this level, pulse Quest button
            element.schedule.Execute(() =>
            {
                BottomNavUI.Instance?.PulseButton("quest", 2);
            }).ExecuteLater(200);
        }).ExecuteLater(3500);
```

Find the exact timer/schedule point where the level badge fades. The existing code uses scheduled callbacks — add this after the last scheduled step. Check if the level unlocks specific features (check `ConfigService` for level-gated content) and only pulse the relevant button.

- [ ] **Step 2: Verify**

Use debug tools to trigger a flame level-up. Verify: after the celebration finishes, nav buttons pulse to indicate new capabilities.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/UI/FlameLevelUpAnimator.cs
git commit -m "feat: add post-level-up nav button pulses for unlocked capabilities"
```

---

## Task 19: Final Polish — Run All Tests & Verify

**Files:**
- Modify: `Assets/Tests/EditMode/VFXToolkitTests.cs` (if needed)

- [ ] **Step 1: Run all EditMode tests**

Run via Unity Test Runner (EditMode) or `run_tests` MCP tool with `mode: "EditMode"`.
Expected: All tests pass, including VFXToolkit easing tests from Task 1.

- [ ] **Step 2: Check Unity Console for errors**

Open Unity, enter Play mode. Check Console for:
- No null reference exceptions from new event subscriptions
- No USS parse errors from new CSS classes
- No compilation errors

Use `read_console` MCP tool to check.

- [ ] **Step 3: Manual smoke test of all effects**

Run through the complete game loop and verify each effect:
1. Watch flame cell pulse every ~1 second (mana heartbeat)
2. Watch for golden motes drifting at night, smoke wisps from flame
3. Watch for green shimmer on growing plots
4. Plant a seed — green glow + ripple + bounce
5. Water a plot — blue ripple + squash
6. Harvest — gold ripple (check quality affects intensity)
7. Open Build panel — panel slides up with spring, items stagger in
8. Open Apotheke, mix — ingredients shrink, result bounces
9. Close panel — smooth reverse animation
10. Trigger dialogue — scales in, portrait slides
11. Watch growth stage sprite crossfade (may need time acceleration)
12. Check weather change crossfade (toggle debug weather)
13. Collect a bird — feather burst + button pulse
14. Complete quest — tier-appropriate celebration + button pulse
15. Level up flame — existing celebration + nav button pulses after

- [ ] **Step 4: Fix any issues found**

Address any visual glitches, timing issues, or missing effects found during testing.

- [ ] **Step 5: Final commit if any fixes were needed**

```bash
git add -A  # only if fixes were made
git commit -m "fix: polish visual effects after smoke testing"
```
