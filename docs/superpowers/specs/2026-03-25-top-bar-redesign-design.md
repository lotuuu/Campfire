# Top Bar Redesign — Floating HUD

## Problem

The current top bar is a 3-row stacked layout (~140px tall) with a solid opaque background. It takes too much vertical screen real estate and the visual design feels heavy/disconnected from the game art.

## Design

Replace the solid top bar with three floating HUD elements positioned over the game canvas. No background bar — the campsite art extends fully to the top of the screen.

### Floating HUD Elements

Three elements positioned inside the safe area inset, at the top of the screen:

1. **Profile circle** (top-left) — 36px circle with dark semi-transparent background (`rgba(20,40,20,0.65)`), gold border (`rgba(214,154,53,0.2)`), drop shadow. Shows the player's profile pic (loaded from `SpriteService` via `ui/profile` key). Tapping opens the Profile Popup.

2. **Resource pill** (top-center) — Single floating rounded pill (`border-radius: 16px`) with the same semi-transparent background. Contains mana, water, and mallum counts, each as icon + number, separated by subtle vertical dividers (`1px, rgba(255,255,255,0.1)`). Resource icons are 16px circles. Text uses existing resource colors (`--color-mana`, `--color-water`, `--color-text`).

3. **Weather button** (top-right) — 36px circle, same style as profile circle. Shows the current weather condition icon (sun/cloud/rain/storm/snow sprite from `SpriteService`). At night or when moon is notable, shows moon phase icon instead. Tapping triggers the bloom-expand animation into the Forecast Card.

### Spacing and Sizing

- All three elements share `top: 10px` (plus safe area inset)
- Profile: `left: 12px`
- Resource pill: centered horizontally, `padding: 5px 14px`
- Weather button: `right: 12px`
- Total height consumed: ~50px (down from ~140px)
- Elements have `box-shadow: 0 2px 8px rgba(0,0,0,0.3)` for depth

### Weather Forecast — Bloom Expand Animation

When the weather button is tapped:

1. **Expand phase** (0–400ms): The weather button circle expands into a 280px-wide card anchored at the top-right. Uses a springy `cubic-bezier(0.34, 1.56, 0.64, 1)` easing. Border-radius transitions from circular (50%) to card (16px). Background remains the same semi-transparent dark green.

2. **Content fade phase** (150ms–450ms, overlapping): After a 150ms delay, the forecast content fades in and slides up slightly (`translateY(8px)` to `0`). This staggered timing ensures the shape is mostly formed before content appears.

3. **Close**: Tapping the button again or tapping outside the card reverses the animation. The weather button icon rotates 180 degrees while the panel is open.

#### Forecast Card Content

- **Header**: "Today — {Condition}" with weather icon, gold colored
- **Stats rows**: Temperature, humidity, wind speed, moon phase — label left, value right, separated by subtle borders
- **5-day forecast**: Day name, condition icon, high/low temp per row

This reuses the existing `WeatherBarUI.PopulateForecast()` data but renders into the new bloom card instead of the old `#forecast-panel`.

### Profile Popup — Replaces Settings Overlay

When the profile pic is tapped, a popup opens using the same bloom-expand animation (blooming from the top-left profile circle). This popup replaces the current Settings overlay panel entirely.

#### Profile Popup Sections

1. **Player identity**: Profile pic (larger, 64px), display name (editable inline), friend code
2. **Camp stats**: Flame level, date/time, current weather summary (one-line)
3. **Settings**:
   - Language dropdown
   - Vibration toggle
   - Music volume slider
   - SFX volume slider
4. **Account info**: Player ID, server, version
5. **Danger zone**: Delete save data (with confirm flow)
6. **Debug button** (dev builds only): Opens debug panel

The popup is scrollable if content exceeds the viewport. Uses the same semi-transparent dark background as other floating elements, with a gold-tinted border.

#### Closing the Profile Popup

- Tap outside the popup
- Tap the profile pic again
- Both trigger the reverse bloom animation

### Profile Popup — Implementation Approach

Since Unity UI Toolkit doesn't support CSS `backdrop-filter` or `clip-path` animations natively, the bloom animation will be implemented as:

- A `VisualElement` with animated `width`, `height`, `border-radius`, and `opacity` properties via Unity's USS transitions
- Content inside has a separate `opacity` and `translate` transition with a delay
- Position anchored via USS `position: absolute` with `top`/`right` (weather) or `top`/`left` (profile)

USS transition support covers: `width`, `height`, `border-radius`, `opacity`, `translate`, `scale` — all needed properties are supported.

## UXML Changes

### Remove from `CampFireRoot.uxml`

- The entire `#top-bar` container and its children (`#top-header`, `#weather-bar`, `#resource-bar`)
- The `#settings-panel` from the overlay (absorbed into profile popup)

### Add to `CampFireRoot.uxml`

New floating elements as direct children of `#camp-root`, before `#campsite-viewport`:

