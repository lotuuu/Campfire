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

        [Header("Aura Dust per Hour (by rarity)")]
        public float commonDustPerHour = 1f;
        public float uncommonDustPerHour = 3f;
        public float rareDustPerHour = 8f;
        public float epicDustPerHour = 20f;
        public float legendaryDustPerHour = 50f;

        [Header("Greenhouse")]
        public int defaultSlots = 6;
        public int slotCostSunShards = 50;

        public int GetDewdropsForRarity(Rarity r) => r switch
        {
            Rarity.Common => commonDewdrops,
            Rarity.Uncommon => uncommonDewdrops,
            Rarity.Rare => rareDewdrops,
            Rarity.Epic => epicDewdrops,
            Rarity.Legendary => legendaryDewdrops,
            _ => commonDewdrops
        };

        public float GetDustPerHourForRarity(Rarity r) => r switch
        {
            Rarity.Common => commonDustPerHour,
            Rarity.Uncommon => uncommonDustPerHour,
            Rarity.Rare => rareDustPerHour,
            Rarity.Epic => epicDustPerHour,
            Rarity.Legendary => legendaryDustPerHour,
            _ => commonDustPerHour
        };
    }
}