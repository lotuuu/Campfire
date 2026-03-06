using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class BuildUI : MonoBehaviour
    {
        private VisualElement buildList;

        public event Action<CampBuildingType> OnRequestPlacement;
        public string SelectedGardenPlant { get; private set; }

        public void Initialize(VisualElement root)
        {
            buildList = root.Q("build-list");
            Refresh();
        }

        public void Refresh()
        {
            if (buildList == null) return;
            buildList.Clear();

            bool canPlace = FlameManager.Instance != null && FlameManager.Instance.CanPlaceEntity;
            string capText = FlameManager.Instance != null
                ? $"{FlameManager.Instance.CurrentEntityCount}/{FlameManager.Instance.MaxEntities}"
                : "";

            // Plot
            if (PlotManager.Instance != null && FlameManager.Instance != null)
            {
                var plotCost = PlotManager.Instance.GetNextPlotCost();
                bool canAfford = canPlace && plotCost != null
                    && CurrencyManager.Instance.CanAffordMana(plotCost.manaCost)
                    && MallumManager.CanAffordHarvests(SaveManager.Instance.Data.items, plotCost.harvestCosts);
                buildList.Add(BuildCardHelper.CreateBuildCard(
                    "Plot", "Grow seeds", "UI/Icons/Buildings/plot", null,
                    BuildCardHelper.FromBuildingCost(plotCost), capText,
                    canAfford, canPlace,
                    () => OnRequestPlacement?.Invoke(CampBuildingType.Plot)));
            }

            // Vase
            if (VaseManager.Instance != null)
            {
                var vaseCost = VaseManager.Instance.GetNextVaseCost();
                bool canAfford = canPlace && vaseCost != null
                    && CurrencyManager.Instance.CanAffordMana(vaseCost.manaCost)
                    && MallumManager.CanAffordHarvests(SaveManager.Instance.Data.items, vaseCost.harvestCosts);
                buildList.Add(BuildCardHelper.CreateBuildCard(
                    "Vase", "Stores water", "UI/Icons/Buildings/vase", null,
                    BuildCardHelper.FromBuildingCost(vaseCost), capText,
                    canAfford, canPlace,
                    () => OnRequestPlacement?.Invoke(CampBuildingType.Vase)));
            }

            // House
            if (MallumManager.Instance != null)
            {
                var nextCost = MallumManager.Instance.GetNextHouseCost();
                if (nextCost != null)
                {
                    bool canAfford = canPlace
                        && CurrencyManager.Instance.CanAffordMana(nextCost.manaCost)
                        && MallumManager.CanAffordHarvests(SaveManager.Instance.Data.items, nextCost.harvestCosts);
                    buildList.Add(BuildCardHelper.CreateBuildCard(
                        "House", "Houses 1 Mallum", "UI/Icons/Buildings/house", null,
                        BuildCardHelper.FromBuildingCost(nextCost), capText,
                        canAfford, canPlace,
                        () => OnRequestPlacement?.Invoke(CampBuildingType.MallumHouse)));
                }
            }

            // Garden entries
            if (GardenManager.Instance != null && FlameManager.Instance != null)
            {
                var data = SaveManager.Instance.Data;
                foreach (var plantData in Resources.LoadAll<GardenPlantData>("GardenPlants"))
                {
                    int existingCount = 0;
                    foreach (var g in data.gardens)
                        if (g.plantName == plantData.plantName) existingCount++;

                    var cost = plantData.GetCost(existingCount);
                    if (cost == null)
                    {
                        buildList.Add(BuildCardHelper.CreateBuildCard(
                            plantData.plantName, $"Yields {plantData.yieldItem}",
                            null, plantData.icon, null, "Max",
                            false, false, null));
                        continue;
                    }

                    var item = data.items.Find(it => it.itemName == plantData.yieldItem);
                    int haveItems = item?.count ?? 0;
                    bool canAfford = canPlace
                        && data.mana >= cost.manaCost
                        && haveItems >= cost.seedCost;

                    string pName = plantData.plantName;
                    buildList.Add(BuildCardHelper.CreateBuildCard(
                        pName, $"Yields {plantData.yieldItem}",
                        null, plantData.icon,
                        BuildCardHelper.FromGardenCost(cost, plantData.yieldItem), capText,
                        canAfford, canPlace, () =>
                        {
                            SelectedGardenPlant = pName;
                            OnRequestPlacement?.Invoke(CampBuildingType.Garden);
                        }));
                }
            }

            // Flame upgrade
            if (FlameManager.Instance != null && FlameManager.Instance.CanUpgrade())
            {
                var recipe = FlameManager.Instance.Config.GetUpgradeRecipe(FlameManager.Instance.Level);
                buildList.Add(BuildCardHelper.CreateBuildCard(
                    "Upgrade Flame", "Expand your camp", "UI/Icons/Buildings/flame", null,
                    BuildCardHelper.FromFlameRecipe(recipe), "", true, true, () =>
                    {
                        FlameManager.Instance.UpgradeFlame();
                        Refresh();
                    }));
            }
        }
    }
}
