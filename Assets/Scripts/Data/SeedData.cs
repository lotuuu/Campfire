using UnityEngine;

namespace Garden
{
    [CreateAssetMenu(fileName = "NewSeed", menuName = "CampFire/Seed Data")]
    public class SeedData : ScriptableObject
    {
        public string seedName;
        public float growthDurationHours = 4f;
        public int waterRequired = 1;
        public TriggerCondition preferredWeather;
        public int baseYield = 1;

        [Header("Visuals")]
        public Sprite icon;
        public Sprite[] growthSprites;

        [Header("Shop")]
        public float manaCost;

        public static readonly float WeatherMatchBonus = 0.25f;
        public static readonly float MinQualityMultiplier = 0.8f;
        public static readonly float MaxQualityMultiplier = 2.0f;
    }
}
