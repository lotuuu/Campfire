# Greenhouse Sell Feature Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Let players tap a greenhouse plant card to select it, showing a sell bar with dust rate and sell price, then tap Sell to exchange it for Dewdrops.

**Architecture:** `GreenhouseManager.SellPlant(int index)` already exists and handles all backend logic. This is purely UI: add selection state to `GreenhouseUI`, add a sell bar element to the greenhouse page UXML, and wire click handlers on filled plant slots.

**Tech Stack:** Unity 6, UI Toolkit (UXML/USS), `GreenhouseUI` MonoBehaviour.

---

## Key file paths (read before coding)

- `Assets/Scripts/UI/GreenhouseUI.cs` — controller to modify
- `Assets/UI/Documents/GardenRoot.uxml` — greenhouse page structure (lines 88-96)
- `Assets/UI/Styles/Greenhouse.uss` — styles to extend
- `Assets/Resources/UI/Templates/PlantSlot.uxml` — slot template (has `.plant-slot`, `.plant-swatch`, `.plant-name`)
- `Assets/Scripts/Managers/GreenhouseManager.cs` — `SellPlant(int index)`, `Plants` list, `GetTotalDustPerHour()`
- `Assets/Scripts/Data/CurrencyConfig.cs` — `GetSellValue(baseSellPrice, qualityTier)`, `GetDustPerHourForPlant(rarity, qualityTier)`, `GetQualityLabel(tier)` (static)
- `Assets/Scripts/Data/GreenhousePlant` (defined at bottom of GreenhouseManager.cs) — fields: `seedName`, `variantName`, `rarity`, `qualityTier`, `primaryColor`

---

## Task 1: Add sell bar to GardenRoot.uxml + styles to Greenhouse.uss

**Files:**
- Modify: `Assets/UI/Documents/GardenRoot.uxml`
- Modify: `Assets/UI/Styles/Greenhouse.uss`

**Step 1: Add sell bar element to UXML**

In `GardenRoot.uxml`, find the `<!-- Page 3: Greenhouse -->` block (currently ends after `<ui:ScrollView name="greenhouse-grid" ...>`). Add the sell bar **after** the `greenhouse-grid` ScrollView, before the closing `</ui:VisualElement>`:

```xml
        <ui:VisualElement name="greenhouse-sell-bar" style="display: none;">
            <ui:Label name="greenhouse-sell-name" text="" />
            <ui:Label name="greenhouse-sell-dust" text="" />
            <ui:Button name="greenhouse-sell-btn" class="btn" text="Sell" />
        </ui:VisualElement>
```

The greenhouse page block should end up as:
```xml
    <!-- Page 3: Greenhouse -->
    <ui:VisualElement name="greenhouse-page" class="page-content" style="display: none;">
        <ui:Label text="Greenhouse" class="panel-header" />
        <ui:VisualElement name="greenhouse-header">
            <ui:Label name="greenhouse-dust-rate" text="+0 Aura Dust/hr" />
            <ui:Label name="greenhouse-slots-text" text="0 / 0" />
        </ui:VisualElement>
        <ui:ScrollView name="greenhouse-grid" class="scroll-view" />
        <ui:VisualElement name="greenhouse-sell-bar" style="display: none;">
            <ui:Label name="greenhouse-sell-name" text="" />
            <ui:Label name="greenhouse-sell-dust" text="" />
            <ui:Button name="greenhouse-sell-btn" class="btn" text="Sell" />
        </ui:VisualElement>
    </ui:VisualElement>
```

**Step 2: Add styles to Greenhouse.uss**

Append to the end of `Assets/UI/Styles/Greenhouse.uss`:

