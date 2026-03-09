using System.Collections.Generic;
using System.Threading.Tasks;
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
            if (CurrencyManager.FreeMode) return true;
            var items = SaveManager.Instance.Data.items;
            var item = items.Find(i => i.itemName == skin.costItemName);
            return item != null && item.count >= skin.costQuantity;
        }

        public bool IsSkinUnlocked(CampBuildingType type, int index, string skinName)
        {
            var data = SaveManager.Instance.Data;
            return type switch
            {
                CampBuildingType.Plot => index >= 0 && index < data.plots.Count && data.plots[index].unlockedSkins.Contains(skinName),
                CampBuildingType.Vase => index >= 0 && index < data.vases.Count && data.vases[index].unlockedSkins.Contains(skinName),
                CampBuildingType.MallumHouse => index >= 0 && index < data.mallumHouses.Count && data.mallumHouses[index].unlockedSkins.Contains(skinName),
                _ => false
            };
        }

        public bool ApplySkin(CampBuildingType type, int index, SkinData skin)
        {
            if (skin.buildingType != type) return false;

            var data = SaveManager.Instance.Data;

            // Validate index
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

            bool alreadyUnlocked = IsSkinUnlocked(type, index, skin.skinName);

            // Only charge if not already unlocked on this building
            if (!alreadyUnlocked)
            {
                if (!CanAffordSkin(skin)) return false;
                if (!CurrencyManager.FreeMode)
                {
                    var item = data.items.Find(i => i.itemName == skin.costItemName);
                    if (item == null) return false;
                    item.count -= skin.costQuantity;
                    if (item.count <= 0) data.items.Remove(item);
                }
            }

            switch (type)
            {
                case CampBuildingType.Plot:
                    data.plots[index].skinName = skin.skinName;
                    if (!alreadyUnlocked) data.plots[index].unlockedSkins.Add(skin.skinName);
                    break;
                case CampBuildingType.Vase:
                    data.vases[index].skinName = skin.skinName;
                    if (!alreadyUnlocked) data.vases[index].unlockedSkins.Add(skin.skinName);
                    break;
                case CampBuildingType.MallumHouse:
                    data.mallumHouses[index].skinName = skin.skinName;
                    if (!alreadyUnlocked) data.mallumHouses[index].unlockedSkins.Add(skin.skinName);
                    break;
            }

            SaveManager.Instance.Save();
            return true;
        }

        public async Task<bool> ApplySkinOnServer(CampBuildingType type, int index, SkinData skin)
        {
            if (GameService.Instance == null || !GameService.Instance.IsOnline)
                return ApplySkin(type, index, skin);

            var data = SaveManager.Instance.Data;
            int serverId;

            switch (type)
            {
                case CampBuildingType.Plot:
                    if (index < 0 || index >= data.plots.Count) return false;
                    serverId = data.plots[index].serverId;
                    if (serverId <= 0) return ApplySkin(type, index, skin);
                    var plotResult = await GameService.Instance.SetPlotSkin(serverId, skin.skinName);
                    if (plotResult == null) return false;
                    data.plots[index].skinName = plotResult.skinName;
                    data.plots[index].unlockedSkins = plotResult.unlockedSkins ?? new List<string>();
                    break;

                case CampBuildingType.Vase:
                    if (index < 0 || index >= data.vases.Count) return false;
                    serverId = data.vases[index].serverId;
                    if (serverId <= 0) return ApplySkin(type, index, skin);
                    var vaseResult = await GameService.Instance.SetVaseSkin(serverId, skin.skinName);
                    if (vaseResult == null) return false;
                    data.vases[index].skinName = vaseResult.skinName;
                    data.vases[index].unlockedSkins = vaseResult.unlockedSkins ?? new List<string>();
                    break;

                case CampBuildingType.MallumHouse:
                    if (index < 0 || index >= data.mallumHouses.Count) return false;
                    serverId = data.mallumHouses[index].serverId;
                    if (serverId <= 0) return ApplySkin(type, index, skin);
                    var houseResult = await GameService.Instance.SetMallumHouseSkin(serverId, skin.skinName);
                    if (houseResult == null) return false;
                    data.mallumHouses[index].skinName = houseResult.skinName;
                    data.mallumHouses[index].unlockedSkins = houseResult.unlockedSkins ?? new List<string>();
                    break;

                default:
                    return ApplySkin(type, index, skin);
            }

            SaveManager.Instance.Save();
            // Sync economy (items may have been spent for unlock)
            if (EconomyService.Instance != null)
                EconomyService.Instance.Initialize();
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
