using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class SeedShopUI : MonoBehaviour
    {
        private VisualTreeAsset shopCardTemplate;

        private VisualElement panel;
        private ScrollView shopGrid;
        private Button closeButton;

        public void Initialize(VisualElement root)
        {
            shopCardTemplate = Resources.Load<VisualTreeAsset>("UI/Templates/SeedShopCard");

            panel = root.Q<VisualElement>("shop-panel");
            shopGrid = root.Q<ScrollView>("shop-grid");
            closeButton = root.Q<Button>("shop-close");

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
            shopGrid.Clear();

            var seeds = SeedShopManager.Instance.GetShopSeeds();
            foreach (var seed in seeds)
            {
                var card = shopCardTemplate.CloneTree();

                var nameLabel = card.Q<Label>(className: "shop-seed-name");
                var priceLabel = card.Q<Label>(className: "shop-price");
                var conditionLabel = card.Q<Label>(className: "shop-condition");
                var icon = card.Q<VisualElement>(className: "shop-icon");
                var buyBtn = card.Q<Button>(className: "shop-buy-btn");

                int owned = SeedRegistry.Instance.GetSeedCount(seed.seedName);
                if (nameLabel != null) nameLabel.text = $"{seed.seedName} (x{owned})";
                if (priceLabel != null) priceLabel.text = $"{seed.buyPrice} Dew";
                if (conditionLabel != null) conditionLabel.text = seed.description ?? "";
                if (icon != null && seed.icon != null)
                    icon.style.backgroundImage = new StyleBackground(seed.icon);

                if (buyBtn != null)
                {
                    bool canBuy = SeedShopManager.Instance.CanBuy(seed.seedName);
                    buyBtn.SetEnabled(canBuy);
                    buyBtn.text = $"Buy ({seed.buyPrice})";

                    var seedName = seed.seedName;
                    buyBtn.clicked += () =>
                    {
                        if (SeedShopManager.Instance.BuySeed(seedName))
                            RefreshDisplay();
                    };
                }

                shopGrid.Add(card);
            }
        }
    }
}
