using NUnit.Framework;
using UnityEngine;

namespace Garden.Tests
{
    public class TestSaveData
    {
        [Test]
        public void NewSaveData_HasCorrectDefaults()
        {
            var data = new SaveData();
            Assert.AreEqual(1, data.version);
            Assert.AreEqual(0f, data.mana);
            Assert.AreEqual(1, data.flameLevel);
            Assert.AreEqual(0, data.vases.Count);
            Assert.AreEqual(0, data.plots.Count);
            Assert.AreEqual(0, data.gardens.Count);
            Assert.AreEqual(0, data.inventory.Count);
        }

        [Test]
        public void SaveData_RoundTrips_ThroughJson()
        {
            var data = new SaveData
            {
                mana = 123.5f,
                flameLevel = 3,
            };
            data.inventory.Add(new InventoryItem { itemKey = "Basil_Seed", count = 5 });
            data.vases.Add(new VaseSave { capacity = 10, currentWater = 7 });
            data.plots.Add(new PlotSave { seedName = "Basil", waterCount = 2, state = PlotState.Growing });
            data.gardens.Add(new GardenSave { plantName = "Oak", mature = true });
            data.inventory.Add(new InventoryItem { itemKey = "Acorn", count = 3 });

            var json = JsonUtility.ToJson(data);
            var restored = JsonUtility.FromJson<SaveData>(json);

            Assert.AreEqual(3, restored.flameLevel);
            Assert.AreEqual(123.5f, restored.mana);
            Assert.AreEqual(2, restored.inventory.Count);
            Assert.AreEqual("Basil_Seed", restored.inventory[0].itemKey);
            Assert.AreEqual(5, restored.inventory[0].count);
            Assert.AreEqual("Acorn", restored.inventory[1].itemKey);
            Assert.AreEqual(3, restored.inventory[1].count);
            Assert.AreEqual(1, restored.vases.Count);
            Assert.AreEqual(7, restored.vases[0].currentWater);
            Assert.AreEqual(1, restored.plots.Count);
            Assert.AreEqual(2, restored.plots[0].waterCount);
            Assert.AreEqual(PlotState.Growing, restored.plots[0].state);
            Assert.AreEqual(1, restored.gardens.Count);
            Assert.IsTrue(restored.gardens[0].mature);
        }
    }
}
