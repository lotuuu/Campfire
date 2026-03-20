# Weather VFX Overlay Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add rain, snow, and thunderstorm lightning visual effects to the campsite viewport.

**Architecture:** A single new `WeatherVFXOverlay` MonoBehaviour renders weather particles via `Painter2D` on a viewport-level `VisualElement`. It subscribes to `WeatherService.OnWeatherUpdated` and smoothly transitions between weather states. Storm reuses rain particles plus a separate lightning flash element.

**Tech Stack:** Unity 6 UI Toolkit, Painter2D, C#

**Spec:** `docs/superpowers/specs/2026-03-20-weather-vfx-design.md`

---

## File Structure

| File | Action | Responsibility |
|---|---|---|
| `Assets/Scripts/UI/WeatherVFXOverlay.cs` | Create | All weather particle simulation, rendering, and state transitions |
| `Assets/Scripts/UI/CampsiteViewUI.cs` | Modify (~line 209) | Initialize the new overlay alongside the lighting overlay |

---

### Task 1: Scaffold WeatherVFXOverlay with data structures and Initialize

Create the MonoBehaviour with particle/splash structs, pre-allocated lists, overlay element setup, and weather subscription. No rendering yet — just the skeleton.

**Files:**
- Create: `Assets/Scripts/UI/WeatherVFXOverlay.cs`

