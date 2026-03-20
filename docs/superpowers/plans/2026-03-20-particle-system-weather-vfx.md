# ParticleSystem Weather VFX Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace Painter2D weather particles with GPU-accelerated ParticleSystem rendering via RenderTexture displayed on a UI Toolkit VisualElement.

**Architecture:** Runtime-created Camera + ParticleSystems render weather effects to a RenderTexture. A VisualElement on the viewport displays the texture. Camera position tracks the canvas pan offset for perfect sync. Lightning flash remains a simple VisualElement opacity pulse.

**Tech Stack:** Unity 6 ParticleSystem, RenderTexture, UI Toolkit, C#

**Spec:** `docs/superpowers/specs/2026-03-20-particle-system-weather-vfx-design.md`

---

## File Structure

| File | Action | Responsibility |
|---|---|---|
| `Assets/Scripts/UI/WeatherVFXOverlay.cs` | Rewrite | Complete rewrite — ParticleSystem setup, RenderTexture management, camera sync, emission control, lightning flash |
| `Assets/Resources/VFX/raindrop.png` | Create | 32x32 soft white circle sprite with feathered alpha edges |
| `Assets/Resources/VFX/snowflake.png` | Create | 32x32 soft white circle sprite with feathered alpha edges |

---

### Task 1: Create sprite assets

Create the two particle sprite textures needed by the ParticleSystem renderers.

**Files:**
- Create: `Assets/Resources/VFX/raindrop.png`
- Create: `Assets/Resources/VFX/snowflake.png`

- [ ] **Step 1: Generate sprite textures via script**

Both sprites are identical: 32x32 white filled circles with alpha-feathered edges. Create them programmatically using a Unity editor script or at runtime. The simplest approach is to generate them in `WeatherVFXOverlay` at runtime if not found in Resources, but for cleanliness create them as actual assets.

Use the Unity MCP or a Bash script to create them. Alternatively, generate them in code during `Initialize()`:

```csharp
private static Texture2D CreateCircleTexture(int size)
{
    var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
    float center = size / 2f;
    float maxDist = center;
    for (int y = 0; y < size; y++)
    {
        for (int x = 0; x < size; x++)
        {
            float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
            float alpha = Mathf.Clamp01(1f - (dist / maxDist));
            alpha = alpha * alpha; // quadratic falloff for soft edges
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
        }
    }
    tex.Apply();
    tex.filterMode = FilterMode.Bilinear;
    return tex;
}
```

Since both sprites are identical soft circles, generate one texture and reuse it for both rain and snow materials. This avoids needing actual PNG files in Resources.

- [ ] **Step 2: Commit**

```bash
git add Assets/Scripts/UI/WeatherVFXOverlay.cs
git commit -m "feat: add runtime circle texture generation for weather particles"
```

---

### Task 2: Rewrite WeatherVFXOverlay — scaffold and Initialize

Gut the existing file and rebuild with: layer setup, RenderTexture creation, Camera setup, VisualElement for display, weather event subscription. No particle systems yet.

**Files:**
- Rewrite: `Assets/Scripts/UI/WeatherVFXOverlay.cs`

- [ ] **Step 1: Write the new WeatherVFXOverlay scaffold**

