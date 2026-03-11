using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class BuildUI : MonoBehaviour
    {
        private VisualElement buildList;

        public event Action<CampBuildingType> OnRequestPlacement;

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
                    && MallumManager.CanAffordHarvests(SaveManager.Instance.Data.inventory, plotCost.harvestCosts);
                buildList.Add(BuildCardHelper.CreateBuildCard(
                    "Plot", "Grow seeds", "ui/buildings/plot", null,
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
                    && MallumManager.CanAffordHarvests(SaveManager.Instance.Data.inventory, vaseCost.harvestCosts);
                buildList.Add(BuildCardHelper.CreateBuildCard(
                    "Vase", "Stores water", "ui/buildings/vase", null,
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
                        && MallumManager.CanAffordHarvests(SaveManager.Instance.Data.inventory, nextCost.harvestCosts);
                    buildList.Add(BuildCardHelper.CreateBuildCard(
                        "House", "Houses 1 Mallum", "ui/buildings/house", null,
                        BuildCardHelper.FromBuildingCost(nextCost), capText,
                        canAfford, canPlace,
                        () => OnRequestPlacement?.Invoke(CampBuildingType.MallumHouse)));
                }
            }

            // Garden (unlocked at flame level 4)
            if (GardenManager.Instance != null && FlameManager.Instance != null)
            {
                bool gardenUnlocked = FlameManager.Instance.Level >= GardenManager.GardenUnlockLevel;
                if (gardenUnlocked)
                {
                    var gardenCost = GardenManager.Instance.GetNextGardenCost();
                    bool canAfford = canPlace && gardenCost != null
                        && CurrencyManager.Instance.CanAffordMana(gardenCost.manaCost)
                        && MallumManager.CanAffordHarvests(SaveManager.Instance.Data.inventory, gardenCost.harvestCosts);
                    buildList.Add(BuildCardHelper.CreateBuildCard(
                        "Garden", "Grow fruit trees", "ui/buildings/garden", null,
                        BuildCardHelper.FromBuildingCost(gardenCost), capText,
                        canAfford, canPlace,
                        () => OnRequestPlacement?.Invoke(CampBuildingType.Garden)));
                }
                else
                {
                    buildList.Add(BuildCardHelper.CreateBuildCard(
                        "Garden", $"Unlocks at Fire Lv.{GardenManager.GardenUnlockLevel}",
                        "ui/buildings/garden", null,
                        null, capText, false, false, null));
                }
            }

            // Flame upgrade
            if (FlameManager.Instance != null && FlameManager.Instance.CanUpgrade())
            {
                var recipe = FlameManager.Instance.GetUpgradeRecipe();
                buildList.Add(BuildCardHelper.CreateBuildCard(
                    "Upgrade Flame", "Expand your camp", "ui/buildings/flame", null,
                    BuildCardHelper.FromFlameRecipe(recipe), "", true, true, () =>
                    {
                        FlameManager.Instance.UpgradeFlame();
                        Refresh();
                    }));
            }
        }
    }
}
