using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public static class BuildCardHelper
    {
        private static VisualTreeAsset _cardTemplate;

        private static VisualTreeAsset CardTemplate
        {
            get
            {
                if (_cardTemplate == null)
                    _cardTemplate = Resources.Load<VisualTreeAsset>("UI/Templates/BuildCard");
                return _cardTemplate;
            }
        }

        public struct CostChip
        {
            public Sprite icon;
            public string amount;
            public string name;
        }

        public static VisualElement CreateBuildCard(
            string name, string desc, string iconPath, Sprite spriteIcon,
            List<CostChip> costs, string capText,
            bool canAfford, bool canPlace, Action onClick,
            string disabledReason = null)
        {
            var tree = CardTemplate.CloneTree();
            var card = tree.Q<Button>(className: "build-card");

            // Icon
            var iconEl = tree.Q(className: "build-card__icon");
            if (iconEl != null)
            {
                Sprite icon = spriteIcon;
                if (icon == null && !string.IsNullOrEmpty(iconPath))
                {
                    var tex = SpriteService.Instance?.GetTexture(iconPath);
                    if (tex != null)
                        iconEl.style.backgroundImage = new StyleBackground(tex);
                }
                else if (icon != null)
                {
                    iconEl.style.backgroundImage = new StyleBackground(icon);
                }
            }

            // Name & desc
            var nameLabel = tree.Q<Label>(className: "build-card__name");
            if (nameLabel != null) nameLabel.text = name;

            var descLabel = tree.Q<Label>(className: "build-card__desc");
            if (descLabel != null) descLabel.text = desc;

            // Cost chips
            var costsRow = tree.Q(className: "build-card__costs");
            if (costsRow != null && costs != null)
            {
                foreach (var chip in costs)
                {
                    var chipEl = new VisualElement();
                    chipEl.AddToClassList("build-card__cost-chip");

                    var chipIcon = new VisualElement();
                    chipIcon.AddToClassList("build-card__cost-chip-icon");
                    if (chip.icon != null)
                        chipIcon.style.backgroundImage = new StyleBackground(chip.icon);
                    chipEl.Add(chipIcon);

                    var chipLabel = new Label(chip.amount);
                    chipLabel.AddToClassList("build-card__cost-chip-label");
                    chipEl.Add(chipLabel);

                    if (!string.IsNullOrEmpty(chip.name))
                    {
                        var chipName = new Label(chip.name);
                        chipName.AddToClassList("build-card__cost-chip-name");
                        chipEl.Add(chipName);
                    }

                    costsRow.Add(chipEl);
                }
            }

            // Cap badge — hide when not provided (flame menu shows cap once above grid)
            var capLabel = tree.Q<Label>(className: "build-card__cap");
            if (capLabel != null)
            {
                if (string.IsNullOrEmpty(capText))
                {
                    capLabel.style.display = DisplayStyle.None;
                }
                else
                {
                    capLabel.text = !canPlace ? $"Cap reached {capText}" : capText;
                }
            }

            // Enabled state & click
            bool enabled = canAfford && canPlace;
            if (card != null)
            {
                card.SetEnabled(enabled);
                if (enabled && onClick != null)
                    card.clicked += onClick;

                if (!enabled && disabledReason != null)
                {
                    var lockedLabel = new Label(disabledReason);
                    lockedLabel.AddToClassList("build-card__locked-label");
                    card.Add(lockedLabel);
                }
            }

            return tree;
        }

        public static List<CostChip> FromBuildingCost(BuildingCost cost)
        {
            var chips = new List<CostChip>();
            if (cost == null) return chips;

            if (cost.manaCost > 0)
            {
                chips.Add(new CostChip
                {
                    icon = LoadManaIcon(),
                    amount = $"{cost.manaCost:F0}",
                    name = Loc.Get("ui.resource.mana", "Mana")
                });
            }

            foreach (var hc in cost.harvestCosts)
            {
                chips.Add(new CostChip
                {
                    icon = LoadHarvestIcon(hc.itemKey),
                    amount = $"{hc.count}",
                    name = FormatItemName(hc.itemKey)
                });
            }

            return chips;
        }


        public static List<CostChip> FromGardenCost(GardenCostTier cost, string yieldItem)
        {
            var chips = new List<CostChip>();
            if (cost == null) return chips;

            if (cost.manaCost > 0)
            {
                chips.Add(new CostChip
                {
                    icon = LoadManaIcon(),
                    amount = $"{cost.manaCost:F0}",
                    name = Loc.Get("ui.resource.mana", "Mana")
                });
            }

            if (cost.seedCost > 0)
            {
                chips.Add(new CostChip
                {
                    icon = LoadHarvestIcon(yieldItem),
                    amount = $"{cost.seedCost}",
                    name = FormatItemName(yieldItem)
                });
            }

            return chips;
        }

        public static List<CostChip> FromFlameRecipe(FlameUpgradeRecipe recipe)
        {
            var chips = new List<CostChip>();
            if (recipe == null) return chips;

            foreach (var ing in recipe.ingredients)
            {
                chips.Add(new CostChip
                {
                    icon = LoadHarvestIcon(ing.itemKey),
                    amount = $"{ing.count}",
                    name = FormatItemName(ing.itemKey)
                });
            }

            return chips;
        }

        private static string FormatItemName(string itemKey)
        {
            if (string.IsNullOrEmpty(itemKey)) return "";
            // Convert snake_case key like "sprouts_harvest" to "Sprouts Harvest"
            var parts = itemKey.Split('_');
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length > 0)
                    parts[i] = char.ToUpper(parts[i][0]) + parts[i].Substring(1);
            }
            return string.Join(" ", parts);
        }

        private static Sprite LoadManaIcon()
        {
            return SpriteService.Instance?.GetSprite("ui/resource-mana");
        }

        private static Sprite LoadHarvestIcon(string itemKey)
        {
            string key = SpriteService.ItemToSpriteKey(itemKey);
            if (key != null)
            {
                var sprite = SpriteService.Instance?.GetSprite(key);
                if (sprite != null) return sprite;
            }

            // Fallback: try garden plant icon
            string slug = SpriteService.SeedToSpriteKey(itemKey);
            return SpriteService.Instance?.GetSprite($"gardens/{slug}/icon");
        }
    }
}
