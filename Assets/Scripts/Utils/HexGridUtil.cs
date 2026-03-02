using UnityEngine;

namespace Garden
{
    public static class HexGridUtil
    {
        private static readonly float Sqrt3 = Mathf.Sqrt(3f);

        public static Vector2 HexToPixel(int q, int r, float size)
        {
            float x = size * (Sqrt3 * q + Sqrt3 / 2f * r);
            float y = size * (1.5f * r);
            return new Vector2(x, y);
        }

        public static bool IsWithinRadius(int q, int r, int radius)
        {
            return Mathf.Max(Mathf.Abs(q), Mathf.Max(Mathf.Abs(r), Mathf.Abs(q + r))) <= radius;
        }
    }
}
