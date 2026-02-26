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

        // Live decay tracking
        private readonly List<(VisualElement bar, Label label, int plantIndex)> _decayWidgets = new();
        private float _decayTick;

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
            _decayWidgets.Clear();
            selectedIndex = -1;
            if (sellBar != null) sellBar.style.display = DisplayStyle.None;

            var gm = GreenhouseManager.Instance;
            slotsText.text = $"{gm.Plants.Count}";
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
                var decayTimeLabel = slot.Q<Label>(className: "plant-decay-time");
                if (!plant.isWithered)
                {
                    float progress = gm.GetDecayProgress(i);
                    float remaining = (1f - progress) * GreenhouseManager.GetStepMinutes(plant.qualityTier, plant.baseGrowthHours);

                    if (decayBarFill != null)
                    {
                        decayBarFill.style.width = Length.Percent((1f - progress) * 100f);
                        decayBarFill.RemoveFromClassList("plant-decay-bar-fill--warning");
                        decayBarFill.RemoveFromClassList("plant-decay-bar-fill--critical");
                        if (plant.qualityTier == QualityTier.D)
                            decayBarFill.AddToClassList("plant-decay-bar-fill--critical");
                        else if (plant.qualityTier == QualityTier.C)
                            decayBarFill.AddToClassList("plant-decay-bar-fill--warning");
                    }

                    if (decayTimeLabel != null)
                        decayTimeLabel.text = FormatMinutes(remaining);

                    _decayWidgets.Add((decayBarFill, decayTimeLabel, i));
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
            int baseSell = seed != null ? seed.greenhouseYield : 100;
            int sellValue = config.GetGreenhouseSellValue(baseSell, plant.qualityTier);
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

        private static string FormatMinutes(float minutes)
        {
            if (minutes < 1f) return $"{Mathf.Max(0, (int)(minutes * 60f))}s";
            int h = (int)(minutes / 60f);
            int m = (int)(minutes % 60f);
            return h > 0 ? $"{h}h {m}m" : $"{m}m";
        }

        private void Update()
        {
            if (_decayWidgets.Count == 0) return;
            _decayTick += Time.deltaTime;
            if (_decayTick < 1f) return;
            _decayTick = 0f;

            var gm = GreenhouseManager.Instance;
            foreach (var (bar, label, idx) in _decayWidgets)
            {
                if (idx >= gm.Plants.Count) continue;
                var plant = gm.Plants[idx];
                if (plant.isWithered) continue;
                float progress = gm.GetDecayProgress(idx);
                float remaining = (1f - progress) * GreenhouseManager.GetStepMinutes(plant.qualityTier, plant.baseGrowthHours);
                if (bar != null) bar.style.width = Length.Percent((1f - progress) * 100f);
                if (label != null) label.text = FormatMinutes(remaining);
            }
        }

        private void OnSell()
        {
            if (selectedIndex < 0) return;
            var plant = GreenhouseManager.Instance.Plants[selectedIndex];
            if (plant.isWithered)
                GreenhouseManager.Instance.TrashPlant(selectedIndex);
            else
                GreenhouseManager.Instance.SellPlant(selectedIndex);
        }
    }
}
