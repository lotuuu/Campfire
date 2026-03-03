using UnityEngine;

namespace Garden
{
    [CreateAssetMenu(fileName = "NewSkin", menuName = "CampFire/Skin Data")]
    public class SkinData : ScriptableObject
    {
        public string skinName;
        public CampBuildingType buildingType;
        public Color hexFillColor;
        public Color hexBorderColor;
        public string costItemName;
        public int costQuantity = 1;
    }
}