```csharp
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace Garden
{
    public class WeatherVFXOverlay : MonoBehaviour
    {
        // ── Configuration ──

        private const int VfxLayer = 6;
        private const string VfxLayerName = "WeatherVFX";

        // Lightning config (retained from previous implementation)
        private static readonly Color LightningColor = new(0.78f, 0.82f, 1f);
        private const float LightningFlashAlpha = 0.3f;
        private const float LightningPulseDuration = 0.15f;
        private const float LightningPulseGap = 0.1f;
        private const float LightningMinInterval = 4f;
        private const float LightningMaxInterval = 10f;
        private const float LightningClusterChance = 0.3f;
        private const float LightningClusterDelay = 1f;

        // Rain config
        private const float RainBaseEmission = 80f; // impacts/sec per viewport-area
        private const float StormEmissionMultiplier = 1.25f;

        // Snow config
        private const float SnowBaseEmission = 4f; // flakes/sec per viewport-width

        // ── State ──

        private VisualElement canvas;
        private VisualElement viewport;
        private VisualElement rtOverlay;       // displays the RenderTexture
        private VisualElement lightningOverlay;

        private RenderTexture renderTexture;
        private Camera vfxCamera;
        private GameObject vfxRoot;
        private ParticleSystem rainPS;
        private ParticleSystem snowPS;

        private float viewportWidth, viewportHeight;
        private float canvasW, canvasH;
        private WeatherCondition targetCondition = WeatherCondition.Clear;

        // Lightning state
        private float nextLightningTime;
        private int lightningPulsesRemaining;
        private float lightningPulseTimer;
        private bool lightningPulseOn;
        private bool lightningClusterPending;
        private float lightningClusterTimer;

        // ── Texture generation ──

        private Texture2D circleTexture;
        private Material particleMaterial;

        private Texture2D CreateCircleTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = size / 2f;
            float maxDist = center;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    float alpha = Mathf.Clamp01(1f - (dist / maxDist));
                    alpha *= alpha; // quadratic falloff for soft edges
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        // ── Public API ──

        public void Initialize(VisualElement canvasElement)
        {
            canvas = canvasElement;
            viewport = canvas.parent;

            // Generate shared particle texture and material
            circleTexture = CreateCircleTexture(32);
            particleMaterial = new Material(Shader.Find("Particles/Standard Unlit"));
            particleMaterial.SetTexture("_MainTex", circleTexture);
            particleMaterial.SetFloat("_Mode", 0); // Additive=0? Actually for alpha: _Mode doesn't exist on this shader
            // Configure for alpha blending
            particleMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            particleMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            particleMaterial.renderQueue = 3000;

            // Set up VFX layer
            SetupLayer();

            // Create RenderTexture
            float dpiScale = Mathf.Clamp(Screen.dpi / 96f, 1f, 3f);
            if (dpiScale <= 0) dpiScale = 1f;
            float vw = viewport.resolvedStyle.width;
            float vh = viewport.resolvedStyle.height;
            if (float.IsNaN(vw) || vw <= 0) vw = 540f;
            if (float.IsNaN(vh) || vh <= 0) vh = 960f;
            viewportWidth = vw;
            viewportHeight = vh;
            int rtW = Mathf.Max(1, (int)(vw * dpiScale));
            int rtH = Mathf.Max(1, (int)(vh * dpiScale));
            renderTexture = new RenderTexture(rtW, rtH, 0, RenderTextureFormat.ARGB32);
            renderTexture.Create();

            // Read initial canvas dimensions
            float cw = canvas.resolvedStyle.width;
            float ch = canvas.resolvedStyle.height;
            if (!float.IsNaN(cw) && cw > 0) canvasW = cw;
            if (!float.IsNaN(ch) && ch > 0) canvasH = ch;
            if (canvasW <= 0) canvasW = viewportWidth;
            if (canvasH <= 0) canvasH = viewportHeight;

            // Create camera
            CreateVFXCamera();

            // RenderTexture display element on viewport
            rtOverlay = new VisualElement();
            rtOverlay.name = "weather-vfx-overlay";
            rtOverlay.pickingMode = PickingMode.Ignore;
            rtOverlay.style.position = Position.Absolute;
            rtOverlay.style.left = 0;
            rtOverlay.style.top = 0;
            rtOverlay.style.right = 0;
            rtOverlay.style.bottom = 0;
            rtOverlay.style.backgroundImage = Background.FromRenderTexture(renderTexture);
            viewport.Add(rtOverlay);

            // Lightning flash overlay (same as before)
            lightningOverlay = new VisualElement();
            lightningOverlay.name = "weather-lightning-overlay";
            lightningOverlay.pickingMode = PickingMode.Ignore;
            lightningOverlay.style.position = Position.Absolute;
            lightningOverlay.style.left = 0;
            lightningOverlay.style.top = 0;
            lightningOverlay.style.right = 0;
            lightningOverlay.style.bottom = 0;
            lightningOverlay.style.backgroundColor =
                new Color(LightningColor.r, LightningColor.g, LightningColor.b, 0f);
            viewport.Add(lightningOverlay);

            // Subscribe to weather
            if (WeatherService.Instance != null)
            {
                WeatherService.Instance.OnWeatherUpdated += OnWeatherUpdated;
                if (WeatherService.Instance.HasWeather)
                    OnWeatherUpdated(WeatherService.Instance.CurrentWeather);
            }
        }

        private void SetupLayer()
        {
            // Layer 6 is used for weather VFX — ensure it exists
            // Note: layer names can't be set at runtime without SerializedObject,
            // so layer 6 must be named "WeatherVFX" in Project Settings > Tags and Layers.
            // The code assigns GameObjects to layer 6 regardless of its name.
        }

        private void CreateVFXCamera()
        {
            vfxRoot = new GameObject("WeatherVFX");
            vfxRoot.layer = VfxLayer;

            var camGO = new GameObject("WeatherVFXCamera");
            camGO.transform.SetParent(vfxRoot.transform);
            camGO.layer = VfxLayer;

            vfxCamera = camGO.AddComponent<Camera>();
            vfxCamera.orthographic = true;
            vfxCamera.orthographicSize = canvasH / 2f;
            vfxCamera.nearClipPlane = 0.1f;
            vfxCamera.farClipPlane = 100f;
            vfxCamera.depth = -10;
            vfxCamera.clearFlags = CameraClearFlags.SolidColor;
            vfxCamera.backgroundColor = Color.clear;
            vfxCamera.cullingMask = 1 << VfxLayer;
            vfxCamera.targetTexture = renderTexture;
            vfxCamera.aspect = viewportWidth / viewportHeight;

            // Position at canvas center initially
            var translate = canvas.resolvedStyle.translate;
            float cx = canvasW / 2f + translate.x;
            float cy = -(canvasH / 2f + translate.y);
            vfxCamera.transform.position = new Vector3(cx, cy, -10f);
        }

        private void OnDestroy()
        {
            if (WeatherService.Instance != null)
                WeatherService.Instance.OnWeatherUpdated -= OnWeatherUpdated;
            rtOverlay?.RemoveFromHierarchy();
            lightningOverlay?.RemoveFromHierarchy();
            if (renderTexture != null)
            {
                renderTexture.Release();
                Destroy(renderTexture);
            }
            if (vfxRoot != null)
                Destroy(vfxRoot);
            if (circleTexture != null)
                Destroy(circleTexture);
            if (particleMaterial != null)
                Destroy(particleMaterial);
        }

        private void OnWeatherUpdated(WeatherData weather)
        {
            targetCondition = weather.condition;
            UpdateEmission();

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

        private void UpdateEmission()
        {
            // Implemented in Task 3
        }

        private void Update()
        {
            if (vfxCamera == null) return;

            // Read viewport/canvas dimensions
            float vw = viewport.resolvedStyle.width;
            float vh = viewport.resolvedStyle.height;
            if (!float.IsNaN(vw) && vw > 0) viewportWidth = vw;
            if (!float.IsNaN(vh) && vh > 0) viewportHeight = vh;
            float cw = canvas.resolvedStyle.width;
            float ch = canvas.resolvedStyle.height;
            if (!float.IsNaN(cw) && cw > 0) canvasW = cw;
            if (!float.IsNaN(ch) && ch > 0) canvasH = ch;

            // Sync camera with canvas pan
            var translate = canvas.resolvedStyle.translate;
            float cx = canvasW / 2f + translate.x;
            float cy = -(canvasH / 2f + translate.y);
            vfxCamera.transform.position = new Vector3(cx, cy, -10f);
            vfxCamera.orthographicSize = viewportHeight / 2f;
            vfxCamera.aspect = viewportWidth / viewportHeight;

            // Lightning
            if (targetCondition == WeatherCondition.Storm)
                UpdateLightning(Time.deltaTime);
        }

        // ── Lightning (retained from previous implementation) ──

        private void UpdateLightning(float dt)
        {
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

            nextLightningTime -= dt;
            if (nextLightningTime <= 0f)
            {
                StartLightningStrike();
                nextLightningTime = Random.Range(LightningMinInterval, LightningMaxInterval);

                if (Random.value < LightningClusterChance)
                {
                    lightningClusterPending = true;
                    lightningClusterTimer = Random.Range(0.3f, LightningClusterDelay);
                }
            }
        }

        private void StartLightningStrike()
        {
            lightningPulsesRemaining = Random.Range(2, 4);
            lightningPulseOn = true;
            lightningPulseTimer = LightningPulseDuration;
            lightningOverlay.style.backgroundColor =
                new Color(LightningColor.r, LightningColor.g, LightningColor.b, LightningFlashAlpha);
        }
    }
}
```

