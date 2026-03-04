namespace Garden
{
    [System.Serializable]
    public struct WeatherData
    {
        public float temperature;
        public float humidity;
        public float windSpeed;
        public WeatherCondition condition;
        public float cloudCover;
        public float sunriseHour; // local time as fractional hour (6.5 = 6:30 AM)
        public float sunsetHour;  // local time as fractional hour (18.75 = 6:45 PM)
        public bool isNight;
        public bool isGoldenHour;
        public TimeOfDay timeOfDay;
        public MoonPhase moonPhase;
        public CalendarEvent calendarEvent;
    }

    [System.Serializable]
    public struct DailyForecast
    {
        public string dayLabel;
        public float tempHigh;
        public float tempLow;
        public WeatherCondition condition;
        public MoonPhase moonPhase;
        public float humidity;
        public float windSpeed;
        public float cloudCover;
    }
}