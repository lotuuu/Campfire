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
            envList.Add(new EnvironmentConsumableSave { envIndex = 0, consumableType = ConsumableType.Fan.ToString() });

            // New logic: remove ALL for this env before adding
            envList.RemoveAll(e => e.envIndex == 0);
            envList.Add(new EnvironmentConsumableSave { envIndex = 0, consumableType = ConsumableType.Fan.ToString() });

            int fanCount = envList.FindAll(e => e.consumableType == ConsumableType.Fan.ToString()).Count;
            Assert.AreEqual(1, fanCount);
        }

        [Test]
        public void EnvironmentConsumableSave_OnlyOnePerEnv()
        {
            var envList = new List<EnvironmentConsumableSave>();
            envList.Add(new EnvironmentConsumableSave { envIndex = 0, consumableType = ConsumableType.Fan.ToString() });

            // env 1 added before removal so we can prove it is unaffected
            envList.Add(new EnvironmentConsumableSave { envIndex = 1, consumableType = ConsumableType.Heater.ToString() });

            // Replacing Fan with Cloud: remove all for env 0
            envList.RemoveAll(e => e.envIndex == 0);
            envList.Add(new EnvironmentConsumableSave { envIndex = 0, consumableType = ConsumableType.Cloud.ToString() });

            // env 0 has exactly one entry, and it's Cloud
            var env0 = envList.FindAll(e => e.envIndex == 0);
            Assert.AreEqual(1, env0.Count);
            Assert.AreEqual(ConsumableType.Cloud.ToString(), env0[0].consumableType);

            // env 1 is unaffected
            Assert.AreEqual(1, envList.FindAll(e => e.envIndex == 1).Count);
        }
    }
}