- [ ] **Step 2: Verify compilation**

Check `read_console` for errors. The file should compile with placeholder `UpdateEmission()`.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/UI/WeatherVFXOverlay.cs
git commit -m "feat: rewrite WeatherVFXOverlay with ParticleSystem camera and RenderTexture scaffold"
```

---

### Task 3: Add Rain and Snow ParticleSystems

Create both ParticleSystems at runtime with all configuration from the spec. Implement `UpdateEmission()` to control them based on weather state.

**Files:**
- Modify: `Assets/Scripts/UI/WeatherVFXOverlay.cs`

- [ ] **Step 1: Add CreateRainPS and CreateSnowPS methods, implement UpdateEmission**

Add after `CreateVFXCamera()`:

```csharp
        private void CreateParticleSystems()
        {
            CreateRainPS();
            CreateSnowPS();
        }

        private void CreateRainPS()
        {
            var go = new GameObject("RainParticles");
            go.transform.SetParent(vfxRoot.transform);
            go.layer = VfxLayer;
            // Position at canvas center in world space
            go.transform.position = new Vector3(canvasW / 2f, -canvasH / 2f, 0f);

            rainPS = go.AddComponent<ParticleSystem>();
            var main = rainPS.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.7f);
            main.startSize = new ParticleSystem.MinMaxCurve(6f, 14f);
            main.startSpeed = 0f;
            main.startColor = new Color(0.71f, 0.78f, 1f, 0.65f);
            main.maxParticles = 1000;
            main.playOnAwake = false;
            main.loop = true;

            // Shape: box covering entire canvas (in world coords, Y is flipped)
            var shape = rainPS.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(canvasW, canvasH, 1f);

            // Emission: off initially
            var emission = rainPS.emission;
            emission.rateOverTime = 0f;

            // Size over lifetime: expand from 0.2 to 1.0
            var sol = rainPS.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.2f),
                new Keyframe(1f, 1f)));

            // Color over lifetime: fade out alpha
            var col = rainPS.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = gradient;

            // Renderer
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.material = particleMaterial;
            renderer.sortingOrder = 100;
        }

        private void CreateSnowPS()
        {
            var go = new GameObject("SnowParticles");
            go.transform.SetParent(vfxRoot.transform);
            go.layer = VfxLayer;
            // Position at top-center of canvas (Y=0 in world = top of canvas)
            go.transform.position = new Vector3(canvasW / 2f, 0f, 0f);

            snowPS = go.AddComponent<ParticleSystem>();
            var main = snowPS.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            float avgSpeed = 60f;
            main.startLifetime = canvasH / avgSpeed;
            main.startSize = new ParticleSystem.MinMaxCurve(6f, 12f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(40f, 80f);
            main.startColor = new Color(0.90f, 0.92f, 1f, 0.55f);
            main.maxParticles = 1000;
            main.playOnAwake = false;
            main.loop = true;
            main.gravityModifier = 0f;
            // Rotate the system so "forward" (default emit direction) points down in world (-Y)
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            // Shape: edge spanning canvas width
            var shape = snowPS.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.SingleSidedEdge;
            shape.radius = canvasW / 2f;

            // Emission: off initially
            var emission = snowPS.emission;
            emission.rateOverTime = 0f;

            // Noise for horizontal sway
            var noise = snowPS.noise;
            noise.enabled = true;
            noise.strength = new ParticleSystem.MinMaxCurve(15f);
            noise.frequency = 1f;
            noise.scrollSpeed = 0.1f;
            noise.octaveCount = 2;
            noise.separateAxes = true;
            noise.strengthX = new ParticleSystem.MinMaxCurve(15f);
            noise.strengthY = new ParticleSystem.MinMaxCurve(0f);
            noise.strengthZ = new ParticleSystem.MinMaxCurve(0f);

            // Rotation over lifetime
            var rot = snowPS.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(
                15f * Mathf.Deg2Rad, 30f * Mathf.Deg2Rad);

            // Color over lifetime: fade in at start, fade out at end
            var col = snowPS.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.05f),
                    new GradientAlphaKey(1f, 0.9f),
                    new GradientAlphaKey(0f, 1f)
                });
            col.color = gradient;

            // Renderer
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.material = particleMaterial;
            renderer.sortingOrder = 100;
        }
