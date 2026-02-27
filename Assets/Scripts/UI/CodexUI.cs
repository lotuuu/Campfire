using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class CodexUI : MonoBehaviour
    {
        private VisualTreeAsset _entryTemplate;

        private ScrollView _variantGrid;
        private VisualElement _detailPanel;
        private VisualElement _detailSprite;
        private VisualElement _detailSpriteGlow;
        private Label _detailName;
        private Label _detailDescription;
        private Label _detailRarity;
        private VisualElement _detailRarityBadge;
        private Label _detailSeedName;
        private Button _selectedEntry;

        public void Initialize(VisualElement root)
        {
            _entryTemplate = Resources.Load<VisualTreeAsset>("UI/Templates/VariantEntry");

            _variantGrid = root.Q<ScrollView>("variant-grid");
            _detailPanel = root.Q<VisualElement>("detail-panel");
            _detailSprite = root.Q<VisualElement>("detail-sprite");
            _detailSpriteGlow = root.Q<VisualElement>("detail-sprite-glow");
            _detailName = root.Q<Label>("detail-name");
            _detailDescription = root.Q<Label>("detail-description");
            _detailRarity = root.Q<Label>("detail-rarity");
            _detailRarityBadge = root.Q<VisualElement>("detail-rarity-badge");
            _detailSeedName = root.Q<Label>("detail-seed-name");
        }

        public void Show()
        {
            RefreshCodex();
        }

        private void RefreshCodex()
        {
            _variantGrid.Clear();
            _variantGrid.schedule.Execute(() => _variantGrid.scrollOffset = Vector2.zero);
            _detailPanel.style.display = DisplayStyle.None;
            _selectedEntry = null;

            var discovered = SaveManager.Instance.Data.discoveredVariants;

            foreach (var seed in SeedRegistry.Instance.AllSeeds.OrderBy(s => s.buyPrice))
            {
                foreach (var variant in seed.variants.OrderBy(v => v.rarity))
                {
                    var entry = _entryTemplate.CloneTree();
                    bool isDiscovered = discovered.Contains(variant.variantName);

                    var nameLabel = entry.Q<Label>(className: "variant-name");
                    var sprite = entry.Q<VisualElement>(className: "entry-sprite");
                    var glow = entry.Q<VisualElement>(className: "entry-glow");
                    var lockIcon = entry.Q<Label>(className: "entry-lock");
                    var rarityBar = entry.Q<VisualElement>(className: "entry-rarity-bar");
                    var button = entry.Q<Button>(className: "variant-entry");

                    // Rarity bar — always visible
                    Color rarityColor = GetRarityColor(variant.rarity);
                    if (rarityBar != null)
                        rarityBar.style.backgroundColor = new StyleColor(rarityColor);

                    if (isDiscovered)
                    {
                        // Plant sprite (fully grown)
                        if (sprite != null && seed.growthSprites is { Length: > 0 })
                            sprite.style.backgroundImage = new StyleBackground(seed.growthSprites[^1]);

                        // Glow behind sprite
                        if (glow != null)
                        {
                            glow.style.backgroundColor = new StyleColor(variant.primaryColor);
                            glow.style.opacity = 0.10f;
                        }

                        // Hide lock icon
                        if (lockIcon != null)
                            lockIcon.style.display = DisplayStyle.None;

                        if (nameLabel != null)
                            nameLabel.text = variant.variantName;
                    }
                    else
                    {
                        // No sprite — show lock icon
                        if (lockIcon != null)
                            lockIcon.style.display = DisplayStyle.Flex;

                        if (glow != null)
                            glow.style.opacity = 0f;

                        if (nameLabel != null)
                        {
                            nameLabel.text = $"??? \u00b7 {variant.rarity}";
                            nameLabel.AddToClassList(RarityClass(variant.rarity));
                        }

                        button?.AddToClassList("variant-entry-undiscovered");
                    }

                    if (button != null)
                    {
                        var v = variant;
                        var d = isDiscovered;
                        var s = seed;
                        var b = button;
                        button.clicked += () =>
                        {
                            _selectedEntry?.RemoveFromClassList("variant-entry-selected");
                            b.AddToClassList("variant-entry-selected");
                            _selectedEntry = b;
                            ShowDetail(v, d, s);
                        };
                    }

                    _variantGrid.Add(entry);
                }
            }
        }

        private void ShowDetail(VariantData variant, bool discovered, SeedData seed)
        {
            _detailPanel.style.display = DisplayStyle.Flex;

            SetRarityClass(_detailRarity, variant.rarity);
            Color rarityColor = GetRarityColor(variant.rarity);

            // Rarity badge coloring
            if (_detailRarityBadge != null)
            {
                var badgeBorder = new StyleColor(WithAlpha(rarityColor, 0.25f));
                _detailRarityBadge.style.backgroundColor = new StyleColor(WithAlpha(rarityColor, 0.10f));
                _detailRarityBadge.style.borderTopColor = badgeBorder;
                _detailRarityBadge.style.borderBottomColor = badgeBorder;
                _detailRarityBadge.style.borderLeftColor = badgeBorder;
                _detailRarityBadge.style.borderRightColor = badgeBorder;
            }

            if (discovered)
            {
                _detailName.text = variant.variantName;
                _detailDescription.text = variant.description;
                _detailRarity.text = variant.rarity.ToString().ToUpper();

                if (_detailSprite != null && seed.growthSprites is { Length: > 0 })
                    _detailSprite.style.backgroundImage = new StyleBackground(seed.growthSprites[^1]);
                else if (_detailSprite != null)
                    _detailSprite.style.backgroundImage = new StyleBackground();

                if (_detailSpriteGlow != null)
                {
                    _detailSpriteGlow.style.backgroundColor = new StyleColor(variant.primaryColor);
                    _detailSpriteGlow.style.opacity = 0.10f;
                }
            }
            else
            {
                _detailName.text = "Unknown Variant";
                _detailDescription.text = variant.discoveryHint;
                _detailRarity.text = variant.rarity.ToString().ToUpper();

                if (_detailSprite != null)
                    _detailSprite.style.backgroundImage = new StyleBackground();

                if (_detailSpriteGlow != null)
                    _detailSpriteGlow.style.opacity = 0f;
            }

            if (_detailSeedName != null)
                _detailSeedName.text = seed.seedName;
        }

        private static readonly string[] RarityClasses =
            { "rarity-common", "rarity-uncommon", "rarity-rare", "rarity-epic", "rarity-legendary" };

        private static string RarityClass(Rarity r) => r switch
        {
            Rarity.Common    => "rarity-common",
            Rarity.Uncommon  => "rarity-uncommon",
            Rarity.Rare      => "rarity-rare",
            Rarity.Epic      => "rarity-epic",
            Rarity.Legendary => "rarity-legendary",
            _                => "rarity-common"
        };

        private static void SetRarityClass(Label label, Rarity rarity)
        {
            foreach (var c in RarityClasses) label.RemoveFromClassList(c);
            label.AddToClassList(RarityClass(rarity));
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

        private static Color WithAlpha(Color c, float a) => new(c.r, c.g, c.b, a);
    }
}
