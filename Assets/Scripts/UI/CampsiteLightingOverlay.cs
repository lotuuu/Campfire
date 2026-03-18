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

        private const int TexSize = 128;               // lightmap resolution
        private const float FireBaseRadius = 0.35f;     // normalized (~3-4 hex rings)
        private const float FirePulseAmount = 0.10f;    // ±10% radius
        private const float FirePulsePeriod = 3.5f;     // seconds per cycle
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
        private float nightAlpha;
        private float targetNightAlpha;
        private float fireflyAlpha;
        private float targetFireflyAlpha;
        private Vector2 firePositionNorm;  // normalized 0–1
        private Vector2 firePositionCanvas; // raw canvas coords (for OnGridRebuilt)
        private float canvasWidth, canvasHeight;
        private float firePhase;
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

        public void Initialize(VisualElement canvasElement)
        {
            canvas = canvasElement;

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
                    nightAlpha = targetNightAlpha;
                    fireflyAlpha = targetFireflyAlpha;
                }
            }
        }

        public void OnGridRebuilt(Vector2 flameCenterCanvas)
        {
            firePositionCanvas = flameCenterCanvas;

            if (overlay != null && canvas != null)
            {
                overlay.RemoveFromHierarchy();
                canvas.Add(overlay);
            }
        }

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
            if (w.isNight)
            {
                targetNightAlpha = 1f;
                targetFireflyAlpha = 1f;
            }
            else if (w.isGoldenHour)
            {
                targetNightAlpha = 0.3f;
                targetFireflyAlpha = 0.5f;
            }
            else
            {
                targetNightAlpha = 0f;
                targetFireflyAlpha = 0f;
            }
        }

        // ── Per-frame update ──

        private void Update()
        {
            if (overlay == null || lightmap == null) return;

            bool dirty = false;
            float lerpSpeed = 2f * Time.deltaTime;

            if (Mathf.Abs(nightAlpha - targetNightAlpha) > 0.001f)
            {
                nightAlpha = Mathf.MoveTowards(nightAlpha, targetNightAlpha, lerpSpeed);
                dirty = true;
            }
            if (Mathf.Abs(fireflyAlpha - targetFireflyAlpha) > 0.001f)
            {
                fireflyAlpha = Mathf.MoveTowards(fireflyAlpha, targetFireflyAlpha, lerpSpeed);
                dirty = true;
            }

            // Show/hide overlay
            if (nightAlpha > 0.001f)
            {
                if (overlay.style.display == DisplayStyle.None)
                    overlay.style.display = DisplayStyle.Flex;
                firePhase += Time.deltaTime;
                dirty = true;
                UpdateFireflies(Time.deltaTime);
            }
            else
            {
                fireflies.Clear();
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
            // Build light list
            float pulse = 1f + Mathf.Sin(firePhase * Mathf.PI * 2f / FirePulsePeriod) * FirePulseAmount;
            float aspect = canvasWidth / canvasHeight;

            var lights = new List<LightSource>();
            lights.Add(new LightSource
            {
                position = firePositionNorm,
                color = new Color(1f, 0.7f, 0.3f),
                radius = FireBaseRadius * pulse,
                intensity = nightAlpha
            });

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

            // Night base color
            Color nightBase = new Color(0.01f, 0.02f, 0.08f);
            float maxAlpha = 0.92f * nightAlpha;

            // Render each pixel
            for (int y = 0; y < TexSize; y++)
            {
                float ny = (float)y / (TexSize - 1);  // normalized 0–1
                for (int x = 0; x < TexSize; x++)
                {
                    float nx = (float)x / (TexSize - 1);

                    // Compute total illumination from all lights at this pixel
                    float totalIllum = 0f;
                    Color warmAccum = Color.black;

                    foreach (var light in lights)
                    {
                        // Distance in normalized coords, corrected for aspect ratio
                        float dx = (nx - light.position.x) * aspect;
                        float dy = ny - light.position.y;
                        float dist = Mathf.Sqrt(dx * dx + dy * dy);

                        float distNorm = dist / light.radius;
                        if (distNorm >= 1f) continue;

                        float falloff = 1f - distNorm;
                        falloff *= falloff; // quadratic
                        float illum = falloff * light.intensity;
                        totalIllum += illum;

                        // Accumulate warm color contribution
                        warmAccum.r += light.color.r * illum * 0.15f;
                        warmAccum.g += light.color.g * illum * 0.1f;
                        warmAccum.b += light.color.b * illum * 0.05f;
                    }

                    totalIllum = Mathf.Clamp01(totalIllum);

                    // Darkness = night color, reduced by illumination
                    float darkness = maxAlpha * (1f - totalIllum);

                    // Blend warm color into the transition zone
                    float r = Mathf.Lerp(nightBase.r, Mathf.Clamp01(nightBase.r + warmAccum.r), totalIllum);
                    float g = Mathf.Lerp(nightBase.g, Mathf.Clamp01(nightBase.g + warmAccum.g), totalIllum);
                    float b = Mathf.Lerp(nightBase.b, Mathf.Clamp01(nightBase.b + warmAccum.b), totalIllum);

                    pixels[y * TexSize + x] = new Color32(
                        (byte)(r * 255), (byte)(g * 255), (byte)(b * 255),
                        (byte)(darkness * 255));
                }
            }

            lightmap.SetPixels32(pixels);
            lightmap.Apply();
        }
    }
}
