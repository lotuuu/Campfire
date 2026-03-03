using System.Collections.Generic;
using UnityEngine;

namespace Garden
{
    public class SkinManager : MonoBehaviour
    {
        public static SkinManager Instance { get; private set; }

        private SkinData[] allSkins;
        private Dictionary<string, SkinData> skinLookup;
        private Dictionary<CampBuildingType, List<SkinData>> skinsByType;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            allSkins = Resources.LoadAll<SkinData>("Skins");
            skinLookup = new Dictionary<string, SkinData>();
            skinsByType = new Dictionary<CampBuildingType, List<SkinData>>();
            foreach (var skin in allSkins)
            {
                skinLookup[skin.skinName] = skin;
                if (!skinsByType.TryGetValue(skin.buildingType, out var list))
                {
                    list = new List<SkinData>();
                    skinsByType[skin.buildingType] = list;
                }
                list.Add(skin);
            }
        }

        public SkinData GetSkin(string skinName)
        {
            if (string.IsNullOrEmpty(skinName)) return null;
            skinLookup.TryGetValue(skinName, out var skin);
            return skin;
        }

        public List<SkinData> GetSkinsForBuilding(CampBuildingType type)
        {
            return skinsByType.TryGetValue(type, out var list) ? list : new List<SkinData>();
        }

        public bool CanAffordSkin(SkinData skin)
        {
            var items = SaveManager.Instance.Data.items;
            var item = items.Find(i => i.itemName == skin.costItemName);
            return item != null && item.count >= skin.costQuantity;
        }

        public bool ApplySkin(CampBuildingType type, int index, SkinData skin)
        {
            if (!CanAffordSkin(skin)) return false;
            if (skin.buildingType != type) return false;

            var data = SaveManager.Instance.Data;

            // Validate index before deducting cost
            switch (type)
            {
                case CampBuildingType.Plot:
                    if (index < 0 || index >= data.plots.Count) return false;
                    break;
                case CampBuildingType.Vase:
                    if (index < 0 || index >= data.vases.Count) return false;
                    break;
                case CampBuildingType.MallumHouse:
                    if (index < 0 || index >= data.mallumHouses.Count) return false;
                    break;
                default: return false;
            }

            var item = data.items.Find(i => i.itemName == skin.costItemName);
            item.count -= skin.costQuantity;
            if (item.count <= 0) data.items.Remove(item);

            switch (type)
            {
                case CampBuildingType.Plot:
                    data.plots[index].skinName = skin.skinName;
                    break;
                case CampBuildingType.Vase:
                    data.vases[index].skinName = skin.skinName;
                    break;
                case CampBuildingType.MallumHouse:
                    data.mallumHouses[index].skinName = skin.skinName;
                    break;
            }

            SaveManager.Instance.Save();
            return true;
        }

        public void RemoveSkin(CampBuildingType type, int index)
        {
            var data = SaveManager.Instance.Data;
            switch (type)
            {
                case CampBuildingType.Plot:
                    if (index < 0 || index >= data.plots.Count) return;
                    data.plots[index].skinName = null;
                    break;
                case CampBuildingType.Vase:
                    if (index < 0 || index >= data.vases.Count) return;
                    data.vases[index].skinName = null;
                    break;
                case CampBuildingType.MallumHouse:
                    if (index < 0 || index >= data.mallumHouses.Count) return;
                    data.mallumHouses[index].skinName = null;
                    break;
            }
            SaveManager.Instance.Save();
        }
    }
}
