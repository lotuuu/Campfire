using UnityEngine;
#if UNITY_IOS
using Unity.Notifications.iOS;
#elif UNITY_ANDROID
using Unity.Notifications.Android;
#endif

namespace Garden
{
    public class NotificationService : MonoBehaviour
    {
        public static NotificationService Instance { get; private set; }

        private const string AndroidChannelId = "campfire_plants";
        private const string AndroidWeatherChannelId = "campfire_weather";

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            InitializePlatform();
        }

        private void InitializePlatform()
        {
#if UNITY_ANDROID
            var channel = new AndroidNotificationChannel
            {
                Id = AndroidChannelId,
                Name = "Plant Growth",
                Description = "Notifications when your plants finish growing",
                Importance = Importance.Default
            };
            AndroidNotificationCenter.RegisterNotificationChannel(channel);
            var weatherChannel = new AndroidNotificationChannel
            {
                Id = AndroidWeatherChannelId,
                Name = "Weather Updates",
                Description = "Notifications about weather changes at your camp",
                Importance = Importance.Default
            };
            AndroidNotificationCenter.RegisterNotificationChannel(weatherChannel);
#elif UNITY_IOS
            using var req = new AuthorizationRequest(
                AuthorizationOption.Alert | AuthorizationOption.Badge | AuthorizationOption.Sound, false);
#endif
        }

        public void SaveWeatherData(string apiKey, float lat, float lon, WeatherCondition condition)
        {
#if UNITY_IOS
            PlayerPrefs.SetString("weather_api_key", apiKey);
            PlayerPrefs.SetFloat("weather_lat", lat);
            PlayerPrefs.SetFloat("weather_lon", lon);
            PlayerPrefs.SetInt("weather_condition", (int)condition);
            PlayerPrefs.Save();
#elif UNITY_ANDROID
            using var plugin = new AndroidJavaClass("com.garden.WeatherPrefsPlugin");
            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var context = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            plugin.CallStatic("saveWeatherData", context, apiKey, lat, lon, (int)condition);
#endif
        }

        public void SchedulePlantNotification(int plotIndex, string seedName, double remainingSeconds)
        {
            if (remainingSeconds <= 0) return;

            string title = $"Your {seedName} is ready!";
            string body = $"Come harvest your {seedName} at the camp!";

#if UNITY_ANDROID
            var notification = new AndroidNotification
            {
                Title = title,
                Text = body,
                FireTime = System.DateTime.Now.AddSeconds(remainingSeconds),
                SmallIcon = "icon_0",
                LargeIcon = "icon_1"
            };
            AndroidNotificationCenter.SendNotificationWithExplicitID(notification, AndroidChannelId, plotIndex);
#elif UNITY_IOS
            var timeTrigger = new iOSNotificationTimeIntervalTrigger
            {
                TimeInterval = new System.TimeSpan(0, 0, (int)remainingSeconds),
                Repeats = false
            };
            var notification = new iOSNotification
            {
                Identifier = plotIndex.ToString(),
                Title = title,
                Body = body,
                ShowInForeground = false,
                Trigger = timeTrigger
            };
            iOSNotificationCenter.ScheduleNotification(notification);
#endif
        }

        private const int WaterNotificationIdOffset = 10000;

        public void ScheduleWaterNotification(int plotIndex, string seedName, double remainingSeconds)
        {
            if (remainingSeconds <= 0) return;

            string title = $"Your {seedName} is ready to water!";
            string body = $"The watering cooldown has ended — give your {seedName} a drink!";
            int id = plotIndex + WaterNotificationIdOffset;

#if UNITY_ANDROID
            var notification = new AndroidNotification
            {
                Title = title,
                Text = body,
                FireTime = System.DateTime.Now.AddSeconds(remainingSeconds),
                SmallIcon = "icon_0",
                LargeIcon = "icon_1"
            };
            AndroidNotificationCenter.SendNotificationWithExplicitID(notification, AndroidChannelId, id);
#elif UNITY_IOS
            var timeTrigger = new iOSNotificationTimeIntervalTrigger
            {
                TimeInterval = new System.TimeSpan(0, 0, (int)remainingSeconds),
                Repeats = false
            };
            var notification = new iOSNotification
            {
                Identifier = id.ToString(),
                Title = title,
                Body = body,
                ShowInForeground = false,
                Trigger = timeTrigger
            };
            iOSNotificationCenter.ScheduleNotification(notification);
#endif
        }

        public void CancelWaterNotification(int plotIndex)
        {
            int id = plotIndex + WaterNotificationIdOffset;
#if UNITY_ANDROID
            AndroidNotificationCenter.CancelNotification(id);
#elif UNITY_IOS
            iOSNotificationCenter.RemoveScheduledNotification(id.ToString());
#endif
        }

        public void CancelPlantNotification(int plotIndex)
        {
#if UNITY_ANDROID
            AndroidNotificationCenter.CancelNotification(plotIndex);
#elif UNITY_IOS
            iOSNotificationCenter.RemoveScheduledNotification(plotIndex.ToString());
#endif
        }

        public void CancelAll()
        {
#if UNITY_ANDROID
            AndroidNotificationCenter.CancelAllNotifications();
#elif UNITY_IOS
            iOSNotificationCenter.RemoveAllScheduledNotifications();
#endif
        }
    }
}
