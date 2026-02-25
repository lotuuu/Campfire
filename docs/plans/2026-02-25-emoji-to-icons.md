# Emoji to Icons Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Replace all 3 emoji characters in the UI with PNG icon assets from the existing ThirdParty icon library.

**Architecture:** Each emoji is cleared from its text/label and replaced with a `background-image` set via USS on the element itself. No extra child VisualElements needed — UI Toolkit renders `background-image` on any element with `-unity-background-scale-mode: scale-to-fit`. The construction badge uses a CSS modifier class added in C# instead of setting text.

**Tech Stack:** Unity UI Toolkit (USS, UXML, C#), ThirdParty icons at `Assets/ThirdParty/icons/`

---

### Task 1: Debug toggle button — ⚙ → gear icon

**Files:**
- Modify: `Assets/UI/Documents/GardenRoot.uxml:23`
- Modify: `Assets/UI/Styles/Debug.uss:1-15`

**Step 1: Clear the emoji text in UXML**

In `GardenRoot.uxml` line 23, change:
```xml
<ui:Button name="debug-toggle" text="⚙" class="btn-debug-toggle" />
```
to:
```xml
<ui:Button name="debug-toggle" text="" class="btn-debug-toggle" />
```

**Step 2: Add background-image to Debug.uss**

In `Debug.uss`, update `.btn-debug-toggle` to add:
```css
.btn-debug-toggle {
    width: 72px;
    height: 72px;
    min-height: 72px;
    border-radius: var(--radius-sm);
    background-color: rgba(10, 20, 30, 0.45);
    border-width: 1px;
    border-color: rgba(120, 200, 220, 0.20);
    -unity-background-image-tint-color: rgb(185, 210, 225);
    background-image: url('../../ThirdParty/icons/darkzaitzev-big-gear.png');
    -unity-background-scale-mode: scale-to-fit;
    padding: 12px;
    margin-right: var(--spacing-sm);
}

.btn-debug-toggle:hover {
    background-color: rgba(20, 40, 55, 0.65);
    -unity-background-image-tint-color: rgb(220, 238, 245);
}
```

(Remove the old `color`, `font-size`, and `-unity-text-align` properties — they have no effect with empty text and just clutter the rule.)

**Step 3: Verify in Unity**

Open Unity, enter Play mode (or just inspect the top bar in the scene view). The debug toggle should display a gear icon instead of ⚙.

**Step 4: Commit**

```bash
git add Assets/UI/Documents/GardenRoot.uxml Assets/UI/Styles/Debug.uss
git commit -m "feat: replace debug toggle emoji with gear icon"
```

---

### Task 2: Consumable picker button — 🌿 → herb icon

**Files:**
- Modify: `Assets/Scripts/UI/BackyardViewUI.cs:125`
- Modify: `Assets/UI/Styles/Backyard.uss:52-66`

**Step 1: Clear the emoji text in C#**

In `BackyardViewUI.cs` line 125, change:
```csharp
_pickerBtn.text = "🌿";
```
to:
```csharp
_pickerBtn.text = "";
```

**Step 2: Add background-image to Backyard.uss**

Update `.consumable-picker-btn` in `Backyard.uss`:
```css
.consumable-picker-btn {
    position: absolute;
    right: 8px;
    top: 50%;
    width: 44px;
    height: 44px;
    border-radius: 22px;
    background-color: rgba(20, 35, 50, 0.75);
    border-width: 1px;
    border-color: var(--color-highlight);
    -unity-background-image-tint-color: rgb(185, 210, 225);
    background-image: url('../../ThirdParty/icons/delapouite-herbs-bundle.png');
    -unity-background-scale-mode: scale-to-fit;
    padding: 8px;
    translate: 0 -22px;
}
```

(Remove `color`, `font-size`, `-unity-text-align` — no longer needed.)

**Step 3: Verify in Unity**

Enter Play mode, navigate to the Terrarium page. The circular picker button should show a herb icon instead of 🌿.

**Step 4: Commit**

```bash
git add Assets/Scripts/UI/BackyardViewUI.cs Assets/UI/Styles/Backyard.uss
git commit -m "feat: replace consumable picker emoji with herb icon"
```

---

### Task 3: Construction badge — ✓ → check-mark icon

**Files:**
- Modify: `Assets/Scripts/UI/ConstructionUI.cs:59,136`
- Modify: `Assets/UI/Styles/Construction.uss:32-35`

**Step 1: Add CSS modifier class in C# instead of setting text**

In `ConstructionUI.cs`, find the two lines that set `badge.text = "✓"` (lines 59 and 136). Replace both occurrences:

```csharp
// Before:
if (badge != null) badge.text = "✓";

// After:
if (badge != null) badge.AddToClassList("construction-card-badge--unlocked");
```

Note: `RefreshDisplay()` re-clones the card template each time, so there is no need to remove the class — each render starts fresh.

**Step 2: Update Construction.uss**

Replace the `.construction-card-badge` block and add the modifier:
```css
.construction-card-badge {
    width: 28px;
    height: 28px;
    -unity-background-scale-mode: scale-to-fit;
}

.construction-card-badge--unlocked {
    background-image: url('../../ThirdParty/icons/delapouite-check-mark.png');
    -unity-background-image-tint-color: rgb(100, 230, 230);
}
```

(The old `font-size` and `color` properties are removed since the badge no longer contains text.)

**Step 3: Verify in Unity**

Enter Play mode, navigate to the Construction tab. Unlocked locations (and the Greenhouse, which is always unlocked) should show a teal check-mark icon in their card header instead of ✓.

**Step 4: Commit**

```bash
git add Assets/Scripts/UI/ConstructionUI.cs Assets/UI/Styles/Construction.uss
git commit -m "feat: replace construction badge checkmark emoji with icon"
```
