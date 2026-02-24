# Construction Tab Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add a Construction tab (replacing the locked placeholder) where players spend Dewdrops to unlock new environments and expand slots; simultaneously reroute the seed shop to cost AuraDust instead of Dewdrops.

**Architecture:** Fully dynamic card-based UI using `VisualTreeAsset.CloneTree()` per `EnvironmentData` asset — same pattern as `SeedShopUI`. `ConstructionUI` is a new MonoBehaviour wired into `HortusUI` at page index 4. Economy changes touch `SeedShopManager`, `GreenhouseManager`, and `CurrencyConfig` only.

**Tech Stack:** Unity 6, UI Toolkit (UXML/USS), C# MonoBehaviour singletons, `EnvironmentManager` (already has `Unlock`/`UnlockSlot` methods).

---

## Key file paths (read these before coding)

- `Assets/Scripts/Data/CurrencyConfig.cs` — ScriptableObject with currency tuning
- `Assets/Resources/Config/CurrencyConfig.asset` — serialized YAML, must be edited directly
- `Assets/Scripts/Managers/GreenhouseManager.cs` — `ExpandSlots()` currently spends SunShards
- `Assets/Scripts/UI/GreenhouseUI.cs` — has expand button wiring to remove
- `Assets/Scripts/Managers/SeedShopManager.cs` — `CanBuy()` / `BuySeed()` currently use Dewdrops
- `Assets/Scripts/UI/SeedShopUI.cs` — price label says "Dew", needs updating
- `Assets/Scripts/Managers/EnvironmentManager.cs` — `Unlock(envIdx)`, `UnlockSlot(envIdx)`, `IsUnlocked(envIdx)`, `GetActiveSlotCount(envIdx)`, `CanUnlockSlot(envIdx)`
- `Assets/Scripts/Data/EnvironmentData.cs` — `environmentName`, `slotCount`, `maxSlotCount`, `unlockCostDewdrops`, `slotUnlockCostDewdrops`
- `Assets/UI/Documents/GardenRoot.uxml` — page names, tab names, greenhouse-expand-button
- `Assets/Scripts/UI/HortusUI.cs` — pageNames array, OnPageChanged switch, Initialize block
- `Assets/Scripts/UI/BottomNavUI.cs` — TabNames array, lockedTabIndex guard
- `Assets/UI/Styles/Variables.uss` — CSS custom properties to reuse in new stylesheet

---

## Task 1: Rename CurrencyConfig greenhouse field

**Files:**
- Modify: `Assets/Scripts/Data/CurrencyConfig.cs`
- Modify: `Assets/Resources/Config/CurrencyConfig.asset`

**Step 1: Update CurrencyConfig.cs**

In `CurrencyConfig.cs`, under `[Header("Greenhouse")]`, replace:
```csharp
public int slotCostSunShards = 50;
```
with:
```csharp
public int greenhouseExpandCostDewdrops = 300;
```

**Step 2: Update the serialized asset YAML**

In `Assets/Resources/Config/CurrencyConfig.asset`, replace:
```yaml
  slotCostSunShards: 50
```
with:
```yaml
  greenhouseExpandCostDewdrops: 300
```

