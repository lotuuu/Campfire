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

        // ── GardenCostTier / CraftGarden Tests ──────────────────────

        [Test]
        public void GardenPlantData_GetCost_ReturnsCorrectTier()
        {
            var data = ScriptableObject.CreateInstance<GardenPlantData>();
            data.costTiers = new System.Collections.Generic.List<GardenCostTier>
            {
                new GardenCostTier { manaCost = 200, seedCost = 1 },
                new GardenCostTier { manaCost = 300, seedCost = 2 },
            };
            Assert.AreEqual(200, data.GetCost(0).manaCost);
            Assert.AreEqual(2, data.GetCost(1).seedCost);
            Assert.IsNull(data.GetCost(2));
        }

        [Test]
        public void CraftGarden_Success_CreatesGardenAndSpendsCosts()
        {
            var data = new SaveData();
            data.items.Add(new InventoryItem { itemName = "Acorn", count = 5 });
            data.mana = 500f;

            var plantData = ScriptableObject.CreateInstance<GardenPlantData>();
            plantData.plantName = "Oak";
            plantData.yieldItem = "Acorn";
            plantData.waterRequired = 3;
            plantData.growthDurationHours = 48;
            plantData.costTiers = new System.Collections.Generic.List<GardenCostTier>
            {
                new GardenCostTier { manaCost = 200, seedCost = 1 },
                new GardenCostTier { manaCost = 300, seedCost = 2 },
            };

            bool result = GardenManager.TryCraftGarden(data, plantData, 2, 3);
            Assert.IsTrue(result);
            Assert.AreEqual(1, data.gardens.Count);
            Assert.AreEqual("Oak", data.gardens[0].plantName);
            Assert.AreEqual(2, data.gardens[0].gridX);
            Assert.AreEqual(3, data.gardens[0].gridY);
            Assert.AreEqual(300f, data.mana, 0.01f);
            Assert.AreEqual(4, data.items[0].count);
        }

        [Test]
        public void CraftGarden_ScalingCost_SecondCostsMore()
        {
            var data = new SaveData();
            data.items.Add(new InventoryItem { itemName = "Acorn", count = 10 });
            data.mana = 1000f;

            var plantData = ScriptableObject.CreateInstance<GardenPlantData>();
            plantData.plantName = "Oak";
            plantData.yieldItem = "Acorn";
            plantData.waterRequired = 0;
            plantData.growthDurationHours = 48;
            plantData.costTiers = new System.Collections.Generic.List<GardenCostTier>
            {
                new GardenCostTier { manaCost = 200, seedCost = 1 },
                new GardenCostTier { manaCost = 300, seedCost = 2 },
            };

            GardenManager.TryCraftGarden(data, plantData, 0, 0);
            GardenManager.TryCraftGarden(data, plantData, 1, 0);

            Assert.AreEqual(2, data.gardens.Count);
            Assert.AreEqual(500f, data.mana, 0.01f);
            Assert.AreEqual(7, data.items[0].count);
        }

        [Test]
        public void CraftGarden_AtCap_ReturnsFalse()
        {
            var data = new SaveData();
            data.items.Add(new InventoryItem { itemName = "Acorn", count = 10 });
            data.mana = 1000f;

            var plantData = ScriptableObject.CreateInstance<GardenPlantData>();
            plantData.plantName = "Oak";
            plantData.yieldItem = "Acorn";
            plantData.costTiers = new System.Collections.Generic.List<GardenCostTier>
            {
                new GardenCostTier { manaCost = 100, seedCost = 1 },
            };

            GardenManager.TryCraftGarden(data, plantData, 0, 0);
            bool result = GardenManager.TryCraftGarden(data, plantData, 1, 0);

            Assert.IsFalse(result);
            Assert.AreEqual(1, data.gardens.Count);
        }

        [Test]
        public void CraftGarden_CantAffordMana_ReturnsFalse()
        {
            var data = new SaveData();
            data.items.Add(new InventoryItem { itemName = "Acorn", count = 5 });
            data.mana = 50f;

            var plantData = ScriptableObject.CreateInstance<GardenPlantData>();
            plantData.plantName = "Oak";
            plantData.yieldItem = "Acorn";
            plantData.costTiers = new System.Collections.Generic.List<GardenCostTier>
            {
                new GardenCostTier { manaCost = 200, seedCost = 1 },
            };

            bool result = GardenManager.TryCraftGarden(data, plantData, 0, 0);
            Assert.IsFalse(result);
            Assert.AreEqual(0, data.gardens.Count);
            Assert.AreEqual(50f, data.mana, 0.01f);
        }

        [Test]
        public void CraftGarden_CantAffordItems_ReturnsFalse()
        {
            var data = new SaveData();
            data.mana = 1000f;

            var plantData = ScriptableObject.CreateInstance<GardenPlantData>();
            plantData.plantName = "Oak";
            plantData.yieldItem = "Acorn";
            plantData.costTiers = new System.Collections.Generic.List<GardenCostTier>
            {
                new GardenCostTier { manaCost = 200, seedCost = 1 },
            };

            bool result = GardenManager.TryCraftGarden(data, plantData, 0, 0);
            Assert.IsFalse(result);
            Assert.AreEqual(0, data.gardens.Count);
        }
    }
}
