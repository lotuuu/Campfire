using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class CraftUI : MonoBehaviour
    {
        private VisualElement craftList;
        private VisualTreeAsset craftTemplate;

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

            // Craft Plot
            AddCraftItem("New Plot", $"{0} Mana", () =>
            {
                PlotManager.Instance?.CraftPlot();
                Refresh();
            });

            // Craft Vase
            if (VaseManager.Instance != null)
            {
                AddCraftItem("New Vase", $"{VaseManager.Instance.Config.CraftCostMana:F0} Mana", () =>
                {
                    VaseManager.Instance.CraftVase();
                    Refresh();
                });
            }

            // Upgrade Flame
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

        private void AddCraftItem(string name, string cost, System.Action onClick)
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
