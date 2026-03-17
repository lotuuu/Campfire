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

        private void OnApplicationPause(bool paused)
        {
            if (!paused) CancelAll();
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
            KeychainHelper.SetString("weather_api_key", apiKey);
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

            string displayName = PlotManager.GetSeedDisplayName(seedName);
            string title = string.Format(Loc.Get("notif.plant_harvest.title", "Your {0} is ready!"), displayName);
            string body = string.Format(Loc.Get("notif.plant_harvest.body", "Come harvest your {0} at the camp!"), displayName);

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
        private const int QuestNotificationIdOffset = 20000;
        private const int WaterFetchNotificationIdOffset = 30000;
        private const int GardenYieldNotificationIdOffset = 40000;

        public void ScheduleWaterNotification(int plotIndex, string seedName, double remainingSeconds)
        {
            if (remainingSeconds <= 0) return;

            string displayName = PlotManager.GetSeedDisplayName(seedName);
            string title = string.Format(Loc.Get("notif.water_ready.title", "Your {0} is ready to water!"), displayName);
            string body = string.Format(Loc.Get("notif.water_ready.body", "The watering cooldown has ended - give your {0} a drink!"), displayName);
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

        public void ScheduleQuestNotification(int mallumIndex, string questName, double remainingSeconds)
        {
            if (remainingSeconds <= 0) return;

            string displayName = questName.Replace("_", " ");
            string title = Loc.Get("notif.quest_complete.title", "Mallum has returned!");
            string body = string.Format(Loc.Get("notif.quest_complete.body", "Your Mallum is back from {0} with rewards to collect!"), displayName);
            int id = mallumIndex + QuestNotificationIdOffset;

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
            var iosNotification = new iOSNotification
            {
                Identifier = id.ToString(),
                Title = title,
                Body = body,
                ShowInForeground = false,
                Trigger = timeTrigger
            };
            iOSNotificationCenter.ScheduleNotification(iosNotification);
#endif
        }

        public void CancelQuestNotification(int mallumIndex)
        {
            int id = mallumIndex + QuestNotificationIdOffset;
#if UNITY_ANDROID
            AndroidNotificationCenter.CancelNotification(id);
#elif UNITY_IOS
            iOSNotificationCenter.RemoveScheduledNotification(id.ToString());
#endif
        }

        public void ScheduleWaterFetchNotification(int mallumIndex, double remainingSeconds)
        {
            if (remainingSeconds <= 0) return;

            string title = Loc.Get("notif.water_fetch.title", "Water is ready!");
            string body = Loc.Get("notif.water_fetch.body", "Your Mallum has finished fetching water for your vase!");
            int id = mallumIndex + WaterFetchNotificationIdOffset;

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
            var iosNotification = new iOSNotification
            {
                Identifier = id.ToString(),
                Title = title,
                Body = body,
                ShowInForeground = false,
                Trigger = timeTrigger
            };
            iOSNotificationCenter.ScheduleNotification(iosNotification);
#endif
        }

        public void CancelWaterFetchNotification(int mallumIndex)
        {
            int id = mallumIndex + WaterFetchNotificationIdOffset;
#if UNITY_ANDROID
            AndroidNotificationCenter.CancelNotification(id);
#elif UNITY_IOS
            iOSNotificationCenter.RemoveScheduledNotification(id.ToString());
#endif
        }

        public void ScheduleGardenYieldNotification(int gardenIndex, string plantName, double remainingSeconds)
        {
            if (remainingSeconds <= 0) return;

            string title = string.Format(Loc.Get("notif.garden_yield.title", "Your {0} has fruit!"), plantName);
            string body = string.Format(Loc.Get("notif.garden_yield.body", "Your {0} garden has produced a harvest - come collect it!"), plantName);
            int id = gardenIndex + GardenYieldNotificationIdOffset;

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
            var iosNotification = new iOSNotification
            {
                Identifier = id.ToString(),
                Title = title,
                Body = body,
                ShowInForeground = false,
                Trigger = timeTrigger
            };
            iOSNotificationCenter.ScheduleNotification(iosNotification);
#endif
        }

        public void CancelGardenYieldNotification(int gardenIndex)
        {
            int id = gardenIndex + GardenYieldNotificationIdOffset;
#if UNITY_ANDROID
            AndroidNotificationCenter.CancelNotification(id);
#elif UNITY_IOS
            iOSNotificationCenter.RemoveScheduledNotification(id.ToString());
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
