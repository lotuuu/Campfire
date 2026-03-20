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
            public float despawnY;         // randomized per-particle so splashes don't form a line
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
        private const float RainFgAlphaMin = 0.4f, RainFgAlphaMax = 0.55f;
        private const float RainFgSpeedMin = 800f, RainFgSpeedMax = 1000f;
        private const float RainFgStreakLenMin = 40f, RainFgStreakLenMax = 60f;
        private const float RainFgDropRx = 2.5f, RainFgDropRy = 7f;
        private const float RainBgAlphaMin = 0.2f, RainBgAlphaMax = 0.35f;
        private const float RainBgSpeedMin = 500f, RainBgSpeedMax = 700f;
        private const float RainBgStreakLenMin = 25f, RainBgStreakLenMax = 40f;
        private const float RainBgDropRx = 1.5f, RainBgDropRy = 5f;
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

        private const float StormSpeedMultiplier = 1.2f;
        private const int RainParticleCount = 80;
        private const int StormParticleCount = 100;
        private const int SnowParticleCount = 60;
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
                    // If weather is already active, pre-seed particles so they're visible immediately
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
            // SpawnParticle fills slots sequentially (all start dead), so slot i matches iteration i
            for (int i = 0; i < targetParticleCount && i < MaxParticles; i++)
            {
                SpawnParticle(isRain);
                particles[i].position.y = Random.Range(0f, viewportHeight);
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

            for (int i = 0; i < MaxSplashes; i++)
            {
                if (!splashes[i].alive) continue;
                splashes[i].age += dt;
                if (splashes[i].age >= splashes[i].lifetime)
                    splashes[i].alive = false;
            }

            if (targetCondition == WeatherCondition.Storm)
                UpdateLightning(dt);

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
                    despawnY = viewportHeight * (1f - SplashZoneRatio + Random.Range(0f, SplashZoneRatio)),
                    type = isStreak ? ParticleType.RainStreak : ParticleType.RainDrop,
                    isForeground = isForeground,
                    alive = true
                };
            }
            else
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

                if (p.position.y > p.despawnY)
                {
                    TrySpawnSplash(p.position);
                    p.alive = false;
                }
            }
            else
            {
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
            }

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
                case ParticleType.RainStreak: DrawRainStreak(painter, ref p); break;
                case ParticleType.RainDrop: DrawRainDrop(painter, ref p); break;
                case ParticleType.SnowDot: DrawSnowDot(painter, ref p); break;
                case ParticleType.SnowFlake: DrawSnowFlake(painter, ref p); break;
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
            float ry = p.size;
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
    }
}