- [ ] **Step 1: Create WeatherVFXOverlay.cs with data structures and Initialize**

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class WeatherVFXOverlay : MonoBehaviour
    {
        // ── Particle types ──

        private enum ParticleType { RainStreak, RainDrop, SnowDot, SnowFlake }

        private struct WeatherParticle
        {
            public Vector2 position;
            public float speed;
            public float angle;
            public float alpha;
            public float size;
            public float swayPhase;
            public float swayFreq;
            public float swayAmplitude;
            public float rotation;
            public float rotationSpeed;
            public ParticleType type;
            public bool isForeground;
            public bool alive;
        }

        private struct Splash
        {
            public Vector2 position;
            public float age;
            public float lifetime;
            public bool alive;
        }

        // ── Configuration ──

        private const int MaxParticles = 120;
        private const int MaxSplashes = 10;
        private static readonly Color RainColor = new(0.71f, 0.78f, 1f);
        private static readonly Color SnowColor = new(0.90f, 0.92f, 1f);
        private static readonly Color LightningColor = new(0.78f, 0.82f, 1f);

        // Rain config
        private const float RainAngle = 10f * Mathf.Deg2Rad;
        private const float RainAngleVariance = 3f * Mathf.Deg2Rad;
        // Foreground rain
        private const float RainFgAlphaMin = 0.4f, RainFgAlphaMax = 0.55f;
        private const float RainFgSpeedMin = 800f, RainFgSpeedMax = 1000f;
        private const float RainFgStreakLenMin = 40f, RainFgStreakLenMax = 60f;
        private const float RainFgDropRx = 2.5f, RainFgDropRy = 7f;
        // Background rain
        private const float RainBgAlphaMin = 0.2f, RainBgAlphaMax = 0.35f;
        private const float RainBgSpeedMin = 500f, RainBgSpeedMax = 700f;
        private const float RainBgStreakLenMin = 25f, RainBgStreakLenMax = 40f;
        private const float RainBgDropRx = 1.5f, RainBgDropRy = 5f;
        // Splash
        private const float SplashMaxRadius = 6f;
        private const float SplashLifetime = 0.3f;
        private const float SplashStartAlpha = 0.3f;
        private const float SplashZoneRatio = 0.2f;

        // Snow config
        private const float SnowFgAlphaMin = 0.45f, SnowFgAlphaMax = 0.6f;
        private const float SnowBgAlphaMin = 0.25f, SnowBgAlphaMax = 0.4f;
        private const float SnowFgRadiusMin = 2f, SnowFgRadiusMax = 4f;
        private const float SnowBgRadiusMin = 1f, SnowBgRadiusMax = 3f;
        private const float SnowFgSpeedMin = 50f, SnowFgSpeedMax = 80f;
        private const float SnowBgSpeedMin = 40f, SnowBgSpeedMax = 60f;
        private const float SnowFlakeRadiusMin = 4f, SnowFlakeRadiusMax = 7f;
        private const float SnowFlakeChance = 0.15f;
        private const float SnowSwayFreqMin = 0.5f, SnowSwayFreqMax = 1.5f;
        private const float SnowSwayAmpMin = 10f, SnowSwayAmpMax = 25f;
        private const float SnowRotSpeedMin = 15f, SnowRotSpeedMax = 30f;
        private const float SnowFadeZoneRatio = 0.1f;

        // Lightning config
        private const float LightningFlashAlpha = 0.3f;
        private const float LightningPulseDuration = 0.15f;
        private const float LightningPulseGap = 0.1f;
        private const float LightningMinInterval = 4f;
        private const float LightningMaxInterval = 10f;
        private const float LightningClusterChance = 0.3f;
        private const float LightningClusterDelay = 1f;

        // Storm speed multiplier
        private const float StormSpeedMultiplier = 1.2f;

        // Particle counts per weather type
        private const int RainParticleCount = 80;
        private const int StormParticleCount = 100;
        private const int SnowParticleCount = 60;

        // Transition
        private const float TransitionDuration = 2f;

        // ── State ──

        private VisualElement viewport;
        private VisualElement particleOverlay;
        private VisualElement lightningOverlay;
        private readonly WeatherParticle[] particles = new WeatherParticle[MaxParticles];
        private readonly Splash[] splashes = new Splash[MaxSplashes];
        private int activeParticleCount;
        private int targetParticleCount;
        private WeatherCondition targetCondition = WeatherCondition.Clear;
        private float spawnAccumulator;
        private float viewportWidth, viewportHeight;
        private float elapsedTime;

        // Lightning state
        private float nextLightningTime;
        private int lightningPulsesRemaining;
        private float lightningPulseTimer;
        private bool lightningPulseOn;
        private bool lightningClusterPending;
        private float lightningClusterTimer;

        // ── Public API ──

        public void Initialize(VisualElement viewportElement)
        {
            viewport = viewportElement;

            // Particle overlay
            particleOverlay = new VisualElement();
            particleOverlay.name = "weather-vfx-overlay";
            particleOverlay.pickingMode = PickingMode.Ignore;
            particleOverlay.style.position = Position.Absolute;
            particleOverlay.style.left = 0;
            particleOverlay.style.top = 0;
            particleOverlay.style.right = 0;
            particleOverlay.style.bottom = 0;
            particleOverlay.style.display = DisplayStyle.None;
            particleOverlay.generateVisualContent += DrawParticles;
            viewport.Add(particleOverlay);

            // Lightning flash overlay
            lightningOverlay = new VisualElement();
            lightningOverlay.name = "weather-lightning-overlay";
            lightningOverlay.pickingMode = PickingMode.Ignore;
            lightningOverlay.style.position = Position.Absolute;
            lightningOverlay.style.left = 0;
            lightningOverlay.style.top = 0;
            lightningOverlay.style.right = 0;
            lightningOverlay.style.bottom = 0;
            lightningOverlay.style.backgroundColor = new Color(LightningColor.r, LightningColor.g, LightningColor.b, 0f);
            viewport.Add(lightningOverlay);

            // Subscribe to weather
            if (WeatherService.Instance != null)
            {
                WeatherService.Instance.OnWeatherUpdated += OnWeatherUpdated;
                if (WeatherService.Instance.HasWeather)
                    OnWeatherUpdated(WeatherService.Instance.CurrentWeather);
            }
        }

        private void OnDestroy()
        {
            if (WeatherService.Instance != null)
                WeatherService.Instance.OnWeatherUpdated -= OnWeatherUpdated;
        }

        private void OnWeatherUpdated(WeatherData weather)
        {
            targetCondition = weather.condition;
            targetParticleCount = weather.condition switch
            {
                WeatherCondition.Rain => RainParticleCount,
                WeatherCondition.Storm => StormParticleCount,
                WeatherCondition.Snow => SnowParticleCount,
                _ => 0
            };

            // Lightning stops immediately when leaving Storm
            if (weather.condition != WeatherCondition.Storm)
            {
                lightningPulsesRemaining = 0;
                lightningOverlay.style.backgroundColor =
                    new Color(LightningColor.r, LightningColor.g, LightningColor.b, 0f);
            }
            else
            {
                nextLightningTime = Random.Range(LightningMinInterval, LightningMaxInterval);
            }
        }

        // ── Placeholder methods for subsequent tasks ──

        private void Update() { }

        private void DrawParticles(MeshGenerationContext ctx) { }
    }
}
```

- [ ] **Step 2: Verify compilation**

Check `read_console` for compilation errors after saving. No runtime test needed yet — just ensure the file compiles.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/UI/WeatherVFXOverlay.cs
git commit -m "feat: scaffold WeatherVFXOverlay with data structures and Initialize"
```

