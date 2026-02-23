using System;
using System.Collections.Generic;

namespace Garden
{
    public static class CalendarEvents
    {
        private static readonly Dictionary<(int month, int day), CalendarEvent> FixedEvents = new()
        {
            { (3, 20), CalendarEvent.SpringEquinox },
            { (9, 22), CalendarEvent.FallEquinox },
        };

        private static readonly HashSet<(int year, int month, int day)> LunarEclipses = new()
        {
            (2025, 3, 14), (2025, 9, 7),
            (2026, 3, 3), (2026, 8, 28),
            (2027, 2, 20), (2027, 7, 18), (2027, 8, 17),
            (2028, 1, 12), (2028, 7, 6), (2028, 12, 31),
        };

        public static CalendarEvent GetEvent(DateTime date)
        {
            if (LunarEclipses.Contains((date.Year, date.Month, date.Day)))
                return CalendarEvent.LunarEclipse;

            if (FixedEvents.TryGetValue((date.Month, date.Day), out var ev))
                return ev;

            var yesterday = date.AddDays(-1);
            if (FixedEvents.TryGetValue((yesterday.Month, yesterday.Day), out var evY))
                return evY;
            var tomorrow = date.AddDays(1);
            if (FixedEvents.TryGetValue((tomorrow.Month, tomorrow.Day), out var evT))
                return evT;

            return CalendarEvent.None;
        }
    }
}