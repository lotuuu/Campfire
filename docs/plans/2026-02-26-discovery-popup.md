# Discovery Popup Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Replace the harvest result popup with a dramatic animated "first discovery" screen when a player harvests a variant they've never grown before.

**Architecture:** `HarvestResult` gains an `isNewDiscovery` flag set in `PlantManager.Harvest()` (discovery tracking moves from plant-time to harvest-time). `HortusUI` routes to `DiscoveryPopupUI` when the flag is true. All animation is CSS keyframes in UI Toolkit.

**Tech Stack:** Unity 6 UI Toolkit (UXML/USS), NativeShare (yasirkula/UnityNativeShare), Unity ScreenCapture API (`com.unity.modules.screencapture` already in manifest).

---

### Task 1: Install NativeShare

**Files:**
- Modify: `Packages/manifest.json`

**Step 1: Add NativeShare to manifest**

In `Packages/manifest.json`, insert a new line inside the `"dependencies"` object, directly after the `com.coplaydev.unity-mcp` line:

Old:
```json
    "com.coplaydev.unity-mcp": "https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#main",
    "com.unity.2d.animation": "13.0.2",
```

New:
```json
    "com.coplaydev.unity-mcp": "https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#main",
    "com.yasirkula.nativeshare": "https://github.com/yasirkula/UnityNativeShare.git",
    "com.unity.2d.animation": "13.0.2",
```

**Step 2: Let Unity resolve the package**

Save the file and switch to Unity Editor. Watch Console for compilation errors. NativeShare exposes a single `NativeShare` class with no namespace.

**Step 3: Commit**

```bash
git add Packages/manifest.json Packages/packages-lock.json
git commit -m "chore: add NativeShare package dependency"
```

---

### Task 2: Add `isNewDiscovery` to HarvestResult + pure helper (TDD)

**Files:**
- Modify: `Assets/Scripts/Data/HarvestResult.cs`
- Modify: `Assets/Scripts/Managers/PlantManager.cs`
- Create: `Assets/Tests/EditMode/TestDiscovery.cs`

**Step 1: Write the failing tests**

Create `Assets/Tests/EditMode/TestDiscovery.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;

namespace Garden.Tests
{
    public class TestDiscovery
    {
        private VariantData CreateVariant(string name)
        {
            var v = ScriptableObject.CreateInstance<VariantData>();
            v.variantName = name;
            return v;
        }

        [Test]
        public void CheckAndMarkDiscovered_ReturnsTrueForNewVariant()
        {
            var variant = CreateVariant("Celestial");
            var save = new SaveData();

            bool result = PlantManager.CheckAndMarkDiscovered(variant, save);

            Assert.IsTrue(result);
            Assert.IsTrue(save.discoveredVariants.Contains("Celestial"));
        }

        [Test]
        public void CheckAndMarkDiscovered_ReturnsFalseForAlreadyDiscoveredVariant()
        {
            var variant = CreateVariant("Celestial");
            var save = new SaveData();
            save.discoveredVariants.Add("Celestial");

            bool result = PlantManager.CheckAndMarkDiscovered(variant, save);

            Assert.IsFalse(result);
            Assert.AreEqual(1, save.discoveredVariants.Count); // not added twice
        }

        [Test]
        public void CheckAndMarkDiscovered_AddsVariantToDiscoveredList()
        {
            var variant = CreateVariant("Storm");
            var save = new SaveData();
            save.discoveredVariants.Add("Celestial"); // pre-existing

            PlantManager.CheckAndMarkDiscovered(variant, save);

            Assert.AreEqual(2, save.discoveredVariants.Count);
            Assert.IsTrue(save.discoveredVariants.Contains("Storm"));
        }
    }
}
```

**Step 2: Verify tests fail to compile**

In Unity Test Runner (Window > General > Test Runner > EditMode), these will fail to compile because `PlantManager.CheckAndMarkDiscovered` doesn't exist yet. Expected.

**Step 3: Add `isNewDiscovery` to HarvestResult**

