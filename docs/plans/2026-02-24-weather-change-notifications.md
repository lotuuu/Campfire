# Weather Change Notifications Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Fire a local push notification whenever the weather condition changes, even when the app is in the background, using iOS Background Fetch and Android WorkManager — no server required.

**Architecture:** Unity C# saves API key, GPS coordinates, and last-known `WeatherCondition` to native storage (NSUserDefaults on iOS, SharedPreferences on Android) after each successful fetch. Native platform code polls the weather API independently every ~15 minutes while the app is backgrounded, compares the result to the stored condition, and fires a local notification on change.

**Tech Stack:** Unity 6, C# (WeatherService/NotificationService), Obj-C (iOS UnityAppController category), Java (Android WorkManager + JNI plugin), Unity `IPostprocessBuildWithReport` for Xcode Info.plist modification.

---

### Task 1: Add Android notification channel and save weather data from C#

**Files:**
- Modify: `Assets/Scripts/Services/NotificationService.cs`
- Modify: `Assets/Scripts/Services/WeatherService.cs`

This task has no tests (MonoBehaviour platform bridge code — not unit-testable). We verify by code review only.

**Step 1: Add weather channel constant and register it on Android**

In `NotificationService.cs`, add a second channel constant after line 14 (`private const string AndroidChannelId = "garden_plants";`):

```csharp
private const string AndroidWeatherChannelId = "garden_weather";
```

In `InitializePlatform()`, add the weather channel registration inside the `#if UNITY_ANDROID` block, after the existing channel registration:

```csharp
var weatherChannel = new AndroidNotificationChannel
{
    Id = AndroidWeatherChannelId,
    Name = "Weather Updates",
    Description = "Notifications when the weather over your garden changes",
    Importance = Importance.Default
};
AndroidNotificationCenter.RegisterNotificationChannel(weatherChannel);
```

**Step 2: Add SaveWeatherData method to NotificationService**

Add this method to `NotificationService.cs` after `InitializePlatform()`:

```csharp
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
```

**Step 3: Call SaveWeatherData from WeatherService after each successful fetch**

In `WeatherService.cs`, `FetchWeather()` method, after `OnWeatherUpdated?.Invoke(weather);` (line ~140), add:

```csharp
NotificationService.Instance?.SaveWeatherData(apiKey, latitude, longitude, weather.condition);
```

Also add a call to register Android WorkManager after location resolves. In `InitializeLocation()`, inside the `Input.location.status == LocationServiceStatus.Running` block, after `StartCoroutine(FetchWeatherLoop());` add:

```csharp
#if UNITY_ANDROID
using var plugin = new AndroidJavaClass("com.garden.WeatherPrefsPlugin");
using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
using var context = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
plugin.CallStatic("scheduleWeatherFetch", context);
#endif
```

**Step 4: Commit**

```bash
git add Assets/Scripts/Services/NotificationService.cs Assets/Scripts/Services/WeatherService.cs
git commit -m "feat: persist weather data for native background fetch bridge"
```

---

### Task 2: Android — WeatherPrefsPlugin (JNI bridge)

**Files:**
- Create: `Assets/Plugins/Android/WeatherPrefsPlugin.java`

**Step 1: Create plugin directory and file**

```bash
mkdir -p Assets/Plugins/Android
```

Create `Assets/Plugins/Android/WeatherPrefsPlugin.java`:

```java
package com.garden;

import android.content.Context;
import android.content.SharedPreferences;
import androidx.work.ExistingPeriodicWorkPolicy;
import androidx.work.PeriodicWorkRequest;
import androidx.work.WorkManager;
import java.util.concurrent.TimeUnit;

public class WeatherPrefsPlugin {

    private static final String PREFS_NAME = "garden_weather";

    /** Called from Unity C# after each successful weather fetch. */
    public static void saveWeatherData(Context context, String apiKey,
                                       float lat, float lon, int condition) {
        context.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE)
               .edit()
               .putString("api_key", apiKey)
               .putFloat("lat", lat)
               .putFloat("lon", lon)
               .putInt("condition", condition)
               .apply();
    }

    /** Called from Unity C# once after GPS resolves. Registers the WorkManager job. */
    public static void scheduleWeatherFetch(Context context) {
        PeriodicWorkRequest request = new PeriodicWorkRequest.Builder(
                WeatherFetchWorker.class, 15, TimeUnit.MINUTES)
                .build();
        WorkManager.getInstance(context).enqueueUniquePeriodicWork(
                "garden_weather_fetch",
                ExistingPeriodicWorkPolicy.KEEP,
                request);
    }
}
```

