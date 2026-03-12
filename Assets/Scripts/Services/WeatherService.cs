using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Garden
{
    public class WeatherService : MonoBehaviour
    {
        public static WeatherService Instance { get; private set; }

        [Header("Debug Override")]
        [SerializeField] private bool useDebugOverride;
        [SerializeField] private WeatherData debugWeather;

        public WeatherData CurrentWeather { get; private set; }
        public List<DailyForecast> Forecast { get; private set; } = new();
        public event Action<WeatherData> OnWeatherUpdated;
        public event Action OnForecastUpdated;
        public event Action<bool> OnLocationResolved;
        public bool IsDebugMode => useDebugOverride;
        public bool IsLocationResolved { get; private set; }
        public bool HasWeather { get; private set; }
        public bool HasLocation => hasLocation;

        private bool hasLocation;
        private float latitude;
        private float longitude;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            StartCoroutine(InitializeLocation());
        }

        private IEnumerator InitializeLocation()
        {
#if UNITY_EDITOR
            // In editor, use a default location (Berlin) so server weather/forecast works.
            latitude = 52.52f;
            longitude = 13.405f;
            hasLocation = true;
            IsLocationResolved = true;
            OnLocationResolved?.Invoke(true);

            // Submit location to server so forecast endpoint has lat/lon
            yield return new WaitUntil(() => GameService.Instance != null && GameService.Instance.IsOnline);
            yield return SubmitLocationAndRetryWeather();
            yield break;
#else
            Input.location.Start(500f, 500f);

            int timeout = 20;
            while (Input.location.status == LocationServiceStatus.Initializing && timeout > 0)
            {
                yield return new WaitForSeconds(1);
                timeout--;
            }

            if (Input.location.status == LocationServiceStatus.Running)
            {
                latitude = Input.location.lastData.latitude;
                longitude = Input.location.lastData.longitude;
                hasLocation = true;
                IsLocationResolved = true;
                Debug.Log($"Location acquired: {latitude}, {longitude}");
                OnLocationResolved?.Invoke(true);

                // Wait for GameService to be online, then submit location and retry weather if needed
                yield return new WaitUntil(() => GameService.Instance != null && GameService.Instance.IsOnline);
                yield return SubmitLocationAndRetryWeather();
            }
            else
            {
                Debug.LogWarning($"Location failed (status: {Input.location.status}). Retrying in 5s...");
                Input.location.Stop();
                yield return new WaitForSeconds(5);
                yield return InitializeLocation();
            }
#endif
        }

        private IEnumerator SubmitLocationAndRetryWeather()
        {
            var task = GameService.Instance.SubmitLocation(latitude, longitude);
            while (!task.IsCompleted) yield return null;

            // If server hadn't fetched weather yet (all zeros), retry a few times
            const int maxRetries = 5;
            for (int i = 0; i < maxRetries && !HasWeather; i++)
            {
                yield return new WaitForSeconds(2f);
                Debug.Log($"[Weather] Retry {i + 1}/{maxRetries} — server weather not ready yet");
                var weatherTask = GameService.Instance.GetWeather();
                while (!weatherTask.IsCompleted) yield return null;
                if (weatherTask.Result != null)
                    ApplyServerWeather(weatherTask.Result);
            }

            if (!HasWeather)
                Debug.LogWarning("[Weather] Server weather unavailable after retries");
        }

        public void RetryLocation()
        {
            IsLocationResolved = false;
            Input.location.Stop();
            StartCoroutine(InitializeLocation());
        }

        // ── Server Weather ──

        public void ApplyServerWeather(ServerWeather sw)
        {
            if (sw == null) return;
            // Skip empty/placeholder weather (server has no real data yet)
            if (sw.humidity == 0 && sw.wind_speed == 0 && sw.temperature == 0) return;
            var now = GameTime.Now;
            float sunriseHour = 6.5f;
            float sunsetHour = 18.5f;
            var weather = new WeatherData
            {
                temperature = sw.temperature,
                humidity = sw.humidity,
                windSpeed = sw.wind_speed,
                cloudCover = sw.cloud_cover,
                sunriseHour = sunriseHour,
                sunsetHour = sunsetHour,
                condition = ParseServerCondition(sw.condition),
                timeOfDay = TimeUtils.GetTimeOfDay(now, sunriseHour, sunsetHour),
                isNight = TimeUtils.IsNight(now, sunriseHour, sunsetHour),
                isGoldenHour = TimeUtils.IsGoldenHour(now, sunsetHour),
                moonPhase = (MoonPhase)sw.moon_phase,
                calendarEvent = CalendarEvents.GetEvent(now)
            };
            useDebugOverride = false;
            CurrentWeather = weather;
            HasWeather = true;
            OnWeatherUpdated?.Invoke(weather);
        }

        private static WeatherCondition ParseServerCondition(string condition)
        {
            if (string.IsNullOrEmpty(condition)) return WeatherCondition.Clear;
            return condition.ToLower() switch
            {
                "rain" or "drizzle" => WeatherCondition.Rain,
                "storm" or "thunderstorm" => WeatherCondition.Storm,
                "snow" => WeatherCondition.Snow,
                "cloudy" or "clouds" => WeatherCondition.Cloudy,
                _ => WeatherCondition.Clear
            };
        }

        public void ApplyServerForecast(List<ServerForecastDay> days)
        {
            if (days == null || days.Count == 0) return;
            var forecast = new List<DailyForecast>();
            foreach (var d in days)
            {
                forecast.Add(new DailyForecast
                {
                    dayLabel = d.dayLabel,
                    tempHigh = d.tempHigh,
                    tempLow = d.tempLow,
                    condition = ParseServerCondition(d.condition),
                    moonPhase = (MoonPhase)d.moonPhase,
                    humidity = d.humidity,
                    windSpeed = d.windSpeed,
                    cloudCover = d.cloudCover
                });
            }
            Forecast = forecast;
            OnForecastUpdated?.Invoke();
        }

        // ── Debug ──

        public void SetDebugWeather(WeatherData data)
        {
            useDebugOverride = true;
            debugWeather = data;
            ApplyDebugWeather();
        }

        public void SetDebugMode(bool enabled)
        {
            useDebugOverride = enabled;
            if (enabled)
                ApplyDebugWeather();
        }

        private void ApplyDebugWeather()
        {
            if (debugWeather.sunriseHour == 0f && debugWeather.sunsetHour == 0f)
            {
                debugWeather.sunriseHour = 6.5f;
                debugWeather.sunsetHour = 18.5f;
            }
            CurrentWeather = debugWeather;
            HasWeather = true;
            OnWeatherUpdated?.Invoke(debugWeather);
            GenerateDebugForecast();
        }

        private void GenerateDebugForecast()
        {
            var conditions = (WeatherCondition[])Enum.GetValues(typeof(WeatherCondition));
            var forecast = new List<DailyForecast>();
            var baseTemp = debugWeather.temperature;
            var today = GameTime.UtcNow;

            for (int i = 0; i < 5; i++)
            {
                var day = today.AddDays(i + 1);
                float variation = UnityEngine.Random.Range(-3f, 3f);
                var cond = conditions[(int)(debugWeather.condition + i) % conditions.Length];
                forecast.Add(new DailyForecast
                {
                    dayLabel = day.ToString("ddd", CultureInfo.InvariantCulture),
                    tempHigh = Mathf.Round(baseTemp + variation + 2f),
                    tempLow = Mathf.Round(baseTemp + variation - 4f),
                    condition = cond,
                    moonPhase = MoonPhaseCalculator.Calculate(day),
                    humidity = Mathf.Round(debugWeather.humidity + UnityEngine.Random.Range(-15f, 15f)),
                    windSpeed = Mathf.Round(debugWeather.windSpeed + UnityEngine.Random.Range(-2f, 3f)),
                    cloudCover = Mathf.Round(Mathf.Clamp(debugWeather.cloudCover + UnityEngine.Random.Range(-20f, 20f), 0f, 100f))
                });
            }

            Forecast = forecast;
            OnForecastUpdated?.Invoke();
        }
    }
}
