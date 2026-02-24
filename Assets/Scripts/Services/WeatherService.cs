using System;
using System.Collections;
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
        public event Action<WeatherData> OnWeatherUpdated;
        public event Action<bool> OnLocationResolved;
        public bool IsDebugMode => useDebugOverride;
        public bool IsLocationResolved { get; private set; }
        public bool HasLocation => hasLocation;

        private float lastPollTime;
        private bool hasLocation;
        private float latitude;
        private float longitude;

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
                StartCoroutine(FetchWeatherLoop());
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
                    yield return FetchWeather();
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
            if (enabled) ApplyDebugWeather();
            else if (hasLocation) StartCoroutine(FetchWeather());
        }

        private void ApplyDebugWeather()
        {
            CurrentWeather = debugWeather;
            OnWeatherUpdated?.Invoke(debugWeather);
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