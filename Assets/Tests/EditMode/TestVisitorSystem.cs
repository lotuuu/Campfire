using System.Collections.Generic;
using NUnit.Framework;

namespace Garden.Tests
{
    public class TestVisitorSystem
    {
        [Test]
        public void DetermineGift_LowWater_GivesWater()
        {
            var vases = new List<VaseSave>
            {
                new VaseSave { currentWater = 1, capacity = 5 },
                new VaseSave { currentWater = 1, capacity = 5 }
            };
            var gift = VisitorSystem.DetermineGift(vases);
            Assert.AreEqual(VisitorGiftType.Water, gift.type);
            Assert.AreEqual(3, gift.amount);
        }

        [Test]
        public void DetermineGift_HighWater_GivesSeed()
        {
            var vases = new List<VaseSave>
            {
                new VaseSave { currentWater = 5, capacity = 5 }
            };
            var gift = VisitorSystem.DetermineGift(vases);
            Assert.AreEqual(VisitorGiftType.Seed, gift.type);
            Assert.AreEqual("Chamomile", gift.seedName);
        }

        [Test]
        public void DetermineGift_ExactlyTwo_GivesWater()
        {
            var vases = new List<VaseSave>
            {
                new VaseSave { currentWater = 2, capacity = 5 }
            };
            var gift = VisitorSystem.DetermineGift(vases);
            Assert.AreEqual(VisitorGiftType.Water, gift.type);
        }

        [Test]
        public void ApplyGift_Water_FillsVases()
        {
            var vases = new List<VaseSave>
            {
                new VaseSave { currentWater = 3, capacity = 5 },
                new VaseSave { currentWater = 0, capacity = 5 }
            };
            var gift = new VisitorGift { type = VisitorGiftType.Water, amount = 3 };
            VisitorSystem.ApplyGift(vases, gift);
            Assert.AreEqual(5, vases[0].currentWater);
            Assert.AreEqual(1, vases[1].currentWater);
        }

        [Test]
        public void ApplyGift_Water_DoesNotOverfill()
        {
            var vases = new List<VaseSave>
            {
                new VaseSave { currentWater = 4, capacity = 5 }
            };
            var gift = new VisitorGift { type = VisitorGiftType.Water, amount = 10 };
            VisitorSystem.ApplyGift(vases, gift);
            Assert.AreEqual(5, vases[0].currentWater);
        }
    }
}
