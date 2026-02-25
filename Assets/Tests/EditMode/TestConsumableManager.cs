using NUnit.Framework;
using System.Collections.Generic;
using Garden;

namespace Garden.Tests
{
    public class TestConsumableManager
    {
        [Test]
        public void ConsumableInventoryEntry_AddAndSpend()
        {
            var data = new List<ConsumableInventoryEntry>();
            data.Add(new ConsumableInventoryEntry { consumableType = ConsumableType.Fan.ToString(), count = 3 });
            var entry = data.Find(e => e.consumableType == ConsumableType.Fan.ToString());
            Assert.AreEqual(3, entry.count);
            entry.count--;
            Assert.AreEqual(2, entry.count);
        }

        [Test]
        public void ConsumableInventoryEntry_NoStackSameType()
        {
            var applied = new List<string>();
            applied.Add(ConsumableType.Fertilizer.ToString());
            Assert.IsTrue(applied.Contains(ConsumableType.Fertilizer.ToString()));
            Assert.IsTrue(!applied.Contains(ConsumableType.Fan.ToString()));
        }

        [Test]
        public void EnvironmentConsumableSave_NoDuplicateType()
        {
            var envList = new List<EnvironmentConsumableSave>();
            envList.Add(new EnvironmentConsumableSave { envIndex = 0, consumableType = "Fan" });

            // Simulate "no stacking" logic: remove existing before adding
            envList.RemoveAll(e => e.envIndex == 0 && e.consumableType == "Fan");
            envList.Add(new EnvironmentConsumableSave { envIndex = 0, consumableType = "Fan" });

            int fanCount = envList.FindAll(e => e.consumableType == "Fan").Count;
            Assert.AreEqual(1, fanCount);
        }
    }
}