```css
/* Selection highlight */
#greenhouse-page .plant-slot--selected {
    border-color: var(--color-border-accent);
    border-width: 2px;
    background-color: var(--color-bg-slot-hover);
}

/* Sell bar */
#greenhouse-sell-bar {
    background-color: var(--color-bg-panel);
    border-color: var(--color-border);
    border-width: 1px;
    border-radius: var(--radius-md);
    padding: var(--spacing-sm);
    margin-top: var(--spacing-sm);
    flex-direction: column;
    align-items: stretch;
}

#greenhouse-sell-name {
    color: var(--color-text-bright);
    font-size: var(--font-md);
    -unity-font-style: bold;
    margin-bottom: var(--spacing-xs);
}

#greenhouse-sell-dust {
    color: var(--color-text-accent);
    font-size: var(--font-sm);
    margin-bottom: var(--spacing-xs);
}
```

**Step 3: Verify in Unity — no UXML parse errors in Console.**

**Step 4: Commit**

```bash
git add Assets/UI/Documents/GardenRoot.uxml Assets/UI/Styles/Greenhouse.uss
git commit -m "$(cat <<'EOF'
feat: add greenhouse sell bar UXML element and selection styles

Generated with [Claude Code](https://claude.ai/code)
via [Happy](https://happy.engineering)

Co-Authored-By: Claude <noreply@anthropic.com>
Co-Authored-By: Happy <yesreply@happy.engineering>
EOF
)"
```

---

## Task 2: Rewrite GreenhouseUI.cs with selection state and sell bar

**Files:**
- Modify: `Assets/Scripts/UI/GreenhouseUI.cs`

**Step 1: Replace the entire file**

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class GreenhouseUI : MonoBehaviour
    {
        private VisualTreeAsset plantSlotTemplate;

        private ScrollView plantGrid;
        private Label dustRateText;
        private Label slotsText;

        // Sell bar
        private VisualElement sellBar;
        private Label sellNameLabel;
        private Label sellDustLabel;
        private Button sellButton;

        // Selection state
        private int selectedIndex = -1;
        private readonly List<VisualElement> filledSlotRoots = new();

        public void Initialize(VisualElement root)
        {
            plantSlotTemplate = Resources.Load<VisualTreeAsset>("UI/Templates/PlantSlot");

            plantGrid = root.Q<ScrollView>("greenhouse-grid");
            dustRateText = root.Q<Label>("greenhouse-dust-rate");
            slotsText = root.Q<Label>("greenhouse-slots-text");

            sellBar = root.Q<VisualElement>("greenhouse-sell-bar");
            sellNameLabel = root.Q<Label>("greenhouse-sell-name");
            sellDustLabel = root.Q<Label>("greenhouse-sell-dust");
            sellButton = root.Q<Button>("greenhouse-sell-btn");

            if (sellButton != null)
                sellButton.clicked += OnSell;
        }

        public void Show()
        {
            RefreshDisplay();
        }

        public void RefreshDisplay()
        {
            plantGrid.Clear();
            filledSlotRoots.Clear();
            selectedIndex = -1;
            if (sellBar != null) sellBar.style.display = DisplayStyle.None;

            var gm = GreenhouseManager.Instance;
            slotsText.text = $"{gm.Plants.Count} / {gm.MaxSlots}";
            dustRateText.text = $"+{gm.GetTotalDustPerHour():F1} Aura Dust/hr";

            for (int i = 0; i < gm.Plants.Count; i++)
            {
                var plant = gm.Plants[i];
                var slot = plantSlotTemplate.CloneTree();
                var slotRoot = slot.Q<VisualElement>(className: "plant-slot");
                var nameLabel = slot.Q<Label>(className: "plant-name");
                var swatch = slot.Q<VisualElement>(className: "plant-swatch");

                if (nameLabel != null) nameLabel.text = plant.variantName;
                if (swatch != null) swatch.style.backgroundColor = plant.primaryColor;

                filledSlotRoots.Add(slotRoot);

                int capturedIndex = i;
                slot.RegisterCallback<ClickEvent>(_ => OnSlotClicked(capturedIndex));

                plantGrid.Add(slot);
            }

            for (int i = gm.Plants.Count; i < gm.MaxSlots; i++)
            {
                var slot = plantSlotTemplate.CloneTree();
                var nameLabel = slot.Q<Label>(className: "plant-name");
                var swatch = slot.Q<VisualElement>(className: "plant-swatch");
                if (nameLabel != null) nameLabel.text = "Empty";
                if (swatch != null) swatch.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f, 0.3f);
                plantGrid.Add(slot);
            }
        }

        private void OnSlotClicked(int index)
        {
            if (selectedIndex == index)
            {
                ClearSelection();
                return;
            }

            if (selectedIndex >= 0 && selectedIndex < filledSlotRoots.Count)
                filledSlotRoots[selectedIndex]?.RemoveFromClassList("plant-slot--selected");

            selectedIndex = index;
            filledSlotRoots[index]?.AddToClassList("plant-slot--selected");
            UpdateSellBar(index);
        }

        private void ClearSelection()
        {
            if (selectedIndex >= 0 && selectedIndex < filledSlotRoots.Count)
                filledSlotRoots[selectedIndex]?.RemoveFromClassList("plant-slot--selected");
            selectedIndex = -1;
            if (sellBar != null) sellBar.style.display = DisplayStyle.None;
        }

        private void UpdateSellBar(int index)
        {
            var gm = GreenhouseManager.Instance;
            var config = CurrencyManager.Instance.Config;
            var plant = gm.Plants[index];

            var seed = SeedRegistry.Instance.GetSeed(plant.seedName);
            int baseSell = seed != null ? seed.baseSellPrice : 100;
            int sellValue = config.GetSellValue(baseSell, plant.qualityTier);
            float dustRate = config.GetDustPerHourForPlant(plant.rarity, plant.qualityTier);
            string qualityLabel = CurrencyConfig.GetQualityLabel(plant.qualityTier);

            if (sellNameLabel != null) sellNameLabel.text = $"{plant.variantName} · {qualityLabel}";
            if (sellDustLabel != null) sellDustLabel.text = $"+{dustRate:F1} Dust/hr";
            if (sellButton != null) sellButton.text = $"Sell for {sellValue} Dew";
            if (sellBar != null) sellBar.style.display = DisplayStyle.Flex;
        }

        private void OnSell()
        {
            if (selectedIndex < 0) return;
            GreenhouseManager.Instance.SellPlant(selectedIndex);
            RefreshDisplay();
        }
    }
}
```

**Step 2: Verify compilation — no errors in Console.**

**Step 3: Commit**

```bash
git add Assets/Scripts/UI/GreenhouseUI.cs
git commit -m "$(cat <<'EOF'
feat: add plant selection and sell bar to GreenhouseUI

