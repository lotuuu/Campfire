# Bottom Navigation with Swipeable Pages

## Summary

Replace the current tap-to-toggle overlay panel navigation with a bottom tab bar and horizontally swipeable pages. Each major section becomes a full page within a SwipeablePageView, navigable by swiping or tapping tabs.

## Tab Layout (left to right)

1. **Codex** — Variant discovery/collection
2. **Shop** — Seed shop
3. **Terrarium** — Home page with isometric Hearth view (default on launch). Later expands to other zones.
4. **Greenhouse** — Passive plant collection generating Aura Dust
5. **Locked** — Disabled placeholder for a future feature

App opens on Terrarium (tab 2, center).

## Visual Structure

```
[top-bar]           <- weather + currency (fixed top)
[page-viewport]     <- SwipeablePageView (fills remaining space)
  [codex-page]        Tab 0
  [shop-page]         Tab 1
  [terrarium-page]    Tab 2 (default)
  [greenhouse-page]   Tab 3
  [locked-page]       Tab 4 (disabled)
[bottom-nav]        <- 5 tab buttons (fixed bottom)
```

## SwipeablePageView Component

Custom `VisualElement` subclass (`Assets/Scripts/UI/SwipeablePageView.cs`):

- Contains a `pageContainer` holding all pages side-by-side horizontally
- Viewport clips overflow (`overflow: hidden`)
- Each page is 100% viewport width
- Handles `PointerDown/Move/Up` for horizontal swipe gestures
- Animates `pageContainer.style.translate` for page transitions
- Swipe threshold: ~50px horizontal drag to trigger page change
- Conflict resolution: if vertical drag exceeds horizontal, cancel swipe (let inner ScrollViews work)
- API:
  - `int PageCount`, `int CurrentPageIndex`
  - `void GoToPage(int index, bool animated = true)`
  - `event Action<int> OnPageChanged`

## Bottom Navigation Bar

`Assets/Scripts/UI/BottomNavUI.cs` (MonoBehaviour):

- Manages tab buttons inside `#bottom-nav`
- Subscribes to `SwipeablePageView.OnPageChanged` to highlight active tab
- Tab tap calls `SwipeablePageView.GoToPage(index)`
- Active tab gets `.nav-tab--active` USS class
- 5th tab visually disabled/dimmed, tap does nothing or shows "coming soon"

## Animations

- **Page slide**: `translate` transition, ~300ms ease-out
- **During swipe drag**: pageContainer follows finger (no transition, direct tracking)
- **On release**: snap to nearest page with transition
- **Tab tap**: animated slide to target page
- **Satchel bottom sheet**: `translate.y` transition, ~250ms ease-out

## Overlay Panels (unchanged pattern)

These stay as overlays on top of the current page:
- **Satchel**: becomes a bottom sheet (slides up from bottom, swipe-down to dismiss)
- **Harvest result popup**: transient modal overlay
- **Debug panel**: accessible via hidden gesture or long-press (not a tab)

## Greenhouse Page (new)

The Greenhouse page needs new UI content showing:
- List of kept plants with variant colors
- Dust/hr rate per plant and total
- Sell button per plant
- Slot count and expansion button
- Uses existing `GreenhouseManager` for data

## New Files

- `Assets/Scripts/UI/SwipeablePageView.cs` — swipe + animated page view component
- `Assets/Scripts/UI/BottomNavUI.cs` — tab bar controller
- `Assets/UI/Styles/BottomNav.uss` — tab bar styling

## Modified Files

- `Assets/UI/Documents/GardenRoot.uxml` — restructure from overlay panels to page layout
- `Assets/Scripts/UI/HortusUI.cs` — replace toggle/close logic with page orchestration
- `Assets/UI/Styles/Common.uss` — panels no longer absolute overlays
- `Assets/UI/Styles/HUD.uss` — adjust bottom bar to bottom nav
- `Assets/Scripts/UI/HearthViewUI.cs` — becomes terrarium page content
- `Assets/Scripts/UI/CodexUI.cs` — remove close button, adapt to page lifecycle
- `Assets/Scripts/UI/SeedShopUI.cs` — remove close button, adapt to page lifecycle
- `Assets/Scripts/UI/TerrariumUI.cs` — remove close button, adapt to page lifecycle
- `Assets/Scripts/UI/GreenhouseUI.cs` — rework for greenhouse page content
- `Assets/Scripts/UI/SatchelUI.cs` — convert to bottom sheet pattern
