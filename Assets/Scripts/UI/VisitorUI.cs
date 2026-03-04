using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class VisitorUI : MonoBehaviour
    {
        private VisualElement merchantSection, gifterSection, questerSection;
        private VisualElement offerList;
        private Label flavorLabel, giftText, questText;
        private Button claimGiftBtn, acceptQuestBtn, turninQuestBtn;
        private VisualTreeAsset offerTemplate;

        public void Initialize(VisualElement root)
        {
            flavorLabel = root.Q<Label>("visitor-flavor");
            merchantSection = root.Q("visitor-merchant-section");
            gifterSection = root.Q("visitor-gifter-section");
            questerSection = root.Q("visitor-quester-section");
            offerList = root.Q("visitor-offer-list");
            giftText = root.Q<Label>("visitor-gift-text");
            questText = root.Q<Label>("visitor-quest-text");
            claimGiftBtn = root.Q<Button>("visitor-claim-gift-btn");
            acceptQuestBtn = root.Q<Button>("visitor-accept-quest-btn");
            turninQuestBtn = root.Q<Button>("visitor-turnin-quest-btn");
            offerTemplate = Resources.Load<VisualTreeAsset>("UI/Templates/MerchantOfferRow");

            claimGiftBtn?.RegisterCallback<ClickEvent>(evt => OnClaimGift());
            acceptQuestBtn?.RegisterCallback<ClickEvent>(evt => OnAcceptQuest());
            turninQuestBtn?.RegisterCallback<ClickEvent>(evt => OnTurninQuest());
        }

        public void ShowVisitor()
        {
            var visitor = SaveManager.Instance?.Data?.currentVisitor;
            if (visitor == null) return;

            // Set flavor text from dialogue (first line or empty)
            if (flavorLabel != null)
                flavorLabel.text = visitor.dialogueLines != null && visitor.dialogueLines.Count > 0
                    ? visitor.dialogueLines[0]
                    : "";

            // Hide all sections first
            SetDisplay(merchantSection, false);
            SetDisplay(gifterSection, false);
            SetDisplay(questerSection, false);

            switch (visitor.type)
            {
                case VisitorType.Merchant:
                    SetDisplay(merchantSection, true);
                    RefreshMerchantOffers(visitor);
                    break;
                case VisitorType.Gifter:
                    SetDisplay(gifterSection, true);
                    RefreshGifter(visitor);
                    break;
                case VisitorType.Quester:
                    SetDisplay(questerSection, true);
                    RefreshQuester(visitor);
                    break;
            }
        }

        private void RefreshMerchantOffers(VisitorSave visitor)
        {
            offerList?.Clear();
            var data = SaveManager.Instance.Data;

            if (visitor.offers == null) return;

            foreach (var offer in visitor.offers)
            {
                if (offerTemplate == null) break;
                var el = offerTemplate.CloneTree();
                var costsContainer = el.Q("offer-costs");
                var rewardText = el.Q<Label>("reward-text");
                var tradeBtn = el.Q<Button>("trade-btn");

                bool canAfford = VisitorManager.CanAffordOffer(offer, data.items);

                // Populate costs
                if (costsContainer != null)
                {
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
                }

                // Reward
                if (rewardText != null)
                    rewardText.text = $"{offer.rewardCount}x {PlotManager.GetSeedDisplayName(offer.rewardSeedName)}";

                // Trade button
                if (tradeBtn != null)
                {
                    tradeBtn.SetEnabled(canAfford);
                    var capturedOffer = offer;
                    tradeBtn.clicked += () =>
                    {
                        if (!VisitorManager.CanAffordOffer(capturedOffer, data.items)) return;
                        VisitorManager.ExecuteTrade(capturedOffer, data.items, data.seedInventory);
                        SaveManager.Instance.Save();
                        ShowVisitor(); // refresh
                    };
                }

                offerList.Add(el);
            }
        }

        private void RefreshGifter(VisitorSave visitor)
        {
            if (visitor.giftClaimed)
            {
                if (giftText != null) giftText.text = "You already received this gift.";
                SetDisplay(claimGiftBtn, false);
            }
            else
            {
                string desc = visitor.giftType switch
                {
                    "water" => $"{visitor.giftAmount} Water",
                    "seed" => $"{visitor.giftAmount}x {visitor.giftName} seeds",
                    "item" => $"{visitor.giftAmount}x {visitor.giftName}",
                    _ => "A mysterious gift"
                };
                if (giftText != null) giftText.text = $"Gift: {desc}";
                SetDisplay(claimGiftBtn, true);
            }
        }

        private void RefreshQuester(VisitorSave visitor)
        {
            if (visitor.isReturnVisit)
            {
                // Return visit - show turn-in UI
                var data = SaveManager.Instance.Data;
                var item = data.items.Find(i => i.itemName == visitor.requestItem);
                int have = item?.count ?? 0;

                if (questText != null)
                    questText.text = $"Requesting: {visitor.requestCount}x {visitor.requestItem}\nYou have: {have}";

                SetDisplay(acceptQuestBtn, false);
                bool canTurnIn = have >= visitor.requestCount;
                SetDisplay(turninQuestBtn, true);
                turninQuestBtn?.SetEnabled(canTurnIn);
            }
            else if (visitor.questFulfilled)
            {
                if (questText != null) questText.text = "Quest completed! Thank you.";
                SetDisplay(acceptQuestBtn, false);
                SetDisplay(turninQuestBtn, false);
            }
            else
            {
                // Initial visit - show quest details and accept button
                if (questText != null)
                    questText.text = $"Request: {visitor.requestCount}x {visitor.requestItem}";
                SetDisplay(acceptQuestBtn, true);
                SetDisplay(turninQuestBtn, false);
            }
        }

        private void OnClaimGift()
        {
            var data = SaveManager.Instance?.Data;
            if (data?.currentVisitor == null) return;
            VisitorManager.ApplyGift(data.currentVisitor, data);
            SaveManager.Instance.Save();
            ShowVisitor(); // refresh
        }

        private void OnAcceptQuest()
        {
            var data = SaveManager.Instance?.Data;
            if (data?.currentVisitor == null) return;
            VisitorManager.Instance?.AcceptQuest(data.currentVisitor);
            ShowVisitor();
        }

        private void OnTurninQuest()
        {
            var data = SaveManager.Instance?.Data;
            if (data?.currentVisitor == null) return;

            // Find matching active quest for this visitor
            var quest = data.activeQuests.Find(q => q.visitorId == data.currentVisitor.visitorId);
            if (quest != null)
            {
                VisitorManager.Instance?.CompleteQuest(quest);
            }
            ShowVisitor();
        }

        private static void SetDisplay(VisualElement el, bool visible)
        {
            if (el != null) el.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
