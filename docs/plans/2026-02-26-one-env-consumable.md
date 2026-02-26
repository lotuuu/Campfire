# One-Env-Consumable-At-A-Time Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Enforce a single environment-scoped consumable per environment, with an inline confirmation prompt when replacing and a visual indicator on the herb button.

**Architecture:** Three small, isolated changes — logic enforcement in `ConsumableManager`, visual clearing in `BackyardIsometricView`, and confirmation + indicator UX in `BackyardViewUI` + `Backyard.uss`. No save format changes needed.

**Tech Stack:** C# Unity 6, UI Toolkit (USS/UXML-free; all confirmation UI is built in code)

---

### Task 1: Update the test for one-per-env logic and add a new one

**Files:**
- Modify: `Assets/Tests/EditMode/TestConsumableManager.cs`

**Step 1: Update the existing same-type test to reflect new behavior**

The test `EnvironmentConsumableSave_NoDuplicateType` currently mirrors the old "remove same-type only" logic. Change it to mirror the new "remove all for this env" logic:

```csharp
[Test]
public void EnvironmentConsumableSave_NoDuplicateType()
{
    var envList = new List<EnvironmentConsumableSave>();
    envList.Add(new EnvironmentConsumableSave { envIndex = 0, consumableType = "Fan" });

    // New logic: remove ALL for this env before adding
    envList.RemoveAll(e => e.envIndex == 0);
    envList.Add(new EnvironmentConsumableSave { envIndex = 0, consumableType = "Fan" });

    int fanCount = envList.FindAll(e => e.consumableType == "Fan").Count;
    Assert.AreEqual(1, fanCount);
}
```

**Step 2: Add a new test covering mixed-type replacement**

Add this test immediately after the one above in `TestConsumableManager.cs`:

```csharp
[Test]
public void EnvironmentConsumableSave_OnlyOnePerEnv()
{
    var envList = new List<EnvironmentConsumableSave>();
    envList.Add(new EnvironmentConsumableSave { envIndex = 0, consumableType = "Fan" });

    // Replacing Fan with Cloud: remove all for env 0
    envList.RemoveAll(e => e.envIndex == 0);
    envList.Add(new EnvironmentConsumableSave { envIndex = 0, consumableType = "Cloud" });

    // env 0 has exactly one entry, and it's Cloud
    var env0 = envList.FindAll(e => e.envIndex == 0);
    Assert.AreEqual(1, env0.Count);
    Assert.AreEqual("Cloud", env0[0].consumableType);

    // env 1 is unaffected
    envList.Add(new EnvironmentConsumableSave { envIndex = 1, consumableType = "Heater" });
    Assert.AreEqual(1, envList.FindAll(e => e.envIndex == 1).Count);
}
```

**Step 3: Run tests to confirm both pass**

Unity Test Runner → Window > General > Test Runner > EditMode tab → Run All.
Expected: all tests pass (these two test pure data logic, no MonoBehaviour needed).

**Step 4: Commit**

```bash
git add Assets/Tests/EditMode/TestConsumableManager.cs
git commit -m "test: update consumable env tests for one-per-env constraint"
```

---

### Task 2: Enforce one-per-env in `ConsumableManager`

**Files:**
- Modify: `Assets/Scripts/Managers/ConsumableManager.cs:89-106`

**Step 1: Change the RemoveAll guard**

Find this line in `ApplyToEnvironment()` (line 97):
```csharp
envList.RemoveAll(e => e.envIndex == envIndex && e.consumableType == type.ToString());
```

Replace with:
```csharp
// One consumable per environment — discard any existing (regardless of type)
envList.RemoveAll(e => e.envIndex == envIndex);
```

Also update the XML doc comment on the method (lines 85-88) to reflect the new behaviour:
```csharp
/// <summary>
/// Spends one consumable from inventory and applies it to the environment.
/// Replaces any existing env-scoped consumable on this environment (one per env).
/// Only env-scoped types allowed. The replaced consumable is discarded.
/// </summary>
```

