# Visual Polish — Full Design Spec

**Date:** 2026-03-26
**Scope:** Interaction juice, UI panel polish, ambient life, smooth transitions, reward moments
**Approach:** Toolkit-first — build shared VFXToolkit, then implement five areas using its primitives

---

## Phase 0: VFXToolkit

A static utility class at `Assets/Scripts/Utils/VFXToolkit.cs` that consolidates animation patterns currently scattered across FlameLevelUpAnimator, CampsiteLightingOverlay, and CampsiteViewUI.

### Primitives

**Easing Functions** — static methods on VFXToolkit:
- `EaseOutBack(float t)` — overshoot ease for bouncy entrances
- `EaseOutQuad(float t)` — smooth deceleration
- `EaseOutElastic(float t)` — springy settle for dramatic moments
- `Smoothstep(float t)` — smooth acceleration + deceleration
- `Spring(float t, float damping, float frequency)` — configurable spring curve

**Tween Scheduler:**
- `Tween(VisualElement element, float durationMs, Action<float> onUpdate, Func<float,float> easing, float delayMs = 0)` — schedules a 0→1 animation using `element.schedule`. Returns `IVisualElementScheduledItem` for cancellation.
- Replaces all ad-hoc `schedule.Execute(() => {...}).Every(16)` patterns.
- `onUpdate` receives the eased progress value (0-1). Caller applies it to scale, opacity, color, position, etc.

**Glow Pulse:**
- `GlowPulse(VisualElement cell, Color color, float peakAlpha, float fadeInMs, float holdMs, float fadeOutMs)` — writes to `CampsiteViewUI.GlowColor` dictionary to render a glow on a hex cell. Handles the full fade-in → hold → fade-out lifecycle.

**Ripple Cascade:**
- `RippleCascade(VisualElement centerCell, Color color, float ringDelayMs, int maxRings, float peakAlpha, CampsiteViewUI gridRef)` — fires GlowPulse outward ring-by-ring from a center cell. Uses `gridRef` to look up neighbors by ring distance.

**Number Tween:**
- `TweenNumber(Label label, float fromValue, float toValue, float durationMs, string format = "F0")` — animates a label's text from one numeric value to another with EaseOutQuad interpolation.

**Scale Bounce:**
- `ScaleBounce(VisualElement element, float peakScale, float durationMs, Func<float,float> easing = null)` — scale from 1 → peak → 1. Defaults to EaseOutElastic. Two-phase: up (first 30% of duration) then settle (remaining 70%).

**Spawn Sparkles:**
- `SpawnSparkles(VisualElement overlay, Vector2 center, int count, Color color, float spread, float lifetimeMs)` — renders small circles via Painter2D on an overlay element. Each sparkle has outward velocity (80-150 px/s), slight gravity, fades over lifetime. Used for reward celebrations.

**Screen Vignette Pulse:**
- `ScreenVignettePulse(VisualElement fullscreenOverlay, Color color, float peakAlpha, float durationMs)` — fades a full-screen border glow in and out. Used for rare reward reveals.

**Viewport Micro-Shake:**
- `ViewportMicroShake(VisualElement viewport, float amplitude, float durationMs, float decay = 0.92f)` — applies translate offsets with exponential decay. Same pattern as FlameLevelUpAnimator but parameterized.

### Design constraints
- No MonoBehaviour. No singleton. No Update() loop. Purely static methods.
- All scheduling through `VisualElement.schedule` (already proven across codebase).
- No DOTween dependency (uses native UI Toolkit scheduling).
- GlowPulse/RippleCascade write to `CampsiteViewUI.GlowColor` (existing rendering path).
- SpawnSparkles/ScreenVignettePulse use `generateVisualContent` + Painter2D (same as FlameLevelUpAnimator).

---

## Phase 1: Interaction Juice

Every core player action gets immediate, satisfying visual feedback. Effects are additive (cosmetic overlay) — game state updates immediately, animations are non-blocking.

