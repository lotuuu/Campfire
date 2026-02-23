using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class GreenhouseUI : MonoBehaviour
    {
        private VisualTreeAsset plantSlotTemplate;

        private VisualElement panel;
        private ScrollView plantGrid;
        private Label dustRateText;
        private Label slotsText;
        private Button expandButton;
        private Button closeButton;

        public void Initialize(VisualElement root)
        {
            plantSlotTemplate = Resources.Load<VisualTreeAsset>("UI/Templates/PlantSlot");

            panel = root.Q<VisualElement>("greenhouse-panel");
            plantGrid = root.Q<ScrollView>("plant-grid");
            dustRateText = root.Q<Label>("dust-rate");
            slotsText = root.Q<Label>("slots-text");
            expandButton = root.Q<Button>("expand-button");
            closeButton = root.Q<Button>("greenhouse-close");

            expandButton.clicked += OnExpand;
            closeButton.clicked += Hide;
        }

        public void Show()
        {
            panel.style.display = DisplayStyle.Flex;
            RefreshDisplay();
        }

        public void Hide()
        {
            panel.style.display = DisplayStyle.None;
        }

        private void RefreshDisplay()
        {
            plantGrid.Clear();

            var gm = GreenhouseManager.Instance;
            slotsText.text = $"{gm.Plants.Count} / {gm.MaxSlots}";
            dustRateText.text = $"+{gm.GetTotalDustPerHour():F1} Aura Dust/hr";

            foreach (var plant in gm.Plants)
            {
                var slot = plantSlotTemplate.CloneTree();
                var nameLabel = slot.Q<Label>(className: "plant-name");
                var swatch = slot.Q<VisualElement>(className: "plant-swatch");
                if (nameLabel != null) nameLabel.text = plant.variantName;
                if (swatch != null) swatch.style.backgroundColor = plant.primaryColor;
                plantGrid.Add(slot);
            }

            for (int i = gm.Plants.Count; i < gm.MaxSlots; i++)
            {
                var slot = plantSlotTemplate.CloneTree();
                var nameLabel = slot.Q<Label>(className: "plant-name");
                var swatch = slot.Q<VisualElement>(className: "plant-swatch");
                if (nameLabel != null) nameLabel.text = "Empty";
                if (swatch != null) swatch.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f, 0.3f);
                plantGrid.Add(slot);
            }

            var config = CurrencyManager.Instance.Config;
            expandButton.SetEnabled(CurrencyManager.Instance.CanAfford(
                CurrencyType.SunShards, config.slotCostSunShards));
        }

        private void OnExpand()
        {
            if (GreenhouseManager.Instance.ExpandSlots())
                RefreshDisplay();
        }
    }
}