```xml
<!-- Floating HUD -->
<ui:VisualElement name="floating-hud" picking-mode="Ignore">
    <!-- Profile circle -->
    <ui:VisualElement name="hud-profile" class="hud-circle hud-left">
        <ui:VisualElement name="hud-profile-pic" />
    </ui:VisualElement>

    <!-- Resource pill -->
    <ui:VisualElement name="hud-resources" class="hud-pill">
        <ui:VisualElement class="hud-res">
            <ui:VisualElement name="hud-mana-icon" class="hud-res-icon" />
            <ui:Label name="hud-mana" text="0" />
        </ui:VisualElement>
        <ui:VisualElement class="hud-res-divider" />
        <ui:VisualElement class="hud-res">
            <ui:VisualElement name="hud-water-icon" class="hud-res-icon" />
            <ui:Label name="hud-water" text="0" />
        </ui:VisualElement>
        <ui:VisualElement class="hud-res-divider" />
        <ui:VisualElement class="hud-res">
            <ui:VisualElement name="hud-mallum-icon" class="hud-res-icon" />
            <ui:Label name="hud-mallum" text="0/0" />
        </ui:VisualElement>
    </ui:VisualElement>

    <!-- Weather circle -->
    <ui:VisualElement name="hud-weather" class="hud-circle hud-right">
        <ui:VisualElement name="hud-weather-icon" />
    </ui:VisualElement>
</ui:VisualElement>

<!-- Forecast bloom card (hidden, expands from weather button) -->
<ui:VisualElement name="forecast-bloom" class="bloom-card bloom-right">
    <ui:VisualElement name="forecast-bloom-content" class="bloom-content">
        <ui:Label name="forecast-bloom-title" />
        <ui:VisualElement name="forecast-bloom-stats" />
        <ui:VisualElement name="forecast-bloom-days" />
    </ui:VisualElement>
</ui:VisualElement>

<!-- Profile bloom popup (hidden, expands from profile button) -->
<ui:VisualElement name="profile-bloom" class="bloom-card bloom-left">
    <ui:ScrollView name="profile-bloom-content" class="bloom-content"
        touch-scroll-type="Clamped" elasticity="0"
        vertical-scroller-visibility="Hidden">

        <!-- Player identity -->
        <ui:VisualElement name="profile-identity">
            <ui:VisualElement name="profile-pic-large" />
            <ui:TextField name="profile-display-name" label="Name" max-length="20" />
            <ui:Label name="profile-friend-code" text="Code: ---" />
        </ui:VisualElement>

        <!-- Camp stats -->
        <ui:VisualElement name="profile-stats">
            <ui:Label name="profile-flame-level" text="Flame Level 1" />
            <ui:Label name="profile-date-time" text="--" />
            <ui:Label name="profile-weather-summary" text="--" />
        </ui:VisualElement>

        <!-- Settings: Language -->
        <ui:VisualElement name="profile-settings">
            <ui:Label text="SETTINGS" class="profile-section-header" />
            <ui:DropdownField name="profile-language-dropdown" label="Language" />
            <ui:VisualElement class="profile-toggle-row">
                <ui:Label text="Vibration" />
                <ui:Toggle name="profile-vibration-toggle" />
            </ui:VisualElement>
            <ui:VisualElement class="profile-slider-row">
                <ui:Label text="Music" />
                <ui:Slider name="profile-music-slider" low-value="0" high-value="100" value="100" />
                <ui:Label name="profile-music-value" text="100%" />
            </ui:VisualElement>
            <ui:VisualElement class="profile-slider-row">
                <ui:Label text="Sound FX" />
                <ui:Slider name="profile-sfx-slider" low-value="0" high-value="100" value="100" />
                <ui:Label name="profile-sfx-value" text="100%" />
            </ui:VisualElement>
        </ui:VisualElement>

        <!-- Account info -->
        <ui:VisualElement name="profile-account">
            <ui:Label text="ACCOUNT" class="profile-section-header" />
            <ui:Label name="profile-player-id" text="Player: ---" />
            <ui:Label name="profile-server" text="Server: ---" />
            <ui:Label name="profile-version" text="Version: ---" />
        </ui:VisualElement>

        <!-- Danger zone -->
        <ui:VisualElement name="profile-danger">
            <ui:Label text="DANGER ZONE" class="profile-section-header" />
            <ui:Button name="profile-delete-btn" text="Delete Save Data" />
            <ui:VisualElement name="profile-confirm-row" style="display: none;">
                <ui:Label text="Are you sure?" />
                <ui:Button name="profile-confirm-cancel" text="Cancel" />
                <ui:Button name="profile-confirm-delete" text="Delete" />
            </ui:VisualElement>
        </ui:VisualElement>

        <!-- Debug (dev only) -->
        <ui:Button name="profile-debug-btn" text="DEBUG" style="display: none;" />

    </ui:ScrollView>
</ui:VisualElement>
```

## USS Changes

### New file: `Assets/UI/Styles/FloatingHud.uss`