`Assets/Scripts/Data/HarvestResult.cs` currently ends with:
```csharp
        public VariantData variant;
        public SeedData seed;
    }
}
```

Change to:
```csharp
        public VariantData variant;
        public SeedData seed;
        public bool isNewDiscovery;
    }
}
```

**Step 4: Add `CheckAndMarkDiscovered` to PlantManager**

Read `Assets/Scripts/Managers/PlantManager.cs`. Find the `KeepHarvest` method:
```csharp
        public void KeepHarvest(HarvestResult result)
        {
            GreenhouseManager.Instance.AddPlant(result.seed, result.variant, result.tier);
        }
```

Add the new static helper directly after it:
```csharp
        public void KeepHarvest(HarvestResult result)
        {
            GreenhouseManager.Instance.AddPlant(result.seed, result.variant, result.tier);
        }

        internal static bool CheckAndMarkDiscovered(VariantData variant, SaveData save)
        {
            if (save.discoveredVariants.Contains(variant.variantName)) return false;
            save.discoveredVariants.Add(variant.variantName);
            return true;
        }
```

**Step 5: Run tests — expect all 3 to pass**

Run `TestDiscovery` in Unity Test Runner. All 3 must pass before continuing.

**Step 6: Commit**

```bash
git add Assets/Scripts/Data/HarvestResult.cs \
        Assets/Scripts/Managers/PlantManager.cs \
        Assets/Tests/EditMode/TestDiscovery.cs \
        Assets/Tests/EditMode/TestDiscovery.cs.meta
git commit -m "feat: add isNewDiscovery to HarvestResult with discovery helper"
```

---

### Task 3: Move discovery tracking from Plant() to Harvest()

**Files:**
- Modify: `Assets/Scripts/Managers/PlantManager.cs`

**Step 1: Remove discovery from Plant()**

In `PlantManager.Plant(SeedData seed, int environmentIndex, int slotIndex)`, find:
```csharp
            var save = SaveManager.Instance.Data;
            if (!save.discoveredVariants.Contains(result.variant.variantName))
                save.discoveredVariants.Add(result.variant.variantName);

            var entry = save.seedInventory.Find(e => e.seedName == seed.seedName);
```

