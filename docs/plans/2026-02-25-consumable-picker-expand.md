# Consumable Picker Expand-Downward Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Replace the consumable picker's left-side text dropdown with a pill container that expands downward showing icons for each consumable in stock.

**Architecture:** A single `VisualElement` container holds the herb button at top and an icon list below. `overflow: hidden` clips the icon list when collapsed; adding `.consumable-picker--open` raises `max-height` via USS transition to reveal icons. No structural UXML changes needed — both elements are built procedurally in `BackyardViewUI.cs`.

**Tech Stack:** Unity UIToolkit USS transitions (max-height), C# UIElements API

---

### Task 1: Rewrite USS — replace dropdown styles with pill container styles

**Files:**
- Modify: `Assets/UI/Styles/Backyard.uss`

**Step 1: Replace the consumable block**

Find and delete these classes in `Backyard.uss`:
- `.consumable-picker-btn` (lines 52–67)
- `.consumable-picker-btn:hover` (lines 69–71)
- `.consumable-dropdown` (lines 73–84)
- `.consumable-row` (lines 86–93)
- `.consumable-row:hover` (lines 95–98)
- `.consumable-row-name` (lines 100–104)
- `.consumable-row-count` (lines 106–108)

Replace with:

```css
/* Consumable picker — pill container that expands downward */
.consumable-picker {
    position: absolute;
    right: 8px;
    top: 50%;
    translate: 0 -22px;
    width: 44px;
    max-height: 44px;
    overflow: hidden;
    background-color: rgba(20, 35, 50, 0.75);
    border-width: 1px;
    border-color: var(--color-highlight);
    border-top-left-radius: 22px;
    border-top-right-radius: 22px;
    border-bottom-left-radius: 22px;
    border-bottom-right-radius: 22px;
    transition-property: max-height, border-bottom-left-radius, border-bottom-right-radius;
    transition-duration: 0.25s;
    transition-timing-function: ease-out;
}

.consumable-picker--open {
    max-height: 300px;
    border-bottom-left-radius: 12px;
    border-bottom-right-radius: 12px;
}

/* Herb icon button — sits at top of container, transparent bg (container provides it) */
.consumable-picker-btn {
    width: 44px;
    height: 44px;
    flex-shrink: 0;
    background-color: rgba(0, 0, 0, 0);
    border-width: 0;
    padding: 8px;
    -unity-background-image-tint-color: rgb(185, 210, 225);
    background-image: url('../../ThirdParty/icons/delapouite-herbs-bundle.png');
    -unity-background-scale-mode: scale-to-fit;
}

.consumable-picker-btn:hover {
    -unity-background-image-tint-color: rgb(220, 238, 245);
}

/* Icon list section below the herb button */
.consumable-picker-icons {
    flex-direction: column;
    align-items: center;
    padding-bottom: 6px;
}

/* Each consumable icon button */
.consumable-icon-btn {
    width: 36px;
    height: 36px;
    border-radius: 8px;
    background-color: rgba(0, 0, 0, 0);
    border-width: 0;
    padding: 6px;
    margin-top: 2px;
    -unity-background-scale-mode: scale-to-fit;
    -unity-background-image-tint-color: rgb(185, 210, 225);
}

.consumable-icon-btn:hover {
    background-color: rgba(255, 255, 255, 0.10);
    -unity-background-image-tint-color: rgb(220, 238, 245);
}

/* Count badge overlaid at bottom-right of each icon button */
.consumable-icon-badge {
    position: absolute;
    right: 1px;
    bottom: 1px;
    color: var(--color-text-dim);
    font-size: 8px;
    -unity-text-align: lower-right;
}
```

**Step 2: Verify in Editor**