```

Replace the placeholder `UpdateEmission`:

```csharp
        private void UpdateEmission()
        {
            if (rainPS == null || snowPS == null) return;

            float vpArea = Mathf.Max(viewportWidth * viewportHeight, 1f);
            float cvArea = Mathf.Max(canvasW * canvasH, vpArea);
            float areaRatio = cvArea / vpArea;
            float widthRatio = Mathf.Max(canvasW / Mathf.Max(viewportWidth, 1f), 1f);

            var rainEmission = rainPS.emission;
            var snowEmission = snowPS.emission;
            var rainMain = rainPS.main;

            switch (targetCondition)
            {
                case WeatherCondition.Rain:
                    rainEmission.rateOverTime = areaRatio * RainBaseEmission;
                    rainMain.startSize = new ParticleSystem.MinMaxCurve(6f, 14f);
                    snowEmission.rateOverTime = 0f;
                    if (!rainPS.isPlaying) rainPS.Play();
                    break;
                case WeatherCondition.Storm:
                    rainEmission.rateOverTime = areaRatio * RainBaseEmission * StormEmissionMultiplier;
                    rainMain.startSize = new ParticleSystem.MinMaxCurve(8f, 18f);
                    snowEmission.rateOverTime = 0f;
                    if (!rainPS.isPlaying) rainPS.Play();
                    break;
                case WeatherCondition.Snow:
                    rainEmission.rateOverTime = 0f;
                    snowEmission.rateOverTime = widthRatio * SnowBaseEmission;
                    if (!snowPS.isPlaying) snowPS.Play();
                    // Pre-seed if no particles alive
                    if (snowPS.particleCount == 0)
                    {
                        float simTime = canvasH / 60f;
                        snowPS.Simulate(simTime, true, true);
                        snowPS.Play();
                    }
                    break;
                default:
                    rainEmission.rateOverTime = 0f;
                    snowEmission.rateOverTime = 0f;
                    break;
            }
        }