Replace with (keep `var save` — it's used for `seedInventory` below):
```csharp
            var save = SaveManager.Instance.Data;

            var entry = save.seedInventory.Find(e => e.seedName == seed.seedName);
```

**Step 2: Add discovery check to Harvest()**

In `Harvest(int environmentIndex, int slotIndex)`, find:
```csharp
            var result = HarvestEngine.Roll(slot.seed, slot.variant, effectiveWeather, qualityBoosted);

            ClearSlot(slot);
            return result;
```

Replace with:
```csharp
            var result = HarvestEngine.Roll(slot.seed, slot.variant, effectiveWeather, qualityBoosted);

            result.isNewDiscovery = CheckAndMarkDiscovered(slot.variant, SaveManager.Instance.Data);
            if (result.isNewDiscovery)
                SaveManager.Instance.Save();

            ClearSlot(slot);
            return result;
```

**Step 3: Run all EditMode tests**

All 21 tests (18 existing + 3 new) must pass.

**Step 4: Commit**

```bash
git add Assets/Scripts/Managers/PlantManager.cs
git commit -m "feat: move variant discovery tracking from plant-time to harvest-time"
```

---

### Task 4: Create DiscoveryPopup.uxml template

**Files:**
- Create: `Assets/Resources/UI/Templates/DiscoveryPopup.uxml`

**Step 1: Create the file**

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <ui:VisualElement name="discovery-overlay" class="discovery-overlay" picking-mode="Position">
        <ui:VisualElement name="discovery-card" class="discovery-card">
            <ui:VisualElement name="glow-bg" class="discovery-glow" />
            <ui:VisualElement name="sprite-container" class="discovery-sprite" />
            <ui:Label name="variant-name" class="discovery-name" text="" />
            <ui:Label name="rarity-label" class="discovery-rarity" text="" />
            <ui:VisualElement class="discovery-divider" />
            <ui:Label name="variant-description" class="discovery-description" text="" />
            <ui:VisualElement class="discovery-actions">
                <ui:Button name="share-button" text="Share Discovery" class="discovery-share-btn" />
                <ui:Label class="discovery-dismiss-hint" text="Tap anywhere to continue" />
            </ui:VisualElement>
        </ui:VisualElement>
    </ui:VisualElement>
</ui:UXML>
```

---

### Task 5: Create DiscoveryPopup.uss with animations

**Files:**
- Create: `Assets/UI/Styles/DiscoveryPopup.uss`

**Step 1: Create the file**

```css
/* ── Keyframes ───────────────────────────────────────────── */

@keyframes overlay-in {
    from { opacity: 0; }
    to   { opacity: 1; }
}

@keyframes card-in {
    from { opacity: 0; scale: 0.85; }
    to   { opacity: 1; scale: 1; }
}

@keyframes sprite-in {
    from { opacity: 0; scale: 0.7; }
    to   { opacity: 1; scale: 1; }
}

@keyframes slide-up-in {
    from { opacity: 0; translate: 0 16px; }
    to   { opacity: 1; translate: 0 0; }
}

@keyframes fade-in {
    from { opacity: 0; }
    to   { opacity: 1; }
}

@keyframes glow-pulse {
    0%   { opacity: 0.3; }
    50%  { opacity: 0.7; }
    100% { opacity: 0.3; }
}

/* ── Overlay ─────────────────────────────────────────────── */

.discovery-overlay {
    position: absolute;
    left: 0; right: 0; top: 0; bottom: 0;
    background-color: rgba(0, 0, 0, 0.85);
    align-items: center;
    justify-content: center;
    opacity: 0;
    animation-name: overlay-in;
    animation-duration: 0.3s;
    animation-fill-mode: both;
}

/* ── Card ────────────────────────────────────────────────── */

.discovery-card {
    width: 300px;
    max-width: 88%;
    background-color: rgb(18, 18, 30);
    border-radius: 20px;
    padding: 24px;
    align-items: center;
    overflow: hidden;
    opacity: 0;
    scale: 0.85;
    animation-name: card-in;
    animation-duration: 0.5s;
    animation-delay: 0.1s;
    animation-timing-function: cubic-bezier(0.34, 1.56, 0.64, 1);
    animation-fill-mode: both;
}

/* ── Glow ────────────────────────────────────────────────── */

.discovery-glow {
    position: absolute;
    left: -40px; right: -40px;
    top: -40px; bottom: -40px;
    border-radius: 50%;
    /* background-color set inline from C# using variant.primaryColor */
    opacity: 0;
    animation-name: glow-pulse;
    animation-duration: 2s;
    animation-delay: 0.2s;
    animation-iteration-count: infinite;
    animation-fill-mode: both;
}

/* ── Sprite ──────────────────────────────────────────────── */

.discovery-sprite {
    width: 180px;
    height: 180px;
    margin-bottom: 16px;
    background-size: contain;
    background-repeat: no-repeat;
    background-position: center;
    opacity: 0;
    scale: 0.7;
    animation-name: sprite-in;
    animation-duration: 0.5s;
    animation-delay: 0.2s;
    animation-timing-function: cubic-bezier(0.34, 1.56, 0.64, 1);
    animation-fill-mode: both;
}

/* ── Name ────────────────────────────────────────────────── */

.discovery-name {
    font-size: 22px;
    -unity-font-style: bold;
    color: rgb(240, 230, 210);
    -unity-text-align: middle-center;
    white-space: normal;
    margin-bottom: 4px;
    opacity: 0;
    translate: 0 16px;
    animation-name: slide-up-in;
    animation-duration: 0.3s;
    animation-delay: 0.5s;
    animation-fill-mode: both;
}

/* ── Rarity ──────────────────────────────────────────────── */

.discovery-rarity {
    font-size: 11px;
    letter-spacing: 2px;
    -unity-text-align: middle-center;
    margin-bottom: 12px;
    opacity: 0;
    animation-name: fade-in;
    animation-duration: 0.3s;
    animation-delay: 0.7s;
    animation-fill-mode: both;
}

/* Rarity color classes — must match Rarity enum: Common, Uncommon, Rare, Epic, Legendary */
.rarity-common    { color: rgb(180, 180, 180); }
.rarity-uncommon  { color: rgb(100, 220, 120); }
.rarity-rare      { color: rgb(100, 160, 255); }
.rarity-epic      { color: rgb(200, 100, 255); }
.rarity-legendary { color: rgb(255, 200, 60);  }

/* ── Divider ─────────────────────────────────────────────── */

.discovery-divider {
    height: 1px;
    width: 100%;
    background-color: rgba(255, 255, 255, 0.15);
    margin-bottom: 14px;
    opacity: 0;
    animation-name: fade-in;
    animation-duration: 0.3s;
    animation-delay: 0.75s;
    animation-fill-mode: both;
}

/* ── Description ─────────────────────────────────────────── */

.discovery-description {
    font-size: 13px;
    color: rgb(180, 170, 160);
    -unity-text-align: middle-center;
    white-space: normal;
    margin-bottom: 20px;
    opacity: 0;
    animation-name: fade-in;
    animation-duration: 0.4s;
    animation-delay: 1s;
    animation-fill-mode: both;
}

/* ── Actions row ─────────────────────────────────────────── */

.discovery-actions {
    width: 100%;
    align-items: center;
    opacity: 0;
    animation-name: fade-in;
    animation-duration: 0.3s;
    animation-delay: 1.5s;
    animation-fill-mode: both;
}

.discovery-share-btn {
    width: 100%;
    height: 44px;
    border-radius: 10px;
    background-color: rgba(255, 255, 255, 0.1);
    border-color: rgba(255, 255, 255, 0.2);
    border-width: 1px;
    color: rgb(220, 210, 200);
    font-size: 14px;
    margin-bottom: 10px;
}

.discovery-share-btn:hover {
    background-color: rgba(255, 255, 255, 0.18);
}

.discovery-dismiss-hint {
    font-size: 11px;
    color: rgba(255, 255, 255, 0.35);
    -unity-text-align: middle-center;
}
```

---

### Task 6: Create DiscoveryPopupUI.cs

**Files:**
- Create: `Assets/Scripts/UI/DiscoveryPopupUI.cs`

**Step 1: Create the controller**

```csharp
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class DiscoveryPopupUI : MonoBehaviour
    {
        public event Action OnDismissed;

        private VisualElement _container;
        private VisualTreeAsset _template;
        private string _pendingVariantName;

        public void Initialize(VisualElement root)
        {
            _container = root.Q<VisualElement>("discovery-popup");
            _template = Resources.Load<VisualTreeAsset>("UI/Templates/DiscoveryPopup");
        }

        public void Show(VariantData variant, HarvestResult result)
        {
            _container.Clear();

            var popup = _template.CloneTree();
            popup.style.flexGrow = 1;
            _container.Add(popup);
            _container.style.display = DisplayStyle.Flex;

            // Variant sprite
            var spriteContainer = popup.Q<VisualElement>("sprite-container");
            if (variant.variantSprite != null)
                spriteContainer.style.backgroundImage = new StyleBackground(variant.variantSprite);

            // Glow color from variant primary color
            popup.Q<VisualElement>("glow-bg").style.backgroundColor = new StyleColor(variant.primaryColor);

            // Text
            popup.Q<Label>("variant-name").text = variant.variantName;
            popup.Q<Label>("variant-description").text = variant.description;

            var rarityLabel = popup.Q<Label>("rarity-label");
            rarityLabel.text = variant.rarity.ToString().ToUpper();
            rarityLabel.AddToClassList($"rarity-{variant.rarity.ToString().ToLower()}");

            // Share button
            _pendingVariantName = variant.variantName;
            popup.Q<Button>("share-button").clicked += OnShareClicked;

            // Tap card stops propagation — prevents card taps from dismissing the overlay
            popup.Q<VisualElement>("discovery-card").RegisterCallback<ClickEvent>(evt => evt.StopPropagation());

            // Tap overlay background to dismiss
            popup.Q<VisualElement>("discovery-overlay").RegisterCallback<ClickEvent>(_ => Dismiss());
        }

        private void OnShareClicked()
        {
            StartCoroutine(ShareCoroutine());
        }

        private IEnumerator ShareCoroutine()
        {
            yield return new WaitForEndOfFrame();

            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            string path = Path.Combine(Application.temporaryCachePath, "discovery.png");
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Destroy(texture);

            new NativeShare()
                .AddFile(path)
                .SetText($"I just discovered {_pendingVariantName} in Garden! 🌱")
                .Share();
        }

        private void Dismiss()
        {
            _container.Clear();
            _container.style.display = DisplayStyle.None;
            OnDismissed?.Invoke();
        }
    }
}
```

**Step 2: Check compilation in Unity Console — zero errors required before continuing.**

---

### Task 7: Add overlay container to GardenRoot.uxml + link stylesheet

**Files:**
- Modify: `Assets/UI/Documents/GardenRoot.uxml`

**Step 1: Add the stylesheet import**

The stylesheet block ends at line 15. Insert the new import after `Construction.uss`:

```xml
    <Style src="../Styles/Construction.uss" />