**Step 2: Commit**

```bash
git add Assets/Plugins/Android/WeatherPrefsPlugin.java
git commit -m "feat: add Android JNI plugin for weather prefs and WorkManager scheduling"
```

---

### Task 3: Android — WeatherFetchWorker

**Files:**
- Create: `Assets/Plugins/Android/WeatherFetchWorker.java`

**Step 1: Create the Worker**

Create `Assets/Plugins/Android/WeatherFetchWorker.java`:

```java
package com.garden;

import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.content.Context;
import android.content.SharedPreferences;
import android.os.Build;
import androidx.annotation.NonNull;
import androidx.core.app.NotificationCompat;
import androidx.work.Worker;
import androidx.work.WorkerParameters;
import org.json.JSONObject;
import java.io.BufferedReader;
import java.io.InputStreamReader;
import java.net.HttpURLConnection;
import java.net.URL;

public class WeatherFetchWorker extends Worker {

    private static final String PREFS_NAME   = "garden_weather";
    private static final String CHANNEL_ID   = "garden_weather";
    private static final String CHANNEL_NAME = "Weather Updates";

    public WeatherFetchWorker(@NonNull Context ctx, @NonNull WorkerParameters params) {
        super(ctx, params);
    }

    @NonNull
    @Override
    public Result doWork() {
        SharedPreferences prefs = getApplicationContext()
                .getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE);

        String apiKey = prefs.getString("api_key", null);
        float lat     = prefs.getFloat("lat", 0f);
        float lon     = prefs.getFloat("lon", 0f);
        int lastCond  = prefs.getInt("condition", 0);

        if (apiKey == null || apiKey.isEmpty()) return Result.success();

        try {
            String urlStr = "https://api.openweathermap.org/data/2.5/weather"
                    + "?lat=" + lat + "&lon=" + lon
                    + "&appid=" + apiKey + "&units=metric";
            HttpURLConnection conn = (HttpURLConnection) new URL(urlStr).openConnection();
            conn.setConnectTimeout(10_000);
            conn.setReadTimeout(10_000);

            if (conn.getResponseCode() != 200) return Result.failure();

            BufferedReader br = new BufferedReader(
                    new InputStreamReader(conn.getInputStream()));
            StringBuilder sb = new StringBuilder();
            String line;
            while ((line = br.readLine()) != null) sb.append(line);
            br.close();

            JSONObject json = new JSONObject(sb.toString());
            int weatherId  = json.getJSONArray("weather")
                                 .getJSONObject(0).getInt("id");
            int newCond    = mapCondition(weatherId);

            if (newCond == lastCond) return Result.success();

            // Condition changed — persist new value and notify.
            prefs.edit().putInt("condition", newCond).apply();
            sendNotification(newCond);
            return Result.success();

        } catch (Exception e) {
            return Result.failure();
        }
    }

    /** Maps OWM weather ID to WeatherCondition int (must match C# enum). */
    private int mapCondition(int id) {
        if (id >= 200 && id < 300) return 3; // Storm
        if (id >= 300 && id < 600) return 2; // Rain
        if (id >= 600 && id < 700) return 4; // Snow
        if (id >= 801)             return 1; // Cloudy
        return 0;                            // Clear
    }

    private String messageForCondition(int cond) {
        switch (cond) {
            case 1:  return "Clouds have rolled in over your garden";
            case 2:  return "Rain has arrived — your plants are drinking deep";
            case 3:  return "A storm is brewing over your garden";
            case 4:  return "Snow is falling on your garden";
            default: return "Clear skies over your garden";
        }
    }

    private void sendNotification(int condition) {
        Context ctx = getApplicationContext();
        NotificationManager nm =
                (NotificationManager) ctx.getSystemService(Context.NOTIFICATION_SERVICE);

        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            NotificationChannel ch = new NotificationChannel(
                    CHANNEL_ID, CHANNEL_NAME, NotificationManager.IMPORTANCE_DEFAULT);
            nm.createNotificationChannel(ch);
        }

        NotificationCompat.Builder builder = new NotificationCompat.Builder(ctx, CHANNEL_ID)
                .setSmallIcon(android.R.drawable.ic_dialog_info)
                .setContentTitle("\uD83C\uDF3F Weather Update")
                .setContentText(messageForCondition(condition))
                .setAutoCancel(true);

        nm.notify(9000, builder.build());
    }
}
```

