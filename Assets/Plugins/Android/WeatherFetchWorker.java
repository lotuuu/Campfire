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
