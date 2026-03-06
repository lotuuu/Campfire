using UnityEngine;

namespace Garden
{
    [CreateAssetMenu(fileName = "NewGardenPlant", menuName = "CampFire/Garden Plant Data")]
    public class GardenPlantData : ScriptableObject
    {
        public string plantName;
        public float growthDurationHours = 24f;
        public string yieldItem;
        public int yieldAmount = 1;
        public float yieldIntervalHours = 12f;
        public int waterRequired = 3;
        public float manaCost;

        [Header("Visuals")]
        public Sprite icon;
        public Sprite[] growthSprites;
        public Sprite matureSprite;
    }
}
