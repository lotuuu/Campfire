using System;
using NUnit.Framework;

namespace Garden.Tests
{
    public class TestWeatherService
    {
        private WeatherData MakeWeather(bool isNight = false, bool isGoldenHour = false,
            float sunriseHour = 6.5f, float sunsetHour = 18.5f)
        {
            return new WeatherData
            {
                condition = WeatherCondition.Clear,
                sunriseHour = sunriseHour,
                sunsetHour = sunsetHour,
                isNight = isNight,
                isGoldenHour = isGoldenHour,
                timeOfDay = isNight ? TimeOfDay.Night
                    : isGoldenHour ? TimeOfDay.GoldenHour : TimeOfDay.Day
            };
        }

        // ── Day → Night transition ──

        [Test]
        public void UpdateTimeOfDay_DayToNight_ReturnsTrue()
        {
            var weather = MakeWeather(isNight: false);
            var nightTime = new DateTime(2026, 3, 18, 22, 0, 0, DateTimeKind.Local);

            bool changed = WeatherService.UpdateTimeOfDay(weather, nightTime, out var updated);

            Assert.IsTrue(changed);
            Assert.IsTrue(updated.isNight);
            Assert.IsFalse(updated.isGoldenHour);
            Assert.AreEqual(TimeOfDay.Night, updated.timeOfDay);
        }

        [Test]
        public void UpdateTimeOfDay_NightToDay_ReturnsTrue()
        {
            var weather = MakeWeather(isNight: true);
            var dayTime = new DateTime(2026, 3, 18, 12, 0, 0, DateTimeKind.Local);

            bool changed = WeatherService.UpdateTimeOfDay(weather, dayTime, out var updated);

            Assert.IsTrue(changed);
            Assert.IsFalse(updated.isNight);
            Assert.IsFalse(updated.isGoldenHour);
            Assert.AreEqual(TimeOfDay.Day, updated.timeOfDay);
        }

        // ── Golden hour transitions ──

        [Test]
        public void UpdateTimeOfDay_DayToGoldenHour_ReturnsTrue()
        {
            var weather = MakeWeather(isNight: false);
            // Golden hour = sunset - 1 to sunset = 17:30 to 18:30
            var goldenTime = new DateTime(2026, 3, 18, 18, 0, 0, DateTimeKind.Local);

            bool changed = WeatherService.UpdateTimeOfDay(weather, goldenTime, out var updated);

            Assert.IsTrue(changed);
            Assert.IsFalse(updated.isNight);
            Assert.IsTrue(updated.isGoldenHour);
            Assert.AreEqual(TimeOfDay.GoldenHour, updated.timeOfDay);
        }

        [Test]
        public void UpdateTimeOfDay_GoldenHourToNight_ReturnsTrue()
        {
            var weather = MakeWeather(isGoldenHour: true);
            var nightTime = new DateTime(2026, 3, 18, 19, 0, 0, DateTimeKind.Local);

            bool changed = WeatherService.UpdateTimeOfDay(weather, nightTime, out var updated);

            Assert.IsTrue(changed);
            Assert.IsTrue(updated.isNight);
            Assert.IsFalse(updated.isGoldenHour);
            Assert.AreEqual(TimeOfDay.Night, updated.timeOfDay);
        }

        // ── No-change scenarios ──

        [Test]
        public void UpdateTimeOfDay_StillDay_ReturnsFalse()
        {
            var weather = MakeWeather(isNight: false);
            var dayTime = new DateTime(2026, 3, 18, 12, 0, 0, DateTimeKind.Local);

            bool changed = WeatherService.UpdateTimeOfDay(weather, dayTime, out _);

            Assert.IsFalse(changed);
        }

        [Test]
        public void UpdateTimeOfDay_StillNight_ReturnsFalse()
        {
            var weather = MakeWeather(isNight: true);
            var nightTime = new DateTime(2026, 3, 18, 3, 0, 0, DateTimeKind.Local);

            bool changed = WeatherService.UpdateTimeOfDay(weather, nightTime, out _);

            Assert.IsFalse(changed);
        }

        [Test]
        public void UpdateTimeOfDay_StillGoldenHour_ReturnsFalse()
        {
            var weather = MakeWeather(isGoldenHour: true);
            var goldenTime = new DateTime(2026, 3, 18, 18, 0, 0, DateTimeKind.Local);

            bool changed = WeatherService.UpdateTimeOfDay(weather, goldenTime, out _);

            Assert.IsFalse(changed);
        }

        // ── Boundary cases ──

        [Test]
        public void UpdateTimeOfDay_ExactSunrise_IsDay()
        {
            var weather = MakeWeather(isNight: true);
            // sunriseHour = 6.5 → 6:30 AM
            var sunrise = new DateTime(2026, 3, 18, 6, 30, 0, DateTimeKind.Local);

            bool changed = WeatherService.UpdateTimeOfDay(weather, sunrise, out var updated);

            Assert.IsTrue(changed);
            Assert.IsFalse(updated.isNight);
        }

        [Test]
        public void UpdateTimeOfDay_ExactSunset_IsGoldenHour()
        {
            var weather = MakeWeather(isNight: false);
            // sunsetHour = 18.5 → 18:30; golden = 17:30–18:30
            var sunset = new DateTime(2026, 3, 18, 18, 30, 0, DateTimeKind.Local);

            bool changed = WeatherService.UpdateTimeOfDay(weather, sunset, out var updated);

            Assert.IsTrue(changed);
            Assert.IsTrue(updated.isGoldenHour);
        }

        [Test]
        public void UpdateTimeOfDay_JustAfterSunset_IsNight()
        {
            var weather = MakeWeather(isGoldenHour: true);
            var afterSunset = new DateTime(2026, 3, 18, 18, 31, 0, DateTimeKind.Local);

            bool changed = WeatherService.UpdateTimeOfDay(weather, afterSunset, out var updated);

            Assert.IsTrue(changed);
            Assert.IsTrue(updated.isNight);
        }

        // ── Original WeatherData is not mutated ──

        [Test]
        public void UpdateTimeOfDay_DoesNotMutateInput()
        {
            var weather = MakeWeather(isNight: false);
            var nightTime = new DateTime(2026, 3, 18, 22, 0, 0, DateTimeKind.Local);

            WeatherService.UpdateTimeOfDay(weather, nightTime, out _);

            Assert.IsFalse(weather.isNight, "Original struct should not be mutated");
        }

        // ── Custom sunrise/sunset ──

        [Test]
        public void UpdateTimeOfDay_CustomSunriseSunset()
        {
            var weather = MakeWeather(isNight: false, sunriseHour: 5f, sunsetHour: 20f);
            // 19:30 should be golden hour (20 - 1 = 19)
            var time = new DateTime(2026, 3, 18, 19, 30, 0, DateTimeKind.Local);

            bool changed = WeatherService.UpdateTimeOfDay(weather, time, out var updated);

            Assert.IsTrue(changed);
            Assert.IsTrue(updated.isGoldenHour);
        }
    }
}