```
→
```xml
    <Style src="../Styles/Construction.uss" />
    <Style src="../Styles/DiscoveryPopup.uss" />
```

**Step 2: Add the discovery-popup container**

Line 140 is:
```xml
    <!-- Harvest Result Popup -->
    <ui:VisualElement name="harvest-popup" style="position: absolute; left: 0; right: 0; top: 0; bottom: 0; display: none;" />
```

Add the discovery popup directly after it:
```xml
    <!-- Harvest Result Popup -->
    <ui:VisualElement name="harvest-popup" style="position: absolute; left: 0; right: 0; top: 0; bottom: 0; display: none;" />

    <!-- Discovery Popup (first-time variant reveal) -->
    <ui:VisualElement name="discovery-popup" style="position: absolute; left: 0; right: 0; top: 0; bottom: 0; display: none;" />
```

---

### Task 8: Add DiscoveryPopupUI component to scene + wire in HortusUI

**Files:**
- Modify: `Assets/Scripts/UI/HortusUI.cs`
- Modify: `Assets/Scenes/Garden.unity` (via Unity Editor)

**Step 1: Add DiscoveryPopupUI component to the scene**

In Unity Editor, select the `--- UI ---` GameObject (the one that has `HortusUI`, `HarvestResultUI`, etc.). In the Inspector, click **Add Component** and add `DiscoveryPopupUI`. This makes `GetComponent<DiscoveryPopupUI>()` work.

**Step 2: Add field to HortusUI**

In `HortusUI.cs`, the private fields block (lines 12–24) currently ends with:
```csharp
        private HarvestResultUI harvestResultUI;
        private DebugWeatherPanel debugPanel;
