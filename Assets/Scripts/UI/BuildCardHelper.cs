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
        }

        public static VisualElement CreateBuildCard(
            string name, string desc, string iconPath, Sprite spriteIcon,
            List<CostChip> costs, string capText,
            bool canAfford, bool canPlace, Action onClick)
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
                    var tex = Resources.Load<Texture2D>(iconPath);
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

                    costsRow.Add(chipEl);
                }
            }

            // Cap badge
            var capLabel = tree.Q<Label>(className: "build-card__cap");
            if (capLabel != null) capLabel.text = capText;

            // Enabled state & click
            bool enabled = canAfford && canPlace;
            if (card != null)
            {
                card.SetEnabled(enabled);
                if (enabled && onClick != null)
                    card.clicked += onClick;

                if (!enabled)
                {
                    string reason = !canPlace ? "Cap reached" : "Can't afford";
                    var lockedLabel = new Label(reason);
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
                    amount = $"{cost.manaCost:F0}"
                });
            }

            foreach (var hc in cost.harvestCosts)
            {
                chips.Add(new CostChip
                {
                    icon = LoadHarvestIcon(hc.itemName),
                    amount = $"{hc.count}"
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
                    amount = $"{cost.manaCost:F0}"
                });
            }

            if (cost.seedCost > 0)
            {
                chips.Add(new CostChip
                {
                    icon = LoadHarvestIcon(yieldItem),
                    amount = $"{cost.seedCost}"
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
                    icon = LoadHarvestIcon(ing.itemName),
                    amount = $"{ing.count}"
                });
            }

            return chips;
        }

        private static Sprite _manaIcon;

        private static Sprite LoadManaIcon()
        {
            if (_manaIcon == null)
            {
                var tex = Resources.Load<Texture2D>("UI/Icons/resource-mana");
                if (tex != null)
                    _manaIcon = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }
            return _manaIcon;
        }

        private static Sprite LoadHarvestIcon(string itemName)
        {
            string seedName = itemName.Replace("_harvest", "");
            var seedData = Resources.Load<SeedData>($"Seeds/{seedName}");
            if (seedData != null && seedData.icon != null)
                return seedData.icon;

            // Fallback: try garden plant data
            var plantData = Resources.Load<GardenPlantData>($"GardenPlants/{seedName}");
            if (plantData != null && plantData.icon != null)
                return plantData.icon;

            return null;
        }
    }
}
