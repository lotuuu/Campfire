using System.Collections.Generic;
using UnityEngine;

namespace Garden
{
    public static class HarvestEngine
    {
        public static Dictionary<QualityTier, float> GetBaseProbabilities()
        {
            return new Dictionary<QualityTier, float>
            {
                { QualityTier.D, 0.15f },
                { QualityTier.C, 0.55f },
                { QualityTier.B, 0.20f },
                { QualityTier.A, 0.08f },
                { QualityTier.S, 0.02f }
            };
        }

        public static Dictionary<QualityTier, float> GetSyncShieldProbabilities()
        {
            return new Dictionary<QualityTier, float>
            {
                { QualityTier.D, 0f },
                { QualityTier.C, 0.50f },
                { QualityTier.B, 0.30f },
                { QualityTier.A, 0.15f },
                { QualityTier.S, 0.05f }
            };
        }

        public static HarvestResult Roll(SeedData seed, VariantData variant, WeatherData weather)
        {
            bool syncShield = weather.condition == seed.preferredWeather;
            var probs = syncShield ? GetSyncShieldProbabilities() : GetBaseProbabilities();

            ApplySpecialConditions(seed, weather, probs);
            NormalizeProbabilities(probs);

            QualityTier tier = RollTier(probs);
            float multiplier = CurrencyConfig.GetQualityMultiplier(tier);

            return new HarvestResult
            {
                tier = tier,
                valueMultiplier = multiplier,
                syncShieldActive = syncShield,
                goldValue = Mathf.RoundToInt(seed.baseSellPrice * multiplier),
                variant = variant,
                seed = seed
            };
        }

        private static void ApplySpecialConditions(
            SeedData seed, WeatherData weather, Dictionary<QualityTier, float> probs)
        {
            if (seed.specialConditions == null) return;

            foreach (var sc in seed.specialConditions)
            {
                if (sc.condition == null || !sc.condition.Evaluate(weather)) continue;

                float bonus = sc.bonusPercent;
                probs[sc.targetTier] += bonus;

                float otherTotal = 0f;
                foreach (var kv in probs)
                    if (kv.Key != sc.targetTier) otherTotal += kv.Value;

                if (otherTotal <= 0f) continue;

                var keys = new List<QualityTier>(probs.Keys);
                foreach (var key in keys)
                {
                    if (key == sc.targetTier) continue;
                    probs[key] -= bonus * (probs[key] / otherTotal);
                    if (probs[key] < 0f) probs[key] = 0f;
                }
            }
        }

        private static void NormalizeProbabilities(Dictionary<QualityTier, float> probs)
        {
            float total = 0f;
            foreach (var kv in probs) total += kv.Value;
            if (total <= 0f || Mathf.Approximately(total, 1f)) return;

            var keys = new List<QualityTier>(probs.Keys);
            foreach (var key in keys)
                probs[key] /= total;
        }

        private static QualityTier RollTier(Dictionary<QualityTier, float> probs)
        {
            float roll = Random.value;
            float cumulative = 0f;

            QualityTier[] order = { QualityTier.D, QualityTier.C, QualityTier.B, QualityTier.A, QualityTier.S };
            foreach (var tier in order)
            {
                cumulative += probs[tier];
                if (roll < cumulative) return tier;
            }

            return QualityTier.C;
        }
    }
}