(The field name in YAML must exactly match the C# field name.)

**Step 3: Verify compilation**

Open Unity (or wait for auto-compile). Check Console — no errors.

**Step 4: Commit**

```bash
git add Assets/Scripts/Data/CurrencyConfig.cs Assets/Resources/Config/CurrencyConfig.asset
git commit -m "refactor: rename slotCostSunShards to greenhouseExpandCostDewdrops (300 Dewdrops)"
```

---

## Task 2: GreenhouseManager — spend Dewdrops not SunShards

**Files:**
- Modify: `Assets/Scripts/Managers/GreenhouseManager.cs`

**Step 1: Update ExpandSlots()**

In `GreenhouseManager.cs`, replace the entire `ExpandSlots()` method body:

```csharp
public bool ExpandSlots()
{
    var config = CurrencyManager.Instance.Config;
    if (!CurrencyManager.Instance.Spend(CurrencyType.Dewdrops, config.greenhouseExpandCostDewdrops))
        return false;
    SaveManager.Instance.Data.greenhouseSlots++;
    SaveManager.Instance.Save();
    OnGreenhouseChanged?.Invoke();
    return true;
}
```

**Step 2: Verify compilation — no errors in Console.**

**Step 3: Commit**

```bash
git add Assets/Scripts/Managers/GreenhouseManager.cs
git commit -m "feat: greenhouse expansion now costs Dewdrops instead of SunShards"
```

---

## Task 3: SeedShopManager + SeedShopUI — spend AuraDust not Dewdrops

**Files:**
- Modify: `Assets/Scripts/Managers/SeedShopManager.cs`
- Modify: `Assets/Scripts/UI/SeedShopUI.cs`

**Step 1: Update SeedShopManager**

Replace both `CurrencyType.Dewdrops` references in `SeedShopManager.cs`:

```csharp
public bool CanBuy(string seedName)
{
    var seed = SeedRegistry.Instance.GetSeed(seedName);
    if (seed == null) return false;
    return CurrencyManager.Instance.CanAfford(CurrencyType.AuraDust, seed.buyPrice);
}

public bool BuySeed(string seedName)
{
    var seed = SeedRegistry.Instance.GetSeed(seedName);
    if (seed == null) return false;
    if (!CurrencyManager.Instance.Spend(CurrencyType.AuraDust, seed.buyPrice))
        return false;

    SeedRegistry.Instance.AddSeed(seedName);
    OnSeedPurchased?.Invoke(seedName);
    return true;
}
```

**Step 2: Update SeedShopUI price labels**

In `SeedShopUI.cs`, `RefreshDisplay()`, replace both label strings:
```csharp
if (priceLabel != null) priceLabel.text = $"{seed.buyPrice} Dust";
// ...
buyBtn.text = $"Buy ({seed.buyPrice} Dust)";
```

**Step 3: Verify compilation — no errors.**

**Step 4: Commit**

```bash
git add Assets/Scripts/Managers/SeedShopManager.cs Assets/Scripts/UI/SeedShopUI.cs
git commit -m "feat: seed shop now costs AuraDust instead of Dewdrops"
```

---

## Task 4: Remove expand button from GreenhouseUI and UXML

**Files:**
- Modify: `Assets/Scripts/UI/GreenhouseUI.cs`
- Modify: `Assets/UI/Documents/GardenRoot.uxml`

**Step 1: Clean GreenhouseUI.cs**

Remove the `expandButton` field, the `expandButton` lines in `Initialize()`, the `expandButton.SetEnabled(...)` call in `RefreshDisplay()`, and the `OnExpand()` method entirely.

Result — `GreenhouseUI.cs` should look like:

```csharp
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

        public void Initialize(VisualElement root)
        {
            plantSlotTemplate = Resources.Load<VisualTreeAsset>("UI/Templates/PlantSlot");

            plantGrid = root.Q<ScrollView>("greenhouse-grid");
            dustRateText = root.Q<Label>("greenhouse-dust-rate");
            slotsText = root.Q<Label>("greenhouse-slots-text");
        }

        public void Show()
        {
            RefreshDisplay();
        }

        public void RefreshDisplay()
        {
            plantGrid.Clear();

            var gm = GreenhouseManager.Instance;
            slotsText.text = $"{gm.Plants.Count} / {gm.MaxSlots}";
            dustRateText.text = $"+{gm.GetTotalDustPerHour():F1} Aura Dust/hr";

            foreach (var plant in gm.Plants)
            {
                var slot = plantSlotTemplate.CloneTree();
                var nameLabel = slot.Q<Label>(className: "plant-name");
                var swatch = slot.Q<VisualElement>(className: "plant-swatch");
                if (nameLabel != null) nameLabel.text = plant.variantName;
                if (swatch != null) swatch.style.backgroundColor = plant.primaryColor;
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
    }
}
```

**Step 2: Remove expand button from GardenRoot.uxml**

In the `<!-- Page 3: Greenhouse -->` section, delete this line:
```xml
<ui:Button name="greenhouse-expand-button" text="Expand Slots" class="btn" />
```

**Step 3: Verify compilation — no errors.**

**Step 4: Commit**

```bash
git add Assets/Scripts/UI/GreenhouseUI.cs Assets/UI/Documents/GardenRoot.uxml
git commit -m "refactor: remove greenhouse expand button (moved to Construction tab)"
```

---

## Task 5: Rename locked → construction in GardenRoot.uxml

**Files:**
- Modify: `Assets/UI/Documents/GardenRoot.uxml`

**Step 1: Rename the tab button**

Replace:
```xml
<ui:Button name="tab-locked" class="nav-tab nav-tab-disabled">
```
with:
```xml
<ui:Button name="tab-construction" class="nav-tab">
```

(Removing `nav-tab-disabled` enables the tab.)

**Step 2: Replace the locked page with the construction page**

Replace the entire `<!-- Page 4: Locked -->` block:
```xml
<!-- Page 4: Locked -->
<ui:VisualElement name="locked-page" class="page-content" style="display: none;">
    <ui:VisualElement style="flex-grow: 1; justify-content: center; align-items: center;">
        <ui:Label text="Coming Soon" class="label-dim label-coming-soon" />
    </ui:VisualElement>
</ui:VisualElement>
```

with:
```xml
<!-- Page 4: Construction -->
<ui:VisualElement name="construction-page" class="page-content" style="display: none;">
    <ui:Label text="Construction" class="panel-header" />
    <ui:ScrollView name="construction-scroll" class="scroll-view" />
</ui:VisualElement>
```

**Step 3: Add Construction stylesheet reference**

After the last `<Style .../>` line near the top of the file (after `Hearth.uss`), add:
```xml
<Style src="../Styles/Construction.uss" />
```

**Step 4: Commit**

```bash
git add Assets/UI/Documents/GardenRoot.uxml
git commit -m "feat: rename locked tab to construction tab, replace placeholder content"
```

---

## Task 6: Create Construction.uss

**Files:**
- Create: `Assets/UI/Styles/Construction.uss`

**Step 1: Create the stylesheet**

```css
/* Construction Tab */

.construction-card {
    background-color: var(--color-bg-panel);
    border-color: var(--color-border);
    border-width: 1px;
    border-radius: var(--radius-md);
    padding: var(--spacing-sm);
    margin-bottom: var(--spacing-sm);
}

.construction-card-header {
    flex-direction: row;
    justify-content: space-between;
    align-items: center;
    margin-bottom: var(--spacing-xs);
}

.construction-card-name {
    font-size: var(--font-md);
    color: var(--color-text-bright);
    -unity-font-style: bold;
}

.construction-card-badge {
    font-size: var(--font-xs);
    color: var(--color-text-accent);
}

/* Locked state */
.construction-locked-section {
    flex-direction: column;
    align-items: stretch;
}

.construction-unlock-cost {
    font-size: var(--font-sm);
    color: var(--color-text-dim);
    margin-bottom: var(--spacing-xs);
}

/* Unlocked state — upgrade rows */
.construction-upgrades-section {
    flex-direction: column;
}

.construction-upgrade-row {
    flex-direction: row;
    align-items: center;
    justify-content: space-between;
    margin-top: var(--spacing-xs);
}

.construction-upgrade-label {
    font-size: var(--font-sm);
    color: var(--color-text);
    flex-grow: 1;
}

.construction-upgrade-progress {
    font-size: var(--font-xs);
    color: var(--color-text-dim);
    margin-right: var(--spacing-sm);
}

.construction-unlock-btn,
.construction-upgrade-btn {
    min-width: 200px;
}
```

**Step 2: Commit**

```bash
git add Assets/UI/Styles/Construction.uss
git commit -m "feat: add Construction.uss stylesheet for construction tab cards"
```

---

## Task 7: Create UXML templates

**Files:**
- Create: `Assets/Resources/UI/Templates/ConstructionLocationCard.uxml`
- Create: `Assets/Resources/UI/Templates/ConstructionUpgradeButton.uxml`

**Step 1: Create ConstructionLocationCard.uxml**

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <ui:VisualElement class="construction-card">
        <ui:VisualElement class="construction-card-header">
            <ui:Label class="construction-card-name" text="Location" />
            <ui:Label class="construction-card-badge" text="" />
        </ui:VisualElement>
        <!-- Shown when location is locked -->
        <ui:VisualElement class="construction-locked-section" name="locked-section">
            <ui:Label class="construction-unlock-cost" name="unlock-cost-label" text="" />
            <ui:Button class="btn construction-unlock-btn" name="unlock-btn" text="Unlock Location" />
        </ui:VisualElement>
        <!-- Shown when location is unlocked: upgrade buttons injected here by ConstructionUI -->
        <ui:VisualElement class="construction-upgrades-section" name="upgrades-section" />
    </ui:VisualElement>
</ui:UXML>
```

**Step 2: Create ConstructionUpgradeButton.uxml**

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <ui:VisualElement class="construction-upgrade-row">
        <ui:Label class="construction-upgrade-label" name="upgrade-label" text="Add Slot" />
        <ui:Label class="construction-upgrade-progress" name="upgrade-progress" text="" />
        <ui:Button class="btn construction-upgrade-btn" name="upgrade-btn" text="+" />
    </ui:VisualElement>
</ui:UXML>
```

**Step 3: Commit**

```bash
git add Assets/Resources/UI/Templates/ConstructionLocationCard.uxml Assets/Resources/UI/Templates/ConstructionUpgradeButton.uxml
git commit -m "feat: add UXML templates for construction location cards and upgrade buttons"
```

---

## Task 8: Create ConstructionUI.cs

**Files:**
- Create: `Assets/Scripts/UI/ConstructionUI.cs`

**Step 1: Write the controller**

```csharp
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class ConstructionUI : MonoBehaviour
    {
        private VisualTreeAsset cardTemplate;
        private VisualTreeAsset upgradeButtonTemplate;

        private ScrollView scrollView;

        public void Initialize(VisualElement root)
        {
            cardTemplate = Resources.Load<VisualTreeAsset>("UI/Templates/ConstructionLocationCard");
            upgradeButtonTemplate = Resources.Load<VisualTreeAsset>("UI/Templates/ConstructionUpgradeButton");

            scrollView = root.Q<ScrollView>("construction-scroll");

            if (CurrencyManager.Instance != null)
                CurrencyManager.Instance.OnCurrencyChanged += OnCurrencyChanged;
        }

        public void Show()
        {
            RefreshDisplay();
        }

        private void OnCurrencyChanged(CurrencyType type, int oldVal, int newVal)
        {
            if (type == CurrencyType.Dewdrops)
                RefreshDisplay();
        }

        private void RefreshDisplay()
        {
            if (scrollView == null) return;
            scrollView.Clear();

            var em = EnvironmentManager.Instance;
            for (int i = 0; i < em.Environments.Count; i++)
            {
                if (!IsRevealedForConstruction(em, i)) continue;

                var env = em.Environments[i];
                bool unlocked = em.IsUnlocked(i);

                var card = cardTemplate.CloneTree();
                var nameLabel = card.Q<Label>(className: "construction-card-name");
                var badge = card.Q<Label>(className: "construction-card-badge");
                var lockedSection = card.Q<VisualElement>("locked-section");
                var upgradesSection = card.Q<VisualElement>("upgrades-section");

                if (nameLabel != null) nameLabel.text = env.environmentName;

                if (unlocked)
                {
                    lockedSection?.Hide();
                    badge.text = "✓";

                    // Slot upgrade button
                    int current = em.GetActiveSlotCount(i);
                    int max = env.maxSlotCount;

                    var upgradeRow = upgradeButtonTemplate.CloneTree();
                    var upgradeLabel = upgradeRow.Q<Label>("upgrade-label");
                    var upgradeProgress = upgradeRow.Q<Label>("upgrade-progress");
                    var upgradeBtn = upgradeRow.Q<Button>("upgrade-btn");

                    if (upgradeLabel != null) upgradeLabel.text = "Slots";
                    if (upgradeProgress != null) upgradeProgress.text = $"{current} / {max}";

                    if (upgradeBtn != null)
                    {
                        bool canAdd = current < max;
                        bool canAfford = CurrencyManager.Instance.CanAfford(
                            CurrencyType.Dewdrops, env.slotUnlockCostDewdrops);
                        upgradeBtn.text = canAdd ? $"+ ({env.slotUnlockCostDewdrops} Dew)" : "Max";
                        upgradeBtn.SetEnabled(canAdd && canAfford);

                        int capturedIndex = i;
                        upgradeBtn.clicked += () =>
                        {
                            EnvironmentManager.Instance.UnlockSlot(capturedIndex);
                            RefreshDisplay();
                        };
                    }

                    upgradesSection?.Add(upgradeRow);
                }
                else
                {
                    upgradesSection?.Hide();
                    badge.text = "🔒";

                    var costLabel = card.Q<Label>("unlock-cost-label");
                    var unlockBtn = card.Q<Button>("unlock-btn");

                    if (costLabel != null) costLabel.text = $"{env.unlockCostDewdrops} Dewdrops to unlock";

                    if (unlockBtn != null)
                    {
                        bool canAfford = CurrencyManager.Instance.CanAfford(
                            CurrencyType.Dewdrops, env.unlockCostDewdrops);
                        unlockBtn.SetEnabled(canAfford);

                        int capturedIndex = i;
                        unlockBtn.clicked += () =>
                        {
                            EnvironmentManager.Instance.Unlock(capturedIndex);
                            RefreshDisplay();
                        };
                    }
                }

                scrollView.Add(card);
            }

            // Greenhouse card — always visible
            AppendGreenhouseCard();
        }

        private void AppendGreenhouseCard()
        {
            var gm = GreenhouseManager.Instance;
            var config = CurrencyManager.Instance.Config;

            var card = cardTemplate.CloneTree();
            var nameLabel = card.Q<Label>(className: "construction-card-name");
            var badge = card.Q<Label>(className: "construction-card-badge");
            var lockedSection = card.Q<VisualElement>("locked-section");
            var upgradesSection = card.Q<VisualElement>("upgrades-section");

            if (nameLabel != null) nameLabel.text = "Greenhouse";
            if (badge != null) badge.text = "✓";
            lockedSection?.Hide();

            var upgradeRow = upgradeButtonTemplate.CloneTree();
            var upgradeLabel = upgradeRow.Q<Label>("upgrade-label");
            var upgradeProgress = upgradeRow.Q<Label>("upgrade-progress");
            var upgradeBtn = upgradeRow.Q<Button>("upgrade-btn");

            if (upgradeLabel != null) upgradeLabel.text = "Capacity";
            if (upgradeProgress != null) upgradeProgress.text = $"{gm.Plants.Count} / {gm.MaxSlots}";

            if (upgradeBtn != null)
            {
                bool canAfford = CurrencyManager.Instance.CanAfford(
                    CurrencyType.Dewdrops, config.greenhouseExpandCostDewdrops);
                upgradeBtn.text = $"+ ({config.greenhouseExpandCostDewdrops} Dew)";
                upgradeBtn.SetEnabled(canAfford);
                upgradeBtn.clicked += () =>
                {
                    GreenhouseManager.Instance.ExpandSlots();
                    RefreshDisplay();
                };
            }

            upgradesSection?.Add(upgradeRow);
            scrollView.Add(card);
        }

        // A locked environment is revealed only after the previous environment
        // has all its slots purchased (progression gate).
        private static bool IsRevealedForConstruction(EnvironmentManager em, int envIndex)
        {
            if (envIndex == 0) return true;
            int prevIdx = envIndex - 1;
            return em.GetActiveSlotCount(prevIdx) >= em.Environments[prevIdx].maxSlotCount;
        }

        private void OnDestroy()
        {
            if (CurrencyManager.Instance != null)
                CurrencyManager.Instance.OnCurrencyChanged -= OnCurrencyChanged;
        }
    }

    // Extension helpers (UI Toolkit shorthand)
    internal static class VisualElementExtensions
    {
        public static void Hide(this VisualElement el)
        {
            if (el != null) el.style.display = DisplayStyle.None;
        }
    }
}
```

> **Note:** If `VisualElementExtensions` already exists elsewhere in the codebase, remove the duplicate and use the existing one. Search with grep for `static.*Hide.*VisualElement` before committing.

**Step 2: Verify compilation — no errors in Console.**

**Step 3: Commit**

```bash
git add Assets/Scripts/UI/ConstructionUI.cs
git commit -m "feat: add ConstructionUI controller with environment cards and progression gate"
```

---

## Task 9: Wire BottomNavUI

**Files:**
- Modify: `Assets/Scripts/UI/BottomNavUI.cs`

**Step 1: Update tab names and remove locked guard**

Replace the full `BottomNavUI.cs` content:

```csharp
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class BottomNavUI : MonoBehaviour
    {
        private Button[] tabs;
        private SwipeablePageView pageView;

        private static readonly string[] TabNames = {
            "tab-codex", "tab-shop", "tab-terrarium", "tab-greenhouse", "tab-construction"
        };

        public void Initialize(VisualElement root, SwipeablePageView pageView)
        {
            this.pageView = pageView;

            tabs = new Button[TabNames.Length];
            for (int i = 0; i < TabNames.Length; i++)
            {
                tabs[i] = root.Q<Button>(TabNames[i]);
                int index = i;
                tabs[i].clicked += () => OnTabClicked(index);
            }

            pageView.OnPageChanged += UpdateActiveTab;
            UpdateActiveTab(pageView.CurrentPageIndex);
        }

        private void OnTabClicked(int index)
        {
            pageView.GoToPage(index);
        }

        private void UpdateActiveTab(int activeIndex)
        {
            for (int i = 0; i < tabs.Length; i++)
            {
                tabs[i].RemoveFromClassList("nav-tab-active");
            }
            if (activeIndex >= 0 && activeIndex < tabs.Length)
            {
                tabs[activeIndex].AddToClassList("nav-tab-active");
            }
        }

        private void OnDestroy()
        {
            if (pageView != null)
                pageView.OnPageChanged -= UpdateActiveTab;
        }
    }
}
```

**Step 2: Verify compilation — no errors.**

**Step 3: Commit**

```bash
git add Assets/Scripts/UI/BottomNavUI.cs
git commit -m "feat: enable construction tab in bottom nav, remove locked tab guard"
```

---

## Task 10: Wire HortusUI

**Files:**
- Modify: `Assets/Scripts/UI/HortusUI.cs`

**Step 1: Add constructionUI field**

After `private GreenhouseUI greenhouseUI;`, add:
```csharp
private ConstructionUI constructionUI;
```

**Step 2: GetComponent in Start()**

After `greenhouseUI = GetComponent<GreenhouseUI>();`, add:
```csharp
constructionUI = GetComponent<ConstructionUI>();
```

**Step 3: Initialize in Start()**

After `greenhouseUI?.Initialize(root);`, add:
```csharp
constructionUI?.Initialize(root);
```

**Step 4: Update pageNames array**

Change `"locked-page"` → `"construction-page"`:
```csharp
string[] pageNames = { "codex-page", "shop-page", "terrarium-page", "greenhouse-page", "construction-page" };
```

**Step 5: Wire page 4 in OnPageChanged**

In the `switch (pageIndex)` block, add:
```csharp
case 4: constructionUI?.Show(); break;
```

**Step 6: Verify compilation — no errors.**

**Step 7: Add ConstructionUI component to the scene**

In Unity: select the `--- UI ---` GameObject in the Hierarchy → Add Component → `ConstructionUI`.

**Step 8: Commit**

```bash
git add Assets/Scripts/UI/HortusUI.cs
git commit -m "feat: wire ConstructionUI into HortusUI at page index 4"
```

---

## Task 11: Run existing tests and smoke test in Play mode

**Step 1: Run EditMode tests**

In Unity: Window > General > Test Runner > EditMode tab > Run All.

Expected: all 7 existing tests pass (no regressions from economy changes).

**Step 2: Play mode smoke test checklist**

- [ ] Construction tab is clickable (no longer disabled)
- [ ] First environment card (e.g. Sunny Windowsill) shows as unlocked with slot count and upgrade button
- [ ] Subsequent environment cards hidden (progression gate works)
- [ ] Greenhouse card visible at bottom with capacity and Expand button
- [ ] Clicking "Add Slot" on an unlocked environment with enough Dewdrops: spends Dewdrops, increments slot count, button becomes "Max" at maxSlotCount
- [ ] Upgrade button disabled when cannot afford
- [ ] Seed shop: prices show "Dust", button enabled only when AuraDust is sufficient
- [ ] Greenhouse tab: no expand button (removed)
- [ ] Debug panel > Max Currency: all three currencies fill; construction and shop buttons enable

**Step 3: Final commit if any fixes needed**

```bash
git add -p
git commit -m "fix: <describe what you fixed>"
```
