# Glassmorphism UI Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Replace the opaque dark-green UI theme with faux glassmorphism using cool teal/blue-green semi-transparent panels across all UI elements.

**Architecture:** Pure USS variable and style changes. The color palette in Variables.uss drives most elements via CSS custom properties. Component-specific hardcoded colors in HUD.uss, Satchel.uss, Codex.uss, Greenhouse.uss, and Debug.uss also need updating. Border widths drop from 2px to 1px and border-color transitions are added for hover glow.

**Tech Stack:** Unity UI Toolkit (USS stylesheets only)

**Design doc:** `docs/plans/2026-02-23-glassmorphism-ui-design.md`

---

### Task 1: Update Variables.uss — Full Palette Replacement

**Files:**
- Modify: `Assets/UI/Styles/Variables.uss`

**Step 1: Replace the entire Variables.uss color block**

Replace the `:root` color variables with the new teal glass palette. Keep spacing, radius, and font variables unchanged.

```css
:root {
    /* Colors - Glassmorphism teal-glass theme */
    --color-bg-dark: rgb(8, 15, 22);
    --color-bg-panel: rgba(15, 25, 35, 0.55);
    --color-bg-slot: rgba(20, 35, 50, 0.40);
    --color-bg-slot-hover: rgba(30, 50, 65, 0.55);
    --color-border: rgba(120, 200, 220, 0.15);
    --color-border-accent: rgba(140, 230, 240, 0.35);

    --color-text: rgb(200, 220, 230);
    --color-text-dim: rgb(120, 150, 170);
    --color-text-bright: rgb(230, 245, 255);
    --color-text-accent: rgb(100, 220, 220);

    --color-button-bg: rgba(20, 40, 55, 0.50);
    --color-button-bg-hover: rgba(30, 55, 70, 0.65);
    --color-button-bg-active: rgba(40, 70, 85, 0.75);
    --color-button-bg-disabled: rgba(15, 25, 35, 0.35);

    --color-highlight: rgb(255, 220, 80);
    --color-dim: rgb(100, 130, 150);
    --color-empty: rgba(30, 45, 60, 0.25);
    --color-unknown: rgba(15, 25, 35, 0.60);

    /* Spacing, radius, font sizes unchanged */
}
```

**Step 2: Visual check**

Enter Play mode in Unity. The HUD, buttons, and any visible panels should now show teal-tinted semi-transparent backgrounds instead of opaque green. Text should be cool white/cyan.

**Step 3: Commit**

```
git add Assets/UI/Styles/Variables.uss
git commit -m "style: replace green palette with teal glassmorphism variables"
```

---

### Task 2: Update Common.uss — Glass Panel and Button Styles

**Files:**
- Modify: `Assets/UI/Styles/Common.uss`

**Step 1: Update `.panel` class**

Add a 1px border and top-edge highlight for the frosted glass effect:

```css
.panel {
    position: absolute;
    left: 0;
    right: 0;
    top: 0;
    bottom: 0;
    background-color: var(--color-bg-panel);
    padding: var(--spacing-lg);
    padding-top: var(--spacing-xl);
    border-width: 1px;
    border-color: var(--color-border);
    border-top-color: rgba(140, 230, 240, 0.25);
}
```

**Step 2: Update `.btn` class**

Reduce border to 1px, add border-color to transitions:

```css
.btn {
    background-color: var(--color-button-bg);
    color: var(--color-text);
    border-width: 1px;
    border-color: var(--color-border-accent);
    border-radius: var(--radius-md);
    padding: var(--spacing-sm) var(--spacing-lg);
    font-size: var(--font-md);
    min-height: 88px;
    -unity-text-align: middle-center;
    transition-property: background-color, border-color;
    transition-duration: 0.2s;
}
```

**Step 3: Update `.btn:hover`**

Add brighter border on hover for glow effect:

```css
.btn:hover {
    background-color: var(--color-button-bg-hover);
    border-color: rgba(140, 230, 240, 0.5);
}
```

