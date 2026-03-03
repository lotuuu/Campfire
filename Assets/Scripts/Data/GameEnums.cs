namespace Garden
{
    public enum WeatherCondition { Clear, Cloudy, Rain, Storm, Snow }

    public enum MoonPhase
    {
        NewMoon, WaxingCrescent, FirstQuarter, WaxingGibbous,
        FullMoon, WaningGibbous, LastQuarter, WaningCrescent
    }

    public enum TimeOfDay { Day, Night, GoldenHour }

    public enum CalendarEvent { None, SpringEquinox, FallEquinox, LunarEclipse }

    public enum CurrencyType { Mana, Water, Gems }

    public enum PlotState { Empty, Growing, Mature }

    public enum VaseState { Empty, Filling, Full }

    public enum CampBuildingType { None, Flame, Plot, Vase, Garden, Apotheke }
}