### Plant Seed
- **Trigger:** `PlotManager.OnPlotChanged` when a seed is planted (plot transitions from empty to growing)
- **Effect:** ScaleBounce on cell (peak 1.12, 400ms, EaseOutElastic) + green GlowPulse (fadeIn 100ms, hold 100ms, fadeOut 250ms, alpha 0.25) + RippleCascade (1 ring, 100ms delay, green tint, alpha 0.12)
- **Touchpoint:** CampsiteViewUI subscribes to OnPlotChanged

### Water Plot
- **Trigger:** `PlotManager.OnPlotChanged` when a plot is watered
- **Effect:** Keep existing blue glow pulse + add RippleCascade (2 rings, 80ms delay, blue tint Color(0.4, 0.65, 1), alpha 0.15) + subtle scale squash on watered cell (0.95→1.0, 200ms, EaseOutQuad)
- **Touchpoint:** CampsiteViewUI — extend existing water glow handler

### Harvest
- **Trigger:** `PlotManager.OnHarvested` with quality score
- **Effect:** ScaleBounce on cell (peak 1.15, 400ms) + golden RippleCascade where ring count scales with quality:
  - Quality < 0.3: 1 ring, alpha 0.12
  - Quality 0.3-0.7: 2 rings, alpha 0.18
  - Quality > 0.7: 3 rings, alpha 0.25
  - Quality = 1.0: 3 rings, alpha 0.30 + warm glow pulse on cell (alpha 0.35)
- **Color:** Warm gold Color(1, 0.72, 0.2)
- **Touchpoint:** CampsiteViewUI subscribes to OnHarvested; PlotManager passes quality through event args

### Craft Building
- **Trigger:** Already has scale bounce + neighbor ripple (existing)
- **Enhancement:** Add brief warm flash on the cell — white overlay element, opacity 0.4→0 over 300ms
- **Touchpoint:** CampsiteViewUI craft animation handler

### Apotheke Mix
- **Trigger:** Successful mix in ApothekeUI
- **Effect:** Consumed ingredient items scale 1→0 over 200ms (EaseOutQuad). After 200ms delay, result item scales 0→1.15→1 over 350ms (EaseOutBack). Result item gets a warm glow ring (GlowPulse-style CSS glow, not hex-cell glow).
- **Touchpoint:** ApothekeUI.cs mix success handler

### Mana Collection (Tap Flame)
- **Trigger:** Player taps flame cell to collect accumulated mana
- **Effect:** Flame cell warm GlowPulse (amber Color(1, 0.6, 0.1), alpha 0.25, fadeIn 80ms, hold 60ms, fadeOut 200ms) + mana counter NumberTween rolling up to new value over 400ms
- **Touchpoint:** CampsiteViewUI flame tap handler + ResourceDisplayUI

### Vase Fill Complete
- **Trigger:** `VaseManager.OnVasesChanged` when fill completes
- **Effect:** Vase cell blue GlowPulse (Color(0.4, 0.65, 1), alpha 0.20, fadeIn 100ms, hold 80ms, fadeOut 250ms) + water counter NumberTween
- **Touchpoint:** CampsiteViewUI subscribes to OnVasesChanged

### Resource Counter Animation
- **Applies to:** All resource display updates (water, gems — mana already updates per-frame)
- **Effect:** NumberTween on label. Duration: 300ms for changes < 20% of current value, 600ms for larger changes. During tween, label color briefly pulses to the resource color (blue for water, purple for gems) then fades back.
- **Touchpoint:** ResourceDisplayUI.cs — replace direct text assignment with NumberTween calls

---

## Phase 2: UI Panel Polish

### Panel Entrance/Exit Animation

**Opening sequence (CampFireUI.OpenOverlay):**
1. Set panel `display: Flex` with initial state classes (translate 0 100%, opacity 0)
2. Next frame: add `.panel--open` class
3. CSS transitions drive the animation:
   - Panel translate: 0 100% → 0 0 (350ms, ease-out-back)
   - Panel opacity: 0 → 1 (250ms, ease)
   - Panel content translate: 0 12px → 0 0 (250ms, ease-out, 150ms delay)
   - Panel content opacity: 0 → 1 (250ms, ease, 150ms delay)

**Closing sequence (CampFireUI.CloseOverlay):**
1. Remove `.panel--open` class (reverse transitions play)
2. Register `TransitionEndEvent` callback
3. On transition end: set `display: None`

