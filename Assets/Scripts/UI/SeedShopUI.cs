using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class SeedShopUI : MonoBehaviour
    {
        private VisualTreeAsset shopCardTemplate;

        private ScrollView shopGrid;

        public void Initialize(VisualElement root)
        {
            shopCardTemplate = Resources.Load<VisualTreeAsset>("UI/Templates/SeedShopCard");

            shopGrid = root.Q<ScrollView>("shop-grid");
        }

        public void Show()
        {
            RefreshDisplay();
        }

        private void RefreshDisplay()
        {
            shopGrid.Clear();

            var seeds = SeedShopManager.Instance.GetShopSeeds();
            seeds.Sort((a, b) => a.buyPrice.CompareTo(b.buyPrice));
            shopGrid.contentContainer.style.flexDirection = FlexDirection.Column;

            foreach (var seed in seeds)
            {
                var card = shopCardTemplate.CloneTree();
                card.style.flexGrow = 1;
                card.style.flexShrink = 0;

                var nameLabel = card.Q<Label>(className: "shop-seed-name");
                var priceLabel = card.Q<Label>(className: "shop-price");
                var conditionLabel = card.Q<Label>(className: "shop-condition");
                var icon = card.Q<VisualElement>(className: "shop-icon");
                var buyBtn = card.Q<Button>(className: "shop-buy-btn");

                int owned = SeedRegistry.Instance.GetSeedCount(seed.seedName);
                if (nameLabel != null) nameLabel.text = $"{seed.seedName} (x{owned})";
                if (priceLabel != null) priceLabel.text = $"{seed.buyPrice} Dust";
                if (conditionLabel != null) conditionLabel.text = seed.description ?? "";
                if (icon != null && seed.icon != null)
                    icon.style.backgroundImage = new StyleBackground(seed.icon);

                if (buyBtn != null)
                {
                    bool canBuy = SeedShopManager.Instance.CanBuy(seed.seedName);
                    buyBtn.SetEnabled(canBuy);
                    buyBtn.text = $"Buy ({seed.buyPrice} Dust)";

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
