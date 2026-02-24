# Weather Change Push Notifications — Design

**Date:** 2026-02-24
**Status:** Approved

## Overview

Fire a local push notification when the device weather condition changes, even when the app is in the background. Purely on-device — no server required. Uses iOS Background Fetch and Android WorkManager to periodically poll the OpenWeatherMap API and compare against the last-known condition.

## Architecture

Native platform code (iOS/Android) wakes periodically, fetches the OpenWeatherMap API independently of Unity, compares the result to the last-known `WeatherCondition`, and fires a local notification if it changed. Unity C# shares the necessary data (API key, GPS coordinates, last condition) with native code via a small bridge each time the app successfully fetches weather.

## Data Flow

```
App open → WeatherService fetches → saves apiKey/lat/lon/condition
                                              ↓
  iOS: PlayerPrefs → NSUserDefaults     Android: Java plugin → SharedPreferences("garden_weather")
                                              ↓
   iOS background fetch                  Android WorkManager
   (system-triggered, ~15 min)           (15-min PeriodicWorkRequest)
                                              ↓
              Fetch weather API → compare condition → notify if changed → update stored condition
```

## Components

### C# — WeatherService (modified)
After every successful `FetchWeather()` call, save to the native bridge:
- `apiKey` (string)
- `latitude`, `longitude` (float)
- `(int)weather.condition`

On iOS: via `PlayerPrefs` (maps directly to NSUserDefaults).
On Android: via `AndroidJavaClass` call to `WeatherPrefsPlugin.saveWeatherData()`.

Also on Android: call `WeatherPrefsPlugin.scheduleWeatherFetch()` once in `Start()` (after location resolves) to ensure WorkManager is registered.

### C# — NotificationService (modified)
Add a second Android notification channel `"garden_weather"` for weather change notifications (separate from the existing `"garden_plants"` plant-growth channel).

### iOS — `Assets/Plugins/iOS/WeatherFetchController.m`
Obj-C category on `UnityAppController` implementing `application:performFetchWithCompletionHandler:`.

- Reads `apiKey`, `lat`, `lon`, `weather_condition` (int) from `NSUserDefaults`
- Calls OpenWeatherMap via `NSURLSession dataTaskWithURL:completionHandler:`
- Maps `weather[0].id` → condition int using the same ranges as C# `MapCondition()`
- If condition changed: updates NSUserDefaults, schedules a `UNUserNotificationCenter` local notification (trigger interval = 1s), calls `completionHandler(UIBackgroundFetchResultNewData)`
- If unchanged: calls `completionHandler(UIBackgroundFetchResultNoData)`
- On error: calls `completionHandler(UIBackgroundFetchResultFailed)`

### iOS — `Assets/Editor/WeatherFetchBuildProcessor.cs`
`IPostprocessBuildWithReport` script (order 100). Adds `UIBackgroundModes` array with value `fetch` to the generated `Info.plist` using `PlistDocument`. No AppDelegate.mm modification needed.

### Android — `Assets/Plugins/Android/WeatherPrefsPlugin.java`
JNI-callable (`public static`) methods:
- `saveWeatherData(Context, String apiKey, float lat, float lon, int condition)` — writes to `SharedPreferences("garden_weather")`
- `scheduleWeatherFetch(Context)` — enqueues a `PeriodicWorkRequest<WeatherFetchWorker>` with a 15-min interval, `ExistingPeriodicWorkPolicy.KEEP`

### Android — `Assets/Plugins/Android/WeatherFetchWorker.java`
`Worker` subclass:
- Reads SharedPreferences `"garden_weather"` for `apiKey`, `lat`, `lon`, `condition`
- Fetches weather via `HttpURLConnection`
- Maps `weather[0].id` → condition int
- If changed: posts a `NotificationManager` notification to channel `"garden_weather"`, updates SharedPreferences
- Returns `Result.success()` or `Result.failure()`

### Gradle — `mainTemplate.gradle`
Add dependency: `implementation 'androidx.work:work-runtime:2.9.0'`

## WeatherCondition Int Mapping (must match C# enum)

```
Clear  = 0
Cloudy = 1
Rain   = 2
Storm  = 3
Snow   = 4
```

OWM ID ranges → condition:
- 200–299 → Storm (3)
- 300–599 → Rain (2)
- 600–699 → Snow (4)
- 801+    → Cloudy (1)
- else    → Clear (0)

## Notification Messages

| Condition | Body |
|-----------|------|
| Clear     | "Clear skies over your garden" |
| Cloudy    | "Clouds have rolled in over your garden" |
| Rain      | "Rain has arrived — your plants are drinking deep" |
| Storm     | "A storm is brewing over your garden" |
| Snow      | "Snow is falling on your garden" |

Title for all: `"🌿 Weather Update"`

## SharedPreferences / NSUserDefaults Keys

| Key | Type | Set by |
|-----|------|--------|
| `weather_api_key` | String | C# (PlayerPrefs / Java plugin) |
| `weather_lat` | Float | C# (PlayerPrefs / Java plugin) |
| `weather_lon` | Float | C# (PlayerPrefs / Java plugin) |
| `weather_condition` | Int | C# on fetch; native on change |

## Edge Cases

- **First run / no stored condition**: defaults to 0 (Clear). If real condition differs, a notification fires on first background fetch — acceptable.
- **No API key or location stored**: native code skips fetch and returns NoData/success.
- **Notification permission not granted**: `UNUserNotificationCenter` and `NotificationManager` silently drop the request — no crash.
- **iOS**: System controls fetch frequency; 15 min is the minimum Android WorkManager interval and a hint on iOS.
