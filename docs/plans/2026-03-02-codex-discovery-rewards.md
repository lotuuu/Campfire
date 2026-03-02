# Codex Discovery Rewards Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add one-time gold rewards for discovered plant variants, claimable via the Codex detail panel, with unclaimed entries highlighted by a pulsing glow border.

**Architecture:** New `claimedDiscoveryRewards` list in `SaveData` tracks claimed state. `CurrencyConfig` gets per-rarity discovery reward fields (2x harvest). `CodexUI` gains a claim button in the detail panel and applies a pulsing CSS class to unclaimed grid entries.

**Tech Stack:** Unity UI Toolkit (UXML/USS/C#), ScriptableObject data, NUnit tests

---

### Task 1: Add discovery reward fields to CurrencyConfig

**Files:**
- Modify: `Assets/Scripts/Data/CurrencyConfig.cs:13` (after legendaryGold)
- Modify: `Assets/Resources/Config/CurrencyConfig.asset:19` (after legendaryGold YAML)

**Step 1: Add C# fields and accessor method**

Add after line 13 (`public int legendaryGold = 250;`) in `CurrencyConfig.cs`:

```csharp
[Header("Discovery Reward (by rarity)")]
public int commonDiscoveryReward = 20;
public int uncommonDiscoveryReward = 50;
public int rareDiscoveryReward = 100;
public int epicDiscoveryReward = 200;
public int legendaryDiscoveryReward = 500;
```

Add accessor method after `GetGoldForRarity` (after line 34):

```csharp
public int GetDiscoveryReward(Rarity r) => r switch
{
    Rarity.Common => commonDiscoveryReward,
    Rarity.Uncommon => uncommonDiscoveryReward,
    Rarity.Rare => rareDiscoveryReward,
    Rarity.Epic => epicDiscoveryReward,
    Rarity.Legendary => legendaryDiscoveryReward,
    _ => commonDiscoveryReward
};
```

**Step 2: Add YAML values to the .asset file**

Add after line 19 (`legendaryGold: 250`) in `CurrencyConfig.asset`:

```yaml
commonDiscoveryReward: 20
uncommonDiscoveryReward: 50
rareDiscoveryReward: 100
epicDiscoveryReward: 200
legendaryDiscoveryReward: 500
```

**Step 3: Compile and verify no errors**

Run: `read_console` to check for compilation errors.

**Step 4: Commit**

```
feat: add discovery reward fields to CurrencyConfig
```

---

### Task 2: Add claimedDiscoveryRewards to SaveData

**Files:**
- Modify: `Assets/Scripts/Data/SaveData.cs:17` (after discoveredVariants)

**Step 1: Add the field**

Add after line 17 (`public List<string> discoveredVariants = new();`) in `SaveData.cs`:

```csharp
public List<string> claimedDiscoveryRewards = new();
```

**Step 2: Compile and verify no errors**

Run: `read_console`.

**Step 3: Commit**

```
feat: add claimedDiscoveryRewards to SaveData
```

---

### Task 3: Write tests for discovery reward claiming

**Files:**
- Modify: `Assets/Tests/EditMode/TestDiscovery.cs`

**Step 1: Add test methods to existing TestDiscovery class**

These tests validate the claim logic we'll implement in Task 5. They test the static helper method `CodexUI.TryClaimDiscoveryReward`. Add at the end of the class:

```csharp
[Test]
public void TryClaimDiscoveryReward_ReturnsTrueForUnclaimed()
{
    var save = new SaveData();
    save.discoveredVariants.Add("Celestial");
    // Not in claimedDiscoveryRewards → should succeed
    bool result = CodexUI.TryClaimDiscoveryReward("Celestial", save);
    Assert.IsTrue(result);
    Assert.IsTrue(save.claimedDiscoveryRewards.Contains("Celestial"));
}

[Test]
public void TryClaimDiscoveryReward_ReturnsFalseForAlreadyClaimed()
{
    var save = new SaveData();
    save.discoveredVariants.Add("Celestial");
    save.claimedDiscoveryRewards.Add("Celestial");

    bool result = CodexUI.TryClaimDiscoveryReward("Celestial", save);
    Assert.IsFalse(result);
    Assert.AreEqual(1, save.claimedDiscoveryRewards.Count);
}

[Test]
public void TryClaimDiscoveryReward_ReturnsFalseForUndiscovered()
{
    var save = new SaveData();
    // Not in discoveredVariants → should fail
    bool result = CodexUI.TryClaimDiscoveryReward("Celestial", save);
    Assert.IsFalse(result);
    Assert.AreEqual(0, save.claimedDiscoveryRewards.Count);
}

[Test]
public void IsDiscoveryRewardUnclaimed_TrueWhenDiscoveredButNotClaimed()
{
    var save = new SaveData();
    save.discoveredVariants.Add("Storm");

    Assert.IsTrue(CodexUI.IsDiscoveryRewardUnclaimed("Storm", save));
}

[Test]
public void IsDiscoveryRewardUnclaimed_FalseWhenClaimed()
{
    var save = new SaveData();
    save.discoveredVariants.Add("Storm");
    save.claimedDiscoveryRewards.Add("Storm");

    Assert.IsFalse(CodexUI.IsDiscoveryRewardUnclaimed("Storm", save));
}

[Test]
public void IsDiscoveryRewardUnclaimed_FalseWhenNotDiscovered()
{
    var save = new SaveData();

    Assert.IsFalse(CodexUI.IsDiscoveryRewardUnclaimed("Storm", save));
}
```

**Step 2: Run tests to verify they fail**

Run: `run_tests` with `mode: "EditMode"`. Expected: 6 new tests FAIL (method not defined).

**Step 3: Commit**

```
test: add discovery reward claim tests
```

---

### Task 4: Add claim button to Codex UXML and USS

**Files:**
- Modify: `Assets/UI/Documents/GardenRoot.uxml:98-99` (detail-info section)
- Modify: `Assets/UI/Styles/Codex.uss` (bottom of file)

**Step 1: Add claim button to UXML**

In `GardenRoot.uxml`, insert between `detail-description` (line 98) and `detail-seed-name` (line 99):

```xml
<ui:Button name="detail-claim-btn" class="btn claim-reward-btn" text="Claim Reward" style="display: none;" />
```

**Step 2: Add CSS styles to Codex.uss**

Append to end of `Codex.uss`:

```css
/* --- Unclaimed Discovery Pulse --- */
.variant-entry-unclaimed {
    transition-property: border-color;
    transition-duration: 1s;
    transition-timing-function: ease-in-out;
    border-width: 2px;
    border-color: rgba(255, 220, 80, 0.6);
}

/* --- Claim Reward Button --- */
.claim-reward-btn {
    margin-top: var(--spacing-sm);
    margin-bottom: var(--spacing-xs);
    padding: 12px 24px;
    border-radius: var(--radius-sm);
    background-color: rgba(255, 200, 50, 0.25);
    border-width: 1px;
    border-color: rgba(255, 220, 80, 0.45);
    color: var(--color-highlight);
    font-size: var(--font-sm);
    -unity-font-style: bold;
    -unity-text-align: middle-center;
    align-self: flex-start;
}

.claim-reward-btn:hover {
    background-color: rgba(255, 200, 50, 0.35);
    border-color: rgba(255, 220, 80, 0.65);
}

.claim-reward-btn:active {
    background-color: rgba(255, 200, 50, 0.45);
}
```

**Step 3: Compile and verify no errors**

Run: `read_console`.

**Step 4: Commit**

```
feat: add claim reward button UXML and unclaimed pulse CSS
```

---

### Task 5: Implement claim logic and UI wiring in CodexUI

**Files:**
- Modify: `Assets/Scripts/UI/CodexUI.cs`

**Step 1: Add static helper methods for testability**

Add before the `RarityClasses` field (line 180):

```csharp
public static bool IsDiscoveryRewardUnclaimed(string variantName, SaveData save)
{
    return save.discoveredVariants.Contains(variantName)
        && !save.claimedDiscoveryRewards.Contains(variantName);
}

public static bool TryClaimDiscoveryReward(string variantName, SaveData save)
{
    if (!save.discoveredVariants.Contains(variantName)) return false;
    if (save.claimedDiscoveryRewards.Contains(variantName)) return false;
    save.claimedDiscoveryRewards.Add(variantName);
    return true;
}
```

**Step 2: Add claim button field and cache it in Initialize**

Add to the field declarations (after line 20, `_selectedEntry`):

```csharp
private Button _detailClaimBtn;
```

Add to `Initialize()` (after line 34, `_detailSeedName` cache):

```csharp
_detailClaimBtn = root.Q<Button>("detail-claim-btn");
```

**Step 3: Apply unclaimed highlight in RefreshCodex**

In `RefreshCodex()`, after the discovered variant `if` block sets up the card (after line 88 `nameLabel.text = variant.variantName;`), add the unclaimed pulse class logic. Replace the end of the `if (isDiscovered)` block. After `nameLabel.text = variant.variantName;` (line 88):

```csharp
// Unclaimed discovery reward pulse
if (IsDiscoveryRewardUnclaimed(variant.variantName, SaveManager.Instance.Data))
{
    Color rc = GetRarityColor(variant.rarity);
    button?.AddToClassList("variant-entry-unclaimed");
    if (button != null)
    {
        button.style.borderTopColor = new StyleColor(WithAlpha(rc, 0.6f));
        button.style.borderBottomColor = new StyleColor(WithAlpha(rc, 0.6f));
        button.style.borderLeftColor = new StyleColor(WithAlpha(rc, 0.6f));
        button.style.borderRightColor = new StyleColor(WithAlpha(rc, 0.6f));
    }
}
```

**Step 4: Wire claim button in ShowDetail**

In `ShowDetail()`, inside the `if (discovered)` block (after `_detailSpriteGlow` setup, around line 161), add claim button logic:

```csharp
// Claim reward button
if (_detailClaimBtn != null)
{
    var save = SaveManager.Instance.Data;
    if (IsDiscoveryRewardUnclaimed(variant.variantName, save))
    {
        int reward = CurrencyManager.Instance.Config.GetDiscoveryReward(variant.rarity);
        _detailClaimBtn.text = $"Claim {reward} Gold";
        _detailClaimBtn.style.display = DisplayStyle.Flex;
        _detailClaimBtn.clickable = new Clickable(() =>
        {
            if (TryClaimDiscoveryReward(variant.variantName, SaveManager.Instance.Data))
            {
                CurrencyManager.Instance.Add(CurrencyType.Gold, reward);
                _detailClaimBtn.style.display = DisplayStyle.None;
                // Remove pulse from the selected grid entry
                _selectedEntry?.RemoveFromClassList("variant-entry-unclaimed");
                if (_selectedEntry != null)
                {
                    _selectedEntry.style.borderTopColor = StyleKeyword.Null;
                    _selectedEntry.style.borderBottomColor = StyleKeyword.Null;
                    _selectedEntry.style.borderLeftColor = StyleKeyword.Null;
                    _selectedEntry.style.borderRightColor = StyleKeyword.Null;
                }
            }
        });
    }
    else
    {
        _detailClaimBtn.style.display = DisplayStyle.None;
    }
}
```

In the `else` (undiscovered) block of `ShowDetail()`, hide the button:

```csharp
if (_detailClaimBtn != null)
    _detailClaimBtn.style.display = DisplayStyle.None;
```

**Step 5: Run all tests**

Run: `run_tests` with `mode: "EditMode"`. Expected: ALL tests pass including the 6 new ones.

**Step 6: Commit**

```
feat: implement Codex discovery reward claiming
```

---

### Task 6: Manual verification and cleanup

**Step 1: Take a screenshot of the Codex to verify visual state**

Use `manage_scene(action="screenshot")` to capture current state.

**Step 2: Verify with debug panel**

- Use "Discover All Plants" debug button to populate discovered variants
- Open Codex — unclaimed entries should have pulsing rarity-colored borders
- Tap an entry — detail panel should show "Claim X Gold" button
- Tap claim — gold should increase, button should disappear, pulse should stop on that entry
- Re-tap same entry — no claim button visible

**Step 3: Final commit if any cleanup needed**
