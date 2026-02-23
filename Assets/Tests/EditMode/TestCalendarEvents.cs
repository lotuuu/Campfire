using NUnit.Framework;
using System;

namespace Garden.Tests
{
    public class TestCalendarEvents
    {
        [Test]
        public void SpringEquinox_Detected()
        {
            Assert.AreEqual(CalendarEvent.SpringEquinox, CalendarEvents.GetEvent(new DateTime(2026, 3, 20)));
        }

        [Test]
        public void LunarEclipse_Detected()
        {
            Assert.AreEqual(CalendarEvent.LunarEclipse, CalendarEvents.GetEvent(new DateTime(2026, 3, 3)));
        }

        [Test]
        public void NormalDay_ReturnsNone()
        {
            Assert.AreEqual(CalendarEvent.None, CalendarEvents.GetEvent(new DateTime(2026, 6, 15)));
        }
    }
}