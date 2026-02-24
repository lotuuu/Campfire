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
