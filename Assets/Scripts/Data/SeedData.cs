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
    }
}