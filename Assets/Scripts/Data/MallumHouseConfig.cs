using UnityEngine;

namespace Garden
{
    [CreateAssetMenu(fileName = "MallumHouseConfig", menuName = "CampFire/Mallum House Config")]
    public class MallumHouseConfig : ScriptableObject
    {
        [SerializeField] private int mallumsPerHouse = 1;

        public int MallumsPerHouse => mallumsPerHouse;

        public int GetMaxMallums(int houseCount)
        {
            return houseCount * mallumsPerHouse;
        }
    }
}
