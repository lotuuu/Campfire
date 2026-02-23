using UnityEngine;

namespace Garden
{
    [CreateAssetMenu(fileName = "NewVariant", menuName = "Garden/Variant Data")]
    public class VariantData : ScriptableObject
    {
        public string variantName;
        [TextArea] public string description;
        [TextArea] public string discoveryHint;
        public Rarity rarity;
        [Range(1, 4)] public int priority = 4;
        public TriggerCondition trigger;

        [Header("Visuals")]
        public Color primaryColor = Color.green;
        public Color secondaryColor = Color.white;
        public Sprite variantSprite;
        public Material variantMaterial;
        public GameObject particleEffectPrefab;
    }
}