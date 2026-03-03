using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class BuildUI : MonoBehaviour
    {
        private VisualElement buildList;
        private VisualTreeAsset buildTemplate;

        public event Action<CampBuildingType> OnRequestPlacement;

        public void Initialize(VisualElement root)
        {
            buildList = root.Q("build-list");
            buildTemplate = Resources.Load<VisualTreeAsset>("UI/Templates/CraftItem");
            Refresh();
        }

        public void Refresh()
        {
            if (buildList == null) return;
            buildList.Clear();

            if (PlotManager.Instance != null && FlameManager.Instance != null)
            {
                bool canPlace = FlameManager.Instance.CanPlaceEntity;
                string capText = $"{FlameManager.Instance.CurrentEntityCount}/{FlameManager.Instance.MaxEntities}";
                var plotCost = PlotManager.Instance.GetNextPlotCost();
                bool canAfford = canPlace && plotCost != null
                    && CurrencyManager.Instance.CanAffordMana(plotCost.manaCost)
                    && MallumManager.CanAffordSeeds(SaveManager.Instance.Data.seedInventory, plotCost.seedCosts);
                string plotCostText = plotCost != null ? FormatBuildingCost(plotCost) : "???";
                AddBuildItem("New Plot", canPlace ? $"{plotCostText} ({capText})" : $"Cap reached ({capText})", () =>
                {
                    if (canAfford)
                        OnRequestPlacement?.Invoke(CampBuildingType.Plot);
                });
            }

            if (VaseManager.Instance != null)
            {
                bool canPlace = FlameManager.Instance != null && FlameManager.Instance.CanPlaceEntity;
                string capText = FlameManager.Instance != null
                    ? $"{FlameManager.Instance.CurrentEntityCount}/{FlameManager.Instance.MaxEntities}"
                    : "";
                var vaseCost = VaseManager.Instance.GetNextVaseCost();
                bool canAfford = canPlace && vaseCost != null
                    && CurrencyManager.Instance.CanAffordMana(vaseCost.manaCost)
                    && MallumManager.CanAffordSeeds(SaveManager.Instance.Data.seedInventory, vaseCost.seedCosts);
                string vaseCostText = vaseCost != null ? FormatBuildingCost(vaseCost) : "???";
                AddBuildItem("New Vase", canPlace ? $"{vaseCostText} ({capText})" : $"Cap reached ({capText})", () =>
                {
                    if (canAfford)
                        OnRequestPlacement?.Invoke(CampBuildingType.Vase);
                });
            }

            if (MallumManager.Instance != null)
            {
                var hConfig = MallumManager.Instance.HouseConfig;
                var nextCost = hConfig.GetNextHouseCost(SaveManager.Instance.Data.mallumHouses.Count);
                if (nextCost != null)
                {
                    bool canPlace = FlameManager.Instance != null && FlameManager.Instance.CanPlaceEntity;
                    string capText = FlameManager.Instance != null
                        ? $"{FlameManager.Instance.CurrentEntityCount}/{FlameManager.Instance.MaxEntities}"
                        : "";
                    string costText = $"{nextCost.manaCost:F0} Mana";
                    foreach (var sc in nextCost.seedCosts)
                        costText += $" + {sc.count} {sc.seedName}";
                    bool canAfford = canPlace
                        && CurrencyManager.Instance.CanAffordMana(nextCost.manaCost)
                        && MallumManager.CanAffordSeeds(SaveManager.Instance.Data.seedInventory, nextCost.seedCosts);
                    AddBuildItem("Mallum House", canPlace ? $"{costText} ({capText})" : $"Cap reached ({capText})", () =>
                    {
                        if (canAfford)
                            OnRequestPlacement?.Invoke(CampBuildingType.MallumHouse);
                    });
                }
            }

            if (FlameManager.Instance != null && FlameManager.Instance.CanUpgrade())
            {
                var recipe = FlameManager.Instance.Config.GetUpgradeRecipe(FlameManager.Instance.Level);
                string costText = recipe != null ? FormatRecipeCost(recipe) : "???";
                AddBuildItem("Upgrade Flame", costText, () =>
                {
                    FlameManager.Instance.UpgradeFlame();
                    Refresh();
                });
            }
        }

        private static string FormatBuildingCost(BuildingCost cost)
        {
            string text = $"{cost.manaCost:F0} Mana";
            foreach (var sc in cost.seedCosts)
                text += $" + {sc.count} {sc.seedName}";
            return text;
        }

        private static string FormatRecipeCost(FlameUpgradeRecipe recipe)
        {
            var parts = new System.Collections.Generic.List<string>();
            foreach (var ing in recipe.ingredients)
            {
                string displayName = ing.itemName.Replace("_harvest", "");
                parts.Add($"{ing.count}x {displayName}");
            }
            return string.Join(", ", parts);
        }

        private void AddBuildItem(string name, string cost, Action onClick)
        {
            var el = buildTemplate.CloneTree();
            var nameLabel = el.Q<Label>(className: "craft-name");
            var costLabel = el.Q<Label>(className: "craft-cost");
            var actionBtn = el.Q<Button>(className: "craft-action");

            if (nameLabel != null) nameLabel.text = name;
            if (costLabel != null) costLabel.text = cost;
            if (actionBtn != null) actionBtn.clicked += onClick;

            buildList.Add(el);
        }
    }
}
