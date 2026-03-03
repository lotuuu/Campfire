using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Networking;

namespace Garden
{
    public class WeatherService : MonoBehaviour
    {
        public static WeatherService Instance { get; private set; }

        [Header("API Configuration")]
        [SerializeField] private float pollIntervalMinutes = 15f;

        private string apiKey;

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
        public bool HasLocation => hasLocation;

        private float lastPollTime;
        private bool hasLocation;
        private float latitude;
        private float longitude;
        private Coroutine _fetchLoopCoroutine;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            LoadApiKey();
        }

        private void LoadApiKey()
        {
            var secrets = Resources.Load<TextAsset>("Config/secrets");
            if (secrets != null)
            {
                var data = JsonUtility.FromJson<SecretsData>(secrets.text);
                apiKey = data.openWeatherMapApiKey;
            }
            else
            {
                Debug.LogWarning("Config/secrets.json not found — weather API will not work.");
            }
        }

        [Serializable] private class SecretsData { public string openWeatherMapApiKey; }

        private void Start()
        {
            StartCoroutine(InitializeLocation());
        }

        private IEnumerator InitializeLocation()
        {
#if UNITY_EDITOR
            Debug.Log("Editor detected — using simulated weather.");
            useDebugOverride = true;
            hasLocation = true;
            IsLocationResolved = true;
            ApplyDebugWeather();
            OnLocationResolved?.Invoke(true);
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
                if (_fetchLoopCoroutine != null) StopCoroutine(_fetchLoopCoroutine);
                _fetchLoopCoroutine = StartCoroutine(FetchWeatherLoop());
#if UNITY_ANDROID
                using var plugin = new AndroidJavaClass("com.garden.WeatherPrefsPlugin");
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var context = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                plugin.CallStatic("scheduleWeatherFetch", context);
#endif
            }
            else
            {
                IsLocationResolved = true;
                Debug.LogWarning($"Location failed (status: {Input.location.status}).");
                OnLocationResolved?.Invoke(false);
            }
#endif
        }

        private IEnumerator FetchWeatherLoop()
        {
            while (true)
            {
                if (!useDebugOverride && hasLocation)
                {
                    yield return FetchWeather();
                    yield return FetchForecast();
                }
                yield return new WaitForSeconds(pollIntervalMinutes * 60f);
            }
        }

        private IEnumerator FetchWeather()
        {
            string url = $"https://api.openweathermap.org/data/2.5/weather?lat={latitude}&lon={longitude}&appid={apiKey}&units=metric";
            using var request = UnityWebRequest.Get(url);
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Weather API error: {request.error}");
                yield break;
            }

            var json = JsonUtility.FromJson<OpenWeatherResponse>(request.downloadHandler.text);
            var now = GameTime.Now;

            var weather = new WeatherData
            {
                temperature = json.main.temp,
                humidity = json.main.humidity,
                windSpeed = json.wind.speed,
                cloudCover = json.clouds.all,
                condition = MapCondition(json.weather[0].id),
                timeOfDay = TimeUtils.GetTimeOfDay(now),
                isNight = TimeUtils.IsNight(now),
                isGoldenHour = TimeUtils.IsGoldenHour(now),
                moonPhase = MoonPhaseCalculator.Calculate(now),
                calendarEvent = CalendarEvents.GetEvent(now)
            };

            CurrentWeather = weather;
            OnWeatherUpdated?.Invoke(weather);
            NotificationService.Instance?.SaveWeatherData(apiKey, latitude, longitude, weather.condition);
        }

        public void RetryLocation()
        {
            IsLocationResolved = false;
            Input.location.Stop();
            StartCoroutine(InitializeLocation());
        }

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
            {
                ApplyDebugWeather();
            }
            else if (hasLocation)
            {
                if (_fetchLoopCoroutine != null) StopCoroutine(_fetchLoopCoroutine);
                _fetchLoopCoroutine = StartCoroutine(FetchWeatherLoop());
            }
        }

        private void ApplyDebugWeather()
        {
            // SaveWeatherData is intentionally not called here: lat/lon are 0
            // in editor/debug mode and arming the native background fetch with
            // invalid coordinates would break weather polling on-device.
            CurrentWeather = debugWeather;
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

        private static WeatherCondition MapCondition(int weatherId)
        {
            return weatherId switch
            {
                >= 200 and < 300 => WeatherCondition.Storm,
                >= 300 and < 600 => WeatherCondition.Rain,
                >= 600 and < 700 => WeatherCondition.Snow,
                >= 801 => WeatherCondition.Cloudy,
                _ => WeatherCondition.Clear
            };
        }

        private IEnumerator FetchForecast()
        {
            string url = $"https://api.openweathermap.org/data/2.5/forecast?lat={latitude}&lon={longitude}&appid={apiKey}&units=metric&cnt=40";
            using var request = UnityWebRequest.Get(url);
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Forecast API error: {request.error}");
                yield break;
            }

            var response = JsonUtility.FromJson<ForecastResponse>(request.downloadHandler.text);
            Forecast = AggregateForecast(response);
            OnForecastUpdated?.Invoke();
        }

        private List<DailyForecast> AggregateForecast(ForecastResponse response)
        {
            var dayMap = new Dictionary<string, (float min, float max, float humSum, float windSum, float cloudSum, int count, Dictionary<int, int> condCounts)>();
            var dayOrder = new List<string>();

            foreach (var entry in response.list)
            {
                var dt = DateTimeOffset.FromUnixTimeSeconds(entry.dt).UtcDateTime;
                string key = dt.ToString("yyyy-MM-dd");

                float hum = entry.main.humidity;
                float wind = entry.wind != null ? entry.wind.speed : 0f;
                float cloud = entry.clouds != null ? entry.clouds.all : 0f;

                if (!dayMap.ContainsKey(key))
                {
                    dayMap[key] = (entry.main.temp_min, entry.main.temp_max, hum, wind, cloud, 1, new Dictionary<int, int>());
                    dayOrder.Add(key);
                }
                else
                {
                    var (min, max, humS, windS, cloudS, cnt, counts) = dayMap[key];
                    min = Mathf.Min(min, entry.main.temp_min);
                    max = Mathf.Max(max, entry.main.temp_max);
                    dayMap[key] = (min, max, humS + hum, windS + wind, cloudS + cloud, cnt + 1, counts);
                }

                int wid = entry.weather[0].id;
                var d = dayMap[key];
                d.condCounts[wid] = d.condCounts.GetValueOrDefault(wid) + 1;
            }

            // Skip today, take next 5 days
            var todayKey = GameTime.UtcNow.ToString("yyyy-MM-dd");
            var forecast = new List<DailyForecast>();

            foreach (string key in dayOrder)
            {
                if (key == todayKey) continue;
                if (forecast.Count >= 5) break;

                var (min, max, humSum, windSum, cloudSum, count, counts) = dayMap[key];
                var dt = DateTime.ParseExact(key, "yyyy-MM-dd", CultureInfo.InvariantCulture);

                int mostFreqId = 800;
                int mostFreqCount = 0;
                foreach (var kv in counts)
                {
                    if (kv.Value > mostFreqCount) { mostFreqId = kv.Key; mostFreqCount = kv.Value; }
                }

                forecast.Add(new DailyForecast
                {
                    dayLabel = dt.ToString("ddd", CultureInfo.InvariantCulture),
                    tempHigh = Mathf.Round(max),
                    tempLow = Mathf.Round(min),
                    condition = MapCondition(mostFreqId),
                    moonPhase = MoonPhaseCalculator.Calculate(dt),
                    humidity = Mathf.Round(humSum / count),
                    windSpeed = Mathf.Round(windSum / count * 10f) / 10f,
                    cloudCover = Mathf.Round(cloudSum / count)
                });
            }

            return forecast;
        }

        [Serializable] private class ForecastResponse { public ForecastEntry[] list; }
        [Serializable] private class ForecastEntry
        {
            public long dt;
            public ForecastMain main;
            public WeatherInfo[] weather;
            public WindData wind;
            public CloudData clouds;
        }
        [Serializable] private class ForecastMain { public float temp_min; public float temp_max; public float humidity; }

        [Serializable] private class OpenWeatherResponse
        {
            public MainData main;
            public WindData wind;
            public CloudData clouds;
            public WeatherInfo[] weather;
        }
        [Serializable] private class MainData { public float temp; public float humidity; }
        [Serializable] private class WindData { public float speed; }
        [Serializable] private class CloudData { public float all; }
        [Serializable] private class WeatherInfo { public int id; public string main; }
    }
}