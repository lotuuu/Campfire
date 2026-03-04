using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Garden.Tests
{
    public class TestMallumHouse
    {
        [Test]
        public void GetMaxMallums_ReturnsHouseCountTimesPerHouse()
        {
            var config = ScriptableObject.CreateInstance<MallumHouseConfig>();
            Assert.AreEqual(2, config.GetMaxMallums(2));
            Assert.AreEqual(3, config.GetMaxMallums(3));
        }

        [Test]
        public void GetMaxMallums_ReturnsZeroForZeroHouses()
        {
            var config = ScriptableObject.CreateInstance<MallumHouseConfig>();
            Assert.AreEqual(0, config.GetMaxMallums(0));
        }

        [Test]
        public void GetNextHouseCost_ReturnsNull_WhenNoCostsDefined()
        {
            var config = ScriptableObject.CreateInstance<MallumHouseConfig>();
            Assert.IsNull(config.GetNextHouseCost(0));
        }

        [Test]
        public void CanBuildNextHouse_ReturnsFalse_WhenNoCostsLeft()
        {
            var config = ScriptableObject.CreateInstance<MallumHouseConfig>();
            Assert.IsFalse(config.CanBuildNextHouse(0));
        }

        [Test]
        public void CanAffordHarvests_ReturnsTrueWhenEnough()
        {
            var items = new List<InventoryItem>
            {
                new() { itemName = "Basil_harvest", count = 5 }
            };
            var costs = new List<HarvestCost>
            {
                new() { itemName = "Basil_harvest", count = 3 }
            };
            Assert.IsTrue(MallumManager.CanAffordHarvests(items, costs));
        }

        [Test]
        public void CanAffordHarvests_ReturnsFalseWhenNotEnough()
        {
            var items = new List<InventoryItem>
            {
                new() { itemName = "Basil_harvest", count = 1 }
            };
            var costs = new List<HarvestCost>
            {
                new() { itemName = "Basil_harvest", count = 3 }
            };
            Assert.IsFalse(MallumManager.CanAffordHarvests(items, costs));
        }

        [Test]
        public void CanAffordHarvests_ReturnsFalseWhenItemMissing()
        {
            var items = new List<InventoryItem>();
            var costs = new List<HarvestCost>
            {
                new() { itemName = "Lavender_harvest", count = 1 }
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
