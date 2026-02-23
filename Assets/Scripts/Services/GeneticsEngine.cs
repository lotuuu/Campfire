using System.Collections.Generic;
using System.Linq;

namespace Garden
{
    public struct GeneticsResult
    {
        public VariantData variant;
        public float growthSpeedMultiplier;
    }

    public static class GeneticsEngine
    {
        public static GeneticsResult Resolve(SeedData seed, WeatherData weather)
        {
            var sorted = seed.variants.OrderBy(v => v.priority).ToList();

            foreach (var variant in sorted)
            {
                if (variant.trigger != null && variant.trigger.Evaluate(weather))
                {
                    return new GeneticsResult
                    {
                        variant = variant,
                        growthSpeedMultiplier = 1.25f
                    };
                }
            }

            var fallback = sorted.LastOrDefault();
            return new GeneticsResult
            {
                variant = fallback,
                growthSpeedMultiplier = 1.0f
            };
        }

        public static List<(VariantData variant, bool isHighProbability)> GetProbabilities(SeedData seed, WeatherData weather)
        {
            var result = new List<(VariantData, bool)>();
            foreach (var variant in seed.variants)
            {
                bool matches = variant.trigger != null && variant.trigger.Evaluate(weather);
                result.Add((variant, matches));
            }
            return result;
        }
    }
}