**Step 2: Commit**

```bash
git add Assets/Plugins/Android/WeatherFetchWorker.java
git commit -m "feat: add Android WorkManager worker for background weather fetch"
```

---

### Task 4: Android — Gradle dependency for WorkManager

Unity needs a custom `mainTemplate.gradle` to declare the WorkManager dependency. If one doesn't exist yet, Unity generates a default — we override it.

**Files:**
- Create: `Assets/Plugins/Android/mainTemplate.gradle`

**Step 1: Check if mainTemplate.gradle already exists**

```bash
find Assets/Plugins/Android -name "*.gradle" 2>/dev/null
```

If it exists, open it. If not, generate it first via Unity menu: **Edit → Project Settings → Player → Android → Publishing Settings → Custom Main Gradle Template** (tick the checkbox). Unity will create `Assets/Plugins/Android/mainTemplate.gradle`. Then open it.

**Step 2: Add WorkManager dependency**

Find the `dependencies { ... }` block and add inside it:

```gradle
implementation 'androidx.work:work-runtime:2.9.0'
implementation 'androidx.core:core:1.12.0'
```

**Step 3: Commit**

```bash
git add Assets/Plugins/Android/mainTemplate.gradle
git commit -m "feat: add WorkManager gradle dependency for weather background fetch"
```

---

### Task 5: iOS — WeatherFetchController native Obj-C

**Files:**
- Create: `Assets/Plugins/iOS/WeatherFetchController.m`

**Step 1: Create plugin directory**

```bash
mkdir -p Assets/Plugins/iOS
```

**Step 2: Create the file**

Create `Assets/Plugins/iOS/WeatherFetchController.m`:

```objc
#import "UnityAppController.h"
@import UserNotifications;
@import Foundation;

// NSUserDefaults keys — must match PlayerPrefs keys set from C#.
static NSString* const kApiKey    = @"weather_api_key";
static NSString* const kLatKey    = @"weather_lat";
static NSString* const kLonKey    = @"weather_lon";
static NSString* const kCondKey   = @"weather_condition";

@implementation UnityAppController (WeatherFetch)

- (void)application:(UIApplication*)application
    performFetchWithCompletionHandler:(void (^)(UIBackgroundFetchResult))completionHandler
{
    NSUserDefaults* defaults = [NSUserDefaults standardUserDefaults];
    NSString* apiKey  = [defaults stringForKey:kApiKey];
    double lat        = [defaults doubleForKey:kLatKey];
    double lon        = [defaults doubleForKey:kLonKey];
    NSInteger lastCond = [defaults integerForKey:kCondKey];

    if (!apiKey.length) {
        completionHandler(UIBackgroundFetchResultNoData);
        return;
    }

    NSString* urlStr = [NSString stringWithFormat:
        @"https://api.openweathermap.org/data/2.5/weather"
        @"?lat=%f&lon=%f&appid=%@&units=metric", lat, lon, apiKey];
    NSURL* url = [NSURL URLWithString:urlStr];

    NSURLSessionDataTask* task = [[NSURLSession sharedSession]
        dataTaskWithURL:url
      completionHandler:^(NSData* data, NSURLResponse* response, NSError* error) {

        if (error || !data) {
            completionHandler(UIBackgroundFetchResultFailed);
            return;
        }

        NSError* jsonErr;
        NSDictionary* json = [NSJSONSerialization JSONObjectWithData:data
                                                            options:0
                                                              error:&jsonErr];
        if (jsonErr || !json) {
            completionHandler(UIBackgroundFetchResultFailed);
            return;
        }

        NSArray* weatherArr = json[@"weather"];
        if (!weatherArr.count) {
            completionHandler(UIBackgroundFetchResultFailed);
            return;
        }

        NSInteger weatherId = [weatherArr[0][@"id"] integerValue];
        NSInteger newCond   = [self wf_mapCondition:weatherId];

        if (newCond == lastCond) {
            completionHandler(UIBackgroundFetchResultNoData);
            return;
        }

        [defaults setInteger:newCond forKey:kCondKey];
        [defaults synchronize];

        [self wf_scheduleNotificationForCondition:newCond];
        completionHandler(UIBackgroundFetchResultNewData);
    }];

    [task resume];
}

/// Maps OWM weather ID → WeatherCondition int. Must match C# MapCondition().
- (NSInteger)wf_mapCondition:(NSInteger)wid
{
    if (wid >= 200 && wid < 300) return 3; // Storm
    if (wid >= 300 && wid < 600) return 2; // Rain
    if (wid >= 600 && wid < 700) return 4; // Snow
    if (wid >= 801)              return 1; // Cloudy
    return 0;                              // Clear
}

- (NSString*)wf_messageForCondition:(NSInteger)cond
{
    switch (cond) {
        case 1:  return @"Clouds have rolled in over your garden";
        case 2:  return @"Rain has arrived \u2014 your plants are drinking deep";
        case 3:  return @"A storm is brewing over your garden";
        case 4:  return @"Snow is falling on your garden";
        default: return @"Clear skies over your garden";
    }
}

- (void)wf_scheduleNotificationForCondition:(NSInteger)cond
{
    UNUserNotificationCenter* center =
        [UNUserNotificationCenter currentNotificationCenter];

    UNMutableNotificationContent* content =
        [[UNMutableNotificationContent alloc] init];
    content.title = @"\U0001F33F Weather Update";
    content.body  = [self wf_messageForCondition:cond];
    content.sound = [UNNotificationSound defaultSound];

    // Fire after 1 second (minimum allowed interval for local notifications).
    UNTimeIntervalNotificationTrigger* trigger =
        [UNTimeIntervalNotificationTrigger triggerWithTimeInterval:1 repeats:NO];

    UNNotificationRequest* request =
        [UNNotificationRequest requestWithIdentifier:@"weather_change"
                                             content:content
                                             trigger:trigger];

    [center addNotificationRequest:request withCompletionHandler:nil];
}

@end
```

