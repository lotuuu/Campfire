using System.Collections.Generic;
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

        // Sell bar
        private VisualElement sellBar;
        private Label sellNameLabel;
        private Label sellDustLabel;
        private Button sellButton;

        // Selection state
        private int selectedIndex = -1;
        private readonly List<VisualElement> filledSlotRoots = new();

        public void Initialize(VisualElement root)
        {
            plantSlotTemplate = Resources.Load<VisualTreeAsset>("UI/Templates/PlantSlot");

            plantGrid = root.Q<ScrollView>("greenhouse-grid");
            dustRateText = root.Q<Label>("greenhouse-dust-rate");
            slotsText = root.Q<Label>("greenhouse-slots-text");

            sellBar = root.Q<VisualElement>("greenhouse-sell-bar");
            sellNameLabel = root.Q<Label>("greenhouse-sell-name");
            sellDustLabel = root.Q<Label>("greenhouse-sell-dust");
            sellButton = root.Q<Button>("greenhouse-sell-btn");

            if (sellButton != null)
                sellButton.clicked += OnSell;

            GreenhouseManager.Instance.OnGreenhouseChanged += RefreshDisplay;
        }

        public void Show()
        {
            RefreshDisplay();
        }

        public void RefreshDisplay()
        {
            plantGrid.Clear();
            filledSlotRoots.Clear();
            selectedIndex = -1;
            if (sellBar != null) sellBar.style.display = DisplayStyle.None;

            var gm = GreenhouseManager.Instance;
            slotsText.text = $"{gm.Plants.Count} / {gm.MaxSlots}";
            dustRateText.text = $"+{gm.GetTotalDustPerSecond() * 60f:F1} Aura Dust/min";

            for (int i = 0; i < gm.Plants.Count; i++)
            {
                var plant = gm.Plants[i];
                var slot = plantSlotTemplate.CloneTree();
                var slotRoot = slot.Q<VisualElement>(className: "plant-slot");
                var nameLabel = slot.Q<Label>(className: "plant-name");
                var swatch = slot.Q<VisualElement>(className: "plant-swatch");

                if (nameLabel != null)
                    nameLabel.text = plant.isWithered ? "Withered" : plant.variantName;
                if (swatch != null) swatch.style.backgroundColor = plant.primaryColor;

                if (plant.isWithered && slotRoot != null)
                    slotRoot.AddToClassList("plant-slot--withered");

                var decayBarFill = slot.Q<VisualElement>(className: "plant-decay-bar-fill");
                if (decayBarFill != null && !plant.isWithered)
                {
                    float progress = gm.GetDecayProgress(i);
                    decayBarFill.style.width = Length.Percent(progress * 100f);
                    decayBarFill.RemoveFromClassList("plant-decay-bar-fill--warning");
                    decayBarFill.RemoveFromClassList("plant-decay-bar-fill--critical");
                    if (plant.qualityTier == QualityTier.D)
                        decayBarFill.AddToClassList("plant-decay-bar-fill--critical");
                    else if (plant.qualityTier == QualityTier.C)
                        decayBarFill.AddToClassList("plant-decay-bar-fill--warning");
                }

                filledSlotRoots.Add(slotRoot);

                int capturedIndex = i;
                slot.RegisterCallback<ClickEvent>(_ => OnSlotClicked(capturedIndex));

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

        private void OnSlotClicked(int index)
        {
            if (selectedIndex == index)
            {
                ClearSelection();
                return;
            }

            if (selectedIndex >= 0 && selectedIndex < filledSlotRoots.Count)
                filledSlotRoots[selectedIndex]?.RemoveFromClassList("plant-slot--selected");

            selectedIndex = index;
            if (index >= 0 && index < filledSlotRoots.Count)
                filledSlotRoots[index]?.AddToClassList("plant-slot--selected");
            UpdateSellBar(index);
        }

        private void ClearSelection()
        {
            if (selectedIndex >= 0 && selectedIndex < filledSlotRoots.Count)
                filledSlotRoots[selectedIndex]?.RemoveFromClassList("plant-slot--selected");
            selectedIndex = -1;
            if (sellBar != null) sellBar.style.display = DisplayStyle.None;
        }

        private void UpdateSellBar(int index)
        {
            var gm = GreenhouseManager.Instance;
            var config = CurrencyManager.Instance.Config;
            var plant = gm.Plants[index];

            var seed = SeedRegistry.Instance.GetSeed(plant.seedName);
            int baseSell = seed != null ? seed.baseSellPrice : 100;
            int sellValue = config.GetSellValue(baseSell, plant.qualityTier);
            float dustRate = config.GetDustPerSecondForPlant(plant.rarity, plant.qualityTier) * 60f;
            string qualityLabel = CurrencyConfig.GetQualityLabel(plant.qualityTier);

            if (plant.isWithered)
            {
                if (sellNameLabel != null) sellNameLabel.text = $"{plant.variantName} · Withered";
                if (sellDustLabel != null) sellDustLabel.text = "No value remaining";
                if (sellButton != null) sellButton.text = "Trash";
            }
            else
            {
                if (sellNameLabel != null) sellNameLabel.text = $"{plant.variantName} · {qualityLabel}";
                if (sellDustLabel != null) sellDustLabel.text = $"+{dustRate:F1} Dust/min";
                if (sellButton != null) sellButton.text = $"Sell for {sellValue} Gold";
            }
            if (sellBar != null) sellBar.style.display = DisplayStyle.Flex;
        }

        private void OnSell()
        {
            if (selectedIndex < 0) return;
            var plant = GreenhouseManager.Instance.Plants[selectedIndex];
            if (plant.isWithered)
                GreenhouseManager.Instance.TrashPlant(selectedIndex);
            else
                GreenhouseManager.Instance.SellPlant(selectedIndex);
            RefreshDisplay();
        }
    }
}
