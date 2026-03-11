using System.Collections.Generic;
using NUnit.Framework;

namespace Garden.Tests
{
    public class TestMallumHouse
    {
        [Test]
        public void GetMaxMallums_ReturnsHouseCountTimesPerHouse()
        {
            var config = new ServerMallumHouseConfig { mallums_per_house = 1 };
            Assert.AreEqual(2, config.GetMaxMallums(2));
            Assert.AreEqual(3, config.GetMaxMallums(3));
        }

        [Test]
        public void GetMaxMallums_ReturnsZeroForZeroHouses()
        {
            var config = new ServerMallumHouseConfig { mallums_per_house = 1 };
            Assert.AreEqual(0, config.GetMaxMallums(0));
        }

        [Test]
        public void BuildingCost_CanStoreHouseCost()
        {
            var cost = new BuildingCost
            {
                manaCost = 500,
                harvestCosts = new List<HarvestCost>
                {
                    new() { itemName = "Basil", count = 10 }
                }
            };
            Assert.AreEqual(500, cost.manaCost);
            Assert.AreEqual(1, cost.harvestCosts.Count);
        }

        [Test]
        public void MallumHouseConfig_HouseCosts_DefaultEmpty()
        {
            var config = new ServerMallumHouseConfig { mallums_per_house = 1 };
            Assert.IsNotNull(config.houseCosts);
            Assert.AreEqual(0, config.houseCosts.Count);
        }

        [Test]
        public void CanAffordHarvests_ReturnsTrueWhenEnough()
        {
            var items = new List<InventoryItem>
            {
                new() { itemName = "Basil", count = 5 }
            };
            var costs = new List<HarvestCost>
            {
                new() { itemName = "Basil", count = 3 }
            };
            Assert.IsTrue(MallumManager.CanAffordHarvests(items, costs));
        }

        [Test]
        public void CanAffordHarvests_ReturnsFalseWhenNotEnough()
        {
            var items = new List<InventoryItem>
            {
                new() { itemName = "Basil", count = 1 }
            };
            var costs = new List<HarvestCost>
            {
                new() { itemName = "Basil", count = 3 }
            };
            Assert.IsFalse(MallumManager.CanAffordHarvests(items, costs));
        }

        [Test]
        public void CanAffordHarvests_ReturnsFalseWhenItemMissing()
        {
            var items = new List<InventoryItem>();
            var costs = new List<HarvestCost>
            {
                new() { itemName = "Lavender", count = 1 }
            };
            Assert.IsFalse(MallumManager.CanAffordHarvests(items, costs));
        }

        [Test]
        public void CanAffordHarvests_ReturnsTrueWhenNoCosts()
        {
            var items = new List<InventoryItem>();
            var costs = new List<HarvestCost>();
            Assert.IsTrue(MallumManager.CanAffordHarvests(items, costs));
        }
    }
}