---

### Task 2: Implement particle simulation in Update

Fill in the `Update()` method: spawn particles based on target count, simulate movement (rain falls, snow drifts), despawn at bottom, manage splashes, handle transitions. No rendering yet.

**Files:**
- Modify: `Assets/Scripts/UI/WeatherVFXOverlay.cs`

- [ ] **Step 1: Implement Update with particle spawning, simulation, despawn, and splash logic**

Replace the placeholder `Update()` with:

```csharp
private void Update()
{
    if (particleOverlay == null) return;

    float dt = Time.deltaTime;
    elapsedTime += dt;

    // Read viewport size
    float vw = viewport.resolvedStyle.width;
    float vh = viewport.resolvedStyle.height;
    if (!float.IsNaN(vw) && vw > 0) viewportWidth = vw;
    if (!float.IsNaN(vh) && vh > 0) viewportHeight = vh;
    if (viewportWidth <= 0 || viewportHeight <= 0) return;

    // Count alive particles
    activeParticleCount = 0;
    for (int i = 0; i < MaxParticles; i++)
        if (particles[i].alive) activeParticleCount++;

    // Spawn particles toward target
    bool isRain = targetCondition == WeatherCondition.Rain || targetCondition == WeatherCondition.Storm;
    bool isSnow = targetCondition == WeatherCondition.Snow;
    if (activeParticleCount < targetParticleCount && (isRain || isSnow))
    {
        float spawnRate = targetParticleCount / TransitionDuration;
        spawnAccumulator += spawnRate * dt;
        while (spawnAccumulator >= 1f && activeParticleCount < targetParticleCount)
        {
            SpawnParticle(isRain);
            spawnAccumulator -= 1f;
            activeParticleCount++;
        }
    }
    else
    {
        spawnAccumulator = 0f;
    }

    // Simulate particles
    for (int i = 0; i < MaxParticles; i++)
    {
        if (!particles[i].alive) continue;
        SimulateParticle(ref particles[i], dt);
    }

    // Simulate splashes
    for (int i = 0; i < MaxSplashes; i++)
    {
        if (!splashes[i].alive) continue;
        splashes[i].age += dt;
        if (splashes[i].age >= splashes[i].lifetime)
            splashes[i].alive = false;
    }

    // Lightning
    if (targetCondition == WeatherCondition.Storm)
        UpdateLightning(dt);

    // Show/hide overlay
    bool hasAnything = activeParticleCount > 0 || HasAliveSplashes();
    if (hasAnything)
    {
        if (particleOverlay.style.display == DisplayStyle.None)
            particleOverlay.style.display = DisplayStyle.Flex;
        particleOverlay.MarkDirtyRepaint();
    }
    else if (targetParticleCount == 0)
    {
        if (particleOverlay.style.display != DisplayStyle.None)
            particleOverlay.style.display = DisplayStyle.None;
    }
}

private void SpawnParticle(bool isRain)
{
    int slot = -1;
    for (int i = 0; i < MaxParticles; i++)
    {
        if (!particles[i].alive) { slot = i; break; }
    }
    if (slot < 0) return;

    bool isForeground = isRain ? Random.value < 0.4f : Random.value < 0.35f;

    if (isRain)
    {
        bool isStreak = Random.value < 0.6f;
        float speedMul = targetCondition == WeatherCondition.Storm ? StormSpeedMultiplier : 1f;
        particles[slot] = new WeatherParticle
        {
            position = new Vector2(Random.Range(0f, viewportWidth), Random.Range(-30f, -10f)),
            speed = (isForeground
                ? Random.Range(RainFgSpeedMin, RainFgSpeedMax)
                : Random.Range(RainBgSpeedMin, RainBgSpeedMax)) * speedMul,
            angle = RainAngle + Random.Range(-RainAngleVariance, RainAngleVariance),
            alpha = isForeground
                ? Random.Range(RainFgAlphaMin, RainFgAlphaMax)
                : Random.Range(RainBgAlphaMin, RainBgAlphaMax),
            size = isStreak
                ? (isForeground
                    ? Random.Range(RainFgStreakLenMin, RainFgStreakLenMax)
                    : Random.Range(RainBgStreakLenMin, RainBgStreakLenMax))
                : (isForeground ? RainFgDropRy : RainBgDropRy),
            type = isStreak ? ParticleType.RainStreak : ParticleType.RainDrop,
            isForeground = isForeground,
            alive = true
        };
    }
    else // snow
    {
        bool isFlake = Random.value < SnowFlakeChance;
        float radius = isFlake
            ? Random.Range(SnowFlakeRadiusMin, SnowFlakeRadiusMax)
            : (isForeground
                ? Random.Range(SnowFgRadiusMin, SnowFgRadiusMax)
                : Random.Range(SnowBgRadiusMin, SnowBgRadiusMax));
        particles[slot] = new WeatherParticle
        {
            position = new Vector2(Random.Range(0f, viewportWidth), Random.Range(-30f, -10f)),
            speed = isForeground
                ? Random.Range(SnowFgSpeedMin, SnowFgSpeedMax)
                : Random.Range(SnowBgSpeedMin, SnowBgSpeedMax),
            alpha = isForeground
                ? Random.Range(SnowFgAlphaMin, SnowFgAlphaMax)
                : Random.Range(SnowBgAlphaMin, SnowBgAlphaMax),
            size = radius,
            swayPhase = Random.Range(0f, Mathf.PI * 2f),
            swayFreq = Random.Range(SnowSwayFreqMin, SnowSwayFreqMax),
            swayAmplitude = Random.Range(SnowSwayAmpMin, SnowSwayAmpMax),
            rotation = Random.Range(0f, 360f),
            rotationSpeed = isFlake
                ? Random.Range(SnowRotSpeedMin, SnowRotSpeedMax) * (Random.value < 0.5f ? 1f : -1f)
                : 0f,
            type = isFlake ? ParticleType.SnowFlake : ParticleType.SnowDot,
            isForeground = isForeground,
            alive = true
        };
    }
}

private void SimulateParticle(ref WeatherParticle p, float dt)
{
    bool isRain = p.type == ParticleType.RainStreak || p.type == ParticleType.RainDrop;

    if (isRain)
    {
        p.position.x += Mathf.Sin(p.angle) * p.speed * dt;
        p.position.y += Mathf.Cos(p.angle) * p.speed * dt;

        // Despawn at bottom — splash zone
        if (p.position.y > viewportHeight * (1f - SplashZoneRatio))
        {
            TrySpawnSplash(p.position);
            p.alive = false;
        }
    }
    else // snow
    {
        p.position.x += Mathf.Sin(elapsedTime * p.swayFreq + p.swayPhase) * p.swayAmplitude * dt;
        p.position.y += p.speed * dt;
        p.rotation += p.rotationSpeed * dt;

        // Fade out near bottom
        float fadeStart = viewportHeight * (1f - SnowFadeZoneRatio);
        if (p.position.y > fadeStart)
        {
            float fadeProgress = (p.position.y - fadeStart) / (viewportHeight * SnowFadeZoneRatio);
            p.alpha = Mathf.Max(0f, p.alpha - fadeProgress * dt * 3f);
            if (p.alpha <= 0.01f)
                p.alive = false;
        }

        if (p.position.y > viewportHeight)
            p.alive = false;
    }

    // Off-screen horizontally
    if (p.position.x < -50f || p.position.x > viewportWidth + 50f)
        p.alive = false;
}

private void TrySpawnSplash(Vector2 position)
{
    for (int i = 0; i < MaxSplashes; i++)
    {
        if (!splashes[i].alive)
        {
            splashes[i] = new Splash
            {
                position = position,
                age = 0f,
                lifetime = SplashLifetime,
                alive = true
            };
            return;
        }
    }
}

private bool HasAliveSplashes()
{
    for (int i = 0; i < MaxSplashes; i++)
        if (splashes[i].alive) return true;
    return false;
}

private void UpdateLightning(float dt)
{
    // Active pulse sequence
    if (lightningPulsesRemaining > 0)
    {
        lightningPulseTimer -= dt;
        if (lightningPulseTimer <= 0f)
        {
            lightningPulseOn = !lightningPulseOn;
            if (lightningPulseOn)
            {
                lightningOverlay.style.backgroundColor =
                    new Color(LightningColor.r, LightningColor.g, LightningColor.b, LightningFlashAlpha);
                lightningPulseTimer = LightningPulseDuration;
            }
            else
            {
                lightningOverlay.style.backgroundColor =
                    new Color(LightningColor.r, LightningColor.g, LightningColor.b, 0f);
                lightningPulsesRemaining--;
                lightningPulseTimer = lightningPulsesRemaining > 0 ? LightningPulseGap : 0f;
            }
        }
        return;
    }

    // Cluster follow-up
    if (lightningClusterPending)
    {
        lightningClusterTimer -= dt;
        if (lightningClusterTimer <= 0f)
        {
            lightningClusterPending = false;
            StartLightningStrike();
        }
        return;
    }

    // Wait for next strike
    nextLightningTime -= dt;
    if (nextLightningTime <= 0f)
    {
        StartLightningStrike();
        nextLightningTime = Random.Range(LightningMinInterval, LightningMaxInterval);

        // Clustering
        if (Random.value < LightningClusterChance)
        {
            lightningClusterPending = true;
            lightningClusterTimer = Random.Range(0.3f, LightningClusterDelay);
        }
    }
}

private void StartLightningStrike()
{
    lightningPulsesRemaining = Random.Range(2, 4); // 2-3 pulses
    lightningPulseOn = true;
    lightningPulseTimer = LightningPulseDuration;
    lightningOverlay.style.backgroundColor =
        new Color(LightningColor.r, LightningColor.g, LightningColor.b, LightningFlashAlpha);
}
```

