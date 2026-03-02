using System;
using NUnit.Framework;

namespace Garden.Tests
{
    public class TestCurrencyManager
    {
        [Test]
        public void SpendWater_DeductsFromFirstNonEmptyVase()
        {
            var data = new SaveData();
            data.vases.Add(new VaseSave { capacity = 10, currentWater = 5 });
            data.vases.Add(new VaseSave { capacity = 10, currentWater = 3 });

            int needed = 4;
            for (int i = 0; i < data.vases.Count && needed > 0; i++)
            {
                int take = Math.Min(data.vases[i].currentWater, needed);
                data.vases[i].currentWater -= take;
                needed -= take;
            }
            Assert.AreEqual(0, needed);
            Assert.AreEqual(1, data.vases[0].currentWater);
            Assert.AreEqual(3, data.vases[1].currentWater);
        }

        [Test]
        public void SpendWater_SpansMultipleVases()
        {
            var data = new SaveData();
            data.vases.Add(new VaseSave { capacity = 10, currentWater = 5 });
            data.vases.Add(new VaseSave { capacity = 10, currentWater = 3 });

            int needed = 7;
            for (int i = 0; i < data.vases.Count && needed > 0; i++)
            {
                int take = Math.Min(data.vases[i].currentWater, needed);
                data.vases[i].currentWater -= take;
                needed -= take;
            }
            Assert.AreEqual(0, needed);
            Assert.AreEqual(0, data.vases[0].currentWater);
            Assert.AreEqual(1, data.vases[1].currentWater);
        }
    }
}
