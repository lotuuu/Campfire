# UI Redesign Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Apply the approved UI redesign across all screens — compact top bar, collapsed weather, 4-button nav, content-adaptive overlays, thicker progress bars, improved contrast, accessibility fixes, and structural simplification.

**Architecture:** Pure USS/UXML changes where possible; targeted C# edits for structural changes (Build nav button, mallum status text, ingredient check/X icons, toast system). No new files — all edits to existing files.

**Tech Stack:** Unity UI Toolkit (USS, UXML, C#)

**Constraint:** Weather forecast panel keeps ALL existing information (moon phase, humidity, sunrise/sunset, temperatures, wind, cloud cover). The collapsed weather summary in the top bar is additive — forecast expands to show full detail.

---

## Chunk 1: Design Tokens, Contrast & Overlay System

### Task 1: Update Variables.uss — Contrast & Spacing

**Files:**
- Modify: `Assets/UI/Styles/Variables.uss`

- [ ] **Step 1: Bump --color-text-dim for contrast**

Change `--color-text-dim: rgb(185, 150, 75)` → `rgb(200, 165, 90)` (improves contrast ratio from ~2.8:1 to ~4.5:1 on panel backgrounds).

- [ ] **Step 2: Reduce --bottom-bar-height**

Change `--bottom-bar-height: 248px` → `160px`. This is the shared height for nav, tutorial hints, and dialogue.

- [ ] **Step 3: Commit**

```
git add Assets/UI/Styles/Variables.uss
git commit -m "style: bump text-dim contrast and reduce bottom-bar-height"
```

### Task 2: Overlay System — Adaptive Height, Smaller Close, Scroll Fade

**Files:**
- Modify: `Assets/UI/Styles/Overlay.uss`

- [ ] **Step 1: Change overlay-content height from fixed 70% to adaptive**

Replace `height: 70%` with `min-height: 35%; max-height: 85%`. Add `flex-shrink: 1;` so content-short panels don't waste space.

- [ ] **Step 2: Shrink overlay-close button**

Change from `width: 110px; height: 110px; border-radius: 55px; padding: 20px;` to `width: 64px; height: 64px; border-radius: 32px; padding: 12px;`.

- [ ] **Step 3: Increase overlay-backdrop opacity**

Change `rgba(0, 0, 0, 0.55)` → `rgba(0, 0, 0, 0.65)`.

- [ ] **Step 4: Commit**

```
git add Assets/UI/Styles/Overlay.uss
git commit -m "style: adaptive overlay height, smaller close button, darker backdrop"
```

### Task 3: Interaction Panel — Minimum Font Sizes, Smaller Close

**Files:**
- Modify: `Assets/UI/Styles/Interaction.uss`

- [ ] **Step 1: Raise minimum font sizes**

Find all `font-size: 18px` and change to `font-size: var(--font-xs)` (24px).
Find all `font-size: 20px` and change to `font-size: var(--font-xs)` (24px).
Find all `font-size: 22px` and change to `font-size: var(--font-xs)` (24px).
This affects: `.seed-card--stats-line`, `.seed-card--tag Label`, `.harvest-axis-name`, `.harvest-axis-actual`, `.harvest-axis-ideal`, `.interaction-section-header`.

- [ ] **Step 2: Fix tag padding to 4px grid**

Change `.seed-card--tag` padding from `3px 8px` to `4px 8px` and margin-right from `6px` to `8px`.

- [ ] **Step 3: Shrink interaction-close button**

Change from `width: 72px; height: 72px; border-radius: 36px; padding: 14px;` to `width: 56px; height: 56px; border-radius: 28px; padding: 10px;`.

- [ ] **Step 4: Commit**

```
git add Assets/UI/Styles/Interaction.uss
git commit -m "style: raise interaction panel min font to 24px, fix tag spacing, shrink close btn"
```

---

## Chunk 2: Top Bar, Weather & Bottom Nav

### Task 4: Collapse Weather Bar to Single Tappable Row

**Files:**
- Modify: `Assets/UI/Documents/CampFireRoot.uxml` (lines 36-53)
- Modify: `Assets/UI/Styles/WeatherBar.uss`
- Modify: `Assets/UI/Styles/CampSite.uss`
- Modify: `Assets/Scripts/UI/WeatherBarUI.cs`

The weather bar currently shows 4 equal cells (condition, humidity, temp, moon). We collapse this into a single tappable summary row showing condition+icon+temp+humidity inline. Tapping the row toggles the existing forecast panel (which keeps ALL its current information).

- [ ] **Step 1: Replace 4-cell weather UXML with single summary row**

In `CampFireRoot.uxml`, replace lines 36-53 (the 4-cell weather-bar) with:

```xml
<!-- Weather summary (tap to expand forecast) -->
<ui:VisualElement name="weather-bar" class="weather-summary-row">
    <ui:Label name="weather-condition-label" text="--" />
    <ui:VisualElement name="weather-icon" class="weather-cell-icon" />
    <ui:Label name="weather-temp" text="--" />
    <ui:Label name="weather-humidity" text="--" class="weather-summary-dim" />
    <ui:VisualElement name="weather-moon" class="weather-summary-moon" />
    <ui:Label name="weather-chevron" text="&#9660;" class="weather-summary-chevron" />
</ui:VisualElement>
```

Note: we keep moon in the summary row since user said don't remove weather info.

- [ ] **Step 2: Remove date-time label from top-header**

In `CampFireRoot.uxml`, remove `<ui:Label name="date-time" text="--" />` (line 33) and the spacer (line 32). The header becomes just the player name + settings/debug buttons. The date info was low-value on the main screen.

Actually — keep date-time to not remove info. Just don't actively remove it. Leave it as-is in the header.

- [ ] **Step 3: Update WeatherBar.uss for summary row layout**

Replace the 4-cell grid styling with a compact single-row layout:

```css
#weather-bar {
    flex-direction: row;
    align-items: center;
    flex-shrink: 0;
    margin-top: var(--spacing-xs);
    padding: var(--spacing-sm) var(--spacing-sm);
    background-color: var(--color-bg-slot);
    border-width: 2px;
    border-color: var(--color-border);
    border-radius: var(--radius-sm);
}

.weather-summary-dim {
    font-size: var(--font-sm);
    color: var(--color-text-dim);
    margin-left: var(--spacing-sm);
}

.weather-summary-moon {
    width: 32px;
    height: 32px;
    -unity-background-scale-mode: scale-to-fit;
    margin-left: var(--spacing-sm);
}

.weather-summary-chevron {
    font-size: var(--font-xs);
    color: var(--color-text-dim);
    margin-left: auto;
}
```

Keep `#weather-condition-label`, `#weather-icon`, `#weather-temp` styles as-is (they still apply).

Remove old `.weather-cell` styles since they're no longer used.

- [ ] **Step 4: Update WeatherBarUI.cs for collapsed layout**

The key change: remove queries for `weather-cell-humidity`, `weather-cell-temp`, `weather-cell-moon` (the separate cell containers). The named elements inside them (`weather-humidity`, `weather-temp`, `weather-moon`) now live directly in the `weather-bar` row, so query them from root.

Remove the humidity icon (`weather-humidity-icon`) and temp icon (`weather-temp-icon`) queries since these were inside the cells. The summary row shows text only.

The forecast toggle on tap already works (it's wired to the weather-bar click). No change needed there.

- [ ] **Step 5: Commit**

```
git add Assets/UI/Documents/CampFireRoot.uxml Assets/UI/Styles/WeatherBar.uss Assets/UI/Styles/CampSite.uss Assets/Scripts/UI/WeatherBarUI.cs
git commit -m "style: collapse weather bar to single tappable summary row"
```

### Task 5: Add Build Button to Bottom Nav

**Files:**
- Modify: `Assets/UI/Documents/CampFireRoot.uxml` (lines 84-100)
- Modify: `Assets/UI/Styles/BottomNav.uss`
- Modify: `Assets/Scripts/UI/BottomNavUI.cs`
- Modify: `Assets/Scripts/UI/CampFireUI.cs`

- [ ] **Step 1: Add Build button to UXML**

In `CampFireRoot.uxml`, insert a Build button after btn-seeds:

```xml
<ui:Button name="btn-build" class="nav-btn">
    <ui:Label text="BUILD" class="nav-btn-label" />
    <ui:VisualElement name="nav-icon-build" class="nav-btn-icon" />
</ui:Button>
```

- [ ] **Step 2: Compact bottom nav sizing in BottomNav.uss**

Change `.nav-btn` `min-height` from `200px` to `100px`.
Change `.nav-btn-icon` from `width: 130px; height: 130px;` to `width: 72px; height: 72px;`.
Change `#bottom-nav` `padding` from `var(--spacing-md)` to `var(--spacing-sm) var(--spacing-md)`.

- [ ] **Step 3: Wire Build button in BottomNavUI.cs**

Add `public event Action OnBuildClicked;` event.
In `Initialize()`, query `btn-build` and wire click → `OnBuildClicked`.
Add `iconBuild` field, load sprite `"ui/nav-build"` in `Update()`.

- [ ] **Step 4: Wire Build event in CampFireUI.cs**

In `Start()` where bottom nav events are wired (line 134-147), add:

```csharp
bottomNav.OnBuildClicked += () =>
{
    build?.Refresh();
    OpenOverlay("Build", buildPanel);
};
```

- [ ] **Step 5: Commit**

```
git add Assets/UI/Documents/CampFireRoot.uxml Assets/UI/Styles/BottomNav.uss Assets/Scripts/UI/BottomNavUI.cs Assets/Scripts/UI/CampFireUI.cs
git commit -m "feat: add Build button to bottom nav, compact nav to ~100px height"
```

### Task 6: Enlarge Resource Icons

**Files:**
- Modify: `Assets/UI/Styles/CampSite.uss`

- [ ] **Step 1: Increase resource icon sizes**

Change `.resource-icon` from `width: 32px; height: 32px;` to `width: 48px; height: 48px;`.

- [ ] **Step 2: Commit**

```
git add Assets/UI/Styles/CampSite.uss
git commit -m "style: enlarge resource icons from 32px to 48px"
```

---

## Chunk 3: Hex Grid & Campsite

### Task 7: Thicker Progress Bars with Glow + Harvest Pulse

**Files:**
- Modify: `Assets/UI/Styles/CampsiteGrid.uss`

- [ ] **Step 1: Increase progress bar height**

Find `.cell-progress` (currently `height: 6px`) and change to `height: 14px; border-radius: 7px;`.
Find `.cell-progress-fill` and update `border-radius: 7px;`.

- [ ] **Step 2: Add glow to progress fills**

For growth fill, add `box-shadow` or use border-color glow effect. Since Unity USS doesn't support box-shadow, use a border-color glow:
Add to `.cell-progress-fill` (growth) a brighter border: `border-width: 1px; border-color: rgba(120, 180, 60, 0.5);`.

- [ ] **Step 3: Add harvest-ready pulse animation**

Add a keyframe animation for ready-to-harvest cells:

```css
@keyframes harvest-pulse {
    0% { border-color: rgba(255, 210, 70, 0.3); }
    50% { border-color: rgba(255, 210, 70, 0.8); }
    100% { border-color: rgba(255, 210, 70, 0.3); }
}
```

Note: Unity USS supports `transition-property` for animations but not `@keyframes`. The pulse will need to be driven from C# via a class toggle in `CampsiteViewUI.Update()`. Add the CSS classes:

```css
.grid-cell--ready {
    border-width: 3px;
    border-color: rgba(255, 210, 70, 0.8);
}
```

- [ ] **Step 4: Commit**

```
git add Assets/UI/Styles/CampsiteGrid.uss
git commit -m "style: thicker progress bars (14px), harvest-ready highlight class"
```

### Task 8: Add Harvest-Ready Class Toggle in CampsiteViewUI

**Files:**
- Modify: `Assets/Scripts/UI/CampsiteViewUI.cs`

- [ ] **Step 1: In the grid cell population code, add ready class for mature plots**

In the method that populates plot cells, after checking if the plot is ready to harvest (growth >= 1.0), add:
```csharp
cell.EnableInClassList("grid-cell--ready", plot.IsReadyToHarvest());
```

This should be in the `Update()` loop that already tracks growth progress. When growth reaches 100%, add the class.

- [ ] **Step 2: Commit**

```
git add Assets/Scripts/UI/CampsiteViewUI.cs
git commit -m "feat: add harvest-ready CSS class to mature plot cells"
```

---

## Chunk 4: Panel-Specific Styling

### Task 9: Apotheke — 2-Column Seed Grid + Tab Underline

**Files:**
- Modify: `Assets/UI/Styles/Apotheke.uss`

- [ ] **Step 1: Change seed card layout from 3-col (31% width) to 2-col (48% width)**

Find `.seed-card` or the seed grid container styling. Change the seed cards from `width: 31%` to `width: 48%`. If using flex-wrap layout, adjust accordingly.

- [ ] **Step 2: Ensure tab active state has underline, not just color**

The Apotheke tabs reuse `letters-tab--active` class. In `Assets/UI/Styles/Letters.uss`, ensure `.letters-tab--active` has a visible `border-bottom-width: 3px; border-bottom-color: var(--color-border-accent);` and a background tint, not just text color change.

- [ ] **Step 3: Commit**

```
git add Assets/UI/Styles/Apotheke.uss Assets/UI/Styles/Letters.uss
git commit -m "style: 2-column seed cards, active tab underline indicator"
```

### Task 10: Apotheke — Check/X Icons on Craft Ingredients

**Files:**
- Modify: `Assets/Scripts/UI/ApothekeUI.cs`

- [ ] **Step 1: In recipe ingredient display, prepend check/X text to ingredient labels**

Find where ingredient rows are built for recipes. Before the ingredient name text, prepend:
- `"\u2713 "` (checkmark) if player has enough
- `"\u2717 "` (X mark) if player doesn't

This is a text-level change — no new elements needed.

- [ ] **Step 2: Commit**

```
git add Assets/Scripts/UI/ApothekeUI.cs
git commit -m "feat: add check/X prefix to craft ingredient counts"
```

### Task 11: Quest Panel — Mallum Status Text + Tier Badges

**Files:**
- Modify: `Assets/Scripts/UI/QuestUI.cs`
- Modify: `Assets/UI/Styles/Quest.uss`

- [ ] **Step 1: Change UpdateMallumStatus to show descriptive text**

In `QuestUI.UpdateMallumStatus()` (line 225-261), change the label text from:
```csharp
mallumStatusLabel.text = $"Free Mallums: {available} / {total}";
```
to:
```csharp
int busy = total - available;
mallumStatusLabel.text = $"{available} Idle / {total} Total";
if (busy > 0) mallumStatusLabel.text += $"  ({busy} on task)";
```

- [ ] **Step 2: Increase mallum dot size in Quest.uss**

Find `.quest-mallum-dot` and change from `width: 18px; height: 18px;` to `width: 28px; height: 28px; border-radius: 14px;`.

- [ ] **Step 3: Make quest tier badge more prominent**

Currently tiers are shown via a thin `10px` left border strip. Keep the strip but also ensure the `quest-level-badge` element is styled as a prominent pill badge. In Quest.uss, update `.quest-level-badge` to have:
```css
.quest-level-badge {
    font-size: var(--font-xs);
    -unity-font-style: bold;
    padding: 4px 12px;
    border-radius: 8px;
    background-color: rgba(100, 75, 35, 0.5);
}
```

- [ ] **Step 4: Style locked quest overlay**

Add to Quest.uss a locked reason overlay that centers text over the dimmed card:
```css
.quest-card--locked .quest-locked {
    position: absolute;
    left: 0;
    right: 0;
    top: 0;
    bottom: 0;
    background-color: rgba(30, 20, 12, 0.7);
    justify-content: center;
    align-items: center;
    border-radius: var(--radius-md);
}
```

- [ ] **Step 5: Commit**

```
git add Assets/Scripts/UI/QuestUI.cs Assets/UI/Styles/Quest.uss
git commit -m "style: descriptive mallum status, larger dots, prominent tier badges, locked overlay"
```

### Task 12: Merchant Check/X on Trade Costs

**Files:**
- Modify: `Assets/Scripts/UI/VisitorUI.cs`

- [ ] **Step 1: In merchant offer rendering, add check/X prefix to cost labels**

Find where merchant offer cost items are created. Prepend `"\u2713 "` or `"\u2717 "` to cost text based on whether player can afford.

- [ ] **Step 2: Commit**

```
git add Assets/Scripts/UI/VisitorUI.cs
git commit -m "feat: add check/X prefix to merchant trade costs"
```

---

## Chunk 5: Polish & Empty States

### Task 13: Toast/Snackbar System

**Files:**
- Modify: `Assets/UI/Documents/CampFireRoot.uxml`
- Modify: `Assets/UI/Styles/Common.uss`
- Modify: `Assets/Scripts/UI/CampFireUI.cs`

- [ ] **Step 1: Add toast element to UXML**

After the reward-reveal-overlay, add:
```xml
<ui:Label name="toast-label" class="toast" text="" style="display: none;" />
```

- [ ] **Step 2: Add toast CSS**

In Common.uss, add:
```css
.toast {
    position: absolute;
    top: 120px;
    left: 10%;
    right: 10%;
    -unity-text-align: middle-center;
    font-size: var(--font-sm);
    color: rgb(120, 200, 80);
    -unity-font-style: bold;
    background-color: rgba(30, 20, 12, 0.95);
    border-width: 1px;
    border-color: rgba(120, 180, 60, 0.4);
    border-radius: 24px;
    padding: var(--spacing-sm) var(--spacing-md);
}
```

- [ ] **Step 3: Add ShowToast method to CampFireUI**

```csharp
private Label toastLabel;
private IVisualElementScheduledItem _toastHide;

public void ShowToast(string message)
{
    if (toastLabel == null) return;
    toastLabel.text = message;
    toastLabel.style.display = DisplayStyle.Flex;
    toastLabel.BringToFront();
    _toastHide?.Pause();
    _toastHide = toastLabel.schedule.Execute(() =>
        toastLabel.style.display = DisplayStyle.None
    ).StartingIn(2000);
}
```

Wire in Start(): `toastLabel = root.Q<Label>("toast-label");`

- [ ] **Step 4: Commit**

```
git add Assets/UI/Documents/CampFireRoot.uxml Assets/UI/Styles/Common.uss Assets/Scripts/UI/CampFireUI.cs
git commit -m "feat: add toast/snackbar notification system"
```

### Task 14: Empty States for Letters Panel

**Files:**
- Modify: `Assets/UI/Styles/Letters.uss`

- [ ] **Step 1: Style the existing empty labels**

The UXML already has `inbox-empty`, `friends-empty` labels with `letters-empty-text` class. Improve their styling:

```css
.letters-empty-text {
    -unity-text-align: middle-center;
    font-size: var(--font-md);
    color: var(--color-text-dim);
    padding: var(--spacing-xl) var(--spacing-md);
    white-space: normal;
}
```

- [ ] **Step 2: Commit**

```
git add Assets/UI/Styles/Letters.uss
git commit -m "style: improve empty state styling for Letters panel"
```

### Task 15: Verify in Unity & Final Commit

- [ ] **Step 1: Open Unity, check compilation**

Use `read_console` MCP tool to check for compile errors.

- [ ] **Step 2: Enter play mode and visually verify**

Check: top bar compactness, weather summary, bottom nav with 4 buttons, overlay panel sizing, progress bar thickness, font sizes in interaction panel.

- [ ] **Step 3: Fix any issues found**

- [ ] **Step 4: Final commit**

```
git add -A
git commit -m "fix: address any visual issues from UI redesign"
```