**Step 3: Commit**

```bash
git add Assets/Plugins/iOS/WeatherFetchController.m
git commit -m "feat: add iOS Obj-C background fetch handler for weather change notifications"
```

---

### Task 6: iOS — Post-build script to enable Background Fetch capability

**Files:**
- Create: `Assets/Editor/WeatherFetchBuildProcessor.cs`

The Xcode project needs `UIBackgroundModes` → `fetch` in `Info.plist`. A Unity post-build script handles this automatically on every build.

**Step 1: Create the editor script**

Create `Assets/Editor/WeatherFetchBuildProcessor.cs`:

```csharp
#if UNITY_IOS
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using System.IO;

namespace Garden.Editor
{
    public static class WeatherFetchBuildProcessor
    {
        [PostProcessBuild(100)]
        public static void OnPostProcessBuild(BuildTarget target, string buildPath)
        {
            if (target != BuildTarget.iOS) return;

            string plistPath = Path.Combine(buildPath, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);

            var root = plist.root;

            // Add fetch to UIBackgroundModes (create array if absent).
            const string key = "UIBackgroundModes";
            if (!root.values.ContainsKey(key))
                root.CreateArray(key);

            var modes = root[key].AsArray();

            // Only add if not already present.
            bool hasFetch = false;
            foreach (var item in modes.values)
            {
                if (item.AsString() == "fetch") { hasFetch = true; break; }
            }
            if (!hasFetch)
                modes.AddString("fetch");

            plist.WriteToFile(plistPath);
        }
    }
}
#endif
```

**Step 2: Commit**

```bash
git add Assets/Editor/WeatherFetchBuildProcessor.cs
git commit -m "feat: add post-build processor to enable iOS background fetch capability"
```

---

### Task 7: Manual smoke test checklist

There are no unit tests for this feature (it's entirely platform I/O). Test manually using the Device Simulator / a physical device.

**iOS:**
1. Build to a physical iPhone (Background Fetch doesn't work in Simulator).
2. Launch app → let GPS and weather fetch run → close app.
3. In Xcode Organizer or via `Debug → Simulate Background Fetch` in the Xcode scheme, trigger a fetch.
4. Confirm: if weather condition differs from stored value, a notification appears in Notification Center.
5. Confirm: if condition is the same, no notification fires.

**Android:**
1. Build to a physical device or emulator with API 26+.
2. Launch app → let GPS resolve → close app.
3. Wait 15+ minutes (or use `adb shell am broadcast -a androidx.work.diagnostics.REQUEST_DIAGNOSTICS_INFO` to inspect WorkManager state).
4. Alternatively, temporarily change the work interval to 1 minute for testing, then revert.
5. Confirm notification appears when condition changes.

**Edge cases to verify:**
- First launch with no stored condition (default 0 = Clear) — if real condition is non-Clear, first fetch fires a notification. Acceptable.
- No API key stored (secrets file missing) — no notification, no crash.
- Notification permission not granted — silent drop, no crash.
