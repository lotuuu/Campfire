using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class CraftUI : MonoBehaviour
    {
        private VisualElement craftList;
        private VisualTreeAsset craftTemplate;

        public event Action<CampBuildingType> OnRequestPlacement;

        public void Initialize(VisualElement root)
        {
            craftList = root.Q("craft-list");
            craftTemplate = Resources.Load<VisualTreeAsset>("UI/Templates/CraftItem");
            Refresh();
        }

        public void Refresh()
        {
            if (craftList == null) return;
            craftList.Clear();

            // Craft Plot — fires placement event
            if (PlotManager.Instance != null && FlameManager.Instance != null)
            {
                bool canAfford = PlotManager.Instance.Plots.Count < FlameManager.Instance.MaxPlots;
                AddCraftItem("New Plot", canAfford ? "Place on grid" : "Max plots reached", () =>
                {
                    if (canAfford)
                        OnRequestPlacement?.Invoke(CampBuildingType.Plot);
                });
            }

            // Craft Vase — fires placement event
            if (VaseManager.Instance != null)
            {
                float cost = VaseManager.Instance.Config.CraftCostMana;
                bool canAfford = CurrencyManager.Instance.CanAffordMana(cost);
                AddCraftItem("New Vase", $"{cost:F0} Mana", () =>
                {
                    if (canAfford)
                        OnRequestPlacement?.Invoke(CampBuildingType.Vase);
                });
            }

            // Upgrade Flame — stays direct, not placed on grid
            if (FlameManager.Instance != null && FlameManager.Instance.CanUpgrade())
            {
                var cost = FlameManager.Instance.Config.GetUpgradeCost(FlameManager.Instance.Level);
                AddCraftItem("Upgrade Flame", $"{cost:F0} Mana", () =>
                {
                    FlameManager.Instance.UpgradeFlame();
                    Refresh();
                });
            }
        }

        private void AddCraftItem(string name, string cost, Action onClick)
        {
            var el = craftTemplate.CloneTree();
            var nameLabel = el.Q<Label>(className: "craft-name");
            var costLabel = el.Q<Label>(className: "craft-cost");
            var actionBtn = el.Q<Button>(className: "craft-action");

            if (nameLabel != null) nameLabel.text = name;
            if (costLabel != null) costLabel.text = cost;
            if (actionBtn != null) actionBtn.clicked += onClick;

            craftList.Add(el);
        }
    }
}