- [ ] **Step 2: Verify compilation**

Check `read_console` for compilation errors.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/UI/WeatherVFXOverlay.cs
git commit -m "feat: implement weather particle simulation and lightning logic"
```

---

### Task 3: Implement Painter2D rendering

Fill in `DrawParticles` to render rain streaks, rain drops, snow dots, snow flakes, and splash ripples using Painter2D.

**Files:**
- Modify: `Assets/Scripts/UI/WeatherVFXOverlay.cs`

- [ ] **Step 1: Implement DrawParticles with all particle type rendering**

Replace the placeholder `DrawParticles` with:

```csharp
private void DrawParticles(MeshGenerationContext ctx)
{
    var painter = ctx.painter2D;

    // Draw background particles first, then foreground
    for (int layer = 0; layer < 2; layer++)
    {
        bool drawForeground = layer == 1;
        for (int i = 0; i < MaxParticles; i++)
        {
            if (!particles[i].alive) continue;
            if (particles[i].isForeground != drawForeground) continue;
            DrawParticle(painter, ref particles[i]);
        }
    }

    // Draw splashes on top
    for (int i = 0; i < MaxSplashes; i++)
    {
        if (!splashes[i].alive) continue;
        DrawSplash(painter, ref splashes[i]);
    }
}

private void DrawParticle(Painter2D painter, ref WeatherParticle p)
{
    switch (p.type)
    {
        case ParticleType.RainStreak:
            DrawRainStreak(painter, ref p);
            break;
        case ParticleType.RainDrop:
            DrawRainDrop(painter, ref p);
            break;
        case ParticleType.SnowDot:
            DrawSnowDot(painter, ref p);
            break;
        case ParticleType.SnowFlake:
            DrawSnowFlake(painter, ref p);
            break;
    }
}