Defines styles for:
- `.hud-circle` — 36px circle, semi-transparent bg, gold border, drop shadow
- `.hud-left` / `.hud-right` — absolute positioning
- `.hud-pill` — centered resource pill
- `.hud-res` / `.hud-res-icon` / `.hud-res-divider` — resource layout within pill
- `.bloom-card` — base bloom card (starts as circle-sized, transitions to expanded)
- `.bloom-card.bloom-open` — expanded state with full width/height/border-radius
- `.bloom-content` — inner content with delayed fade-in
- `.bloom-right` / `.bloom-left` — anchor positions

### Modify: `Assets/UI/Styles/CampSite.uss`

- Remove `#top-bar`, `#top-header`, `#profile-pic`, `#player-name`, `#top-header-spacer`, `#date-time`, `#resource-bar`, `.resource-item`, `.resource-icon`, `#mana-display`, `#water-display`, `#mallum-display`, `.debug-btn`, `.settings-icon-btn` styles
- Keep `.icon-btn` and `.header-icon` if used elsewhere

### Remove: `Assets/UI/Styles/WeatherBar.uss`

The weather bar styles are fully replaced by the bloom card styles. Forecast-specific styles (`.forecast-day`, `.forecast-stat-cell`, etc.) move into the new `FloatingHud.uss` or a renamed forecast section.

## C# Changes

### `WeatherBarUI.cs` — Refactor

- Remove references to `#weather-bar`, `#weather-condition-label`, `#weather-temp`, `#weather-humidity`, `#weather-moon` (old top-bar elements)
- Add references to `#hud-weather-icon` (floating button) and `#forecast-bloom` (bloom card)
- Add `OnWeatherButtonClicked` — toggles `.bloom-open` class on `#forecast-bloom`, rotates icon
- Refactor `PopulateForecast()` to populate `#forecast-bloom-stats` and `#forecast-bloom-days` instead of old `#forecast-days`
- Add click-outside-to-close handler on `#camp-root`

### `ResourceDisplayUI.cs` — Retarget

- Change element queries from `#mana-display` → `#hud-mana`, `#water-display` → `#hud-water`, `#mallum-display` → `#hud-mallum`
- Change icon queries from `#mana-icon` → `#hud-mana-icon`, `#water-icon` → `#hud-water-icon`, `#mallum-icon` → `#hud-mallum-icon`
- No logic changes needed

### New: `ProfilePopupUI.cs`

New MonoBehaviour sub-controller managing the profile bloom popup:
- `Initialize(VisualElement root)` — caches refs to `#hud-profile`, `#profile-bloom`, and all settings elements within
- `OnProfileClicked` — toggles `.bloom-open` on `#profile-bloom`
- Absorbs all settings logic currently in `SettingsUI.cs`: language dropdown, audio sliders, vibration toggle, account info, delete save flow, debug button
- Populates player name, flame level, date/time, weather summary
- Click-outside-to-close handler

### `SettingsUI.cs` — Remove

All functionality moves into `ProfilePopupUI.cs`. Remove from `CampFireUI` initialization.

### `CampFireUI.cs` — Update

- Remove `SettingsUI` initialization
- Add `ProfilePopupUI` initialization
- Remove settings overlay panel registration
- Update `WeatherBarUI` initialization (no more old top-bar refs)
- Remove `#btn-settings` and `#btn-debug` button wiring (moved to profile popup)

### `SafeAreaController.cs` — Update

- Instead of applying safe area insets to `#top-bar`, apply to `#floating-hud`
- The floating elements need the top safe area inset added to their `top` position

## Migration Notes

- The `#forecast-panel` / `#forecast-days` elements in the campsite-viewport can be removed — forecast content moves into the bloom card
- Player name display (`#player-name`) is currently hidden unless visiting. In the new design, the player's own name shows inside the profile popup. When visiting another player's camp, a small label (`#hud-visiting-name`) appears beneath the profile circle showing "{Name}'s Camp". This label is a child of `#hud-profile`, styled as a small pill with the same semi-transparent background, managed by `WeatherBarUI.SetVisitingName()`
- The debug button currently in the resource bar moves into the profile popup, gated by `Debug.isDebugBuild`

## Files Changed

| File | Action |
|------|--------|
| `Assets/UI/Documents/CampFireRoot.uxml` | Remove `#top-bar`, `#settings-panel`; add floating HUD + bloom elements |
| `Assets/UI/Styles/FloatingHud.uss` | New — all floating HUD and bloom styles |
| `Assets/UI/Styles/CampSite.uss` | Remove top-bar/resource-bar styles |
| `Assets/UI/Styles/WeatherBar.uss` | Remove (replaced by FloatingHud.uss) |
| `Assets/Scripts/UI/WeatherBarUI.cs` | Refactor to target new bloom elements |
| `Assets/Scripts/UI/ResourceDisplayUI.cs` | Retarget element queries |
| `Assets/Scripts/UI/ProfilePopupUI.cs` | New — profile popup controller (absorbs SettingsUI) |
| `Assets/Scripts/UI/SettingsUI.cs` | Remove (absorbed into ProfilePopupUI) |
| `Assets/Scripts/UI/CampFireUI.cs` | Wire new controllers, remove old ones |
| `Assets/Scripts/UI/SafeAreaController.cs` | Apply insets to `#floating-hud` |
