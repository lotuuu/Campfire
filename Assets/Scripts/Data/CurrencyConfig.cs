using UnityEngine;

namespace Garden
{
    [CreateAssetMenu(fileName = "CurrencyConfig", menuName = "Garden/Currency Config")]
    public class CurrencyConfig : ScriptableObject
    {
        [Header("Dewdrops per Harvest (by rarity)")]
        public int commonDewdrops = 10;
        public int uncommonDewdrops = 25;
        public int rareDewdrops = 50;
        public int epicDewdrops = 100;
        public int legendaryDewdrops = 250;

        [Header("Aura Dust per Second (by rarity)")]
        public float commonDustPerSecond = 0.5f;
        public float uncommonDustPerSecond = 1.5f;
        public float rareDustPerSecond = 4f;
        public float epicDustPerSecond = 10f;
        public float legendaryDustPerSecond = 25f;

        [Header("Greenhouse")]
        public int defaultSlots = 6;
        public int greenhouseExpandCostDewdrops = 300;

        public int GetDewdropsForRarity(Rarity r) => r switch
        {
            Rarity.Common => commonDewdrops,
            Rarity.Uncommon => uncommonDewdrops,
            Rarity.Rare => rareDewdrops,
            Rarity.Epic => epicDewdrops,
            Rarity.Legendary => legendaryDewdrops,
            _ => commonDewdrops
        };

        public float GetDustPerSecondForRarity(Rarity r) => r switch
        {
            Rarity.Common => commonDustPerSecond,
            Rarity.Uncommon => uncommonDustPerSecond,
            Rarity.Rare => rareDustPerSecond,
            Rarity.Epic => epicDustPerSecond,
            Rarity.Legendary => legendaryDustPerSecond,
            _ => commonDustPerSecond
        };

        public static float GetQualityMultiplier(QualityTier tier) => tier switch
        {
            QualityTier.D => 0.8f,
            QualityTier.C => 1.0f,
            QualityTier.B => 1.5f,
            QualityTier.A => 2.2f,
            QualityTier.S => 3.5f,
            _ => 1.0f
        };

        public static string GetQualityLabel(QualityTier tier) => tier switch
        {
            QualityTier.D => "Faded",
            QualityTier.C => "Stable",
            QualityTier.B => "Vibrant",
            QualityTier.A => "Radiant",
            QualityTier.S => "Eternal",
            _ => "Unknown"
        };

        public int GetSellValue(int baseSellPrice, QualityTier tier)
        {
            return Mathf.RoundToInt(baseSellPrice * GetQualityMultiplier(tier));
        }

        public float GetDustPerSecondForPlant(Rarity rarity, QualityTier tier)
        {
            return GetDustPerSecondForRarity(rarity) * GetQualityMultiplier(tier);
        }
    }
}
