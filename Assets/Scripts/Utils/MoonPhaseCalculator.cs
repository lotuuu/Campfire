using System;

namespace Garden
{
    public static class MoonPhaseCalculator
    {
        public static MoonPhase Calculate(DateTime date)
        {
            int year = date.Year;
            int month = date.Month;
            int day = date.Day;

            if (month < 3) { year--; month += 12; }
            int a = year / 100;
            int b = a / 4;
            int c = 2 - a + b;
            int e = (int)(365.25 * (year + 4716));
            int f = (int)(30.6001 * (month + 1));
            double jd = c + day + e + f - 1524.5;
            double daysSinceNew = jd - 2451549.5;
            double cycles = daysSinceNew / 29.53058770576;
            double phase = (cycles - Math.Floor(cycles)) * 29.53;

            return phase switch
            {
                < 1.85 => MoonPhase.NewMoon,
                < 5.54 => MoonPhase.WaxingCrescent,
                < 9.23 => MoonPhase.FirstQuarter,
                < 12.91 => MoonPhase.WaxingGibbous,
                < 16.61 => MoonPhase.FullMoon,
                < 20.30 => MoonPhase.WaningGibbous,
                < 23.99 => MoonPhase.LastQuarter,
                < 27.68 => MoonPhase.WaningCrescent,
                _ => MoonPhase.NewMoon
            };
        }
    }
}