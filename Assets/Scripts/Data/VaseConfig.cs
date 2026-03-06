using UnityEngine;

namespace Garden
{
    [CreateAssetMenu(fileName = "VaseConfig", menuName = "CampFire/Vase Config")]
    public class VaseConfig : ScriptableObject
    {
        [SerializeField] private int baseCapacity = 5;
        [SerializeField] private float craftCostMana = 100f;
        [SerializeField] private float fillDurationMinutes = 30f;

        public int BaseCapacity => baseCapacity;
        public float CraftCostMana => craftCostMana;
        public float FillDurationMinutes => fillDurationMinutes;
    }
}
