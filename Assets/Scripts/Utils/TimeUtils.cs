using System;

namespace Garden
{
    public static class TimeUtils
    {
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