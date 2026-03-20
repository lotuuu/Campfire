using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class WeatherVFXOverlay : MonoBehaviour
    {
        // ── Particle types ──

        private enum ParticleType { RainImpact, SnowDot, SnowFlake }

        private struct WeatherParticle
        {
            public Vector2 position;
            public float speed;          // px/sec downward (snow only)
            public float alpha;
            public float size;
            public float age;            // seconds since spawn
            public float lifetime;       // total lifetime
            public float swayPhase;      // snow sway
            public float swayFreq;
            public float swayAmplitude;
            public float rotation;       // snowflakes
            public float rotationSpeed;
            public ParticleType type;
            public bool isForeground;
            public bool alive;
        }

        // ── Configuration ──

        private const int MaxParticles = 200;
        private static readonly Color RainColor = new(0.71f, 0.78f, 1f);
        private static readonly Color SnowColor = new(0.90f, 0.92f, 1f);
        private static readonly Color LightningColor = new(0.78f, 0.82f, 1f);

        // Rain impact config (top-down: drops hitting the ground)
        private const float RainImpactLifetime = 0.5f;       // total lifecycle of one impact
        private const float RainImpactDotDuration = 0.08f;    // brief bright dot before ripple
        private const float RainImpactDotRadius = 2f;
        private const float RainImpactRippleMaxRadius = 8f;
        private const float RainImpactDotAlpha = 0.5f;
        private const float RainImpactRippleAlpha = 0.3f;

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

        // Particle counts per weather type
        private const int RainParticleCount = 80;
        private const int StormParticleCount = 100;
        private const int SnowParticleCount = 120;
        private const float TransitionDuration = 2f;

        // ── State ──

        private VisualElement canvas;
        private VisualElement viewport;
        private VisualElement particleOverlay;
        private VisualElement lightningOverlay;
        private readonly WeatherParticle[] particles = new WeatherParticle[MaxParticles];
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

        public void Initialize(VisualElement canvasElement)
        {
            canvas = canvasElement;
            viewport = canvas.parent;

            // Overlays live on the viewport (not canvas) for correct sizing.
            // Rain impacts and snow are screen-space effects — they don't need
            // to pan with the grid since they represent weather falling from above.
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

            if (WeatherService.Instance != null)
            {
                WeatherService.Instance.OnWeatherUpdated += OnWeatherUpdated;
                if (WeatherService.Instance.HasWeather)
                {
                    OnWeatherUpdated(WeatherService.Instance.CurrentWeather);
                    if (targetParticleCount > 0)
                        PreSeedParticles();
                }
            }
        }

        private void OnDestroy()
        {
            if (WeatherService.Instance != null)
                WeatherService.Instance.OnWeatherUpdated -= OnWeatherUpdated;
            particleOverlay?.RemoveFromHierarchy();
            lightningOverlay?.RemoveFromHierarchy();
        }

        private void PreSeedParticles()
        {
            float vw = viewport.resolvedStyle.width;
            float vh = viewport.resolvedStyle.height;
            if (float.IsNaN(vw) || vw <= 0) vw = 1080f;
            if (float.IsNaN(vh) || vh <= 0) vh = 1920f;
            viewportWidth = vw;
            viewportHeight = vh;

            bool isRain = targetCondition == WeatherCondition.Rain || targetCondition == WeatherCondition.Storm;
            for (int i = 0; i < targetParticleCount && i < MaxParticles; i++)
            {
                SpawnParticle(isRain);
                if (!isRain)
                {
                    // Scatter snow Y across viewport
                    particles[i].position.y = Random.Range(0f, viewportHeight);
                }
                else
                {
                    // Stagger rain impact ages so they don't all appear at once
                    particles[i].age = Random.Range(0f, particles[i].lifetime);
                }
            }
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

        // ── Update / Simulation ──

        private void Update()
        {
            if (particleOverlay == null) return;

            float dt = Time.deltaTime;
            elapsedTime += dt;

            float vw = viewport.resolvedStyle.width;
            float vh = viewport.resolvedStyle.height;
            if (!float.IsNaN(vw) && vw > 0) viewportWidth = vw;
            if (!float.IsNaN(vh) && vh > 0) viewportHeight = vh;
            if (viewportWidth <= 0 || viewportHeight <= 0) return;

            activeParticleCount = 0;
            for (int i = 0; i < MaxParticles; i++)
                if (particles[i].alive) activeParticleCount++;

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

            for (int i = 0; i < MaxParticles; i++)
            {
                if (!particles[i].alive) continue;
                SimulateParticle(ref particles[i], dt);
            }

            if (targetCondition == WeatherCondition.Storm)
                UpdateLightning(dt);

            bool hasAnything = activeParticleCount > 0;
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

            if (isRain)
            {
                // Top-down rain: impact appears at random position, plays dot → ripple → fade
                particles[slot] = new WeatherParticle
                {
                    position = new Vector2(Random.Range(0f, viewportWidth), Random.Range(0f, viewportHeight)),
                    age = 0f,
                    lifetime = RainImpactLifetime + Random.Range(-0.1f, 0.15f),
                    size = RainImpactRippleMaxRadius + Random.Range(-2f, 2f),
                    alpha = RainImpactDotAlpha,
                    type = ParticleType.RainImpact,
                    isForeground = Random.value < 0.4f,
                    alive = true
                };
            }
            else
            {
                // Snow: falls from top, drifts with sway
                bool isForeground = Random.value < 0.35f;
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
            if (p.type == ParticleType.RainImpact)
            {
                // Rain impact: age-based lifecycle, no movement
                p.age += dt;
                if (p.age >= p.lifetime)
                    p.alive = false;
            }
            else
            {
                // Snow: drift downward with sway
                p.position.x += Mathf.Sin(elapsedTime * p.swayFreq + p.swayPhase) * p.swayAmplitude * dt;
                p.position.y += p.speed * dt;
                p.rotation += p.rotationSpeed * dt;

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

                if (p.position.x < -50f || p.position.x > viewportWidth + 50f)
                    p.alive = false;
            }
        }

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

        // ── Rendering ──

        private void DrawParticles(MeshGenerationContext ctx)
        {
            var painter = ctx.painter2D;

            // Background layer first, then foreground
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
        }

        private void DrawParticle(Painter2D painter, ref WeatherParticle p)
        {
            switch (p.type)
            {
                case ParticleType.RainImpact: DrawRainImpact(painter, ref p); break;
                case ParticleType.SnowDot: DrawSnowDot(painter, ref p); break;
                case ParticleType.SnowFlake: DrawSnowFlake(painter, ref p); break;
            }
        }

        private void DrawRainImpact(Painter2D painter, ref WeatherParticle p)
        {
            float t = p.age / p.lifetime;

            if (t < RainImpactDotDuration / p.lifetime)
            {
                // Phase 1: bright impact dot
                float dotT = p.age / RainImpactDotDuration;
                float dotAlpha = RainImpactDotAlpha * (1f - dotT * 0.5f);
                float dotRadius = RainImpactDotRadius * (0.5f + dotT * 0.5f);

                painter.BeginPath();
                painter.Arc(new Vector2(p.position.x, p.position.y), dotRadius, 0f, 360f);
                painter.ClosePath();
                painter.fillColor = new Color(RainColor.r, RainColor.g, RainColor.b, dotAlpha);
                painter.Fill();
            }

            // Phase 2: expanding ripple ring (starts immediately, overlaps with dot)
            float rippleT = t;
            float rippleRadius = rippleT * p.size;
            float rippleAlpha = RainImpactRippleAlpha * (1f - rippleT);

            if (rippleAlpha > 0.01f && rippleRadius > 0.5f)
            {
                painter.BeginPath();
                painter.Arc(new Vector2(p.position.x, p.position.y), rippleRadius, 0f, 360f);
                painter.ClosePath();
                painter.strokeColor = new Color(RainColor.r, RainColor.g, RainColor.b, rippleAlpha);
                painter.lineWidth = p.isForeground ? 1.2f : 0.8f;
                painter.Stroke();
            }
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
    }
}
