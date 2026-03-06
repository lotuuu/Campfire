using System;
using System.Collections.Generic;
using UnityEngine;

namespace Garden
{
    [Serializable]
    public class GardenCostTier
    {
        public float manaCost;
        public int seedCost;
    }

    [CreateAssetMenu(fileName = "NewGardenPlant", menuName = "CampFire/Garden Plant Data")]
    public class GardenPlantData : ScriptableObject
    {
        public string plantName;
        public float growthDurationHours = 24f;
        public string yieldItem;
        public int yieldAmount = 1;
        public float yieldIntervalHours = 12f;
        public int waterRequired = 3;

        [Header("Building Costs")]
        public List<GardenCostTier> costTiers = new();

        [Header("Visuals")]
        public Sprite icon;
        public Sprite[] growthSprites;
        public Sprite matureSprite;

        public GardenCostTier GetCost(int existingCount)
        {
            if (existingCount < 0 || existingCount >= costTiers.Count) return null;
            return costTiers[existingCount];
        }
    }
}