Tap filled slot to select; sell bar shows variant name, quality,
dust rate, and sell price. Sell clears selection and refreshes.

Generated with [Claude Code](https://claude.ai/code)
via [Happy](https://happy.engineering)

Co-Authored-By: Claude <noreply@anthropic.com>
Co-Authored-By: Happy <yesreply@happy.engineering>
EOF
)"
```

---

## Task 3: Run tests and smoke test

**Step 1: Run EditMode tests**

Via Unity MCP `run_tests` tool with `mode: "EditMode"`, or in Unity: Window > General > Test Runner > EditMode > Run All.

Expected: all 18 tests pass (no regressions — no logic changed, only UI).

**Step 2: Play mode smoke test checklist**

- [ ] Navigate to Greenhouse tab
- [ ] Tapping an **empty** slot: nothing happens, sell bar stays hidden
- [ ] Tapping a **filled** slot: card highlights with accent border, sell bar appears showing variant name · quality, dust rate, and sell price
- [ ] Tapping the **same** filled slot again: deselects, sell bar hides
- [ ] Tapping a **different** filled slot: previous highlight clears, new slot highlights, sell bar updates
- [ ] Tapping "Sell for X Dew": plant removed, Dewdrops increase by correct amount, selection cleared, display refreshes
- [ ] After selling, total dust rate in header decreases correctly
- [ ] Sell bar stays hidden when navigating away and back (Show() resets state)

**Step 3: Push**

```bash
git push origin main
```