**Step 2: Run tests**

Unity Test Runner → Run All. `EnvironmentConsumableSave_OnlyOnePerEnv` must pass.

**Step 3: Commit**

```bash
git add Assets/Scripts/Managers/ConsumableManager.cs
git commit -m "feat: enforce one env-scoped consumable per environment"
```

---

### Task 3: Clear all env visuals before spawning a new one

**Files:**
- Modify: `Assets/Scripts/UI/BackyardIsometricView.cs`

**Step 1: Add `ClearAllEnvConsumableVisuals()`**

Add this public method directly after the existing `ClearEnvConsumableVisual(ConsumableType type)` method (after line 354):

```csharp
/// <summary>Destroys all env-scoped consumable GOs (called before spawning a replacement).</summary>
public void ClearAllEnvConsumableVisuals()
{
    foreach (var kvp in _envConsumableGOs)
        if (kvp.Value) Destroy(kvp.Value);
    _envConsumableGOs.Clear();
}
```

**Step 2: Use it in `SpawnEnvConsumableVisual`**

In `SpawnEnvConsumableVisual()` (around line 294), replace the same-type-only removal block:

Old:
```csharp
// Remove existing of same type
if (_envConsumableGOs.TryGetValue(type, out var existing))
{
    if (existing) Destroy(existing);
    _envConsumableGOs.Remove(type);
}
```

New:
```csharp
// Clear all env consumable GOs — only one allowed at a time
ClearAllEnvConsumableVisuals();
```

**Step 3: Verify in Play mode**

Open Garden scene → play → apply an env consumable (e.g. Fan) → apply a different one (e.g. Cloud) → confirm the Fan GO disappears and only Cloud is present. Check Unity console for errors.

**Step 4: Commit**

```bash
git add Assets/Scripts/UI/BackyardIsometricView.cs
git commit -m "feat: clear all env consumable visuals when spawning replacement"
```

---

### Task 4: Inline confirmation + active indicator in `BackyardViewUI`

**Files:**
- Modify: `Assets/Scripts/UI/BackyardViewUI.cs`
- Modify: `Assets/UI/Styles/Backyard.uss`

#### 4a — USS: add occupied state style

Open `Assets/UI/Styles/Backyard.uss`. Append after the `.consumable-picker-btn:hover` block (after line 101):

```uss
/* Herb button glow when this environment already has an active consumable */
.consumable-picker-btn--occupied {
    border-width: 2px;
    border-color: var(--color-highlight);
    border-radius: 66px;
}
```

#### 4b — C#: add field and helpers

In `BackyardViewUI.cs`, add one new field alongside the existing `_pendingType` field (line 29):

```csharp
private ConsumableType? _pendingEnvConfirmType; // set while inline confirmation is showing
```

Add these three private methods anywhere in the class (e.g. after `CancelApplyMode()`):

```csharp
private void RefreshPickerIndicator()
{
    if (_pickerBtn == null || ConsumableManager.Instance == null) return;
    bool occupied = ConsumableManager.Instance.GetEnvConsumables(ActiveEnv).Count > 0;
    if (occupied)
        _pickerBtn.AddToClassList("consumable-picker-btn--occupied");
    else
        _pickerBtn.RemoveFromClassList("consumable-picker-btn--occupied");
}

private void ShowEnvReplaceConfirmation(ConsumableType newType, ConsumableData existingData)
{
    _pendingEnvConfirmType = newType;
    _iconsContainer.Clear();
    _pickerContainer.AddToClassList("consumable-picker--open");

    var label = new Label($"Replace {existingData.displayName}?");
    label.AddToClassList("consumable-confirm-label");
    _iconsContainer.Add(label);

    var confirmBtn = new Button(() => ConfirmEnvReplace(newType));
    confirmBtn.text = "Replace";
    confirmBtn.AddToClassList("consumable-confirm-btn");
    _iconsContainer.Add(confirmBtn);

    var cancelBtn = new Button(CancelEnvConfirm);
    cancelBtn.text = "Cancel";
    cancelBtn.AddToClassList("consumable-confirm-cancel-btn");
    _iconsContainer.Add(cancelBtn);
}

private void ConfirmEnvReplace(ConsumableType newType)
{
    _pendingEnvConfirmType = null;
    _pickerContainer.RemoveFromClassList("consumable-picker--open");
    if (ConsumableManager.Instance != null &&
        ConsumableManager.Instance.ApplyToEnvironment(newType, ActiveEnv))
    {
        isometricView?.SpawnEnvConsumableVisual(newType);
        RefreshPickerIndicator();
    }
}

private void CancelEnvConfirm()
{
    _pendingEnvConfirmType = null;
    _pickerContainer.RemoveFromClassList("consumable-picker--open");
}
```

