using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    /// <summary>
    /// Canvas-level 2D lighting overlay rendered via a procedural Texture2D.
    /// Renders night darkness + radial light sources (fire, fireflies).
    /// Sits on top of all hex cells in the campsite canvas.
    /// </summary>
    public class CampsiteLightingOverlay : MonoBehaviour
    {
        // ── Light source data ──

        public struct LightSource
        {
            public Vector2 position;   // normalized 0–1 within canvas
            public Color color;
            public float radius;       // normalized 0–1
            public float intensity;    // 0–1
        }

        // ── Configuration ──

        private const int TexSize = 256;               // lightmap resolution
        private const float FireBaseRadius = 0.45f;     // normalized (~4-5 hex rings)
        private const float FireBaseIntensity = 1f;
        private const int MaxFireflies = 7;
        private const float FireflyRadius = 0.04f;      // normalized
        private const float FireflyDriftSpeed = 0.01f;  // normalized/sec
        private const float FireflyFadeDuration = 2f;
        private const float FireflyLifetime = 6f;
        private const float FireflySpawnInterval = 1.2f;

        // ── State ──

        private VisualElement overlay;
        private VisualElement canvas;
        private Texture2D lightmap;
        private Color32[] pixels;
        private float overlayAlpha;         // current strength of the overlay (lerped)
        private float targetOverlayAlpha;
        private Color overlayBaseColor;     // current base color (lerped)
        private Color targetBaseColor;
        private float targetBaseAlpha;      // max darkness alpha at full strength
        private float baseAlpha;
        private float fireflyAlpha;
        private float targetFireflyAlpha;
        private bool fireGlowActive;       // whether fire punches a hole in the overlay
        private bool targetFireGlow;
        private Vector2 firePositionNorm;  // normalized 0–1
        private Vector2 firePositionCanvas; // raw canvas coords (for OnGridRebuilt)
        private float canvasWidth, canvasHeight;
        private float firePhase;
        private float fireFlickerRadius;   // current animated radius
        private float fireFlickerIntensity; // current animated intensity
        private Color fireFlickerColor;     // current animated color
        private float nextFlareTime;        // countdown to next bright flare
        private float flareStrength;        // current flare (0 = none, 1 = full)
        private readonly List<Firefly> fireflies = new();
        private float nextFireflySpawn;

        private struct Firefly
        {
            public Vector2 position;   // normalized 0–1
            public Vector2 velocity;   // normalized/sec
            public float age;
            public float lifetime;
            public Color color;
        }

        // ── Public API ──

        private VisualElement viewport;

        public void Initialize(VisualElement canvasElement)
        {
            canvas = canvasElement;
            viewport = canvas.parent;

            firePhase = Random.Range(0f, 100f); // randomize so fire doesn't sync with buildings
            lightmap = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            pixels = new Color32[TexSize * TexSize];
            ClearLightmap(); // start fully transparent

            overlay = new VisualElement();
            overlay.name = "lighting-overlay";
            overlay.pickingMode = PickingMode.Ignore;
            overlay.style.position = Position.Absolute;
            overlay.style.left = 0;
            overlay.style.top = 0;
            overlay.style.right = 0;
            overlay.style.bottom = 0;
            overlay.style.backgroundImage = lightmap;
            overlay.style.display = DisplayStyle.None; // hidden until needed

            canvas.Add(overlay);

            if (WeatherService.Instance != null)
            {
                WeatherService.Instance.OnWeatherUpdated += OnWeatherUpdated;
                if (WeatherService.Instance.HasWeather)
                {
                    UpdateTargets(WeatherService.Instance.CurrentWeather);
                    overlayAlpha = targetOverlayAlpha;
                    baseAlpha = targetBaseAlpha;
                    overlayBaseColor = targetBaseColor;
                    fireflyAlpha = targetFireflyAlpha;
                }
            }
        }

        /// <summary>Call after RebuildGrid. Pass building positions for small light sources.</summary>
        public void OnGridRebuilt(Vector2 flameCenterCanvas, List<Vector2> buildingLights = null)
        {
            firePositionCanvas = flameCenterCanvas;
            buildingLightPositions = buildingLights ?? new List<Vector2>();
            buildingPhaseOffsets = new List<float>(buildingLightPositions.Count);
            for (int i = 0; i < buildingLightPositions.Count; i++)
                buildingPhaseOffsets.Add(Random.Range(0f, 100f));

            if (overlay != null && canvas != null)
            {
                overlay.RemoveFromHierarchy();
                canvas.Add(overlay);
            }
        }

        private List<Vector2> buildingLightPositions = new();
        private List<float> buildingPhaseOffsets = new();
        private const float BuildingLightRadius = 0.15f; // small warm glow

        private void OnDestroy()
        {
            if (WeatherService.Instance != null)
                WeatherService.Instance.OnWeatherUpdated -= OnWeatherUpdated;
            if (lightmap != null)
                Destroy(lightmap);
        }

        // ── Weather ──

        private void OnWeatherUpdated(WeatherData w) => UpdateTargets(w);

        private void UpdateTargets(WeatherData w)
        {
            // Time-of-day determines overlay strength and fire behavior
            if (w.isNight)
            {
                targetBaseColor = new Color(0.01f, 0.02f, 0.08f);
                targetBaseAlpha = 0.92f;
                targetOverlayAlpha = 1f;
                targetFireflyAlpha = 1f;
                targetFireGlow = true;
            }
            else if (w.isGoldenHour)
            {
                targetBaseColor = new Color(0.6f, 0.3f, 0.05f);
                targetBaseAlpha = 0.18f;
                targetOverlayAlpha = 1f;
                targetFireflyAlpha = 0.3f;
                targetFireGlow = false;
            }
            else
            {
                targetOverlayAlpha = 0f;
                targetFireflyAlpha = 0f;
                targetFireGlow = false;
            }

            // Weather condition modifies the base color (composited on top of time-of-day)
            if (!w.isNight && !w.isGoldenHour)
            {
                switch (w.condition)
                {
                    case WeatherCondition.Rain:
                    case WeatherCondition.Storm:
                        targetBaseColor = new Color(0.15f, 0.25f, 0.45f);
                        targetBaseAlpha = 0.2f;
                        targetOverlayAlpha = 1f;
                        break;
                    case WeatherCondition.Snow:
                        targetBaseColor = new Color(0.7f, 0.75f, 0.82f);
                        targetBaseAlpha = 0.25f;
                        targetOverlayAlpha = 1f;
                        break;
                    case WeatherCondition.Cloudy:
                        targetBaseColor = new Color(0.25f, 0.25f, 0.3f);
                        targetBaseAlpha = 0.1f;
                        targetOverlayAlpha = 1f;
                        break;
                    default: // Clear day — no overlay
                        break;
                }
            }
        }

        // ── Per-frame update ──

        private void Update()
        {
            if (overlay == null || lightmap == null) return;

            bool dirty = false;
            float lerpSpeed = 2f * Time.deltaTime;

            if (Mathf.Abs(overlayAlpha - targetOverlayAlpha) > 0.001f)
            {
                overlayAlpha = Mathf.MoveTowards(overlayAlpha, targetOverlayAlpha, lerpSpeed);
                dirty = true;
            }
            if (Mathf.Abs(baseAlpha - targetBaseAlpha) > 0.001f)
            {
                baseAlpha = Mathf.MoveTowards(baseAlpha, targetBaseAlpha, lerpSpeed);
                dirty = true;
            }
            if (overlayBaseColor != targetBaseColor)
            {
                overlayBaseColor = new Color(
                    Mathf.MoveTowards(overlayBaseColor.r, targetBaseColor.r, lerpSpeed),
                    Mathf.MoveTowards(overlayBaseColor.g, targetBaseColor.g, lerpSpeed),
                    Mathf.MoveTowards(overlayBaseColor.b, targetBaseColor.b, lerpSpeed));
                dirty = true;
            }
            if (Mathf.Abs(fireflyAlpha - targetFireflyAlpha) > 0.001f)
            {
                fireflyAlpha = Mathf.MoveTowards(fireflyAlpha, targetFireflyAlpha, lerpSpeed);
                dirty = true;
            }
            fireGlowActive = targetFireGlow || fireGlowActive; // stays on during fade-out

            // Tint viewport + canvas background to match night darkness (covers area beyond grid)
            if (dirty)
            {
                float vpAlpha = baseAlpha * overlayAlpha;
                Color bgColor = vpAlpha > 0.001f
                    ? new Color(overlayBaseColor.r, overlayBaseColor.g, overlayBaseColor.b, vpAlpha)
                    : Color.clear;
                if (viewport != null)
                    viewport.style.backgroundColor = vpAlpha > 0.001f ? (StyleColor)bgColor : StyleKeyword.Null;
                if (canvas != null)
                    canvas.style.backgroundColor = vpAlpha > 0.001f ? (StyleColor)bgColor : StyleKeyword.Null;
            }

            // Show/hide overlay
            if (overlayAlpha > 0.001f)
            {
                if (overlay.style.display == DisplayStyle.None)
                    overlay.style.display = DisplayStyle.Flex;
                if (fireGlowActive)
                {
                    firePhase += Time.deltaTime;
                    UpdateFireFlicker(Time.deltaTime);
                }
                dirty = true;
                UpdateFireflies(Time.deltaTime);
            }
            else
            {
                fireflies.Clear();
                fireGlowActive = false;
                if (dirty)
                {
                    ClearLightmap();
                    overlay.style.display = DisplayStyle.None;
                }
                return;
            }

            // Update normalized fire position from canvas dimensions
            float cw = canvas.resolvedStyle.width;
            float ch = canvas.resolvedStyle.height;
            if (!float.IsNaN(cw) && cw > 0 && !float.IsNaN(ch) && ch > 0)
            {
                canvasWidth = cw;
                canvasHeight = ch;
                firePositionNorm = new Vector2(
                    firePositionCanvas.x / cw,
                    firePositionCanvas.y / ch);
            }

            if (dirty && canvasWidth > 0)
                RenderLightmap();
        }

        // ── Fire flicker ──

        private void UpdateFireFlicker(float dt)
        {
            float t = firePhase;

            // Layered radius flicker — fast and noticeable
            float slow = Mathf.Sin(t * 1.2f) * 0.06f;                      // ~5s breathing
            float medium = Mathf.Sin(t * 3.5f + 0.8f) * 0.04f;             // ~1.8s wobble
            float fast = Mathf.Sin(t * 8f + 2.1f) * 0.02f;                 // ~0.8s flicker
            float noise = (Mathf.PerlinNoise(t * 4f, 0f) - 0.5f) * 0.06f;  // organic noise

            fireFlickerRadius = FireBaseRadius * (1f + slow + medium + fast + noise);

            // Intensity flicker — larger swings
            float iSlow = Mathf.Sin(t * 1.5f + 1.3f) * 0.1f;
            float iFast = Mathf.Sin(t * 6f + 0.5f) * 0.08f;
            float iNoise = (Mathf.PerlinNoise(t * 5f, 5f) - 0.5f) * 0.12f;

            // Occasional bright flares
            nextFlareTime -= dt;
            if (nextFlareTime <= 0f)
            {
                flareStrength = Random.Range(0.15f, 0.35f);
                nextFlareTime = Random.Range(2f, 5f);
            }
            flareStrength = Mathf.MoveTowards(flareStrength, 0f, dt * 0.5f);

            fireFlickerIntensity = FireBaseIntensity * (1f + iSlow + iFast + iNoise + flareStrength);

            // Color temperature — deep amber, shifting toward orange on flares
            float warmth = Mathf.PerlinNoise(t * 1.5f, 10f);
            fireFlickerColor = Color.Lerp(
                new Color(1f, 0.4f, 0.05f),   // deep amber-red
                new Color(1f, 0.6f, 0.15f),   // bright orange
                warmth * 0.6f + flareStrength * 0.8f);
        }

        private void ClearLightmap()
        {
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(0, 0, 0, 0);
            lightmap.SetPixels32(pixels);
            lightmap.Apply();
        }

        // ── Firefly simulation ──

        private void UpdateFireflies(float dt)
        {
            for (int i = fireflies.Count - 1; i >= 0; i--)
            {
                var f = fireflies[i];
                f.age += dt;
                f.position += f.velocity * dt;
                f.velocity += new Vector2(
                    Random.Range(-0.005f, 0.005f) * dt,
                    Random.Range(-0.005f, 0.005f) * dt);
                f.velocity = Vector2.ClampMagnitude(f.velocity, FireflyDriftSpeed);

                if (f.age >= f.lifetime)
                {
                    fireflies.RemoveAt(i);
                    continue;
                }
                fireflies[i] = f;
            }

            nextFireflySpawn -= dt;
            if (nextFireflySpawn <= 0f && fireflies.Count < MaxFireflies && fireflyAlpha > 0.1f)
            {
                SpawnFirefly();
                nextFireflySpawn = FireflySpawnInterval + Random.Range(-0.3f, 0.5f);
            }
        }

        private void SpawnFirefly()
        {
            var f = new Firefly
            {
                position = new Vector2(
                    firePositionNorm.x + Random.Range(-0.4f, 0.4f),
                    firePositionNorm.y + Random.Range(-0.4f, 0.4f)),
                velocity = new Vector2(
                    Random.Range(-FireflyDriftSpeed, FireflyDriftSpeed),
                    Random.Range(-FireflyDriftSpeed, FireflyDriftSpeed)),
                age = 0f,
                lifetime = FireflyLifetime + Random.Range(-1f, 2f),
                color = Color.Lerp(
                    new Color(1f, 0.9f, 0.5f),
                    new Color(0.7f, 1f, 0.5f),
                    Random.Range(0f, 1f))
            };
            fireflies.Add(f);
        }

        // ── Lightmap rendering (CPU → Texture2D) ──

        private void RenderLightmap()
        {
            float aspect = canvasWidth / canvasHeight;
            float maxAlpha = baseAlpha * overlayAlpha;

            // Build light list (only when fire glow is active)
            var lights = new List<LightSource>();
            if (fireGlowActive)
            {
                lights.Add(new LightSource
                {
                    position = firePositionNorm,
                    color = fireFlickerColor,
                    radius = fireFlickerRadius,
                    intensity = fireFlickerIntensity * overlayAlpha
                });
            }

            // Building lights (mallum houses etc.) — subtle candle flicker
            if (canvasWidth > 0)
            {
                for (int bi = 0; bi < buildingLightPositions.Count; bi++)
                {
                    var pos = buildingLightPositions[bi];
                    var normPos = new Vector2(pos.x / canvasWidth, pos.y / canvasHeight);
                    if (float.IsNaN(normPos.x) || float.IsNaN(normPos.y)) continue;

                    // Each building has its own random flicker phase
                    float bPhase = firePhase + buildingPhaseOffsets[bi];
                    float bFlicker = 1f + Mathf.Sin(bPhase * 2.5f) * 0.06f
                        + (Mathf.PerlinNoise(bPhase * 1.5f, bi * 10f) - 0.5f) * 0.08f;

                    lights.Add(new LightSource
                    {
                        position = normPos,
                        color = new Color(1f, 0.8f, 0.4f),
                        radius = BuildingLightRadius * bFlicker,
                        intensity = overlayAlpha * bFlicker
                    });
                }
            }

            foreach (var f in fireflies)
            {
                float lifeFrac = f.age / f.lifetime;
                float fadeFrac = FireflyFadeDuration / f.lifetime;
                float fade = lifeFrac < fadeFrac ? lifeFrac / fadeFrac
                    : lifeFrac > 1f - fadeFrac ? (1f - lifeFrac) / fadeFrac : 1f;
                if (fade > 0.01f)
                {
                    lights.Add(new LightSource
                    {
                        position = f.position,
                        color = f.color,
                        radius = FireflyRadius,
                        intensity = fade * fireflyAlpha * 0.5f
                    });
                }
            }

            // Render each pixel (texture Y=0 is bottom, but UI Y=0 is top — flip)
            for (int y = 0; y < TexSize; y++)
            {
                float ny = 1f - (float)y / (TexSize - 1);
                for (int x = 0; x < TexSize; x++)
                {
                    float nx = (float)x / (TexSize - 1);

                    // Compute total illumination from all lights at this pixel
                    float totalIllum = 0f;
                    Color warmAccum = Color.black;

                    foreach (var light in lights)
                    {
                        float dx = (nx - light.position.x) * aspect;
                        float dy = ny - light.position.y;
                        float dist = Mathf.Sqrt(dx * dx + dy * dy);

                        float distNorm = dist / light.radius;
                        if (distNorm >= 1f) continue;

                        float falloff = 1f - distNorm;
                        falloff *= falloff;
                        float illum = falloff * light.intensity;
                        totalIllum += illum;

                        warmAccum.r += light.color.r * illum * 0.8f;
                        warmAccum.g += light.color.g * illum * 0.3f;
                        warmAccum.b += light.color.b * illum * 0.05f;
                    }

                    totalIllum = Mathf.Clamp01(totalIllum);

                    // Darkness reduced by illumination
                    float darkness = maxAlpha * (1f - totalIllum);

                    // Warm tint: visible in the fire-lit zone with residual alpha
                    float warmStrength = Mathf.Clamp01(warmAccum.r + warmAccum.g + warmAccum.b);
                    float warmAlpha = warmStrength * 0.45f;
                    float alpha = Mathf.Max(darkness, warmAlpha);

                    // Color: blend from night base toward warm in illuminated areas
                    float r = Mathf.Lerp(overlayBaseColor.r, Mathf.Clamp01(warmAccum.r), totalIllum);
                    float g = Mathf.Lerp(overlayBaseColor.g, Mathf.Clamp01(warmAccum.g), totalIllum);
                    float b = Mathf.Lerp(overlayBaseColor.b, Mathf.Clamp01(warmAccum.b), totalIllum);

                    pixels[y * TexSize + x] = new Color32(
                        (byte)(r * 255), (byte)(g * 255), (byte)(b * 255),
                        (byte)(alpha * 255));
                }
            }

            lightmap.SetPixels32(pixels);
            lightmap.Apply();
        }
    }
}