**Step 4: Update `.grid-item` class**

Reduce border to 1px, add border-color transition:

```css
.grid-item {
    background-color: var(--color-bg-slot);
    border-width: 1px;
    border-color: var(--color-border);
    border-radius: var(--radius-md);
    padding: var(--spacing-sm);
    margin: var(--spacing-xs);
    transition-property: background-color, border-color;
    transition-duration: 0.2s;
}
```

**Step 5: Update `.grid-item:hover`**

Add glow border on hover:

```css
.grid-item:hover {
    background-color: var(--color-bg-slot-hover);
    border-color: var(--color-border-accent);
}
```

**Step 6: Commit**

```
git add Assets/UI/Styles/Common.uss
git commit -m "style: glassmorphism panel, button, and grid-item styles"
```

---

### Task 3: Update HUD.uss — Glass HUD Elements

**Files:**
- Modify: `Assets/UI/Styles/HUD.uss`

**Step 1: Update `#weather-text`**

Glass chip style with border:

```css
#weather-text {
    color: var(--color-text);
    font-size: var(--font-sm);
    background-color: rgba(10, 20, 30, 0.45);
    border-radius: var(--radius-sm);
    padding: var(--spacing-sm) var(--spacing-md);
    border-width: 1px;
    border-color: rgba(120, 200, 220, 0.20);
    border-top-color: rgba(140, 230, 240, 0.30);
}
```

**Step 2: Update `#currency-panel`**

Matching glass chip:

```css
#currency-panel {
    align-items: flex-end;
    background-color: rgba(10, 20, 30, 0.45);
    border-radius: var(--radius-sm);
    padding: var(--spacing-sm) var(--spacing-md);
    border-width: 1px;
    border-color: rgba(120, 200, 220, 0.20);
    border-top-color: rgba(140, 230, 240, 0.30);
}
```

**Step 3: Update `#pulse-button`**

Glowing glass pill:

```css
#pulse-button {
    width: 400px;
    height: 110px;
    min-height: 110px;
    font-size: var(--font-lg);
    -unity-font-style: bold;
    background-color: rgba(20, 60, 70, 0.60);
    border-width: 2px;
    border-color: rgba(140, 230, 240, 0.40);
    border-top-color: rgba(160, 240, 250, 0.55);
    border-radius: 55px;
    margin-bottom: var(--spacing-md);
    transition-property: background-color, border-color, scale;
    transition-duration: 0.2s;
}

#pulse-button:hover {
    background-color: rgba(25, 75, 85, 0.70);
    border-color: rgba(140, 230, 240, 0.60);
    scale: 1.03;
}

#pulse-button:active {
    background-color: rgba(30, 85, 95, 0.80);
}
```

**Step 4: Update `#nav-bar`**

Frosted dock with bright top edge:

```css
#nav-bar {
    flex-direction: row;
    justify-content: center;
    align-self: stretch;
    padding: var(--spacing-sm) var(--spacing-md);
    background-color: rgba(10, 20, 30, 0.50);
    border-top-left-radius: var(--radius-lg);
    border-top-right-radius: var(--radius-lg);
    border-width: 1px;
    border-color: rgba(120, 200, 220, 0.10);
    border-top-color: rgba(140, 230, 240, 0.30);
    border-bottom-width: 0;
}
```

**Step 5: Update `.nav-btn`**

Glass tab buttons:

```css
.nav-btn {
    flex-grow: 1;
    background-color: rgba(20, 40, 55, 0.50);
    color: var(--color-text);
    border-width: 1px;
    border-color: rgba(120, 200, 220, 0.20);
    border-radius: var(--radius-md);
    padding: var(--spacing-sm) var(--spacing-md);
    margin-left: var(--spacing-xs);
    margin-right: var(--spacing-xs);
    min-height: 88px;
    font-size: var(--font-md);
    transition-property: background-color, border-color;
    transition-duration: 0.2s;
}

.nav-btn:hover {
    background-color: rgba(30, 55, 70, 0.65);
    border-color: rgba(140, 230, 240, 0.40);
}

.nav-btn:active {
    background-color: rgba(40, 70, 85, 0.75);
}
```