private void DrawRainStreak(Painter2D painter, ref WeatherParticle p)
{
    float dx = Mathf.Sin(p.angle) * p.size;
    float dy = Mathf.Cos(p.angle) * p.size;

    painter.BeginPath();
    painter.MoveTo(new Vector2(p.position.x, p.position.y));
    painter.LineTo(new Vector2(p.position.x + dx, p.position.y + dy));
    painter.strokeColor = new Color(RainColor.r, RainColor.g, RainColor.b, p.alpha);
    painter.lineWidth = p.isForeground ? 1.5f : 1f;
    painter.Stroke();
}

private void DrawRainDrop(Painter2D painter, ref WeatherParticle p)
{
    float rx = p.isForeground ? RainFgDropRx : RainBgDropRx;
    float ry = p.size; // size stores ry

    // Approximate elongated drop with an arc (circle) — Painter2D lacks ellipse,
    // so draw a small filled circle scaled by aspect. Use a circle with averaged radius.
    float avgR = (rx + ry) * 0.5f;
    painter.BeginPath();
    painter.Arc(new Vector2(p.position.x, p.position.y), avgR, 0f, 360f);
    painter.ClosePath();
    painter.fillColor = new Color(RainColor.r, RainColor.g, RainColor.b, p.alpha);
    painter.Fill();
}

