using System;
using NUnit.Framework;
using UnityEngine;

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

        // ── BuildingCostConfig Garden Cost Tests ──────────────────────

        [Test]
        public void BuildingCostConfig_GetGardenCost_ReturnsScalingCost()
        {
            var config = ScriptableObject.CreateInstance<BuildingCostConfig>();

            // Use reflection to set private gardenCosts field
            var field = typeof(BuildingCostConfig).GetField("gardenCosts",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field.SetValue(config, new System.Collections.Generic.List<BuildingCost>
            {
                new BuildingCost { manaCost = 200 },
                new BuildingCost { manaCost = 350 },
            });

            Assert.AreEqual(200, config.GetGardenCost(0).manaCost);
            Assert.AreEqual(350, config.GetGardenCost(1).manaCost);
            // Beyond list clamps to last
            Assert.AreEqual(350, config.GetGardenCost(5).manaCost);
        }

        [Test]
        public void BuildingCostConfig_GetGardenCost_EmptyList_ReturnsNull()
        {
            var config = ScriptableObject.CreateInstance<BuildingCostConfig>();
            Assert.IsNull(config.GetGardenCost(0));
        }
    }
}
