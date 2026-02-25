using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public static class SeedSlotUI
    {
        public static VisualElement Create(VisualTreeAsset template, SeedData data, int count, System.Action<SeedData> callback)
        {
            var root = template.CloneTree();
            var slot = root.Q<Button>(className: "seed-slot");

            var nameLabel  = root.Q<Label>(className: "seed-name");
            var countLabel = root.Q<Label>(className: "seed-count");
            var icon       = root.Q<VisualElement>(className: "seed-icon");
            var metaLabel  = root.Q<Label>(className: "seed-meta");
            var variantRow = root.Q<VisualElement>(className: "seed-variants");

            if (nameLabel  != null) nameLabel.text  = data.seedName;
            if (countLabel != null) countLabel.text = count < 0 ? "∞" : $"×{count}";
            if (icon != null && data.icon != null)
                icon.style.backgroundImage = new StyleBackground(data.icon);

            if (metaLabel != null)
                metaLabel.text = $"{data.baseGrowthHours:0.#}h · {data.preferredWeather}";

            if (variantRow != null)
                PopulateVariantChips(variantRow, data);

            if (slot != null)
                slot.clicked += () => callback?.Invoke(data);

            return root;
        }

        private static void PopulateVariantChips(VisualElement container, SeedData data)
        {
            var discovered = SaveManager.Instance.Data.discoveredVariants;
            foreach (var variant in data.variants)
            {
                var chip = new Label();
                chip.AddToClassList("variant-chip");
                if (discovered.Contains(variant.variantName))
                {
                    chip.text = variant.variantName;
                    chip.AddToClassList($"rarity-{variant.rarity.ToString().ToLower()}");
                }
                else
                {
                    chip.text = "?????";
                }
                container.Add(chip);
            }
        }
    }
}