private void DrawSnowDot(Painter2D painter, ref WeatherParticle p)
{
    painter.BeginPath();
    painter.Arc(new Vector2(p.position.x, p.position.y), p.size, 0f, 360f);
    painter.ClosePath();
    painter.fillColor = new Color(SnowColor.r, SnowColor.g, SnowColor.b, p.alpha);
    painter.Fill();
}

private void DrawSnowFlake(Painter2D painter, ref WeatherParticle p)
{
    float r = p.size;
    float rotRad = p.rotation * Mathf.Deg2Rad;

    // 3 crossed lines (6-pointed star)
    for (int arm = 0; arm < 3; arm++)
    {
        float armAngle = rotRad + arm * Mathf.PI / 3f;
        float cos = Mathf.Cos(armAngle);
        float sin = Mathf.Sin(armAngle);

        painter.BeginPath();
        painter.MoveTo(new Vector2(p.position.x - cos * r, p.position.y - sin * r));
        painter.LineTo(new Vector2(p.position.x + cos * r, p.position.y + sin * r));
        painter.strokeColor = new Color(SnowColor.r, SnowColor.g, SnowColor.b, p.alpha);
        painter.lineWidth = 1f;
        painter.Stroke();
    }
}

private void DrawSplash(Painter2D painter, ref Splash s)
{
    float t = s.age / s.lifetime;
    float radius = t * SplashMaxRadius;
    float alpha = SplashStartAlpha * (1f - t);

    painter.BeginPath();
    painter.Arc(new Vector2(s.position.x, s.position.y), radius, 0f, 360f);
    painter.ClosePath();
    painter.strokeColor = new Color(RainColor.r, RainColor.g, RainColor.b, alpha);
    painter.lineWidth = 0.8f;
    painter.Stroke();
}
```

- [ ] **Step 2: Verify compilation**

Check `read_console` for compilation errors.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/UI/WeatherVFXOverlay.cs
git commit -m "feat: implement Painter2D rendering for all weather particle types"
```

---

### Task 4: Integrate into CampsiteViewUI

Wire up `WeatherVFXOverlay` in `CampsiteViewUI.Initialize()` alongside the existing `CampsiteLightingOverlay` initialization.

