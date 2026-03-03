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