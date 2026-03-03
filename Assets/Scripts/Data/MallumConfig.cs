using UnityEngine;

namespace Garden
{
    [CreateAssetMenu(fileName = "MallumConfig", menuName = "CampFire/Mallum Config")]
    public class MallumConfig : ScriptableObject
    {
        [SerializeField] private int[] maxMallumsPerFlameLevel = { 1, 1, 2, 2, 3 };

        public int GetMaxMallums(int flameLevel)
        {
            int index = Mathf.Clamp(flameLevel - 1, 0, maxMallumsPerFlameLevel.Length - 1);
            return maxMallumsPerFlameLevel[index];
        }
    }
}