```

Add the new field after `harvestResultUI`:
```csharp
        private HarvestResultUI harvestResultUI;
        private DiscoveryPopupUI discoveryPopupUI;
        private DebugWeatherPanel debugPanel;
```

**Step 3: Get the component in Start()**

The component-fetch block (lines 48–57) currently ends with:
```csharp
        harvestResultUI = GetComponent<HarvestResultUI>();
        debugPanel = GetComponent<DebugWeatherPanel>();
```

Add the new fetch after `harvestResultUI`:
```csharp
        harvestResultUI = GetComponent<HarvestResultUI>();
        discoveryPopupUI = GetComponent<DiscoveryPopupUI>();
        debugPanel = GetComponent<DebugWeatherPanel>();
```

**Step 4: Initialize in Start()**

The initialize block (lines 75–84) currently ends with:
```csharp
        harvestResultUI?.Initialize(root);
        debugPanel?.Initialize(root);
```

Add after `harvestResultUI`:
```csharp
        harvestResultUI?.Initialize(root);
        discoveryPopupUI?.Initialize(root);
        debugPanel?.Initialize(root);
```

**Step 5: Update the OnMatureSlotTapped handler**

Lines 111–117 currently read:
```csharp
                backyardViewUI.OnMatureSlotTapped += (envIdx, slotIdx) =>
                {
                    var result = PlantManager.Instance.Harvest(envIdx, slotIdx);
                    if (result.seed != null)
                        harvestResultUI?.Show(result);
                    backyardViewUI?.RefreshAllSlots();
                };
