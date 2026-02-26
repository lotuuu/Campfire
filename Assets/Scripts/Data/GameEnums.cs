namespace Garden
{
    public enum Rarity { Common, Uncommon, Rare, Epic, Legendary }

    public enum WeatherCondition { Clear, Cloudy, Rain, Storm, Snow }

    public enum MoonPhase
    {
        NewMoon, WaxingCrescent, FirstQuarter, WaxingGibbous,
        FullMoon, WaningGibbous, LastQuarter, WaningCrescent
    }

    public enum TimeOfDay { Day, Night, GoldenHour }

    public enum CalendarEvent { None, SpringEquinox, FallEquinox, LunarEclipse }

    public enum CurrencyType { Gold, SunShards, Pollen }

    public enum PlantState { Empty, Growing, Mature }

    public enum QualityTier { D, C, B, A, S }
}
