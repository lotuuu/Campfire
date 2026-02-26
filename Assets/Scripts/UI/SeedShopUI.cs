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
            shopGrid.contentContainer.style.flexDirection = FlexDirection.Column;
            shopGrid.contentContainer.style.flexWrap = Wrap.NoWrap;
            shopGrid.verticalScrollerVisibility = ScrollerVisibility.Hidden;

            if (CurrencyManager.Instance != null)
                CurrencyManager.Instance.OnCurrencyChanged += OnCurrencyChanged;
        }

        private void OnDestroy()
        {
            if (CurrencyManager.Instance != null)
                CurrencyManager.Instance.OnCurrencyChanged -= OnCurrencyChanged;
        }

        private void OnCurrencyChanged(CurrencyType type, int oldVal, int newVal) => RefreshDisplay();

        public void Show() => RefreshDisplay();

        private void RefreshDisplay()
        {
            var savedOffset = shopGrid.scrollOffset;
            shopGrid.Clear();
            AddSeedSection();
            AddConsumableSection();
            shopGrid.schedule.Execute(() => shopGrid.scrollOffset = savedOffset);
        }

        private VisualElement MakeSectionBanner(string title, string currencyLabel, string badgeClass)
        {
            var banner = new VisualElement();
            banner.AddToClassList("shop-section-banner");

            var titleLabel = new Label(title);
            titleLabel.AddToClassList("shop-section-banner-title");

            var badge = new Label(currencyLabel);
            badge.AddToClassList("shop-currency-badge");
            badge.AddToClassList(badgeClass);

            banner.Add(titleLabel);
            banner.Add(badge);
            return banner;
        }

        private void AddSeedSection()
        {
            shopGrid.Add(MakeSectionBanner("Seeds", "AuraDust", "shop-currency-badge--aura-dust"));

            var seeds = SeedShopManager.Instance.GetShopSeeds();
            seeds.Sort((a, b) => a.buyPrice.CompareTo(b.buyPrice));

            foreach (var seed in seeds)
            {
                var card = shopCardTemplate.CloneTree();
                card.AddToClassList("shop-card--seeds");
                card.style.flexGrow = 1;
                card.style.flexShrink = 0;

                var nameLabel  = card.Q<Label>(className: "shop-seed-name");
                var priceLabel = card.Q<Label>(className: "shop-price");
                var condLabel  = card.Q<Label>(className: "shop-condition");
                var icon       = card.Q<VisualElement>(className: "shop-icon");
                var buyBtn     = card.Q<Button>(className: "shop-buy-btn");

                int owned = SeedRegistry.Instance.GetSeedCount(seed.seedName);
                if (nameLabel  != null) nameLabel.text  = $"{seed.seedName} (x{owned})";
                if (priceLabel != null) priceLabel.text = $"{seed.buyPrice} Dust";
                if (condLabel  != null) condLabel.text  = seed.description ?? "";
                if (icon != null && seed.icon != null)
                    icon.style.backgroundImage = new StyleBackground(seed.icon);

                if (buyBtn != null)
                {
                    buyBtn.SetEnabled(SeedShopManager.Instance.CanBuy(seed.seedName));
                    buyBtn.text = $"Buy ({seed.buyPrice} Dust)";
                    var seedName = seed.seedName;
                    buyBtn.clicked += () => { if (SeedShopManager.Instance.BuySeed(seedName)) RefreshDisplay(); };
                }
                shopGrid.Add(card);
            }
        }

        private void AddConsumableSection()
        {
            if (ConsumableManager.Instance == null) return;

            var banner = MakeSectionBanner("Consumables", "SunShards", "shop-currency-badge--sun-shards");
            banner.style.marginTop = 32;
            shopGrid.Add(banner);

            var consumables = new System.Collections.Generic.List<ConsumableData>(
                ConsumableManager.Instance.AllConsumables);
            consumables.Sort((a, b) => a.buyPrice.CompareTo(b.buyPrice));

            foreach (var c in consumables)
            {
                var card = shopCardTemplate.CloneTree();
                card.AddToClassList("shop-card--consumables");
                card.style.flexGrow = 1;
                card.style.flexShrink = 0;

                var nameLabel  = card.Q<Label>(className: "shop-seed-name");
                var priceLabel = card.Q<Label>(className: "shop-price");
                var condLabel  = card.Q<Label>(className: "shop-condition");
                var icon       = card.Q<VisualElement>(className: "shop-icon");
                var buyBtn     = card.Q<Button>(className: "shop-buy-btn");

                int owned = ConsumableManager.Instance.GetCount(c.type);
                if (nameLabel  != null) nameLabel.text  = $"{c.displayName} (x{owned})";
                if (priceLabel != null) priceLabel.text = $"{c.buyPrice} {c.currency}";
                if (condLabel  != null) condLabel.text  = c.description ?? "";
                if (icon != null && c.icon != null)
                    icon.style.backgroundImage = new StyleBackground(c.icon);

                if (buyBtn != null)
                {
                    buyBtn.SetEnabled(ConsumableManager.Instance.CanBuy(c));
                    buyBtn.text = $"Buy ({c.buyPrice} {c.currency})";
                    var consumable = c;
                    buyBtn.clicked += () => { if (ConsumableManager.Instance.Buy(consumable)) RefreshDisplay(); };
                }
                shopGrid.Add(card);
            }
        }
    }
}
