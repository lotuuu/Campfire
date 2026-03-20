using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace Garden
{
    public class WeatherVFXOverlay : MonoBehaviour
    {
        // ── Configuration ──

        private const int VfxLayer = 8;

        // Lightning config
        private static readonly Color LightningColor = new(0.78f, 0.82f, 1f);
        private const float LightningFlashAlpha = 0.3f;
        private const float LightningPulseDuration = 0.15f;
        private const float LightningPulseGap = 0.1f;
        private const float LightningMinInterval = 4f;
        private const float LightningMaxInterval = 10f;
        private const float LightningClusterChance = 0.3f;
        private const float LightningClusterDelay = 1f;

        // Particle density
        private const float RainBaseEmission = 80f;
        private const float StormEmissionMultiplier = 1.25f;
        private const float SnowBaseEmission = 4f;

        // ── State ──

        private VisualElement canvas;
        private VisualElement viewport;
        private VisualElement rtOverlay;
        private VisualElement lightningOverlay;

        private RenderTexture renderTexture;
        private Camera vfxCamera;
        private GameObject vfxRoot;
        private ParticleSystem rainPS;
        private ParticleSystem snowPS;
        private Texture2D circleTexture;
        private Material rainMaterial;
        private Material snowMaterial;

        private float viewportWidth, viewportHeight;
        private float canvasW, canvasH;
        private float lastCanvasW, lastCanvasH;
        private WeatherCondition targetCondition = WeatherCondition.Clear;

        // Lightning state
        private float nextLightningTime;
        private int lightningPulsesRemaining;
        private float lightningPulseTimer;
        private bool lightningPulseOn;
        private bool lightningClusterPending;
        private float lightningClusterTimer;

        // ── Texture generation ──

        private Texture2D CreateCircleTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = size / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    float alpha = Mathf.Clamp01(1f - (dist / center));
                    alpha *= alpha;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        private Texture2D CreateRingTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = size / 2f;
            float ringRadius = center * 0.75f;
            float ringWidth = center * 0.25f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    float distFromRing = Mathf.Abs(dist - ringRadius);
                    float alpha = Mathf.Clamp01(1f - (distFromRing / ringWidth));
                    alpha *= alpha;
                    // Also fade out at outer edge
                    float outerFade = Mathf.Clamp01(1f - (dist / center));
                    alpha *= Mathf.Clamp01(outerFade * 3f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        private Material CreateParticleMaterial(Shader shader, Texture2D texture)
        {
            var mat = new Material(shader);
            mat.mainTexture = texture;
            mat.SetFloat("_Surface", 1);
            mat.SetFloat("_Blend", 0);
            mat.SetFloat("_ColorMode", 1);
            mat.renderQueue = 3000;
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.EnableKeyword("_ALPHABLEND_ON");
            return mat;
        }

        // ── Public API ──

        public void Initialize(VisualElement canvasElement)
        {
            canvas = canvasElement;
            viewport = canvas.parent;

            // Generate particle textures and materials
            circleTexture = CreateCircleTexture(32);
            var ringTexture = CreateRingTexture(32);

            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");

            rainMaterial = CreateParticleMaterial(shader, ringTexture);
            snowMaterial = CreateParticleMaterial(shader, circleTexture);

            // Read initial dimensions
            float vw = viewport.resolvedStyle.width;
            float vh = viewport.resolvedStyle.height;
            if (float.IsNaN(vw) || vw <= 0) vw = 540f;
            if (float.IsNaN(vh) || vh <= 0) vh = 960f;
            viewportWidth = vw;
            viewportHeight = vh;

            float cw = canvas.resolvedStyle.width;
            float ch = canvas.resolvedStyle.height;
            if (!float.IsNaN(cw) && cw > 0) canvasW = cw;
            if (!float.IsNaN(ch) && ch > 0) canvasH = ch;
            if (canvasW <= 0) canvasW = viewportWidth;
            if (canvasH <= 0) canvasH = viewportHeight;

            // Create RenderTexture
            float dpiScale = Mathf.Clamp(Screen.dpi / 96f, 1f, 3f);
            if (dpiScale <= 0) dpiScale = 1f;
            int rtW = Mathf.Max(1, (int)(vw * dpiScale));
            int rtH = Mathf.Max(1, (int)(vh * dpiScale));
            renderTexture = new RenderTexture(rtW, rtH, 16, RenderTextureFormat.ARGB32);
            renderTexture.Create();

            // Create scene objects
            CreateVFXCamera();
            CreateRainPS();
            CreateSnowPS();

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

            // Lightning flash overlay
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

        private void CreateVFXCamera()
        {
            vfxRoot = new GameObject("WeatherVFX");
            vfxRoot.layer = VfxLayer;

            var camGO = new GameObject("WeatherVFXCamera");
            camGO.transform.SetParent(vfxRoot.transform);
            camGO.layer = VfxLayer;

            vfxCamera = camGO.AddComponent<Camera>();
            vfxCamera.orthographic = true;
            vfxCamera.orthographicSize = viewportHeight / 2f;
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
            // Visible center in canvas coords: -translate + viewport/2
            // In world coords: X = canvas X, Y = -canvas Y
            float cx = -translate.x + viewportWidth / 2f;
            float cy = -(-translate.y + viewportHeight / 2f);
            vfxCamera.transform.position = new Vector3(cx, cy, -10f);
        }

        private void CreateRainPS()
        {
            var go = new GameObject("RainParticles");
            go.transform.SetParent(vfxRoot.transform);
            go.layer = VfxLayer;
            go.transform.position = new Vector3(canvasW / 2f, -canvasH / 2f, 0f);

            rainPS = go.AddComponent<ParticleSystem>();
            var main = rainPS.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.7f);
            main.startSize = new ParticleSystem.MinMaxCurve(20f, 40f);
            main.startSpeed = 0f;
            main.startColor = new Color(0.71f, 0.78f, 1f, 0.65f);
            main.maxParticles = 1000;
            main.playOnAwake = false;
            main.loop = true;

            var shape = rainPS.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(canvasW, canvasH, 1f);

            var emission = rainPS.emission;
            emission.rateOverTime = 0f;

            var sol = rainPS.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.2f),
                new Keyframe(1f, 1f)));

            var col = rainPS.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = gradient;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.material = rainMaterial;
            renderer.sortingOrder = 100;
        }

        private void CreateSnowPS()
        {
            var go = new GameObject("SnowParticles");
            go.transform.SetParent(vfxRoot.transform);
            go.layer = VfxLayer;
            // Same box as rain — snow appears everywhere, drifts downward
            go.transform.position = new Vector3(canvasW / 2f, -canvasH / 2f, 0f);

            snowPS = go.AddComponent<ParticleSystem>();
            var main = snowPS.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 6f);
            main.startSize = new ParticleSystem.MinMaxCurve(14f, 24f);
            main.startSpeed = 0f; // movement via velocity over lifetime
            main.startColor = new Color(0.90f, 0.92f, 1f, 0.55f);
            main.maxParticles = 1000;
            main.playOnAwake = false;
            main.loop = true;

            // Box shape covering entire canvas
            var shape = snowPS.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(canvasW, canvasH, 1f);

            var emission = snowPS.emission;
            emission.rateOverTime = 0f;

            // Constant downward drift via velocity over lifetime
            var vel = snowPS.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = 0f;
            vel.y = new ParticleSystem.MinMaxCurve(-80f, -40f); // drift down in world -Y
            vel.z = 0f;

            // Noise for horizontal sway
            var noise = snowPS.noise;
            noise.enabled = true;
            noise.separateAxes = true;
            noise.strengthX = new ParticleSystem.MinMaxCurve(15f);
            noise.strengthY = new ParticleSystem.MinMaxCurve(0f);
            noise.strengthZ = new ParticleSystem.MinMaxCurve(0f);
            noise.frequency = 1f;
            noise.scrollSpeed = 0.1f;
            noise.octaveCount = 2;

            // Rotation over lifetime
            var rot = snowPS.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(
                15f * Mathf.Deg2Rad, 30f * Mathf.Deg2Rad);

            // Color over lifetime: fade in/out
            var col = snowPS.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.1f),
                    new GradientAlphaKey(1f, 0.8f),
                    new GradientAlphaKey(0f, 1f)
                });
            col.color = gradient;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.material = snowMaterial;
            renderer.sortingOrder = 100;
        }

        // ── Weather control ──

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
                    rainMain.startSize = new ParticleSystem.MinMaxCurve(20f, 40f);
                    snowEmission.rateOverTime = 0f;
                    if (!rainPS.isPlaying) rainPS.Play();
                    break;
                case WeatherCondition.Storm:
                    rainEmission.rateOverTime = areaRatio * RainBaseEmission * StormEmissionMultiplier;
                    rainMain.startSize = new ParticleSystem.MinMaxCurve(25f, 50f);
                    snowEmission.rateOverTime = 0f;
                    if (!rainPS.isPlaying) rainPS.Play();
                    break;
                case WeatherCondition.Snow:
                    rainEmission.rateOverTime = 0f;
                    snowEmission.rateOverTime = areaRatio * SnowBaseEmission;
                    if (!snowPS.isPlaying) snowPS.Play();
                    if (snowPS.particleCount == 0)
                    {
                        snowPS.Simulate(canvasH / 60f, true, true);
                        snowPS.Play();
                    }
                    break;
                default:
                    rainEmission.rateOverTime = 0f;
                    snowEmission.rateOverTime = 0f;
                    break;
            }
        }

        // ── Update ──

        private void Update()
        {
            if (vfxCamera == null) return;

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
            // Visible center in canvas coords: -translate + viewport/2
            // In world coords: X = canvas X, Y = -canvas Y
            float cx = -translate.x + viewportWidth / 2f;
            float cy = -(-translate.y + viewportHeight / 2f);
            vfxCamera.transform.position = new Vector3(cx, cy, -10f);
            vfxCamera.orthographicSize = viewportHeight / 2f;
            vfxCamera.aspect = viewportWidth / viewportHeight;

            // Update particle shapes if canvas changed
            UpdateParticleShapes();

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

            // Lightning
            if (targetCondition == WeatherCondition.Storm)
                UpdateLightning(Time.deltaTime);
        }

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
                shape.scale = new Vector3(canvasW, canvasH, 1f);
                snowPS.transform.position = new Vector3(canvasW / 2f, -canvasH / 2f, 0f);
            }

            UpdateEmission();
        }

        // ── Cleanup ──

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
            if (rainMaterial != null)
                Destroy(rainMaterial);
            if (snowMaterial != null)
                Destroy(snowMaterial);
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