**Scrim animation:**
- Scrim element gets opacity transition (250ms, ease)
- Open: add `.scrim--visible` (opacity 0 → 0.5)
- Close: remove `.scrim--visible` (opacity 0.5 → 0)

### Staggered List Item Appearance
- When a panel opens, list items (build cards, quest cards, seed rows, etc.) start with opacity 0 + translate 0 8px
- Each item gets `.item--visible` class with 40ms stagger delay per item index
- CSS transition: opacity 200ms ease, translate 200ms ease-out
- Implementation: after panel open transition completes, iterate children and add class with `schedule.Execute` delays

### Card Press Feedback
- CSS `:active` pseudo-class on interactive cards:
  - Scale: 1.0 → 0.97 (100ms, ease)
- On release (transition back):
  - Scale: 0.97 → 1.0 (200ms, ease-out)
- Applies to: build cards, quest cards, seed rows, recipe items, friend items

### Disabled State
- Disabled cards (can't afford): opacity 0.5 + desaturate via `--unity-background-tint-color` gray overlay
- Existing lock icon behavior stays; this adds the visual dimming

### Dialogue Box Entrance
- Container: scale 0.9→1.0 + opacity 0→1 (250ms, EaseOutBack) via CSS transition
- Portrait: translate -20px 0 → 0 0 (200ms, ease-out, 100ms delay)
- Exit: reverse (scale 1.0→0.95, opacity 1→0, 200ms)
- Typewriter text effect: no change (already good)

### Files changed
- `Assets/UI/Styles/Overlay.uss` — new `.panel--open`, `.scrim--visible` classes with transitions
- `Assets/UI/Styles/Common.uss` — `.item--visible` stagger pattern, `:active` press states
- `Assets/UI/Styles/Dialogue.uss` — entrance/exit transitions
- `Assets/Scripts/UI/CampFireUI.cs` — rewrite OpenOverlay/CloseOverlay to use class-toggle + TransitionEndEvent
- Panel-specific USS files — initial states for staggered items

---

## Phase 3: Ambient Life

### Magical Motes
- **Count:** 10-15 simultaneous motes across the full grid area
- **Appearance:** Tiny warm gold circles (day) or soft blue-white (night), radius 0.02 normalized
- **Behavior:** Drift at 0.005 normalized/sec (half firefly speed), random direction with slight upward bias. Lifetime 4-6s with 1.5s fade-in, 1.5s fade-out.
- **Spawn:** Randomly across the full grid area (not flame-centric like fireflies)
- **Spawn interval:** Every 0.4s (±0.1s)
- **Weather modifiers:**
  - Rain/Storm: 0 motes (suppressed)
  - Snow: color shifts to cool blue-white Color(0.7, 0.8, 1.0)
  - Daytime: 50% spawn rate (less visible in bright conditions)
  - Clear night: full count
- **Implementation:** New particle pool in CampsiteLightingOverlay alongside existing firefly pool. Same rendering path (additive circles on the 256x256 lightmap). Different parameters.
- **Intensity:** 0.3 (lower than fireflies at 0.5)

### Smoke Wisps
- **Count:** 2-3 simultaneous wisps rising from the flame
- **Appearance:** Large soft circles (radius 0.06-0.10 normalized), very low opacity (0.08-0.12)
- **Behavior:** Rise upward at 0.008 normalized/sec with horizontal drift matching wind direction from WeatherService. Lifetime 5-8s with 2s fade-in, 2s fade-out.
- **Spawn:** At flame position (±0.02 horizontal offset)
- **Weather modifiers:**
  - Rain/Storm: blown sideways (2x horizontal drift), faster dissipation (halved lifetime)
  - Snow: slower rise (0.6x vertical speed)
  - Daytime: not rendered (lightmap overlay not active during clear day)
- **Implementation:** New particle type in CampsiteLightingOverlay. Rendered as additive smudges on lightmap.

### Mana Heartbeat Pulse
- **Trigger:** New `OnManaTick` event from FlameManager, fired each time mana accumulates in Update()
- **Rate limiting:** Pulse fires at most once per second (even though mana accumulates every frame)
- **Effect:** VFXToolkit.GlowPulse on flame cell — amber Color(1, 0.55, 0.1), alpha 0.15, fadeIn 80ms, hold 60ms, fadeOut 200ms
- **Scaling:** Peak alpha increases slightly with flame level: `0.12 + flameLevel * 0.005` (capped at 0.20)
- **Implementation:** FlameManager adds a 1s throttled event. CampsiteViewUI subscribes.

### Growing Plant Shimmer
- **Trigger:** CampsiteViewUI Update loop, every 3-5s (random interval per check)
- **Target:** Random actively-growing plot (state == Growing, not harvested/empty)
- **Effect:** VFXToolkit.GlowPulse — green Color(0.3, 0.8, 0.2), alpha 0.10, fadeIn 50ms, hold 0ms, fadeOut 150ms
- **Constraint:** Only one shimmer active at a time (skip if previous hasn't faded)
- **Implementation:** CampsiteViewUI maintains a timer and picks a random growing plot

### Files changed
- `Assets/Scripts/UI/CampsiteLightingOverlay.cs` — add mote pool, smoke wisp pool, weather-responsive parameters
- `Assets/Scripts/Managers/FlameManager.cs` — add OnManaTick event with 1s throttle
- `Assets/Scripts/UI/CampsiteViewUI.cs` — subscribe to OnManaTick for flame pulse, add shimmer timer in Update

---

## Phase 4: Smooth Transitions

### Growth Stage Sprite Crossfade

**Pattern:** Two-layer crossfade using nested VisualElements inside each hex cell.

**Cell structure change (GridCell.uxml):**
```
hex-cell
  ├── cell-sprite-back   (background-image layer)
  ├── cell-sprite-front   (background-image layer, on top)
  ├── cell-icon           (existing icon overlay)
  └── cell-progress       (existing progress bar)
```

**Crossfade sequence (when growth crosses 50% or 100%):**
1. Set `cell-sprite-back` to the new sprite texture
2. Tween `cell-sprite-front` opacity 1→0 over 400ms (EaseOutQuad)
3. Simultaneously: ScaleBounce on the cell (peak 1.05, 300ms) to draw the eye
4. On complete: set `cell-sprite-front` to the new sprite, reset opacity to 1, clear `cell-sprite-back`

**Implementation:** CampsiteViewUI detects stage changes during sprite updates and triggers crossfade instead of instant swap.

### Resource Counter Number Tweens
- **Water/Gems:** On CurrencyManager.OnCurrencyChanged, call VFXToolkit.TweenNumber
- **Duration:** 300ms for small changes (< 20% of current), 600ms for large changes
- **Color flash:** During tween, label background-color briefly pulses to resource color (blue/purple) at 0.15 alpha, fading back over the tween duration
- **Mana:** Already updates per-frame (smooth by nature). No change needed.

### Weather Display Crossfade
- **Trigger:** WeatherService.OnWeatherUpdated
- **Effect:** WeatherBarUI content (icon, temperature, description) crossfades:
  - Old content opacity 1→0 (150ms)
  - New content opacity 0→1 (150ms, 100ms delay)
  - Temperature: NumberTween if only temp changed
- **Implementation:** WeatherBarUI wraps content in a crossfade container, same two-layer pattern as sprites

### Dialogue Box Animation
- **Open:** Container scale 0.9→1.0 + opacity 0→1 (250ms) via CSS `.dialogue--open` class
- **Portrait:** translate -20px→0 (200ms, 100ms delay)
- **Close:** scale 1.0→0.95 + opacity 1→0 (200ms), then display none
- **Implementation:** DialogueUI.cs adds/removes classes, registers TransitionEndEvent for close

### Tutorial Highlight Smoothing
- **Current:** Binary class toggle every 0.8s between highlight/dim states
- **Fix:** Single CSS class with transition on border-color (600ms ease-in-out) + border-width. TutorialUI.cs toggles a `.tutorial-pulse-on` / `.tutorial-pulse-off` pair where each has transition properties. The pulse becomes a smooth breathe.
- **Enhancement:** Add subtle scale oscillation (1.0↔1.02) synced to border pulse

### Files changed
- `Assets/Resources/UI/Templates/GridCell.uxml` — add second sprite layer element
- `Assets/Scripts/UI/CampsiteViewUI.cs` — crossfade logic on sprite stage detection
- `Assets/Scripts/UI/ResourceDisplayUI.cs` — NumberTween integration + color flash
- `Assets/Scripts/UI/WeatherBarUI.cs` — crossfade on weather update
- `Assets/Scripts/UI/DialogueUI.cs` — entrance/exit class toggling
- `Assets/Scripts/UI/TutorialUI.cs` — smooth pulse via CSS transitions
- `Assets/UI/Styles/Dialogue.uss` — transition properties
- `Assets/UI/Styles/Tutorial.uss` — smooth pulse transitions
- `Assets/UI/Styles/CampsiteGrid.uss` — two-layer sprite cell styles

---

## Phase 5: Reward Moments

### Tiered Celebration System

A shared function that takes a tier (1-4) and a target element, and plays the appropriate celebration:

**Tier 1 (Common):**
- Card scale-in (existing 350ms) + warm glow ring behind card (CSS box-shadow pulse, 0.2 alpha, 400ms)

**Tier 2 (Uncommon):**
- Card scale-in + green glow pulse (0.3 alpha, 450ms) + SpawnSparkles (6-8 particles, green, 400ms lifetime, 80px spread)

**Tier 3 (Rare):**
- Card scale-in with overshoot (peak 1.1, EaseOutBack) + blue glow ring expanding (CSS animation, 500ms) + SpawnSparkles (12-15 particles, blue, 500ms lifetime, 120px spread) + ScreenVignettePulse (blue, 0.1 alpha, 300ms)

**Tier 4 (Epic):**
- Card scale-in dramatic (peak 1.2, EaseOutElastic, 500ms) + golden shockwave ring (Painter2D, expanding over 600ms) + SpawnSparkles (20+ particles, gold, 600ms lifetime, 160px spread) + ScreenVignettePulse (gold, 0.15 alpha, 500ms) + ViewportMicroShake (3px amplitude, 300ms)

### Harvest Quality Celebration
- **Trigger:** Harvest reveal panel opens with quality score
- **Quality mapping:**
  - < 0.3: No extra celebration (basic reveal)
  - 0.3-0.7: Tier 1 treatment on the quality display element + warm glow on hex cell
  - 0.7-0.99: Tier 2 treatment + golden hex ripple (2 rings)
  - 1.0 (perfect): Tier 3 treatment + "Perfect Harvest!" text badge that ScaleBounces in (0→1.2→1, 400ms) above the quality score + 3-ring golden ripple on hex cell
- **Touchpoint:** CampsiteViewUI passes quality to harvest reveal; RewardRevealUI or a new HarvestRevealUI handles tier mapping

### Quest Reward Collection Enhancement
- **Existing behavior:** Staggered card reveals + fly-out to seeds button (keep all of this)
- **Enhancement:** Each card gets its tier-appropriate celebration (glow, sparkles) during the reveal stagger. The highest-tier card in the reward set determines the "overall" celebration:
  - If any card is tier 3+: ScreenVignettePulse plays once during reveal
  - If any card is tier 4: ViewportMicroShake plays once
- **Touchpoint:** RewardRevealUI.cs — look up tier per reward item, call tiered celebration per card

### Bird Seed Collection
- **Trigger:** BirdManager.OnBirdCollected
- **Effect on hex grid:** Bird cell gets a "feather burst" — SpawnSparkles (5-6 particles, warm white Color(1, 0.95, 0.85), 500ms lifetime, 60px spread) with low velocity and slight downward gravity (floaty, feather-like). Cell scale 1→0.9→0 over 300ms as bird "departs."
- **Reward popup:** Seeds appear near the bird cell location as a small tooltip-style popup with ScaleBounce entrance. Auto-dismisses after 2s.
- **Touchpoint:** CampsiteViewUI subscribes to OnBirdCollected

### Flame Level-Up Enhancement
- **Existing:** Full 6-stage celebration sequence (keep all of it)
- **Addition:** After level badge fades (at ~3.5s), briefly pulse relevant nav buttons to show what was unlocked:
  - If new entity cap: pulse Build button (ScaleBounce 1→1.15→1 + warm glow, 2 repetitions)
  - If new quest tier unlocked: pulse Quest button
  - Duration: each pulse cycle 500ms, 2 cycles, 200ms gap between button pulses
- **Touchpoint:** FlameLevelUpAnimator.cs — add post-celebration step

### Nav Button Notification Pulse
- **Trigger:** When seeds/items arrive in inventory from external source (quest rewards, bird collection, visitor gifts)
- **Effect:** Target nav button (Apotheke for seeds, etc.) gets ScaleBounce (1→1.15→1, 400ms) + warm golden glow behind (CSS background pulse). Repeats 2 times with 300ms gap.
- **Implementation:** BottomNavUI exposes `PulseButton(string buttonName, int repetitions)` method. Called by RewardRevealUI on collection complete, BirdManager on collect, VisitorUI on gift claim.

### Files changed
- `Assets/Scripts/Utils/VFXToolkit.cs` — SpawnSparkles, ScreenVignettePulse, ViewportMicroShake methods
- `Assets/Scripts/UI/RewardRevealUI.cs` — tier-specific celebrations per card
- `Assets/Scripts/UI/CampsiteViewUI.cs` — harvest quality ripple, bird feather burst
- `Assets/Scripts/UI/BottomNavUI.cs` — PulseButton method
- `Assets/Scripts/UI/FlameLevelUpAnimator.cs` — post-celebration capability pulse
- `Assets/UI/Styles/RewardReveal.uss` — tier-specific glow/sparkle container styles

---

## Implementation Order

1. **Phase 0: VFXToolkit** — foundation, no visible changes yet
2. **Phase 1: Interaction Juice** — highest player-facing impact, uses toolkit immediately
3. **Phase 4: Smooth Transitions** — eliminates visual jarring, pairs well with juice
4. **Phase 2: UI Panel Polish** — CSS-heavy, mostly independent of other phases
5. **Phase 3: Ambient Life** — extends CampsiteLightingOverlay, independent
6. **Phase 5: Reward Moments** — builds on all previous phases, most complex

Phases 2, 3, and 4 are independent of each other and could be parallelized after Phase 1. Phase 5 depends on the toolkit (Phase 0) and benefits from the transitions work (Phase 4) being done first.

---

## Testing Strategy

- **VFXToolkit:** Unit tests for easing functions (pure math, no MonoBehaviour). Verify EaseOutBack, EaseOutQuad, Smoothstep return expected values at t=0, t=0.5, t=1.
- **Interaction effects:** Manual testing in Unity Editor. Use debug weather override + GameTime acceleration to trigger growth stage changes rapidly. Plant/water/harvest cycle to verify all feedback.
- **Panel animations:** Manual verification — open/close each panel, verify entrance/exit animations play without visual glitches. Check TransitionEndEvent fires correctly (panels reach display:none after close).
- **Ambient effects:** Run game at night time (debug override) to verify motes and smoke wisps render on lightmap. Toggle weather states to verify weather-responsive behavior.
- **Reward moments:** Use admin tools to grant items of various tiers. Verify tier-appropriate celebrations fire. Test edge case: multiple tier-4 items in same reward set.

---

## Performance Considerations

- **VFXToolkit scheduling:** All animations use `VisualElement.schedule` which runs on UI thread. No per-frame Update() overhead when no animations are active.
- **Lightmap particles (motes/smoke):** CampsiteLightingOverlay already renders 7 fireflies + building lights per frame on a 256x256 texture. Adding 10-15 motes + 2-3 smoke wisps is negligible (same additive circle rendering).
- **SpawnSparkles:** Painter2D overlay only active during reward moments (brief, infrequent). Overlay element has `generateVisualContent` callback that only fires when marked dirty.
- **Two-layer crossfade:** Adds one extra VisualElement per hex cell. At max grid size (~60 cells), this is ~60 extra elements with no rendering cost when both layers show the same sprite (front at opacity 1 hides back).
- **Number tweens:** 16ms scheduled updates on 2-3 labels. Negligible.