#### 4c — Wire up confirmation in `OnConsumableRowTapped`

Replace the entire `if (isEnvironmentScoped)` block in `OnConsumableRowTapped()` (lines 209-218):

Old:
```csharp
if (isEnvironmentScoped)
{
    // Apply immediately to entire active environment — no slot selection needed
    if (ConsumableManager.Instance != null &&
        ConsumableManager.Instance.ApplyToEnvironment(type, ActiveEnv))
    {
        isometricView?.SpawnEnvConsumableVisual(type);
    }
    return;
}
```

New:
```csharp
if (isEnvironmentScoped)
{
    var existingList = ConsumableManager.Instance?.GetEnvConsumables(ActiveEnv);
    if (existingList != null && existingList.Count > 0)
    {
        ShowEnvReplaceConfirmation(type, existingList[0]);
        return;
    }
    if (ConsumableManager.Instance != null &&
        ConsumableManager.Instance.ApplyToEnvironment(type, ActiveEnv))
    {
        isometricView?.SpawnEnvConsumableVisual(type);
        RefreshPickerIndicator();
    }
    return;
}
```

#### 4d — Guard `ToggleDropdown` against pending confirmation

In `ToggleDropdown()`, add a guard for `_pendingEnvConfirmType` alongside the existing `_pendingType` guard (line 161):

Old:
```csharp
private void ToggleDropdown()
{
    if (_pendingType.HasValue)
    {
        CancelApplyMode();
        return;
    }
```

New:
```csharp
private void ToggleDropdown()
{
    if (_pendingEnvConfirmType.HasValue)
    {
        CancelEnvConfirm();
        return;
    }
    if (_pendingType.HasValue)
    {
        CancelApplyMode();
        return;
    }
```

#### 4e — Refresh indicator on init and env switch

In `Initialize()`, add a call to `RefreshPickerIndicator()` after `BuildConsumablePicker()` (line 54):

```csharp
BuildConsumablePicker();
RefreshPickerIndicator();   // ← add this line
RestoreConsumableVisuals(ActiveEnv);
```

In `RebuildForEnvironment()`, add a call to `RefreshPickerIndicator()` at the end of the method (after `UpdateTitle()`):

```csharp
BuildSlotsForEnv(envIndex);
RestoreConsumableVisuals(envIndex);
RefreshAllSlots();
UpdateTitle();
RefreshPickerIndicator();   // ← add this line
```

**Step 5: Verify in Play mode**

1. Apply an env consumable (Fan) → herb button should show the highlight ring.
2. Open the picker again → tap a different consumable (Cloud) → confirmation "Replace Fan?" appears inline.
3. Tap Cancel → picker closes, Fan remains, ring stays.
4. Repeat → tap Replace → Cloud spawns, Fan GO is gone, ring stays (Cloud is now active).
5. Switch environments → ring is absent on a clean env, present on the one with Cloud.

**Step 6: Commit**

```bash
git add Assets/Scripts/UI/BackyardViewUI.cs Assets/UI/Styles/Backyard.uss
git commit -m "feat: inline confirmation and active indicator for env consumable replacement"
```
