using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Garden.Tests
{
    public class TestGardenManager
    {
        [Test]
        public void GetGrowthProgress_HalfwayThrough_Returns05()
        {
            var garden = new GardenSave
            {
                plantTimeUtc = DateTime.UtcNow.AddHours(-12).ToString("o"),
                mature = false
            };
            float progress = GardenManager.GetGrowthProgress(garden, 24f, DateTime.UtcNow);
            Assert.AreEqual(0.5f, progress, 0.05f);
        }

        [Test]
        public void GetGrowthProgress_Mature_Returns1()
        {
            var garden = new GardenSave { mature = true };
            float progress = GardenManager.GetGrowthProgress(garden, 24f, DateTime.UtcNow);
            Assert.AreEqual(1f, progress);
        }

        [Test]
        public void GetGrowthProgress_NoPlantTime_Returns0()
        {
            var garden = new GardenSave { mature = false, plantTimeUtc = null };
            float progress = GardenManager.GetGrowthProgress(garden, 24f, DateTime.UtcNow);
            Assert.AreEqual(0f, progress);
        }

        [Test]
        public void CheckYieldReady_PastInterval_ReturnsTrue()
        {
            var garden = new GardenSave
            {
                mature = true,
                lastYieldTimeUtc = DateTime.UtcNow.AddHours(-13).ToString("o")
            };
            Assert.IsTrue(GardenManager.CheckYieldReady(garden, 12f, DateTime.UtcNow));
        }

        [Test]
        public void CheckYieldReady_BeforeInterval_ReturnsFalse()
        {
            var garden = new GardenSave
            {
                mature = true,
                lastYieldTimeUtc = DateTime.UtcNow.AddHours(-6).ToString("o")
            };
            Assert.IsFalse(GardenManager.CheckYieldReady(garden, 12f, DateTime.UtcNow));
        }

        [Test]
        public void CheckYieldReady_NotMature_ReturnsFalse()
        {
            var garden = new GardenSave
            {
                mature = false,
                lastYieldTimeUtc = DateTime.UtcNow.AddHours(-100).ToString("o")
            };
            Assert.IsFalse(GardenManager.CheckYieldReady(garden, 12f, DateTime.UtcNow));
        }

        // ── BuildingCost DTO Tests ──────────────────────

        [Test]
        public void BuildingCost_HasManaCostAndHarvestCosts()
        {
            var cost = new BuildingCost
            {
                manaCost = 200,
                harvestCosts = new List<HarvestCost>
                {
                    new() { itemName = "Basil_harvest", count = 5 }
                }
            };
            Assert.AreEqual(200, cost.manaCost);
            Assert.AreEqual(1, cost.harvestCosts.Count);
            Assert.AreEqual("Basil_harvest", cost.harvestCosts[0].itemName);
        }

        [Test]
        public void BuildingCost_DefaultHarvestCosts_IsEmpty()
        {
            var cost = new BuildingCost();
            Assert.IsNotNull(cost.harvestCosts);
            Assert.AreEqual(0, cost.harvestCosts.Count);
        }
    }
}
