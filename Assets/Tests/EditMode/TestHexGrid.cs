using NUnit.Framework;
using UnityEngine;

namespace Garden.Tests
{
    public class TestHexGrid
    {
        [Test]
        public void CenterIsOrigin()
        {
            var pos = HexGridUtil.HexToPixel(0, 0, 220f);
            Assert.AreEqual(0f, pos.x, 0.001f);
            Assert.AreEqual(0f, pos.y, 0.001f);
        }

        [Test]
        public void NeighborsWithinRadius1()
        {
            // All 6 hex neighbors of (0,0)
            var neighbors = new (int q, int r)[]
            {
                (1, 0), (-1, 0), (0, 1), (0, -1), (1, -1), (-1, 1)
            };

            foreach (var (q, r) in neighbors)
                Assert.IsTrue(HexGridUtil.IsWithinRadius(q, r, 1), $"({q},{r}) should be within radius 1");
        }

        [Test]
        public void OutOfRange()
        {
            Assert.IsFalse(HexGridUtil.IsWithinRadius(2, 0, 1));
            Assert.IsFalse(HexGridUtil.IsWithinRadius(0, 2, 1));
            Assert.IsFalse(HexGridUtil.IsWithinRadius(-1, -1, 1));
        }

        [Test]
        public void CellCountRadius2()
        {
            int count = 0;
            int radius = 2;
            for (int q = -radius; q <= radius; q++)
            {
                int rMin = Mathf.Max(-radius, -q - radius);
                int rMax = Mathf.Min(radius, -q + radius);
                for (int r = rMin; r <= rMax; r++)
                    count++;
            }
            Assert.AreEqual(19, count);
        }
    }
}
