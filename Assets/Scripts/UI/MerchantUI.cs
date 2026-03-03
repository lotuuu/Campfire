using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class MerchantUI : MonoBehaviour
    {
        private VisualElement merchantList;
        private Label merchantFlavor;
        private VisualTreeAsset offerTemplate;

        private int activeMerchantIndex = -1;
        private MerchantData[] allMerchants;

        public void Initialize(VisualElement root)
        {
            merchantFlavor = root.Q<Label>("merchant-flavor");
            merchantList = root.Q("merchant-list");
            offerTemplate = Resources.Load<VisualTreeAsset>("UI/Templates/MerchantOfferRow");
            allMerchants = Resources.LoadAll<MerchantData>("Merchants");
        }

        public void ShowMerchant(int index)
        {
            activeMerchantIndex = index;
            Refresh();
        }

        public void Refresh()
        {
            if (merchantList == null) return;
            merchantList.Clear();

            var data = SaveManager.Instance?.Data;
            if (data == null || activeMerchantIndex < 0 || activeMerchantIndex >= data.merchants.Count)
                return;

            var merchant = data.merchants[activeMerchantIndex];

            // Load MerchantData for flavor text
            MerchantData merchantData = null;
            foreach (var md in allMerchants)
            {
                if (md.merchantName == merchant.merchantName) { merchantData = md; break; }
            }

            if (merchantFlavor != null)
                merchantFlavor.text = merchantData != null ? merchantData.flavorText : "";

            foreach (var offer in merchant.offers)
            {
                var el = offerTemplate.CloneTree();
                var costsContainer = el.Q("offer-costs");
                var rewardText = el.Q<Label>("reward-text");
                var tradeBtn = el.Q<Button>("trade-btn");

                bool canAfford = MerchantManager.CanAffordOffer(offer, data.items);

                // Populate costs
                foreach (var cost in offer.costs)
                {
                    string displayName = cost.itemName.Replace("_harvest", "");
                    var item = data.items.Find(i => i.itemName == cost.itemName);
                    int have = item != null ? item.count : 0;
                    bool enough = have >= cost.count;

                    var costLabel = new Label($"{cost.count}x {displayName} ({have})");
                    costLabel.AddToClassList("merchant-cost-item");
                    if (!enough) costLabel.AddToClassList("merchant-cost-item--insufficient");
                    costsContainer.Add(costLabel);
                }

                // Reward
                if (rewardText != null)
                    rewardText.text = $"{offer.rewardCount}x {offer.rewardSeedName}";

                // Trade button
                if (tradeBtn != null)
                {
                    tradeBtn.SetEnabled(canAfford);
                    var capturedOffer = offer;
                    tradeBtn.clicked += () =>
                    {
                        if (!MerchantManager.CanAffordOffer(capturedOffer, data.items)) return;
                        MerchantManager.ExecuteTrade(capturedOffer, data.items, data.seedInventory);
                        SaveManager.Instance.Save();
                        Refresh();
                    };
                }

                merchantList.Add(el);
            }
        }
    }
}
