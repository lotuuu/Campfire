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

            // Tutorial: disable cards for building types not in allowed set
            var allowed = TutorialManager.Instance?.GetAllowedBuildings();

            // Plot
            if (PlotManager.Instance != null && FlameManager.Instance != null)
            {
                bool plotAllowed = allowed == null || allowed.Contains(CampBuildingType.Plot);
                var plotCost = PlotManager.Instance.GetNextPlotCost();
                bool canAfford = plotAllowed && canPlace && plotCost != null
                    && CurrencyManager.Instance.CanAffordMana(plotCost.manaCost)
                    && MallumManager.CanAffordHarvests(SaveManager.Instance.Data.inventory, plotCost.harvestCosts);
                buildList.Add(BuildCardHelper.CreateBuildCard(
                    "Plot", "Grow seeds", "ui/buildings/plot", null,
                    BuildCardHelper.FromBuildingCost(plotCost), capText,
                    canAfford, plotAllowed && canPlace,
                    () => OnRequestPlacement?.Invoke(CampBuildingType.Plot)));
            }

            // Vase (unlocked at flame level 2)
            if (VaseManager.Instance != null)
            {
                bool vaseAllowed = allowed == null || allowed.Contains(CampBuildingType.Vase);
                bool vaseUnlocked = FlameManager.Instance != null
                    && FlameManager.Instance.Level >= VaseManager.VaseUnlockLevel;
                if (vaseUnlocked)
                {
                    var vaseCost = VaseManager.Instance.GetNextVaseCost();
                    bool canAfford = vaseAllowed && canPlace && vaseCost != null
                        && CurrencyManager.Instance.CanAffordMana(vaseCost.manaCost)
                        && MallumManager.CanAffordHarvests(SaveManager.Instance.Data.inventory, vaseCost.harvestCosts);
                    buildList.Add(BuildCardHelper.CreateBuildCard(
                        "Vase", "Stores water", "ui/buildings/vase", null,
                        BuildCardHelper.FromBuildingCost(vaseCost), capText,
                        canAfford, vaseAllowed && canPlace,
                        () => OnRequestPlacement?.Invoke(CampBuildingType.Vase)));
                }
                else
                {
                    buildList.Add(BuildCardHelper.CreateBuildCard(
                        "Vase", $"Unlocks at Fire Lv.{VaseManager.VaseUnlockLevel}",
                        "ui/buildings/vase", null,
                        null, capText, false, false, null));
                }
            }

            // House
            if (MallumManager.Instance != null)
            {
                bool houseAllowed = allowed == null || allowed.Contains(CampBuildingType.MallumHouse);
                var nextCost = MallumManager.Instance.GetNextHouseCost();
                if (nextCost != null)
                {
                    bool canAfford = houseAllowed && canPlace
                        && CurrencyManager.Instance.CanAffordMana(nextCost.manaCost)
                        && MallumManager.CanAffordHarvests(SaveManager.Instance.Data.inventory, nextCost.harvestCosts);
                    buildList.Add(BuildCardHelper.CreateBuildCard(
                        "House", "Houses 1 Mallum", "ui/buildings/house", null,
                        BuildCardHelper.FromBuildingCost(nextCost), capText,
                        canAfford, houseAllowed && canPlace,
                        () => OnRequestPlacement?.Invoke(CampBuildingType.MallumHouse)));
                }
            }

            // Garden (unlocked at flame level 4)
            if (GardenManager.Instance != null && FlameManager.Instance != null)
            {
                bool gardenAllowed = allowed == null || allowed.Contains(CampBuildingType.Garden);
                bool gardenUnlocked = FlameManager.Instance.Level >= GardenManager.GardenUnlockLevel;
                if (gardenUnlocked)
                {
                    var gardenCost = GardenManager.Instance.GetNextGardenCost();
                    bool canAfford = gardenAllowed && canPlace && gardenCost != null
                        && CurrencyManager.Instance.CanAffordMana(gardenCost.manaCost)
                        && MallumManager.CanAffordHarvests(SaveManager.Instance.Data.inventory, gardenCost.harvestCosts);
                    buildList.Add(BuildCardHelper.CreateBuildCard(
                        "Garden", "Grow fruit trees", "ui/buildings/garden", null,
                        BuildCardHelper.FromBuildingCost(gardenCost), capText,
                        canAfford, gardenAllowed && canPlace,
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
                bool flameAllowed = allowed == null || allowed.Contains(CampBuildingType.Flame);
                var recipe = FlameManager.Instance.GetUpgradeRecipe();
                buildList.Add(BuildCardHelper.CreateBuildCard(
                    "Upgrade Flame", "Expand your camp", "ui/buildings/flame", null,
                    BuildCardHelper.FromFlameRecipe(recipe), "", flameAllowed, flameAllowed, () =>
                    {
                        FlameManager.Instance.UpgradeFlame();
                        Refresh();
                    }));
            }
        }
    }
}
