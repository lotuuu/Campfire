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

        public static (int q, int r) PixelToHex(float px, float py, float size)
        {
            float q = (Sqrt3 / 3f * px - 1f / 3f * py) / size;
            float r = (2f / 3f * py) / size;
            return CubeRound(q, r);
        }

        private static (int q, int r) CubeRound(float fq, float fr)
        {
            float fs = -fq - fr;
            int q = Mathf.RoundToInt(fq);
            int r = Mathf.RoundToInt(fr);
            int s = Mathf.RoundToInt(fs);
            float qd = Mathf.Abs(q - fq), rd = Mathf.Abs(r - fr), sd = Mathf.Abs(s - fs);
            if (qd > rd && qd > sd) q = -r - s;
            else if (rd > sd) r = -q - s;
            return (q, r);
        }

        public static bool IsWithinRadius(int q, int r, int radius)
        {
            return Mathf.Max(Mathf.Abs(q), Mathf.Max(Mathf.Abs(r), Mathf.Abs(q + r))) <= radius;
        }

        /// <summary>
        /// Returns all valid hex positions within the given radius, excluding the center (0,0).
        /// </summary>
        public static System.Collections.Generic.List<(int q, int r)> GetNonCenterPositions(int radius)
        {
            var positions = new System.Collections.Generic.List<(int q, int r)>();
            for (int q = -radius; q <= radius; q++)
            {
                for (int r = -radius; r <= radius; r++)
                {
                    if (q == 0 && r == 0) continue;
                    if (IsWithinRadius(q, r, radius)) positions.Add((q, r));
                }
            }
            return positions;
        }

        /// <summary>
        /// Fisher-Yates shuffle of a list in place.
        /// </summary>
        public static void Shuffle<T>(System.Collections.Generic.List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
