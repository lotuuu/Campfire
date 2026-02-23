using System;
using System.Collections.Generic;
using UnityEngine;

namespace Garden
{
    [CreateAssetMenu(fileName = "NewSeed", menuName = "Garden/Seed Data")]
    public class SeedData : ScriptableObject
    {
        public string seedName;
        public Sprite icon;
        [TextArea] public string description;
        [Range(0.01f, 72f)] public float baseGrowthHours = 24f;
        public List<VariantData> variants = new();

        [Header("Shop")]
        public int buyPrice;
        public int baseSellPrice = 120;

        [Header("Sync Shield")]
        public WeatherCondition preferredWeather = WeatherCondition.Clear;

        [Header("Special Conditions")]
        public List<SeedSpecialCondition> specialConditions = new();
    }

    [Serializable]
    public class SeedSpecialCondition
    {
        public QualityTier targetTier;
        [Range(0f, 1f)] public float bonusPercent = 0.1f;
        public TriggerCondition condition;
    }
}
