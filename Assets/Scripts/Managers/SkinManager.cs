using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Garden
{
    public class SkinManager : MonoBehaviour
    {
        public static SkinManager Instance { get; private set; }

        private SkinData[] allSkins;
        private Dictionary<string, SkinData> skinLookup;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            allSkins = Resources.LoadAll<SkinData>("Skins");
            skinLookup = new Dictionary<string, SkinData>();
            foreach (var skin in allSkins)
                skinLookup[skin.skinName] = skin;
        }

        public SkinData GetSkin(string skinName)
        {
            if (string.IsNullOrEmpty(skinName)) return null;
            skinLookup.TryGetValue(skinName, out var skin);
            return skin;
        }

        public List<SkinData> GetSkinsForBuilding(CampBuildingType type)
        {
            return allSkins.Where(s => s.buildingType == type).ToList();
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

            var data = SaveManager.Instance.Data;
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
                    data.plots[index].skinName = null;
                    break;
                case CampBuildingType.Vase:
                    data.vases[index].skinName = null;
                    break;
                case CampBuildingType.MallumHouse:
                    data.mallumHouses[index].skinName = null;
                    break;
            }
            SaveManager.Instance.Save();
        }
    }
}
