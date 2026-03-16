using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class VisitorUI : MonoBehaviour
    {
        // Modal root
        private VisualElement modal;
        private VisualElement backdrop;
        private Button closeBtn;

        // Header
        private VisualElement portrait;
        private Label nameLabel;
        private Label flavorLabel;

        // Gifter
        private VisualElement gifterSection;
        private VisualElement giftIcon;
        private Label giftName;
        private Label giftDesc;
        private Label giftClaimedText;
        private Button claimGiftBtn;

        // Merchant
        private VisualElement merchantSection;
        private VisualElement offerList;
        private VisualTreeAsset offerTemplate;
        private readonly List<VisualElement> offerCardPool = new();

        // Quester
        private VisualElement questerSection;
        private Label questLabel;
        private Label questValue;
        private Button acceptQuestBtn;
        private Button turninQuestBtn;

        public void Initialize(VisualElement root)
        {
            modal = root.Q("visitor-modal");
            backdrop = root.Q("visitor-modal-backdrop");
            closeBtn = root.Q<Button>("visitor-modal-close");

            portrait = root.Q("visitor-modal-portrait");
            nameLabel = root.Q<Label>("visitor-modal-name");
            flavorLabel = root.Q<Label>("visitor-modal-flavor");

            gifterSection = root.Q("visitor-gifter-section");
            giftIcon = root.Q("visitor-gift-icon");
            giftName = root.Q<Label>("visitor-gift-name");
            giftDesc = root.Q<Label>("visitor-gift-desc");
            giftClaimedText = root.Q<Label>("visitor-gift-claimed-text");
            claimGiftBtn = root.Q<Button>("visitor-claim-gift-btn");

            merchantSection = root.Q("visitor-merchant-section");
            offerList = root.Q("visitor-offer-list");
            offerTemplate = Resources.Load<VisualTreeAsset>("UI/Templates/MerchantOfferRow");

            questerSection = root.Q("visitor-quester-section");
            questLabel = root.Q<Label>("visitor-quest-label");
            questValue = root.Q<Label>("visitor-quest-value");
            acceptQuestBtn = root.Q<Button>("visitor-accept-quest-btn");
            turninQuestBtn = root.Q<Button>("visitor-turnin-quest-btn");

            backdrop?.RegisterCallback<ClickEvent>(_ => HideModal());
            closeBtn?.RegisterCallback<ClickEvent>(_ => HideModal());
            claimGiftBtn?.RegisterCallback<ClickEvent>(_ => OnClaimGift());
            acceptQuestBtn?.RegisterCallback<ClickEvent>(_ => OnAcceptQuest());
            turninQuestBtn?.RegisterCallback<ClickEvent>(_ => OnTurninQuest());

            HideModal();
        }

        public void ShowModal()
        {
            var visitor = SaveManager.Instance?.Data?.currentVisitor;
            if (visitor == null) return;

            // Header
            if (nameLabel != null) nameLabel.text = visitor.visitorName ?? "";
            if (flavorLabel != null)
                flavorLabel.text = visitor.dialogueLines != null && visitor.dialogueLines.Count > 0
                    ? visitor.dialogueLines[0]
                    : "";

            // Portrait
            if (portrait != null)
            {
                var tex = SpriteService.Instance?.GetTexture($"portraits/{visitor.portraitId}");
                if (tex != null)
                    portrait.style.backgroundImage = new StyleBackground(tex);
                else
                    portrait.style.backgroundImage = StyleKeyword.None;
            }

            // Hide all sections
            SetDisplay(gifterSection, false);
            SetDisplay(merchantSection, false);
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

            AudioManager.Instance?.PlaySFX("ui_panel_open");
            if (modal != null) modal.style.display = DisplayStyle.Flex;
        }

        public void HideModal()
        {
            if (modal != null && modal.style.display != DisplayStyle.None)
                AudioManager.Instance?.PlaySFX("ui_panel_close");
            if (modal != null) modal.style.display = DisplayStyle.None;
        }

        private void RefreshGifter(VisitorSave visitor)
        {
            if (visitor.giftClaimed)
            {
                SetDisplay(giftIcon?.parent, false);
                SetDisplay(giftClaimedText, true);
                if (giftClaimedText != null) giftClaimedText.text = "Gift received! Thank you.";
                SetDisplay(claimGiftBtn, false);
            }
            else
            {
                SetDisplay(giftIcon?.parent, true);
                SetDisplay(giftClaimedText, false);
                SetDisplay(claimGiftBtn, true);

                // Set icon class based on gift type
                if (giftIcon != null)
                {
                    giftIcon.RemoveFromClassList("visitor-gift-icon--seed");
                    giftIcon.RemoveFromClassList("visitor-gift-icon--item");
                    switch (visitor.giftType)
                    {
                        case "seed":
                            giftIcon.AddToClassList("visitor-gift-icon--seed");
                            break;
                        case "item":
                            giftIcon.AddToClassList("visitor-gift-icon--item");
                            break;
                    }
                }

                string itemName = visitor.giftType switch
                {
                    "water" => "Water",
                    "seed" => ConfigService.Instance?.GetItemDisplayName(visitor.giftName) ?? visitor.giftName ?? "Seeds",
                    "item" => ConfigService.Instance?.GetItemDisplayName(visitor.giftName) ?? visitor.giftName ?? "Item",
                    _ => "Mysterious Gift"
                };
                if (itemName == "Mysterious Gift")
                    Debug.LogWarning($"[VisitorUI] Unknown gift type '{visitor.giftType}' for visitor '{visitor.visitorId}' — showing fallback");
                if (visitor.giftAmount <= 0)
                    Debug.LogWarning($"[VisitorUI] Gift amount is {visitor.giftAmount} for visitor '{visitor.visitorId}' — likely a server data issue");

                if (giftName != null) giftName.text = $"{visitor.giftAmount}x {itemName}";
                if (giftDesc != null)
                    giftDesc.text = visitor.giftType switch
                    {
                        "water" => "Fills your vases",
                        "seed" => "Added to your seed pouch",
                        "item" => "Added to your inventory",
                        _ => "Something special"
                    };
            }
        }

        private void RefreshMerchantOffers(VisitorSave visitor)
        {
            var data = SaveManager.Instance.Data;
            if (visitor.offers == null)
            {
                foreach (var old in offerCardPool) old.RemoveFromHierarchy();
                offerCardPool.Clear();
                return;
            }

            var newCards = new List<VisualElement>();
            foreach (var offer in visitor.offers)
            {
                if (offerTemplate == null) break;
                var el = offerTemplate.CloneTree();
                var costsContainer = el.Q("offer-costs");
                var rewardText = el.Q<Label>("reward-text");
                var tradeBtn = el.Q<Button>("trade-btn");

                bool canAfford = VisitorManager.CanAffordOffer(offer, data.inventory);

                if (costsContainer != null)
                {
                    foreach (var cost in offer.costs)
                    {
                        string displayName = ConfigService.Instance.GetItemDisplayName(cost.itemKey);
                        var item = data.inventory.Find(i => i.itemKey == cost.itemKey);
                        int have = item != null ? item.count : 0;
                        bool enough = have >= cost.count;

                        var costLabel = new Label($"{cost.count}x {displayName} ({have})");
                        costLabel.AddToClassList("merchant-cost-item");
                        if (!enough) costLabel.AddToClassList("merchant-cost-item--insufficient");
                        costsContainer.Add(costLabel);
                    }
                }

                if (rewardText != null)
                    rewardText.text = $"{offer.rewardCount}x {ConfigService.Instance.GetItemDisplayName(offer.rewardItemKey)}";

                if (tradeBtn != null)
                {
                    tradeBtn.SetEnabled(canAfford);
                    var capturedOffer = offer;
                    tradeBtn.clickable = new Clickable(() =>
                    {
                        if (!VisitorManager.CanAffordOffer(capturedOffer, data.inventory)) return;
                        VisitorManager.ExecuteTrade(capturedOffer, data.inventory);
                        SaveManager.Instance.Save();
                        ShowModal(); // refresh
                    });
                }

                newCards.Add(el);
            }

            // Swap: add new cards first, then remove old ones to prevent height collapse
            foreach (var card in newCards) offerList.Add(card);
            foreach (var old in offerCardPool) old.RemoveFromHierarchy();
            offerCardPool.Clear();
            offerCardPool.AddRange(newCards);
        }

        private void RefreshQuester(VisitorSave visitor)
        {
            if (string.IsNullOrEmpty(visitor.requestItem) && !visitor.questFulfilled)
                Debug.LogWarning($"[VisitorUI] Quest visitor '{visitor.visitorId}' has no requestItem — likely a server data issue");

            if (visitor.isReturnVisit)
            {
                var data = SaveManager.Instance.Data;
                var item = data.inventory.Find(i => i.itemKey == visitor.requestItem);
                int have = item?.count ?? 0;

                if (questLabel != null) questLabel.text = "Requesting:";
                if (questValue != null) questValue.text = $"{visitor.requestCount}x {visitor.requestItem} (you have {have})";

                SetDisplay(acceptQuestBtn, false);
                bool canTurnIn = have >= visitor.requestCount;
                SetDisplay(turninQuestBtn, true);
                turninQuestBtn?.SetEnabled(canTurnIn);
            }
            else if (visitor.questFulfilled)
            {
                if (questLabel != null) questLabel.text = "";
                if (questValue != null) questValue.text = "Quest completed! Thank you.";
                SetDisplay(acceptQuestBtn, false);
                SetDisplay(turninQuestBtn, false);
            }
            else
            {
                if (questLabel != null) questLabel.text = "Request:";
                if (questValue != null) questValue.text = $"{visitor.requestCount}x {visitor.requestItem}";
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
            ShowModal(); // refresh to show claimed state
        }

        private void OnAcceptQuest()
        {
            var data = SaveManager.Instance?.Data;
            if (data?.currentVisitor == null) return;
            VisitorManager.Instance?.AcceptQuest(data.currentVisitor);
            ShowModal();
        }

        private void OnTurninQuest()
        {
            var data = SaveManager.Instance?.Data;
            if (data?.currentVisitor == null) return;

            var quest = data.activeQuests.Find(q => q.visitorId == data.currentVisitor.visitorId);
            if (quest != null)
            {
                VisitorManager.Instance?.CompleteQuest(quest);
            }
            ShowModal();
        }

        private static void SetDisplay(VisualElement el, bool visible)
        {
            if (el != null) el.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