Open Unity — the project should compile with no errors. The existing herb button may disappear temporarily (C# still references old class name structure); that's expected until Task 2.

**Step 3: Commit**

```bash
git add Assets/UI/Styles/Backyard.uss
git commit -m "style: replace consumable dropdown styles with expand-pill styles"
```

---

### Task 2: Rewrite BuildConsumablePicker() in BackyardViewUI.cs

**Files:**
- Modify: `Assets/Scripts/UI/BackyardViewUI.cs`

**Step 1: Update fields**

Find these field declarations near the top of the class (around line 24–26):

```csharp
private Button _pickerBtn;
private VisualElement _dropdown;
private ConsumableType? _pendingType; // only set for slot-scoped apply mode
```

Replace with:

```csharp
private VisualElement _pickerContainer;
private Button _pickerBtn;
private VisualElement _iconsContainer;
private ConsumableType? _pendingType; // only set for slot-scoped apply mode
```

**Step 2: Rewrite BuildConsumablePicker()**

Find the method `BuildConsumablePicker()` (around line 122):

```csharp
private void BuildConsumablePicker()
{
    _pickerBtn = new Button(ToggleDropdown);
    _pickerBtn.text = "";
    _pickerBtn.AddToClassList("consumable-picker-btn");
    terrariumPage.Add(_pickerBtn);

    _dropdown = new VisualElement();
    _dropdown.AddToClassList("consumable-dropdown");
    _dropdown.style.display = DisplayStyle.None;
    terrariumPage.Add(_dropdown);
}
```

Replace with:

```csharp
private void BuildConsumablePicker()
{
    _pickerContainer = new VisualElement();
    _pickerContainer.AddToClassList("consumable-picker");
    terrariumPage.Add(_pickerContainer);

    _pickerBtn = new Button(ToggleDropdown);
    _pickerBtn.text = "";
    _pickerBtn.AddToClassList("consumable-picker-btn");
    _pickerContainer.Add(_pickerBtn);

    _iconsContainer = new VisualElement();
    _iconsContainer.AddToClassList("consumable-picker-icons");
    _pickerContainer.Add(_iconsContainer);
}
```

**Step 3: Verify compile**

Check Unity console — no errors. The herb button should now be visible in the Terrarium page, positioned at center-right.

**Step 4: Commit**

```bash
git add Assets/Scripts/UI/BackyardViewUI.cs
git commit -m "refactor: rebuild consumable picker as single pill container"
```

---

### Task 3: Replace ToggleDropdown() and RefreshDropdown() with icon-based versions

**Files:**
- Modify: `Assets/Scripts/UI/BackyardViewUI.cs`

**Step 1: Rewrite ToggleDropdown()**

Find `ToggleDropdown()` (around line 135):

```csharp
private void ToggleDropdown()
{
    if (_pendingType.HasValue)
    {
        CancelApplyMode();
        return;
    }

    bool showing = _dropdown.style.display == DisplayStyle.Flex;
    if (showing)
    {
        _dropdown.style.display = DisplayStyle.None;
        return;
    }

    RefreshDropdown();
    _dropdown.style.display = DisplayStyle.Flex;
}
```

Replace with:

```csharp
private void ToggleDropdown()
{
    if (_pendingType.HasValue)
    {
        CancelApplyMode();
        return;
    }

    bool open = _pickerContainer.ClassListContains("consumable-picker--open");
    if (open)
    {
        _pickerContainer.RemoveFromClassList("consumable-picker--open");
        return;
    }

    RefreshIcons();
    _pickerContainer.AddToClassList("consumable-picker--open");
}
```

**Step 2: Rewrite RefreshDropdown() → RefreshIcons()**

Find `RefreshDropdown()` (around line 154):

```csharp
private void RefreshDropdown()
{
    _dropdown.Clear();
    if (ConsumableManager.Instance == null) return;

    foreach (var c in ConsumableManager.Instance.AllConsumables)
    {
        int count = ConsumableManager.Instance.GetCount(c.type);
        if (count <= 0) continue;

        var row = new Button();
        row.AddToClassList("consumable-row");

        var nameLabel = new Label(c.displayName);
        nameLabel.AddToClassList("consumable-row-name");

        var countLabel = new Label($"x{count}");
        countLabel.AddToClassList("consumable-row-count");

        row.Add(nameLabel);
        row.Add(countLabel);

        var capturedType = c.type;
        var capturedIsEnvScoped = c.isEnvironmentScoped;
        row.clicked += () => OnConsumableRowTapped(capturedType, capturedIsEnvScoped);

        _dropdown.Add(row);
    }

    if (_dropdown.childCount == 0)
    {
        var empty = new Label("No consumables owned");
        empty.AddToClassList("consumable-row-name");
        empty.style.paddingTop = empty.style.paddingBottom =
            empty.style.paddingLeft = empty.style.paddingRight = new StyleLength(8);
        _dropdown.Add(empty);
    }
}
```

Replace with:

```csharp
private void RefreshIcons()
{
    _iconsContainer.Clear();
    if (ConsumableManager.Instance == null) return;

    foreach (var c in ConsumableManager.Instance.AllConsumables)
    {
        int count = ConsumableManager.Instance.GetCount(c.type);
        if (count <= 0) continue;

        var btn = new Button();
        btn.AddToClassList("consumable-icon-btn");
        if (c.icon != null)
            btn.style.backgroundImage = new StyleBackground(c.icon);

        var badge = new Label($"x{count}");
        badge.AddToClassList("consumable-icon-badge");
        btn.Add(badge);

        var capturedType = c.type;
        var capturedIsEnvScoped = c.isEnvironmentScoped;
        btn.clicked += () => OnConsumableRowTapped(capturedType, capturedIsEnvScoped);

        _iconsContainer.Add(btn);
    }
}
```

**Step 3: Update OnConsumableRowTapped() to close the picker**

Find the line in `OnConsumableRowTapped()` (around line 195):

```csharp
_dropdown.style.display = DisplayStyle.None;
```

Replace with:

```csharp
_pickerContainer.RemoveFromClassList("consumable-picker--open");
```

**Step 4: Verify in Editor**

- Enter Play mode in the Terrarium tab
- Tap the herb button → picker should expand downward showing icon buttons for each owned consumable
- Tap an icon → picker should collapse and apply the consumable
- Tap herb button again when open → should collapse

**Step 5: Commit**

```bash
git add Assets/Scripts/UI/BackyardViewUI.cs
git commit -m "feat: consumable picker expands downward with icon buttons"
```
