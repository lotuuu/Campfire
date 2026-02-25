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

        private const string AndroidChannelId = "garden_plants";
        private const string AndroidWeatherChannelId = "garden_weather";

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
                Description = "Notifications when the weather over your garden changes",
                Importance = Importance.Default
            };
            AndroidNotificationCenter.RegisterNotificationChannel(weatherChannel);
#elif UNITY_IOS
            using var req = new AuthorizationRequest(
                AuthorizationOption.Alert | AuthorizationOption.Badge | AuthorizationOption.Sound, false);
#endif
        }

        /// <summary>
        /// Persists API key, GPS coords, and current condition so native background
        /// fetch code can poll weather independently while the app is backgrounded.
        /// </summary>
        public void SaveWeatherData(string apiKey, float lat, float lon, WeatherCondition condition)
        {
#if UNITY_IOS
            // PlayerPrefs maps directly to NSUserDefaults on iOS — readable by native Obj-C.
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

        public void SchedulePlantNotification(int envIndex, int slotIndex, string seedName, double remainingSeconds)
        {
            if (remainingSeconds <= 0) return;

            int notificationId = envIndex * 100 + slotIndex;
            string title = $"\ud83c\udf31 {seedName} is ready!";
            string body = $"Your {seedName} has finished growing. Come harvest it!";

#if UNITY_ANDROID
            var notification = new AndroidNotification
            {
                Title = title,
                Text = body,
                FireTime = System.DateTime.Now.AddSeconds(remainingSeconds),
                SmallIcon = "icon_0",
                LargeIcon = "icon_1"
            };
            AndroidNotificationCenter.SendNotificationWithExplicitID(notification, AndroidChannelId, notificationId);
#elif UNITY_IOS
            var timeTrigger = new iOSNotificationTimeIntervalTrigger
            {
                TimeInterval = new System.TimeSpan(0, 0, (int)remainingSeconds),
                Repeats = false
            };
            var notification = new iOSNotification
            {
                Identifier = notificationId.ToString(),
                Title = title,
                Body = body,
                ShowInForeground = false,
                Trigger = timeTrigger
            };
            iOSNotificationCenter.ScheduleNotification(notification);
#endif
            Debug.Log($"[NotificationService] Scheduled notification for {seedName} in {remainingSeconds:F0}s (id={notificationId})");
        }

        public void CancelPlantNotification(int envIndex, int slotIndex)
        {
            int notificationId = envIndex * 100 + slotIndex;

#if UNITY_ANDROID
            AndroidNotificationCenter.CancelNotification(notificationId);
#elif UNITY_IOS
            iOSNotificationCenter.RemoveScheduledNotification(notificationId.ToString());
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

        public void RescheduleAll()
        {
            CancelAll();

            if (PlantManager.Instance == null) return;

            foreach (var slot in PlantManager.Instance.Slots)
            {
                if (slot.state == PlantState.Growing)
                {
                    float remainingHours = PlantManager.Instance.GetRemainingHours(
                        slot.environmentIndex, slot.slotIndex);
                    double remainingSeconds = remainingHours * 3600.0;

                    if (remainingSeconds > 0)
                    {
                        SchedulePlantNotification(
                            slot.environmentIndex, slot.slotIndex,
                            slot.seed.seedName, remainingSeconds);
                    }
                }
                else if (slot.state == PlantState.Mature)
                {
                    // Plant finished while app was open; send a reminder 1s after backgrounding
                    SchedulePlantNotification(
                        slot.environmentIndex, slot.slotIndex,
                        slot.seed.seedName, 1.0);
                }
            }
        }
    }
}
