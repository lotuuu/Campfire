using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Garden.Tests
{
    public class TestRainAndWatering
    {
        // Task 2: CanWaterPlot tests
        [Test]
        public void CanWater_NeverWatered_ReturnsTrue()
        {
            var plot = new PlotSave { state = PlotState.Growing, lastWateredUtc = null };
            bool result = PlotManager.CanWaterPlot(plot, DateTime.UtcNow, PlotManager.ManualWaterCooldownHours);
            Assert.IsTrue(result);
        }

        [Test]
        public void CanWater_WateredRecently_ReturnsFalse()
        {
            string recent = DateTime.UtcNow.AddMinutes(-30).ToString("o");
            var plot = new PlotSave { state = PlotState.Growing, lastWateredUtc = recent };
            bool result = PlotManager.CanWaterPlot(plot, DateTime.UtcNow, PlotManager.ManualWaterCooldownHours);
            Assert.IsFalse(result);
        }

        [Test]
        public void CanWater_WateredOverTwoHoursAgo_ReturnsTrue()
        {
            string old = DateTime.UtcNow.AddHours(-3).ToString("o");
            var plot = new PlotSave { state = PlotState.Growing, lastWateredUtc = old };
            bool result = PlotManager.CanWaterPlot(plot, DateTime.UtcNow, PlotManager.ManualWaterCooldownHours);
            Assert.IsTrue(result);
        }

        [Test]
        public void CanWater_RainCooldown_SixHours()
        {
            string threeHoursAgo = DateTime.UtcNow.AddHours(-3).ToString("o");
            var plot = new PlotSave { state = PlotState.Growing, lastWateredUtc = threeHoursAgo };
            bool manual = PlotManager.CanWaterPlot(plot, DateTime.UtcNow, PlotManager.ManualWaterCooldownHours);
            bool rain = PlotManager.CanWaterPlot(plot, DateTime.UtcNow, PlotManager.RainWaterCooldownHours);
            Assert.IsTrue(manual);
            Assert.IsFalse(rain);
        }

        // Task 3: ApplyWatering tests
        [Test]
        public void ApplyWatering_SetsLastWateredUtc()
        {
            var plot = new PlotSave { state = PlotState.Growing, waterCount = 0, lastWateredUtc = null };
            string now = DateTime.UtcNow.ToString("o");
            PlotManager.ApplyWatering(plot, now);
            Assert.AreEqual(1, plot.waterCount);
            Assert.AreEqual(now, plot.lastWateredUtc);
        }

        [Test]
        public void ApplyWatering_IncrementsWaterCount()
        {
            var plot = new PlotSave { state = PlotState.Growing, waterCount = 2, lastWateredUtc = null };
            string now = DateTime.UtcNow.ToString("o");
            PlotManager.ApplyWatering(plot, now);
            Assert.AreEqual(3, plot.waterCount);
        }

        // Task 4: RainFillAllVases tests
        [Test]
        public void RainFillAllVases_FillsEmptyAndFillingVases()
        {
            var vases = new List<VaseSave>
            {
                new VaseSave { capacity = 5, currentWater = 0, state = VaseState.Empty },
                new VaseSave { capacity = 5, currentWater = 0, state = VaseState.Filling, fillStartTimeUtc = DateTime.UtcNow.ToString("o") },
                new VaseSave { capacity = 5, currentWater = 5, state = VaseState.Full },
            };

            VaseManager.RainFillAllVases(vases);

            Assert.AreEqual(VaseState.Full, vases[0].state);
            Assert.AreEqual(5, vases[0].currentWater);
            Assert.AreEqual(VaseState.Full, vases[1].state);
            Assert.AreEqual(5, vases[1].currentWater);
            Assert.IsNull(vases[1].fillStartTimeUtc);
            Assert.AreEqual(VaseState.Full, vases[2].state);
        }

        // Task 5: RainWaterAllPlots tests
        [Test]
        public void RainWaterAllPlots_WatersGrowingPlotsWithExpiredCooldown()
        {
            var plots = new List<PlotSave>
            {
                new PlotSave { state = PlotState.Growing, waterCount = 0, lastWateredUtc = null },
                new PlotSave { state = PlotState.Growing, waterCount = 1, lastWateredUtc = DateTime.UtcNow.AddHours(-7).ToString("o") },
                new PlotSave { state = PlotState.Growing, waterCount = 1, lastWateredUtc = DateTime.UtcNow.AddHours(-3).ToString("o") },
                new PlotSave { state = PlotState.Empty, waterCount = 0, lastWateredUtc = null },
                new PlotSave { state = PlotState.Mature, waterCount = 2, lastWateredUtc = null },
            };

            int watered = PlotManager.RainWaterAllPlots(plots, DateTime.UtcNow);

            Assert.AreEqual(2, watered);
            Assert.AreEqual(1, plots[0].waterCount);
            Assert.IsNotNull(plots[0].lastWateredUtc);
            Assert.AreEqual(2, plots[1].waterCount);
            Assert.AreEqual(1, plots[2].waterCount);
            Assert.AreEqual(0, plots[3].waterCount);
            Assert.AreEqual(2, plots[4].waterCount);
        }

        // Task 6: CheckRainEvent tests
        [Test]
        public void CheckRainEvent_FirstRainPoll_ReturnsFalse()
        {
            var data = new SaveData();
            bool triggered = PlotManager.CheckRainEvent(data, WeatherCondition.Rain, DateTime.UtcNow);
            Assert.IsFalse(triggered);
            Assert.IsNotNull(data.rainStartTimeUtc);
        }

        [Test]
        public void CheckRainEvent_RainFor15Min_ReturnsTrue()
        {
            var data = new SaveData();
            data.rainStartTimeUtc = DateTime.UtcNow.AddMinutes(-16).ToString("o");
            bool triggered = PlotManager.CheckRainEvent(data, WeatherCondition.Rain, DateTime.UtcNow);
            Assert.IsTrue(triggered);
            Assert.IsNotNull(data.lastRainEffectTimeUtc);
        }

        [Test]
        public void CheckRainEvent_AlreadyTriggeredThisRain_ReturnsFalse()
        {
            var data = new SaveData();
            data.rainStartTimeUtc = DateTime.UtcNow.AddMinutes(-30).ToString("o");
            data.lastRainEffectTimeUtc = DateTime.UtcNow.AddMinutes(-15).ToString("o");
            bool triggered = PlotManager.CheckRainEvent(data, WeatherCondition.Rain, DateTime.UtcNow);
            Assert.IsFalse(triggered);
        }

        [Test]
        public void CheckRainEvent_ClearWeather_ClearsTimer()
        {
            var data = new SaveData();
            data.rainStartTimeUtc = DateTime.UtcNow.AddMinutes(-10).ToString("o");
            bool triggered = PlotManager.CheckRainEvent(data, WeatherCondition.Clear, DateTime.UtcNow);
            Assert.IsFalse(triggered);
            Assert.IsNull(data.rainStartTimeUtc);
        }

        [Test]
        public void CheckRainEvent_StormCountsAsRain()
        {
            var data = new SaveData();
            data.rainStartTimeUtc = DateTime.UtcNow.AddMinutes(-20).ToString("o");
            bool triggered = PlotManager.CheckRainEvent(data, WeatherCondition.Storm, DateTime.UtcNow);
            Assert.IsTrue(triggered);
        }

        [Test]
        public void CheckRainEvent_NewRainAfterClear_TriggersAgain()
        {
            var data = new SaveData();
            data.lastRainEffectTimeUtc = DateTime.UtcNow.AddHours(-2).ToString("o");
            data.rainStartTimeUtc = DateTime.UtcNow.AddMinutes(-20).ToString("o");
            bool triggered = PlotManager.CheckRainEvent(data, WeatherCondition.Rain, DateTime.UtcNow);
            Assert.IsTrue(triggered);
        }
    }
}
