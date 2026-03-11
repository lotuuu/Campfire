# Visitor UI Redesign

## Problem

Three issues with the current visitor UI:

1. **Dialogue popup is tiny** — `#dialogue-box` has `min-height: 340px` with small fonts, feels like a notification toast instead of a VN moment
2. **Visitor panel is ugly** — uses the shared slide-up overlay which scrolls unnecessarily, has tiny action buttons, and lacks visual hierarchy
3. **Missing information** — gift visitors show bare "Gift: 3 Water" with no visual treatment; all visitor types lack clear presentation of what's happening

## Design

### 1. Dialogue Box — CSS-only fix

Increase sizing in `Dialogue.uss`:

- `#dialogue-box` min-height: 340px → 500px
- `#dialogue-speaker` font-size: `--font-md` → `--font-lg`
- `#dialogue-text` font-size: `--font-sm` → `--font-md`
- `#dialogue-tap-hint` font-size: `--font-xs` → `--font-sm`
- Add more vertical padding for breathing room

No structural changes. Keep the dark overlay + bottom-anchored box layout.

### 2. Dedicated Visitor Modal — replaces overlay usage

A new centered modal card, rendered as a sibling to `dialogue-overlay` in CampFireRoot.uxml (not inside the shared `overlay-body`).

**Structure:**
```
visitor-modal (full-screen backdrop + centered card)
├── visitor-modal-backdrop (dark overlay, click to close)
├── visitor-modal-card (centered container)
│   ├── visitor-modal-close (X button, top-right)
│   ├── visitor-modal-header
│   │   ├── visitor-modal-portrait (circle with portrait texture)
│   │   ├── visitor-modal-name (visitor name)
│   │   └── visitor-modal-flavor (italic flavor text)
│   ├── visitor-modal-content
│   │   ├── visitor-gifter-section
│   │   │   ├── gift card (icon + name + amount in a styled row)
│   │   │   └── claim button or "Gift received!" state
│   │   ├── visitor-merchant-section
│   │   │   └── offer rows (reuse MerchantOfferRow template, restyled)
│   │   └── visitor-quester-section
│   │       ├── quest description
│   │       ├── request/reward info
│   │       └── accept or turn-in button
│   └── visitor-modal-actions (bottom action buttons)
```

**Styling (new `Visitor.uss`):**
- Modal backdrop: `rgba(0, 0, 0, 0.7)`
- Card: ~85% width, centered vertically, `--color-bg-panel` background, rounded corners (`--radius-lg`), subtle border
- Header: gradient from slightly lighter brown, portrait circle with accent border
- Gift/trade cards: `--color-bg-slot` background, rounded, with clear item name and amount
- Action buttons: full-width, generous padding (14px+), `--font-md` font size, accent border
- No scroll by default; only enable vertical scroll on merchant section if many offers

### 3. Flow Change in CampFireUI

Currently, tapping a visitor on the hex grid triggers:
1. If dialogue not seen → show DialogueUI → on complete, open shared overlay with visitor panel
2. If dialogue seen → open shared overlay directly

**New flow:**
1. If dialogue not seen → show DialogueUI → on complete, call `VisitorUI.ShowModal()`
2. If dialogue seen → call `VisitorUI.ShowModal()` directly

`VisitorUI` gains `ShowModal()` and `HideModal()` methods. It manages its own modal visibility instead of relying on `CampFireUI.OpenOverlay()`. The visitor panel is removed from the shared overlay body entirely.

### 4. Files Changed

- `Assets/UI/Styles/Dialogue.uss` — increase sizes (CSS only)
- `Assets/UI/Styles/Visitor.uss` — new stylesheet for visitor modal
- `Assets/UI/Documents/CampFireRoot.uxml` — remove visitor-panel from overlay-body, add visitor-modal as sibling to dialogue-overlay
- `Assets/Scripts/UI/VisitorUI.cs` — add ShowModal/HideModal, wire new element refs, add portrait support
- `Assets/Scripts/UI/CampFireUI.cs` — change visitor tap handler to use VisitorUI.ShowModal() instead of OpenOverlay()
