using UnityEngine;

namespace Garden
{
    [CreateAssetMenu(fileName = "CurrencyConfig", menuName = "Garden/Currency Config")]
    public class CurrencyConfig : ScriptableObject
    {
        [Header("Gold per Harvest (by rarity)")]
        public int commonGold = 10;
        public int uncommonGold = 25;
        public int rareGold = 50;
        public int epicGold = 100;
        public int legendaryGold = 250;

        [Header("Pollen per Second (by rarity)")]
        public float commonPollenPerSecond = 0.5f;
        public float uncommonPollenPerSecond = 1.5f;
        public float rarePollenPerSecond = 4f;
        public float epicPollenPerSecond = 10f;
        public float legendaryPollenPerSecond = 25f;

        [Header("Greenhouse")]
        public int defaultSlots = 6;
        public int greenhouseExpandCostGold = 300;

        public int GetGoldForRarity(Rarity r) => r switch
        {
            Rarity.Common => commonGold,
            Rarity.Uncommon => uncommonGold,
            Rarity.Rare => rareGold,
            Rarity.Epic => epicGold,
            Rarity.Legendary => legendaryGold,
            _ => commonGold
        };

        public float GetPollenPerSecondForRarity(Rarity r) => r switch
        {
            Rarity.Common => commonPollenPerSecond,
            Rarity.Uncommon => uncommonPollenPerSecond,
            Rarity.Rare => rarePollenPerSecond,
            Rarity.Epic => epicPollenPerSecond,
            Rarity.Legendary => legendaryPollenPerSecond,
            _ => commonPollenPerSecond
        };

        public static float GetGreenhouseQualityMultiplier(QualityTier tier) => tier switch
        {
            QualityTier.D => 1.0f,
            QualityTier.C => 1.5f,
            QualityTier.B => 2.0f,
            QualityTier.A => 2.5f,
            QualityTier.S => 3.0f,
            _ => 1.5f
        };

        public int GetGreenhouseSellValue(int greenhouseYield, QualityTier tier)
        {
            return Mathf.RoundToInt(greenhouseYield * GetGreenhouseQualityMultiplier(tier));
        }

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

        public float GetPollenPerSecondForPlant(Rarity rarity, QualityTier tier)
        {
            return GetPollenPerSecondForRarity(rarity) * GetQualityMultiplier(tier);
        }
    }
}
