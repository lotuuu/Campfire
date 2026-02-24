using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class GreenhouseUI : MonoBehaviour
    {
        private VisualTreeAsset plantSlotTemplate;

        private ScrollView plantGrid;
        private Label dustRateText;
        private Label slotsText;

        public void Initialize(VisualElement root)
        {
            plantSlotTemplate = Resources.Load<VisualTreeAsset>("UI/Templates/PlantSlot");

            plantGrid = root.Q<ScrollView>("greenhouse-grid");
            dustRateText = root.Q<Label>("greenhouse-dust-rate");
            slotsText = root.Q<Label>("greenhouse-slots-text");
        }

        public void Show()
        {
            RefreshDisplay();
        }

        public void RefreshDisplay()
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
        }
    }
}
