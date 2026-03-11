# Visitor UI Redesign Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix the tiny dialogue popup, replace the ugly visitor overlay panel with a dedicated centered modal, and show proper gift/trade/quest information.

**Architecture:** Three changes: (1) CSS-only size bump for dialogue box, (2) new visitor modal UXML + USS that lives outside the shared overlay system, (3) rewire CampFireUI and VisitorUI to use the new modal instead of OpenOverlay().

**Tech Stack:** Unity UI Toolkit (UXML, USS, C#)

**Spec:** `docs/superpowers/specs/2026-03-11-visitor-ui-redesign-design.md`

---

## Chunk 1: Dialogue Box Size Fix + Visitor Modal Styling

### Task 1: Enlarge the dialogue box

**Files:**
- Modify: `Assets/UI/Styles/Dialogue.uss`

- [ ] **Step 1: Update dialogue box styles**

In `Assets/UI/Styles/Dialogue.uss`, make these changes:

`#dialogue-box`:
- `min-height`: 340px → 500px
- `padding`: add `padding-top: var(--spacing-lg)`

`#dialogue-speaker`:
- `font-size`: `var(--font-md)` → `var(--font-lg)`

`#dialogue-text`:
- `font-size`: `var(--font-sm)` → `var(--font-md)`
- `margin-bottom`: `var(--spacing-sm)` → `var(--spacing-md)`

`#dialogue-tap-hint`:
- `font-size`: `var(--font-xs)` → `var(--font-sm)`

- [ ] **Step 2: Verify in Unity**

Enter Play mode, trigger a visitor dialogue (use debug panel to spawn a visitor if needed). Confirm the dialogue box is significantly larger with readable text.

- [ ] **Step 3: Commit**

```bash
git add Assets/UI/Styles/Dialogue.uss
git commit -m "fix(ui): enlarge dialogue box for readability"
```

### Task 2: Create Visitor modal stylesheet

**Files:**
- Create: `Assets/UI/Styles/Visitor.uss`

- [ ] **Step 1: Create the Visitor.uss stylesheet**

Create `Assets/UI/Styles/Visitor.uss` with these styles:

```css
/* Visitor.uss — Dedicated visitor modal */

#visitor-modal {
    position: absolute;
    left: 0;
    right: 0;
    top: 0;
    bottom: 0;
    display: none;
    align-items: center;
    justify-content: center;
}

#visitor-modal-backdrop {
    position: absolute;
    left: 0;
    right: 0;
    top: 0;
    bottom: 0;
    background-color: rgba(0, 0, 0, 0.7);
}

#visitor-modal-card {
    width: 85%;
    max-height: 80%;
    background-color: var(--color-bg-panel);
    border-radius: var(--radius-lg);
    border-width: 1px;
    border-color: rgba(180, 120, 60, 0.3);
    flex-direction: column;
    overflow: hidden;
}

#visitor-modal-close {
    position: absolute;
    top: var(--spacing-sm);
    right: var(--spacing-sm);
    width: 80px;
    height: 80px;
    background-color: rgba(60, 40, 20, 0.6);
    background-image: url("project://database/Assets/ThirdParty/icons/lorc-cross-mark.png");
    -unity-background-image-tint-color: rgb(200, 120, 100);
    -unity-background-scale-mode: scale-to-fit;
    border-width: 1px;
    border-color: rgba(140, 100, 50, 0.3);
    border-radius: 40px;
    padding: 16px;
}

#visitor-modal-close:hover {
    background-color: rgba(80, 50, 25, 0.7);
    border-color: rgba(180, 120, 60, 0.5);
}

#visitor-modal-header {
    background-color: rgba(60, 40, 15, 0.5);
    padding: var(--spacing-lg) var(--spacing-lg) var(--spacing-md);
    align-items: center;
    border-bottom-width: 1px;
    border-bottom-color: rgba(180, 120, 60, 0.15);
}

#visitor-modal-portrait {
    width: 120px;
    height: 120px;
    border-radius: 60px;
    border-width: 2px;
    border-color: var(--color-border-accent);
    background-color: var(--color-bg-slot);
    -unity-background-scale-mode: scale-to-fit;
    margin-bottom: var(--spacing-sm);
}

#visitor-modal-name {
    font-size: var(--font-xl);
    color: rgb(230, 210, 180);
    -unity-font-style: bold;
    margin-bottom: var(--spacing-xs);
}

#visitor-modal-flavor {
    font-size: var(--font-sm);
    color: var(--color-text-dim);
    -unity-font-style: italic;
    -unity-text-align: upper-center;
    white-space: normal;
    padding: 0 var(--spacing-md);
}

#visitor-modal-content {
    padding: var(--spacing-lg);
    flex-grow: 1;
}

/* Gift card */
.visitor-gift-card {
    background-color: var(--color-bg-slot);
    border-width: 1px;
    border-color: var(--color-border);
    border-radius: var(--radius-sm);
    padding: var(--spacing-md);
    flex-direction: row;
    align-items: center;
    margin-bottom: var(--spacing-md);
}

.visitor-gift-icon {
    width: 64px;
    height: 64px;
    border-radius: var(--radius-sm);
    background-color: rgba(100, 180, 240, 0.15);
    -unity-background-scale-mode: scale-to-fit;
    margin-right: var(--spacing-md);
}

.visitor-gift-icon--seed {
    background-color: rgba(100, 200, 100, 0.15);
}

.visitor-gift-icon--item {
    background-color: rgba(200, 150, 80, 0.15);
}

.visitor-gift-name {
    font-size: var(--font-md);
    color: var(--color-text-bright);
    -unity-font-style: bold;
    margin-bottom: var(--spacing-xxs);
}

.visitor-gift-desc {
    font-size: var(--font-sm);
    color: var(--color-text-dim);
}

.visitor-gift-claimed {
    font-size: var(--font-md);
    color: var(--color-text-dim);
    -unity-text-align: upper-center;
    padding: var(--spacing-md);
}

/* Quest info */
.visitor-quest-info {
    background-color: var(--color-bg-slot);
    border-width: 1px;
    border-color: var(--color-border);
    border-radius: var(--radius-sm);
    padding: var(--spacing-md);
    margin-bottom: var(--spacing-md);
}

.visitor-quest-label {
    font-size: var(--font-sm);
    color: var(--color-text-dim);
    margin-bottom: var(--spacing-xxs);
}

.visitor-quest-value {
    font-size: var(--font-md);
    color: var(--color-text-bright);
    -unity-font-style: bold;
}

/* Shared action button for visitor modal */
.visitor-action-btn {
    padding: var(--spacing-sm) var(--spacing-md);
    background-color: var(--color-button-bg);
    border-radius: var(--radius-sm);
    border-width: 1px;
    border-color: var(--color-border-accent);
    color: var(--color-text-bright);
    font-size: var(--font-md);
    -unity-font-style: bold;
    margin-top: var(--spacing-sm);
}

.visitor-action-btn:hover {
    background-color: var(--color-button-bg-hover);
}

.visitor-action-btn:disabled {
    background-color: var(--color-button-bg-disabled);
    color: var(--color-text-dim);
    border-color: var(--color-border);
}
```

- [ ] **Step 2: Commit**

```bash
git add Assets/UI/Styles/Visitor.uss
git commit -m "feat(ui): add visitor modal stylesheet"
```

---

## Chunk 2: UXML Structure Changes

### Task 3: Add visitor modal to UXML and wire stylesheet

**Files:**
- Modify: `Assets/UI/Documents/CampFireRoot.uxml`

- [ ] **Step 1: Add Visitor.uss stylesheet reference**

In `CampFireRoot.uxml`, add this line after the Dialogue.uss import (line 17):

```xml
    <Style src="project://database/Assets/UI/Styles/Visitor.uss" />
```

- [ ] **Step 2: Remove visitor-panel from overlay-body**

Remove the entire `<!-- visitor-panel -->` block (lines 204-219) from inside the overlay body. This is the block:

```xml
                    <!-- visitor-panel -->
                    <ui:VisualElement name="visitor-panel">
                        <ui:Label name="visitor-flavor" class="merchant-flavor" />
                        <ui:VisualElement name="visitor-merchant-section">
                            <ui:VisualElement name="visitor-offer-list" />
                        </ui:VisualElement>
                        <ui:VisualElement name="visitor-gifter-section">
                            <ui:Label name="visitor-gift-text" class="gift-text" />
                            <ui:Button name="visitor-claim-gift-btn" text="Accept Gift" class="action-button" />
                        </ui:VisualElement>
                        <ui:VisualElement name="visitor-quester-section">
                            <ui:Label name="visitor-quest-text" class="quest-text" />
                            <ui:Button name="visitor-accept-quest-btn" text="Accept Quest" class="action-button" />
                            <ui:Button name="visitor-turnin-quest-btn" text="Turn In" class="action-button" />
                        </ui:VisualElement>
                    </ui:VisualElement>
```

- [ ] **Step 3: Add visitor-modal as sibling to dialogue-overlay**

Insert the new visitor modal block right after the `<!-- Dialogue overlay (VN-style) -->` closing tag (`</ui:VisualElement>` for dialogue-overlay) and before `<!-- Visit transition curtains -->`. Add this:

```xml
        <!-- Visitor modal (centered card) -->
        <ui:VisualElement name="visitor-modal">
            <ui:VisualElement name="visitor-modal-backdrop" />
            <ui:VisualElement name="visitor-modal-card">
                <ui:Button name="visitor-modal-close" />
                <ui:VisualElement name="visitor-modal-header">
                    <ui:VisualElement name="visitor-modal-portrait" />
                    <ui:Label name="visitor-modal-name" text="" />
                    <ui:Label name="visitor-modal-flavor" text="" />
                </ui:VisualElement>
                <ui:VisualElement name="visitor-modal-content">
                    <!-- Gifter section -->
                    <ui:VisualElement name="visitor-gifter-section">
                        <ui:VisualElement class="visitor-gift-card">
                            <ui:VisualElement name="visitor-gift-icon" class="visitor-gift-icon" />
                            <ui:VisualElement style="flex-grow: 1;">
                                <ui:Label name="visitor-gift-name" class="visitor-gift-name" />
                                <ui:Label name="visitor-gift-desc" class="visitor-gift-desc" />
                            </ui:VisualElement>
                        </ui:VisualElement>
                        <ui:Label name="visitor-gift-claimed-text" class="visitor-gift-claimed" />
                        <ui:Button name="visitor-claim-gift-btn" text="Accept Gift" class="visitor-action-btn" />
                    </ui:VisualElement>
                    <!-- Merchant section -->
                    <ui:VisualElement name="visitor-merchant-section">
                        <ui:VisualElement name="visitor-offer-list" />
                    </ui:VisualElement>
                    <!-- Quester section -->
                    <ui:VisualElement name="visitor-quester-section">
                        <ui:VisualElement name="visitor-quest-info-card" class="visitor-quest-info">
                            <ui:Label name="visitor-quest-label" class="visitor-quest-label" />
                            <ui:Label name="visitor-quest-value" class="visitor-quest-value" />
                        </ui:VisualElement>
                        <ui:Button name="visitor-accept-quest-btn" text="Accept Quest" class="visitor-action-btn" />
                        <ui:Button name="visitor-turnin-quest-btn" text="Turn In" class="visitor-action-btn" />
                    </ui:VisualElement>
                </ui:VisualElement>
            </ui:VisualElement>
        </ui:VisualElement>
```

- [ ] **Step 4: Commit**

```bash
git add Assets/UI/Documents/CampFireRoot.uxml
git commit -m "feat(ui): add visitor modal UXML, remove old visitor panel from overlay"
```

---

## Chunk 3: C# Wiring

### Task 4: Rewrite VisitorUI to use the modal

**Files:**
- Modify: `Assets/Scripts/UI/VisitorUI.cs`

- [ ] **Step 1: Rewrite VisitorUI.cs**

Replace the entire contents of `VisitorUI.cs` with:

```csharp
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

        // Quester
        private VisualElement questerSection;
        private VisualElement questInfoCard;
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
            questInfoCard = root.Q("visitor-quest-info-card");
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
                    "seed" => visitor.giftName ?? "Seeds",
                    "item" => visitor.giftName ?? "Item",
                    _ => "Mysterious Gift"
                };

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

                if (rewardText != null)
                    rewardText.text = $"{offer.rewardCount}x {PlotManager.GetSeedDisplayName(offer.rewardSeedName)}";

                if (tradeBtn != null)
                {
                    tradeBtn.SetEnabled(canAfford);
                    var capturedOffer = offer;
                    tradeBtn.clicked += () =>
                    {
                        if (!VisitorManager.CanAffordOffer(capturedOffer, data.items)) return;
                        VisitorManager.ExecuteTrade(capturedOffer, data.items, data.seedInventory);
                        SaveManager.Instance.Save();
                        ShowModal(); // refresh
                    };
                }

                offerList.Add(el);
            }
        }

        private void RefreshQuester(VisitorSave visitor)
        {
            if (visitor.isReturnVisit)
            {
                var data = SaveManager.Instance.Data;
                var item = data.items.Find(i => i.itemName == visitor.requestItem);
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
```

- [ ] **Step 2: Commit**

```bash
git add Assets/Scripts/UI/VisitorUI.cs
git commit -m "feat(ui): rewrite VisitorUI to use dedicated modal"
```

### Task 5: Update CampFireUI to use visitor modal

**Files:**
- Modify: `Assets/Scripts/UI/CampFireUI.cs`

- [ ] **Step 1: Remove visitorPanel field and references**

In `CampFireUI.cs`:

1. Remove the field declaration (line ~38):
```csharp
        private VisualElement visitorPanel;
```

2. Remove the Q lookup (line ~101):
```csharp
            visitorPanel = root.Q("visitor-panel");
```

3. Remove the visitorPanel hide in `HideAllPanels()` (line ~424):
```csharp
            if (visitorPanel != null) visitorPanel.style.display = DisplayStyle.None;
```

- [ ] **Step 2: Update visitor tap handler to use ShowModal**

Replace the visitor tap handler block (lines ~180-205). Change from:

```csharp
            // Wire Visitor tile tap
            if (campsiteView != null)
                campsiteView.OnVisitorTapped += () =>
                {
                    var data = SaveManager.Instance?.Data;
                    if (data?.currentVisitor == null) return;
                    var visitor = data.currentVisitor;

                    if (!visitor.dialogueSeen && visitor.dialogueLines != null && visitor.dialogueLines.Count > 0 && dialogueUI != null)
                    {
                        Texture2D portrait = SpriteService.Instance?.GetTexture($"portraits/{visitor.portraitId}");

                        dialogueUI.Show(visitor.visitorName, visitor.dialogueLines, () =>
                        {
                            visitor.dialogueSeen = true;
                            SaveManager.Instance.Save();
                            visitorUI?.ShowVisitor();
                            OpenOverlay(visitor.visitorName, visitorPanel);
                        }, portrait);
                    }
                    else
                    {
                        visitorUI?.ShowVisitor();
                        OpenOverlay(visitor.visitorName, visitorPanel);
                    }
                };
```

To:

```csharp
            // Wire Visitor tile tap
            if (campsiteView != null)
                campsiteView.OnVisitorTapped += () =>
                {
                    var data = SaveManager.Instance?.Data;
                    if (data?.currentVisitor == null) return;
                    var visitor = data.currentVisitor;

                    if (!visitor.dialogueSeen && visitor.dialogueLines != null && visitor.dialogueLines.Count > 0 && dialogueUI != null)
                    {
                        Texture2D portrait = SpriteService.Instance?.GetTexture($"portraits/{visitor.portraitId}");

                        dialogueUI.Show(visitor.visitorName, visitor.dialogueLines, () =>
                        {
                            visitor.dialogueSeen = true;
                            SaveManager.Instance.Save();
                            visitorUI?.ShowModal();
                        }, portrait);
                    }
                    else
                    {
                        visitorUI?.ShowModal();
                    }
                };
```

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/UI/CampFireUI.cs
git commit -m "feat(ui): wire visitor tap to new modal instead of overlay"
```

### Task 6: Verify in Unity and fix issues

- [ ] **Step 1: Check console for compilation errors**

Use `read_console` to check for any compilation errors after the script changes.

- [ ] **Step 2: Enter Play mode and test all visitor types**

Use the debug panel to spawn each visitor type (Gifter, Merchant, Quester). Verify:
- Dialogue box is larger and readable
- After dialogue, centered modal appears (not the old slide-up overlay)
- Gifter: shows gift card with item name, amount, description. Accept button works. Shows "Gift received!" after claiming.
- Merchant: shows trade offers with costs and reward. Trade button works.
- Quester: shows quest request info. Accept/Turn In buttons work.
- Close button and backdrop tap both dismiss the modal.
- No unwanted scrolling.

- [ ] **Step 3: Fix any visual issues found during testing**

Adjust padding, spacing, or sizing in `Visitor.uss` as needed based on how it looks on screen.

- [ ] **Step 4: Final commit if any fixes were made**

```bash
git add -A
git commit -m "fix(ui): polish visitor modal after testing"
```