```

Call `CreateParticleSystems()` at the end of `Initialize()`, after `CreateVFXCamera()` and before the weather subscription:

```csharp
            CreateVFXCamera();
            CreateParticleSystems();
```

- [ ] **Step 2: Verify compilation**

Check `read_console` for errors.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/UI/WeatherVFXOverlay.cs
git commit -m "feat: add rain and snow ParticleSystems with emission control"
```

---

### Task 4: Set up WeatherVFX layer and test

The ParticleSystem needs layer 6 to be named "WeatherVFX" in Project Settings so the camera culling works. Set it up and verify the full pipeline renders.

**Files:**
- Modify: `ProjectSettings/TagManager.asset` (via Unity MCP or manual edit)

- [ ] **Step 1: Add WeatherVFX layer**

Use the Unity MCP `manage_editor` tool or manually edit `ProjectSettings/TagManager.asset` to set layer 6 name to "WeatherVFX". Also ensure the main camera (if any) excludes layer 6 from its culling mask.

- [ ] **Step 2: Test in Unity Editor**

Enter Play mode with weather set to Rain, Snow, and Storm via the debug weather panel:
- Rain: expanding circles appearing across the canvas, fading out
- Snow: flakes drifting down with sway, fading in/out
- Storm: denser rain + lightning flashes
- Pan the camera — particles should stay anchored to the grid
- Switch weather types — smooth transitions (old particles fade naturally)