**Step 6: Commit**

```
git add Assets/UI/Styles/HUD.uss
git commit -m "style: glassmorphism HUD — weather, currency, pulse, nav"
```

---

### Task 4: Update Satchel.uss — Glass Probability Panel

**Files:**
- Modify: `Assets/UI/Styles/Satchel.uss`

**Step 1: Update `#probability-panel`**

Glass subpanel:

```css
#probability-panel {
    margin-top: var(--spacing-md);
    padding: var(--spacing-md);
    background-color: rgba(15, 30, 40, 0.50);
    border-radius: var(--radius-md);
    border-width: 1px;
    border-color: rgba(120, 200, 220, 0.20);
    border-top-color: rgba(140, 230, 240, 0.30);
}
```

**Step 2: Commit**

```
git add Assets/UI/Styles/Satchel.uss
git commit -m "style: glassmorphism satchel probability panel"
```

---

### Task 5: Update Codex.uss — Glass Detail Panel

**Files:**
- Modify: `Assets/UI/Styles/Codex.uss`

**Step 1: Update `#detail-panel`**

Glass subpanel:

```css
#detail-panel {
    margin-top: var(--spacing-md);
    padding: var(--spacing-md);
    background-color: rgba(15, 30, 40, 0.50);
    border-radius: var(--radius-md);
    border-width: 1px;
    border-color: rgba(120, 200, 220, 0.20);
    border-top-color: rgba(140, 230, 240, 0.30);
}
```

**Step 2: Update `#detail-color-swatch` border**

Subtle glass border on the swatch:

```css
#detail-color-swatch {
    width: 48px;
    height: 48px;
    border-radius: 24px;
    margin-top: var(--spacing-sm);
    border-width: 1px;
    border-color: rgba(120, 200, 220, 0.25);
}
```

**Step 3: Commit**

```
git add Assets/UI/Styles/Codex.uss
git commit -m "style: glassmorphism codex detail panel"
```

---

### Task 6: Update Greenhouse.uss — Glass Stats

**Files:**
- Modify: `Assets/UI/Styles/Greenhouse.uss`

No hardcoded colors to change — `#dust-rate` and `#slots-text` already use CSS variables. File is already correct after Variables.uss update.

**Step 1: Verify visually**

Open Greenhouse panel in Play mode. Colors should already be teal-tinted via variables.

**Step 2: Skip commit** (no changes needed)

---

### Task 7: Update Debug.uss — Glass Debug Panel

**Files:**
- Modify: `Assets/UI/Styles/Debug.uss`

All debug styles use CSS variables (`--color-text-dim`, `--color-text-accent`, `--color-border`, etc.) which are already updated. No hardcoded colors to change.

**Step 1: Verify visually**

Open Debug panel in Play mode. Sliders, dropdowns, and presets should render with teal glass theme.

**Step 2: Skip commit** (no changes needed)

---

### Task 8: Full Visual Verification

**Step 1: Enter Play mode**

Start the game in Unity Editor.

**Step 2: Check each UI screen**

- HUD: Weather bar and currency panel should be translucent teal glass chips with subtle bright top edges
- Pulse button: Glowing teal glass pill with cyan border
- Nav bar: Frosted dock with glass tab buttons
- Satchel: Open via pulse button. Panel should be semi-transparent teal. Seed slots and probability panel should be glass-on-glass
- Codex: Open via nav. Same glass treatment, detail panel visible as inner pane
- Greenhouse: Open via nav. Glass overlay with teal-tinted slots
- Debug: Open via nav. Controls should be teal-themed

**Step 3: Take screenshot for comparison**

Use Unity MCP `manage_scene(action="screenshot")` to capture the result.

**Step 4: Final commit if any tweaks were needed**

```
git commit -m "style: glassmorphism visual polish tweaks"
```
