using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class GreenhouseUI : MonoBehaviour
    {
        private VisualTreeAsset plantSlotTemplate;

        private ScrollView plantGrid;
        private Label pollenRateText;
        private Label slotsText;

        // Sell bar
        private VisualElement sellBar;
        private VisualElement sellBarSprite;
        private Label sellNameLabel;
        private Label sellPollenLabel;
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
            pollenRateText = root.Q<Label>("greenhouse-pollen-rate");
            slotsText = root.Q<Label>("greenhouse-slots-text");

            sellBar = root.Q<VisualElement>("greenhouse-sell-bar");
            sellBarSprite = root.Q<VisualElement>("sell-bar-sprite");
            sellNameLabel = root.Q<Label>("greenhouse-sell-name");
            sellPollenLabel = root.Q<Label>("greenhouse-sell-pollen");
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
            pollenRateText.text = $"+{gm.GetTotalPollenPerSecond() * 60f:F1} Pollen/min";

            if (gm.Plants.Count == 0)
            {
                var hint = new Label("Harvest plants from your Backyard\nto fill the Greenhouse.");
                hint.AddToClassList("greenhouse-empty-hint");
                plantGrid.Add(hint);
            }

            for (int i = 0; i < gm.Plants.Count; i++)
            {
                var plant = gm.Plants[i];
                var slot = plantSlotTemplate.CloneTree();
                var slotRoot = slot.Q<VisualElement>(className: "plant-slot");
                var nameLabel = slot.Q<Label>(className: "plant-name");
                var sprite = slot.Q<VisualElement>(className: "plant-sprite");
                var glow = slot.Q<VisualElement>(className: "plant-glow");
                var qualityLabel = slot.Q<Label>(className: "plant-quality");
                var rarityBar = slot.Q<VisualElement>(className: "plant-rarity-bar");

                // Look up seed for growth sprites
                var seed = SeedRegistry.Instance.GetSeed(plant.seedName);

                // Plant sprite (fully grown)
                if (sprite != null && seed?.growthSprites is { Length: > 0 })
                    sprite.style.backgroundImage = new StyleBackground(seed.growthSprites[^1]);

                // Rarity bar
                if (rarityBar != null)
                    rarityBar.style.backgroundColor = new StyleColor(GetRarityColor(plant.rarity));

                if (plant.isWithered)
                {
                    if (nameLabel != null) nameLabel.text = "Withered";
                    if (slotRoot != null) slotRoot.AddToClassList("plant-slot--withered");
                }
                else
                {
                    // Glow from variant color
                    if (glow != null)
                    {
                        glow.style.backgroundColor = new StyleColor(plant.primaryColor);
                        glow.style.opacity = 0.10f;
                    }

                    if (nameLabel != null) nameLabel.text = plant.variantName;

                    // Quality tier label
                    if (qualityLabel != null)
                    {
                        qualityLabel.text = CurrencyConfig.GetQualityLabel(plant.qualityTier);
                        qualityLabel.style.color = new StyleColor(GetTierColor(plant.qualityTier));
                    }

                    // Decay bar
                    var decayBarFill = slot.Q<VisualElement>(className: "plant-decay-bar-fill");
                    var decayTimeLabel = slot.Q<Label>(className: "plant-decay-time");

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

            // Empty slots
            for (int i = gm.Plants.Count; i < gm.MaxSlots; i++)
            {
                var slot = plantSlotTemplate.CloneTree();
                var slotRoot = slot.Q<VisualElement>(className: "plant-slot");
                var nameLabel = slot.Q<Label>(className: "plant-name");
                if (nameLabel != null) nameLabel.text = "Empty";
                if (slotRoot != null) slotRoot.AddToClassList("plant-slot--empty");
                plantGrid.Add(slot);
            }

            plantGrid.schedule.Execute(() => plantGrid.scrollOffset = Vector2.zero);
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
            float dustRate = config.GetPollenPerSecondForPlant(plant.rarity, plant.qualityTier) * 60f;
            string qualityLabel = CurrencyConfig.GetQualityLabel(plant.qualityTier);

            // Sell bar sprite
            if (sellBarSprite != null && seed?.growthSprites is { Length: > 0 })
                sellBarSprite.style.backgroundImage = new StyleBackground(seed.growthSprites[^1]);
            else if (sellBarSprite != null)
                sellBarSprite.style.backgroundImage = new StyleBackground();

            if (plant.isWithered)
            {
                if (sellNameLabel != null) sellNameLabel.text = $"{plant.variantName} \u00b7 Withered";
                if (sellPollenLabel != null) sellPollenLabel.text = "No value remaining";
                if (sellButton != null) sellButton.text = "Trash";
            }
            else
            {
                if (sellNameLabel != null) sellNameLabel.text = $"{plant.variantName} \u00b7 {qualityLabel}";
                if (sellPollenLabel != null) sellPollenLabel.text = $"+{dustRate:F1} Pollen/min";
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

        private static Color GetRarityColor(Rarity r) => r switch
        {
            Rarity.Common    => new Color(0.627f, 0.686f, 0.725f),
            Rarity.Uncommon  => new Color(0.314f, 0.784f, 0.471f),
            Rarity.Rare      => new Color(0.314f, 0.627f, 1f),
            Rarity.Epic      => new Color(0.745f, 0.314f, 1f),
            Rarity.Legendary => new Color(1f, 0.725f, 0.196f),
            _                => new Color(0.627f, 0.686f, 0.725f)
        };

        private static Color GetTierColor(QualityTier tier) => tier switch
        {
            QualityTier.D => new Color(1f, 0.275f, 0.235f),
            QualityTier.C => new Color(1f, 0.706f, 0.196f),
            QualityTier.B => new Color(0.314f, 0.863f, 0.471f),
            QualityTier.A => new Color(0.392f, 0.863f, 1f),
            QualityTier.S => new Color(1f, 0.863f, 0.196f),
            _             => new Color(0.667f, 0.765f, 0.824f)
        };
    }
}
