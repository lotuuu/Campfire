using UnityEngine;

namespace Garden
{
    [CreateAssetMenu(fileName = "NewSeed", menuName = "CampFire/Seed Data")]
    public class SeedData : ScriptableObject
    {
        public string seedName;
        public float growthDurationHours = 4f;
        public int minDrops = 1;
        public int maxDrops = 3;
        public GrowthRecipe recipe;

        [Header("Visuals")]
        public Sprite icon;
        public Sprite[] growthSprites;

        [Header("Progression")]
        public int tier = 1;

        [Header("Shop")]
        public float manaCost;
    }
}
