using UnityEngine;

namespace Garden
{
    [System.Serializable]
    public class TriggerCondition
    {
        [Header("Temperature")]
        public bool useTemperature;
        public float minTemp = -50f;
        public float maxTemp = 60f;

        [Header("Weather")]
        public bool useWeatherCondition;
        public WeatherCondition[] requiredConditions;

        [Header("Wind")]
        public bool useWindSpeed;
        public float minWindSpeed;

        [Header("Humidity")]
        public bool useHumidity;
        public float minHumidity;

        [Header("Time")]
        public bool useTimeOfDay;
        public TimeOfDay requiredTimeOfDay;

        [Header("Moon")]
        public bool useMoonPhase;
        public MoonPhase requiredMoonPhase;

        [Header("Calendar")]
        public bool useCalendarEvent;
        public CalendarEvent requiredCalendarEvent;

        public bool Evaluate(WeatherData weather)
        {
            if (useCalendarEvent && weather.calendarEvent != requiredCalendarEvent) return false;
            if (useTemperature && (weather.temperature < minTemp || weather.temperature > maxTemp)) return false;
            if (useWeatherCondition)
            {
                bool match = false;
                foreach (var c in requiredConditions)
                    if (c == weather.condition) { match = true; break; }
                if (!match) return false;
            }
            if (useWindSpeed && weather.windSpeed < minWindSpeed) return false;
            if (useHumidity && weather.humidity < minHumidity) return false;
            if (useTimeOfDay && weather.timeOfDay != requiredTimeOfDay) return false;
            if (useMoonPhase && weather.moonPhase != requiredMoonPhase) return false;
            return true;
        }
    }
}