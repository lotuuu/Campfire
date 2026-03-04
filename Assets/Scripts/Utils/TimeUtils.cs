using System;
using UnityEngine;

namespace Garden
{
    public static class TimeUtils
    {
        /// <summary>Formats a duration in hours to a human-readable string (e.g. "30s", "5m", "1h 30m").</summary>
        public static string FormatDurationHours(float hours)
        {
            float totalSeconds = hours * 3600f;
            if (totalSeconds < 60f)
            {
                int secs = Mathf.Max(1, Mathf.RoundToInt(totalSeconds));
                return $"{secs}s";
            }
            int totalMinutes = Mathf.RoundToInt(totalSeconds / 60f);
            if (totalMinutes < 60) return $"{totalMinutes}m";
            int h = totalMinutes / 60;
            int m = totalMinutes % 60;
            return m > 0 ? $"{h}h {m}m" : $"{h}h";
        }

        public static TimeOfDay GetTimeOfDay(DateTime time, float sunriseHour = 6f, float sunsetHour = 18f)
        {
            float hour = time.Hour + time.Minute / 60f;
            float goldenStart = sunsetHour - 1f;

            if (hour >= goldenStart && hour <= sunsetHour)
                return TimeOfDay.GoldenHour;
            if (hour < sunriseHour || hour > sunsetHour)
                return TimeOfDay.Night;
            return TimeOfDay.Day;
        }

        public static bool IsNight(DateTime time, float sunriseHour = 6f, float sunsetHour = 18f)
        {
            float hour = time.Hour + time.Minute / 60f;
            return hour < sunriseHour || hour > sunsetHour;
        }

        public static bool IsGoldenHour(DateTime time, float sunsetHour = 18f)
        {
            float hour = time.Hour + time.Minute / 60f;
            return hour >= sunsetHour - 1f && hour <= sunsetHour;
        }
    }
}