```

Replace with:
```csharp
                backyardViewUI.OnMatureSlotTapped += (envIdx, slotIdx) =>
                {
                    var result = PlantManager.Instance.Harvest(envIdx, slotIdx);
                    if (result.seed != null)
                    {
                        if (result.isNewDiscovery)
                            discoveryPopupUI?.Show(result.variant, result);
                        else
                            harvestResultUI?.Show(result);
                    }
                    backyardViewUI?.RefreshAllSlots();
                };
```

**Step 6: Subscribe discoveryPopupUI to OnDismissed**

Lines 120–127 currently read:
```csharp
            if (harvestResultUI != null)
            {
                harvestResultUI.OnDismissed += () =>
                {
                    backyardViewUI?.RefreshAllSlots();
                    greenhouseUI?.RefreshDisplay();
                };
            }
```

Add a matching block directly after:
```csharp
            if (harvestResultUI != null)
            {
                harvestResultUI.OnDismissed += () =>
                {
                    backyardViewUI?.RefreshAllSlots();
                    greenhouseUI?.RefreshDisplay();
                };
            }

            if (discoveryPopupUI != null)
            {
                discoveryPopupUI.OnDismissed += () =>
                {
                    backyardViewUI?.RefreshAllSlots();
                    greenhouseUI?.RefreshDisplay();
                };
            }
```

**Step 7: Check compilation — zero errors in Console.**

**Step 8: Commit**

```bash
git add Assets/Scripts/UI/DiscoveryPopupUI.cs \
        Assets/Scripts/UI/DiscoveryPopupUI.cs.meta \
        Assets/Scripts/UI/HortusUI.cs \
        Assets/Resources/UI/Templates/DiscoveryPopup.uxml \
        Assets/Resources/UI/Templates/DiscoveryPopup.uxml.meta \
        Assets/UI/Styles/DiscoveryPopup.uss \
        Assets/UI/Styles/DiscoveryPopup.uss.meta \
        Assets/UI/Documents/GardenRoot.uxml \
        Assets/Scenes/Garden.unity
git commit -m "feat: add discovery popup with animated variant reveal and share button"
```

---

### Task 9: Manual verification in Unity Editor

**Step 1: Enter Play Mode**

**Step 2: Plant a seed**

Open the Satchel (tap an empty slot) and plant any seed. Note which variant resolves in the Console or Codex — make sure it's one that isn't already in `discoveredVariants` (clear save data via debug panel if needed).

**Step 3: Advance time to mature**

Use debug panel > Skip Time to mature the plant instantly.

**Step 4: Harvest and verify discovery popup**

Tap the mature slot. Confirm the **Discovery popup** appears (not the normal harvest popup).

**Checklist:**
- [ ] Dark scrim fades in smoothly
- [ ] Card bounces in with overshoot spring
- [ ] Sprite scales up with bounce
- [ ] Variant name slides up and fades in (~0.5s in)
- [ ] Rarity label appears with correct color (check `Rarity` enum: Common=grey, Uncommon=green, Rare=blue, Epic=purple, Legendary=gold)
- [ ] Divider fades in
- [ ] Description fades in (~1s in)
- [ ] Share button and dismiss hint appear last (~1.5s in)
- [ ] Tapping outside the card (on the scrim) dismisses and returns to game
- [ ] Tapping the card itself does NOT dismiss
- [ ] After dismiss: backyard slots refresh, greenhouse refreshes
- [ ] Harvesting the **same** variant again shows the **normal** harvest popup

**Step 5: Run all EditMode tests**

Window > General > Test Runner > EditMode > Run All. All 21 tests must pass.

**Step 6: Commit any tweaks**

```bash
git add -p
git commit -m "fix: adjust discovery popup animation timing/layout"
```
