using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Garden
{
    /// <summary>
    /// Drives the Living Canvas background shader, cross-fading colors
    /// when weather or time-of-day changes.
    /// Attach to a full-screen quad behind all gameplay on the Background sorting layer.
    /// </summary>
    public class LivingCanvasController : MonoBehaviour
    {
        // ── Color Presets ──────────────────────────────────────────────

        public struct ColorPreset
        {
            public Color TopColor;
            public Color BottomColor;

            public ColorPreset(string topHex, string bottomHex)
            {
                ColorUtility.TryParseHtmlString(topHex, out TopColor);
                ColorUtility.TryParseHtmlString(bottomHex, out BottomColor);
            }
        }

        public static readonly Dictionary<string, ColorPreset> WeatherPresets = new()
        {
            ["GoldenHour"] = new ColorPreset("#E8837C", "#F4A259"),  // Warm Pink / Soft Orange
            ["Midnight"]   = new ColorPreset("#0D1B2A", "#415A77"),  // Deep Navy / Slate
            ["Stormy"]     = new ColorPreset("#2B2D30", "#4A4E54"),  // Dark Gray / Charcoal
            ["ClearDay"]   = new ColorPreset("#89CFF0", "#F0F8FF"),  // Baby Blue / Alice Blue
            ["Cloudy"]     = new ColorPreset("#8E9AAF", "#DEE2E6"),  // Muted Lavender / Light Gray
            ["Rain"]       = new ColorPreset("#4A6670", "#7B8E97"),  // Steel Blue / Pewter
            ["Snow"]       = new ColorPreset("#B8C6DB", "#E8EDF2"),  // Frost Blue / Ice White
            ["ClearNight"] = new ColorPreset("#0B132B", "#1C2541"),  // Ink Black / Dark Navy
        };

        // ── Inspector ──────────────────────────────────────────────────

        [Header("References")]
        [SerializeField] private Renderer targetRenderer;

        [Header("Transition")]
        [SerializeField] private float crossFadeDuration = 5f;

        [Header("Wind Linkage")]
        [Tooltip("Multiplier applied to real wind speed for particle drift")]
        [SerializeField] private float windToParticleScale = 0.02f;

        // ── Shader Property IDs (cached) ───────────────────────────────

        static readonly int TopColorID = Shader.PropertyToID("_TopColor");
        static readonly int BottomColorID = Shader.PropertyToID("_BottomColor");
        static readonly int ParticleSpeedID = Shader.PropertyToID("_ParticleSpeed");
        static readonly int ParticleDriftID = Shader.PropertyToID("_ParticleDrift");

        // ── State ──────────────────────────────────────────────────────

        private Material mat;
        private Color currentTop;
        private Color currentBottom;
        private Coroutine fadeCoroutine;

        // ── Lifecycle ──────────────────────────────────────────────────

        private void Awake()
        {
            if (targetRenderer == null)
                targetRenderer = GetComponent<Renderer>();

            // Instance the material so we don't pollute the shared asset
            mat = targetRenderer.material;

            // Initialize to clear day
            var preset = WeatherPresets["ClearDay"];
            currentTop = preset.TopColor;
            currentBottom = preset.BottomColor;
            mat.SetColor(TopColorID, currentTop);
            mat.SetColor(BottomColorID, currentBottom);
        }

        private void OnEnable()
        {
            if (WeatherService.Instance != null)
            {
                WeatherService.Instance.OnWeatherUpdated += OnWeatherUpdated;
                OnWeatherUpdated(WeatherService.Instance.CurrentWeather);
            }
            else
            {
                StartCoroutine(WaitForWeatherService());
            }
        }

        private void OnDisable()
        {
            if (WeatherService.Instance != null)
                WeatherService.Instance.OnWeatherUpdated -= OnWeatherUpdated;
        }

        private void OnDestroy()
        {
            if (mat != null)
                Destroy(mat);
        }

        private IEnumerator WaitForWeatherService()
        {
            while (WeatherService.Instance == null)
                yield return null;

            WeatherService.Instance.OnWeatherUpdated += OnWeatherUpdated;
            OnWeatherUpdated(WeatherService.Instance.CurrentWeather);
        }

        // ── Weather Handling ───────────────────────────────────────────

        private void OnWeatherUpdated(WeatherData data)
        {
            var preset = ResolvePreset(data);
            TransitionTo(preset.TopColor, preset.BottomColor);
            UpdateWind(data.windSpeed);
        }

        private static ColorPreset ResolvePreset(WeatherData data)
        {
            // Storm overrides everything
            if (data.condition == WeatherCondition.Storm)
                return WeatherPresets["Stormy"];

            // Time-of-day takes next priority
            if (data.timeOfDay == TimeOfDay.GoldenHour)
                return WeatherPresets["GoldenHour"];

            if (data.timeOfDay == TimeOfDay.Night || data.isNight)
            {
                return data.condition == WeatherCondition.Clear
                    ? WeatherPresets["ClearNight"]
                    : WeatherPresets["Midnight"];
            }

            // Daytime conditions
            return data.condition switch
            {
                WeatherCondition.Rain  => WeatherPresets["Rain"],
                WeatherCondition.Snow  => WeatherPresets["Snow"],
                WeatherCondition.Cloudy => WeatherPresets["Cloudy"],
                _ => WeatherPresets["ClearDay"],
            };
        }

        // ── Cross-Fade ─────────────────────────────────────────────────

        public void TransitionTo(Color targetTop, Color targetBottom)
        {
            if (fadeCoroutine != null)
                StopCoroutine(fadeCoroutine);

            fadeCoroutine = StartCoroutine(CrossFade(targetTop, targetBottom));
        }

        public void SetPresetImmediate(string presetName)
        {
            if (!WeatherPresets.TryGetValue(presetName, out var preset))
            {
                Debug.LogWarning($"LivingCanvas: Unknown preset '{presetName}'");
                return;
            }

            if (fadeCoroutine != null)
                StopCoroutine(fadeCoroutine);

            currentTop = preset.TopColor;
            currentBottom = preset.BottomColor;
            mat.SetColor(TopColorID, currentTop);
            mat.SetColor(BottomColorID, currentBottom);
        }

        private IEnumerator CrossFade(Color targetTop, Color targetBottom)
        {
            Color fromTop = currentTop;
            Color fromBottom = currentBottom;
            float elapsed = 0f;

            while (elapsed < crossFadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / crossFadeDuration);

                currentTop = Color.Lerp(fromTop, targetTop, t);
                currentBottom = Color.Lerp(fromBottom, targetBottom, t);

                mat.SetColor(TopColorID, currentTop);
                mat.SetColor(BottomColorID, currentBottom);

                yield return null;
            }

            currentTop = targetTop;
            currentBottom = targetBottom;
            mat.SetColor(TopColorID, currentTop);
            mat.SetColor(BottomColorID, currentBottom);

            fadeCoroutine = null;
        }

        // ── Wind → Particle Linkage ────────────────────────────────────

        private void UpdateWind(float windSpeedMs)
        {
            float drift = Mathf.Clamp01(windSpeedMs * windToParticleScale);
            mat.SetFloat(ParticleDriftID, drift);

            // Faster wind also subtly speeds particles upward
            float rise = Mathf.Lerp(0.2f, 0.8f, Mathf.Clamp01(windSpeedMs / 20f));
            mat.SetFloat(ParticleSpeedID, rise);
        }
    }
}
