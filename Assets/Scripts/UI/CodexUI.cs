using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class CodexUI : MonoBehaviour
    {
        private VisualTreeAsset variantEntryTemplate;

        private ScrollView variantGrid;
        private Label detailName;
        private Label detailDescription;
        private Label detailRarity;
        private VisualElement detailColorSwatch;

        public void Initialize(VisualElement root)
        {
            variantEntryTemplate = Resources.Load<VisualTreeAsset>("UI/Templates/VariantEntry");

            variantGrid = root.Q<ScrollView>("variant-grid");
            detailName = root.Q<Label>("detail-name");
            detailDescription = root.Q<Label>("detail-description");
            detailRarity = root.Q<Label>("detail-rarity");
            detailColorSwatch = root.Q<VisualElement>("detail-color-swatch");
        }

        public void Show()
        {
            RefreshCodex();
        }

        private void RefreshCodex()
        {
            variantGrid.Clear();

            var discovered = SaveManager.Instance.Data.discoveredVariants;

            foreach (var seed in SeedRegistry.Instance.AllSeeds)
            {
                foreach (var variant in seed.variants)
                {
                    var entry = variantEntryTemplate.CloneTree();
                    bool isDiscovered = discovered.Contains(variant.variantName);

                    var nameLabel = entry.Q<Label>(className: "variant-name");
                    var swatch = entry.Q<VisualElement>(className: "variant-swatch");
                    var button = entry.Q<Button>(className: "variant-entry");

                    if (isDiscovered)
                    {
                        if (nameLabel != null) nameLabel.text = variant.variantName;
                        if (swatch != null) swatch.style.backgroundColor = variant.primaryColor;
                    }
                    else
                    {
                        if (nameLabel != null) nameLabel.text = "???";
                        if (swatch != null) swatch.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
                    }

                    if (button != null)
                    {
                        var v = variant;
                        var d = isDiscovered;
                        button.clicked += () => ShowDetail(v, d);
                    }

                    variantGrid.Add(entry);
                }
            }
        }

        private void ShowDetail(VariantData variant, bool discovered)
        {
            if (discovered)
            {
                detailName.text = variant.variantName;
                detailDescription.text = variant.description;
                detailRarity.text = variant.rarity.ToString();
                detailColorSwatch.style.backgroundColor = variant.primaryColor;
            }
            else
            {
                detailName.text = "Unknown Variant";
                detailDescription.text = variant.discoveryHint;
                detailRarity.text = "???";
                detailColorSwatch.style.backgroundColor = Color.black;
            }
        }
    }
}