**Files:**
- Modify: `Assets/Scripts/UI/CampsiteViewUI.cs` (~line 209)

- [ ] **Step 1: Add field and initialization**

In `CampsiteViewUI.cs`, add a field near the existing `lightingOverlay` field (around line 38):

```csharp
private WeatherVFXOverlay weatherVFXOverlay;
```

After the lighting overlay initialization block (after line 209), add:

```csharp
            // Initialize viewport-level weather VFX overlay (rain, snow, lightning)
            weatherVFXOverlay = GetComponent<WeatherVFXOverlay>();
            if (weatherVFXOverlay == null)
                weatherVFXOverlay = gameObject.AddComponent<WeatherVFXOverlay>();
            weatherVFXOverlay.Initialize(viewport);
```

- [ ] **Step 2: Verify compilation**

Check `read_console` for compilation errors.

- [ ] **Step 3: Test in Unity Editor**

Use `WeatherService.SetDebugWeather()` or the debug weather panel to set weather to Rain, Snow, and Storm. Verify:
- Rain: mixed streaks and drops falling diagonally, splashes at bottom
- Snow: dots drifting with sway, occasional crystalline flakes rotating
- Storm: denser rain + periodic white/blue screen flashes
- Clear/Cloudy: no particles visible
- Transitions between states smooth (particles fade in/out over ~2s)

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/UI/CampsiteViewUI.cs Assets/Scripts/UI/WeatherVFXOverlay.cs
git commit -m "feat: integrate WeatherVFXOverlay into campsite view"
```

---

### Task 5: Polish and edge cases

Handle edge cases: overlay cleanup on destroy, weather already active at startup, night-time interaction with lighting overlay.

**Files:**
- Modify: `Assets/Scripts/UI/WeatherVFXOverlay.cs`

- [ ] **Step 1: Add cleanup in OnDestroy**

Ensure `OnDestroy` also removes the overlay elements from the viewport:

```csharp
private void OnDestroy()
{
    if (WeatherService.Instance != null)
        WeatherService.Instance.OnWeatherUpdated -= OnWeatherUpdated;
    particleOverlay?.RemoveFromHierarchy();
    lightningOverlay?.RemoveFromHierarchy();
}
```

- [ ] **Step 2: Handle weather already active at startup**

In `Initialize()`, after subscribing to weather, if weather is already rain/snow/storm, we should seed some particles immediately rather than waiting 2 seconds for them to spawn gradually. After the `OnWeatherUpdated` call in `Initialize`, add:

```csharp
                // If weather is already active, pre-seed particles so they're visible immediately
                if (targetParticleCount > 0)
                    PreSeedParticles();
```

Add the `PreSeedParticles` method:

```csharp
private void PreSeedParticles()
{
    // Approximate viewport size from viewport layout (may not be resolved yet)
    float vw = viewport.resolvedStyle.width;
    float vh = viewport.resolvedStyle.height;
    if (float.IsNaN(vw) || vw <= 0) vw = 1080f; // reasonable mobile default
    if (float.IsNaN(vh) || vh <= 0) vh = 1920f;
    viewportWidth = vw;
    viewportHeight = vh;

    bool isRain = targetCondition == WeatherCondition.Rain || targetCondition == WeatherCondition.Storm;
    // SpawnParticle fills slots sequentially (all start dead), so slot i matches iteration i
    for (int i = 0; i < targetParticleCount && i < MaxParticles; i++)
    {
        SpawnParticle(isRain);
        // Scatter Y position throughout viewport instead of all at top
        particles[i].position.y = Random.Range(0f, viewportHeight);
    }
}
```

- [ ] **Step 3: Verify compilation and test**

Check `read_console`. Test:
- Start with weather set to Rain — particles should be visible immediately, not spawning from top
- Switch to Clear — particles drain naturally
- Switch to Storm — lightning flashes begin after 4-10 seconds

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/UI/WeatherVFXOverlay.cs
git commit -m "fix: handle cleanup, pre-seed particles when weather active at startup"
```
