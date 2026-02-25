using UnityEngine;

namespace Garden
{
    [CreateAssetMenu(fileName = "NewConsumable", menuName = "Garden/Consumable Data")]
    public class ConsumableData : ScriptableObject
    {
        public ConsumableType type;
        public string displayName;
        public Sprite icon;
        public int buyPrice;
        public CurrencyType currency = CurrencyType.SunShards;
        public float magnitude;        // Fan: m/s added; Igloo/Heater: °C delta; unused for others
        public bool isEnvironmentScoped; // Fan, Igloo, Heater, Cloud = true
        [TextArea] public string description;
    }
}