- [ ] **Step 3: Commit**

```bash
git add ProjectSettings/TagManager.asset Assets/Scripts/UI/WeatherVFXOverlay.cs
git commit -m "feat: configure WeatherVFX layer and verify particle rendering pipeline"
```

---

### Task 5: Polish — RenderTexture resize, canvas dimension updates

Handle edge cases: viewport resize recreates the RenderTexture, canvas dimension changes update particle system shapes and camera size.

**Files:**
- Modify: `Assets/Scripts/UI/WeatherVFXOverlay.cs`

- [ ] **Step 1: Add RenderTexture resize check and canvas update in Update**

Add to `Update()` after the dimension reads:

```csharp
            // Recreate RT if viewport size changed significantly
            if (renderTexture != null)
            {
                float dpiScale = Mathf.Clamp(Screen.dpi / 96f, 1f, 3f);
                if (dpiScale <= 0) dpiScale = 1f;
                int targetW = Mathf.Max(1, (int)(viewportWidth * dpiScale));
                int targetH = Mathf.Max(1, (int)(viewportHeight * dpiScale));
                if (Mathf.Abs(renderTexture.width - targetW) > targetW * 0.1f ||
                    Mathf.Abs(renderTexture.height - targetH) > targetH * 0.1f)
                {
                    renderTexture.Release();
                    renderTexture.width = targetW;
                    renderTexture.height = targetH;
                    renderTexture.Create();
                    vfxCamera.targetTexture = renderTexture;
                    rtOverlay.style.backgroundImage = Background.FromRenderTexture(renderTexture);
                }
            }

            // Update particle shapes if canvas size changed (grid expansion)
            UpdateParticleShapes();
```

Add the shape update method:

```csharp
        private float lastCanvasW, lastCanvasH;

        private void UpdateParticleShapes()
        {
            if (Mathf.Approximately(canvasW, lastCanvasW) && Mathf.Approximately(canvasH, lastCanvasH))
                return;
            lastCanvasW = canvasW;
            lastCanvasH = canvasH;

            if (rainPS != null)
            {
                var shape = rainPS.shape;
                shape.scale = new Vector3(canvasW, canvasH, 1f);
                rainPS.transform.position = new Vector3(canvasW / 2f, -canvasH / 2f, 0f);
            }
            if (snowPS != null)
            {
                var shape = snowPS.shape;
                shape.radius = canvasW / 2f;
                snowPS.transform.position = new Vector3(canvasW / 2f, 0f, 0f);

                var main = snowPS.main;
                main.startLifetime = canvasH / 60f;
            }

            UpdateEmission(); // recalculate rates for new canvas size
        }
```

- [ ] **Step 2: Verify compilation and test**

Check `read_console`. Test grid expansion (flame level up) — particles should adapt to the larger canvas.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/UI/WeatherVFXOverlay.cs
git commit -m "fix: handle viewport resize and canvas dimension changes for weather VFX"
```
