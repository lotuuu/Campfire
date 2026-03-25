# Top Bar Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the 3-row solid top bar with floating HUD elements (profile circle, resource pill, weather button) that use bloom-expand animations for forecast and profile popups.

**Architecture:** Remove `#top-bar` and `#settings-panel` from UXML. Add `#floating-hud` with three floating elements positioned absolutely over the game canvas. Bloom animations use USS transitions on `width`, `height`, `border-radius`, and `opacity`. `ProfilePopupUI` absorbs all `SettingsUI` logic. `WeatherBarUI` retargets to bloom card. `ResourceDisplayUI` retargets element IDs.

**Tech Stack:** Unity 6 UI Toolkit (UXML, USS, C#), namespace `Garden`

**Spec:** `docs/superpowers/specs/2026-03-25-top-bar-redesign-design.md`

---

## File Map

| File | Action | Responsibility |
|------|--------|----------------|
| `Assets/UI/Styles/FloatingHud.uss` | Create | All HUD circle, pill, bloom card, and forecast styles |
| `Assets/UI/Documents/CampFireRoot.uxml` | Modify | Remove `#top-bar` + `#settings-panel`, add floating HUD + bloom elements |
| `Assets/UI/Styles/CampSite.uss` | Modify | Remove old top-bar/resource-bar styles |
| `Assets/UI/Styles/WeatherBar.uss` | Delete | Replaced by FloatingHud.uss |
| `Assets/UI/Styles/Forecast.uss` | Delete | Forecast styles move into FloatingHud.uss |
| `Assets/UI/Styles/Settings.uss` | Modify | Re-scope selectors from `#settings-panel` to `#profile-bloom` |
| `Assets/Scripts/UI/ProfilePopupUI.cs` | Create | Profile bloom popup controller (absorbs SettingsUI) |
| `Assets/Scripts/UI/SettingsUI.cs` | Delete | Absorbed into ProfilePopupUI |
| `Assets/Scripts/UI/ResourceDisplayUI.cs` | Modify | Retarget element queries to `#hud-*` IDs |
| `Assets/Scripts/UI/WeatherBarUI.cs` | Modify | Refactor to floating button + bloom card |
| `Assets/Scripts/UI/CampFireUI.cs` | Modify | Wire ProfilePopupUI, remove SettingsUI, remove old button wiring |
| `Assets/Scripts/UI/SafeAreaController.cs` | Modify | Apply safe area insets to `#floating-hud` instead of `#top-bar` |

---

### Task 1: Create FloatingHud.uss

**Files:**
- Create: `Assets/UI/Styles/FloatingHud.uss`

This is the foundational stylesheet. All subsequent UXML elements reference these classes.

- [ ] **Step 1: Create the stylesheet**

```css
/* FloatingHud.uss — Floating HUD elements + bloom card animations */

/* ── Floating HUD container ── */
#floating-hud {
    position: absolute;
    left: 0;
    right: 0;
    top: 10px;
    flex-direction: row;
    align-items: flex-start;
    padding: 0 12px;
    z-index: 100;
}

/* ── Shared circle style ── */
.hud-circle {
    width: 36px;
    height: 36px;
    border-radius: 18px;
    background-color: rgba(20, 40, 20, 0.65);
    border-width: 1px;
    border-color: rgba(214, 154, 53, 0.2);
    align-items: center;
    justify-content: center;
    flex-shrink: 0;
}

/* ── Profile circle (left) ── */
.hud-left {
    margin-right: auto;
}

#hud-profile-pic {
    width: 32px;
    height: 32px;
    border-radius: 16px;
    -unity-background-scale-mode: scale-to-fit;
}

#hud-visiting-name {
    display: none;
    position: absolute;
    top: 40px;
    left: 0;
    background-color: rgba(20, 40, 20, 0.75);
    border-radius: 8px;
    padding: 2px 8px;
    white-space: nowrap;
}

#hud-visiting-name Label {
    font-size: 18px;
    color: var(--color-text-bright);
}

/* ── Resource pill (center) ── */
.hud-pill {
    flex-direction: row;
    align-items: center;
    background-color: rgba(20, 40, 20, 0.65);
    border-radius: 16px;
    border-width: 1px;
    border-color: rgba(214, 154, 53, 0.2);
    padding: 5px 14px;
}

.hud-res {
    flex-direction: row;
    align-items: center;
}

.hud-res-icon {
    width: 16px;
    height: 16px;
    -unity-background-scale-mode: scale-to-fit;
    margin-right: 3px;
}

.hud-res Label {
    font-size: 24px;
    -unity-font-style: bold;
}

#hud-mana {
    color: var(--color-mana);
}

#hud-water {
    color: var(--color-water);
}

#hud-mallum {
    color: var(--color-text);
}

.hud-res-divider {
    width: 1px;
    height: 14px;
    background-color: rgba(255, 255, 255, 0.1);
    margin: 0 8px;
}

/* ── Weather circle (right) ── */
.hud-right {
    margin-left: auto;
}

#hud-weather-icon {
    width: 24px;
    height: 24px;
    -unity-background-scale-mode: scale-to-fit;
}

/* ── Bloom card base (collapsed = circle-sized, hidden) ── */
.bloom-card {
    position: absolute;
    width: 36px;
    height: 36px;
    border-radius: 18px;
    background-color: rgba(20, 40, 20, 0.85);
    border-width: 1px;
    border-color: rgba(214, 154, 53, 0.3);
    overflow: hidden;
    opacity: 0;
    scale: 0.8 0.8;
    z-index: 200;

    transition-property: width, height, border-radius, opacity, scale;
    transition-duration: 400ms, 400ms, 400ms, 200ms, 400ms;
    transition-timing-function: ease-out-back, ease-out-back, ease, ease, ease-out-back;
}

.bloom-card.bloom-open {
    opacity: 1;
    scale: 1 1;
}

/* ── Bloom content (delayed fade-in) ── */
.bloom-content {
    opacity: 0;
    translate: 0 8px;
    transition-property: opacity, translate;
    transition-duration: 300ms, 300ms;
    transition-delay: 150ms, 150ms;
    transition-timing-function: ease, ease;
    padding: 14px;
}

.bloom-card.bloom-open .bloom-content {
    opacity: 1;
    translate: 0 0;
}

/* ── Forecast bloom: anchored top-right ── */
.bloom-right {
    top: 10px;
    right: 12px;
}

.bloom-right.bloom-open {
    width: 280px;
    height: 420px;
    border-radius: 16px;
}

/* ── Profile bloom: anchored top-left ── */
.bloom-left {
    top: 10px;
    left: 12px;
}

.bloom-left.bloom-open {
    width: 320px;
    height: 520px;
    border-radius: 16px;
}

/* ── Forecast card content ── */
.forecast-bloom-title {
    color: rgb(214, 154, 53);
    font-size: var(--font-md);
    -unity-font-style: bold;
    margin-bottom: var(--spacing-sm);
}

.forecast-stat-row {
    flex-direction: row;
    justify-content: space-between;
    padding: 6px 0;
    border-bottom-width: 1px;
    border-bottom-color: rgba(255, 255, 255, 0.06);
}

.forecast-stat-label {
    color: rgba(255, 255, 255, 0.5);
    font-size: 22px;
}

.forecast-stat-value {
    color: rgb(232, 220, 200);
    font-size: 22px;
    -unity-font-style: bold;
}

.forecast-section-label {
    font-size: 20px;
    color: rgba(255, 255, 255, 0.3);
    margin-top: var(--spacing-sm);
    margin-bottom: var(--spacing-xs);
}

.forecast-day-row {
    flex-direction: row;
    align-items: center;
    padding: 6px 0;
    border-bottom-width: 1px;
    border-bottom-color: rgba(255, 255, 255, 0.06);
}

.forecast-day-name {
    font-size: 20px;
    color: rgba(255, 255, 255, 0.5);
    width: 50px;
}

.forecast-day-icon {
    width: 24px;
    height: 24px;
    -unity-background-scale-mode: scale-to-fit;
    margin-right: var(--spacing-xs);
}

.forecast-day-temp {
    font-size: 22px;
    color: rgb(232, 220, 200);
    margin-left: auto;
}

/* ── Outlier color classes (kept from Forecast.uss) ── */
.stat-hot { color: rgb(255, 110, 60); }
.stat-cold { color: rgb(100, 175, 235); }
.stat-dry { color: rgb(230, 145, 55); }
.stat-humid { color: rgb(100, 175, 235); }
.stat-windy { color: rgb(200, 210, 220); }

/* ── Profile bloom content ── */
.profile-section-header {
    font-size: var(--font-xs);
    color: rgb(160, 140, 110);
    -unity-font-style: bold;
    letter-spacing: 3px;
    margin-top: var(--spacing-lg);
    margin-bottom: var(--spacing-sm);
    padding-bottom: var(--spacing-xs);
    border-bottom-width: 1px;
    border-bottom-color: rgba(180, 120, 60, 0.25);
}

#profile-pic-large {
    width: 64px;
    height: 64px;
    border-radius: 32px;
    -unity-background-scale-mode: scale-to-fit;
    background-color: rgb(60, 95, 45);
    border-width: 2px;
    border-color: rgb(214, 154, 53);
    margin-bottom: var(--spacing-sm);
}

#profile-identity {
    align-items: center;
    margin-bottom: var(--spacing-sm);
}

#profile-friend-code {
    font-size: 20px;
    color: var(--color-text-dim);
    margin-top: var(--spacing-xs);
}

#profile-stats Label {
    font-size: 22px;
    color: var(--color-text);
    margin-bottom: var(--spacing-xxs);
}

#profile-flame-level {
    font-size: var(--font-sm);
    color: var(--color-text-accent);
    -unity-font-style: bold;
}

.profile-toggle-row {
    flex-direction: row;
    align-items: center;
    justify-content: space-between;
    margin-bottom: var(--spacing-sm);
    min-height: 56px;
}

.profile-toggle-row Label {
    color: rgb(160, 140, 110);
    font-size: 22px;
}

.profile-slider-row {
    flex-direction: row;
    align-items: center;
    margin-bottom: var(--spacing-sm);
    min-height: 56px;
}

.profile-slider-row Label {
    color: rgb(160, 140, 110);
    font-size: 22px;
}

.profile-slider-row .unity-base-slider {
    flex-grow: 1;
    margin: 0 var(--spacing-xs);
    min-height: 48px;
}

.profile-slider-row .unity-base-slider .unity-base-slider__dragger {
    width: 40px;
    height: 40px;
    border-radius: 20px;
    background-color: rgb(255, 170, 60);
}

.profile-slider-row .unity-base-slider .unity-base-slider__tracker {
    height: 8px;
    background-color: rgba(180, 120, 60, 0.3);
    border-radius: 4px;
}

#profile-bloom .unity-toggle .unity-toggle__checkmark {
    width: 40px;
    height: 40px;
    border-radius: 6px;
}

#profile-account Label {
    font-size: 20px;
    color: var(--color-text-dim);
    margin-bottom: var(--spacing-xxs);
}

/* Delete button in profile */
#profile-delete-btn {
    min-height: 56px;
    background-color: rgba(140, 40, 30, 0.7);
    border-width: 1px;
    border-color: rgba(200, 80, 60, 0.4);
    border-radius: var(--radius-sm);
    color: rgb(255, 220, 200);
    font-size: 22px;
    -unity-text-align: middle-center;
    margin-top: var(--spacing-sm);
}

#profile-delete-btn:hover {
    background-color: rgba(160, 50, 35, 0.85);
}

#profile-confirm-row {
    flex-direction: row;
    align-items: center;
    margin-top: var(--spacing-xs);
}

#profile-confirm-row Label {
    flex-grow: 1;
    color: rgb(220, 90, 70);
    font-size: 22px;
    -unity-font-style: bold;
}

#profile-confirm-cancel {
    min-height: 48px;
    min-width: 100px;
    background-color: rgba(60, 40, 20, 0.6);
    border-width: 1px;
    border-color: rgba(140, 100, 50, 0.3);
    border-radius: var(--radius-sm);
    color: rgb(220, 200, 160);
    font-size: 22px;
    -unity-text-align: middle-center;
    margin-right: var(--spacing-xs);
}

#profile-confirm-delete {
    min-height: 48px;
    min-width: 100px;
    background-color: rgba(180, 40, 30, 0.8);
    border-width: 1px;
    border-color: rgba(220, 80, 60, 0.5);
    border-radius: var(--radius-sm);
    color: rgb(255, 220, 200);
    font-size: 22px;
    -unity-font-style: bold;
    -unity-text-align: middle-center;
}

#profile-debug-btn {
    min-height: 48px;
    margin-top: var(--spacing-sm);
    background-color: transparent;
    border-width: 0;
    color: var(--color-text-dim);
    font-size: 20px;
}

/* ── Bloom dismiss backdrop ── */
#bloom-dismiss {
    display: none;
    position: absolute;
    left: 0;
    right: 0;
    top: 0;
    bottom: 0;
    z-index: 150;
}

#bloom-dismiss.bloom-dismiss-active {
    display: flex;
}
```

- [ ] **Step 2: Verify the file was created correctly**

Open `Assets/UI/Styles/FloatingHud.uss` in Unity and confirm no USS parse errors appear in the console. Use `read_console` MCP tool to check.

- [ ] **Step 3: Commit**

```bash
git add Assets/UI/Styles/FloatingHud.uss
git commit -m "feat: add FloatingHud.uss with HUD circle, pill, and bloom card styles"
```

---

### Task 2: Update UXML — remove old top bar, add floating HUD + bloom elements

**Files:**
- Modify: `Assets/UI/Documents/CampFireRoot.uxml`

- [ ] **Step 1: Add FloatingHud.uss style import**

After the existing style imports (line 14, after Forecast.uss), add:

```xml
<Style src="project://database/Assets/UI/Styles/FloatingHud.uss" />
```

- [ ] **Step 2: Remove the `#top-bar` block**

Remove lines 27–66 of `CampFireRoot.uxml` — the entire `<ui:VisualElement name="top-bar">` block and all its children (`#top-header`, `#weather-bar`, `#resource-bar`).

- [ ] **Step 3: Remove `#forecast-panel` from campsite-viewport**

Inside `#campsite-viewport`, remove the `#forecast-panel` element and its child `#forecast-days` (lines 71–73).

- [ ] **Step 4: Remove `#settings-panel` from overlay-body**

Inside `#overlay-body`, remove the entire `<ui:VisualElement name="settings-panel">` block (lines 224–279).

- [ ] **Step 5: Add floating HUD elements**

Insert these as the first children of `#camp-root`, before `#campsite-viewport`:

```xml
<!-- Floating HUD -->
<ui:VisualElement name="floating-hud" picking-mode="Ignore">
    <ui:VisualElement name="hud-profile" class="hud-circle hud-left">
        <ui:VisualElement name="hud-profile-pic" />
    </ui:VisualElement>
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
    <ui:VisualElement name="hud-weather" class="hud-circle hud-right">
        <ui:VisualElement name="hud-weather-icon" />
    </ui:VisualElement>
</ui:VisualElement>

<!-- Bloom dismiss backdrop (click-outside-to-close) -->
<ui:VisualElement name="bloom-dismiss" />

<!-- Forecast bloom card -->
<ui:VisualElement name="forecast-bloom" class="bloom-card bloom-right">
    <ui:VisualElement name="forecast-bloom-content" class="bloom-content">
        <ui:Label name="forecast-bloom-title" class="forecast-bloom-title" />
        <ui:VisualElement name="forecast-bloom-stats" />
        <ui:Label text="" class="forecast-section-label" name="forecast-section-label" />
        <ui:VisualElement name="forecast-bloom-days" />
    </ui:VisualElement>
</ui:VisualElement>

<!-- Profile bloom popup -->
<ui:VisualElement name="profile-bloom" class="bloom-card bloom-left">
    <ui:ScrollView name="profile-bloom-content" class="bloom-content"
        touch-scroll-type="Clamped" elasticity="0"
        vertical-scroller-visibility="Hidden">
        <ui:VisualElement name="profile-identity">
            <ui:VisualElement name="profile-pic-large" />
            <ui:TextField name="profile-display-name" label="Name" max-length="20" />
            <ui:Label name="profile-friend-code" text="Code: ---" />
        </ui:VisualElement>
        <ui:VisualElement name="profile-stats">
            <ui:Label name="profile-flame-level" text="Flame Level 1" />
            <ui:Label name="profile-date-time" text="--" />
            <ui:Label name="profile-weather-summary" text="--" />
        </ui:VisualElement>
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
        <ui:VisualElement name="profile-account">
            <ui:Label text="ACCOUNT" class="profile-section-header" />
            <ui:Label name="profile-player-id" text="Player: ---" />
            <ui:Label name="profile-server" text="Server: ---" />
            <ui:Label name="profile-version" text="Version: ---" />
        </ui:VisualElement>
        <ui:VisualElement name="profile-danger">
            <ui:Label text="DANGER ZONE" class="profile-section-header" />
            <ui:Button name="profile-delete-btn" text="Delete Save Data" />
            <ui:VisualElement name="profile-confirm-row" style="display: none;">
                <ui:Label text="Are you sure?" />
                <ui:Button name="profile-confirm-cancel" text="Cancel" />
                <ui:Button name="profile-confirm-delete" text="Delete" />
            </ui:VisualElement>
        </ui:VisualElement>
        <ui:Button name="profile-debug-btn" text="DEBUG" style="display: none;" />
    </ui:ScrollView>
</ui:VisualElement>
```

- [ ] **Step 6: Verify UXML loads without errors**

Open the scene in Unity, check the console for any UXML parse errors. Use `read_console` MCP tool.

- [ ] **Step 7: Commit**

```bash
git add Assets/UI/Documents/CampFireRoot.uxml
git commit -m "feat: replace top-bar with floating HUD and bloom card UXML"
```

---

### Task 3: Clean up old stylesheets

**Files:**
- Modify: `Assets/UI/Styles/CampSite.uss`
- Delete: `Assets/UI/Styles/WeatherBar.uss`
- Delete: `Assets/UI/Styles/Forecast.uss`
- Delete: `Assets/UI/Styles/Settings.uss`

- [ ] **Step 1: Remove old top-bar styles from CampSite.uss**

Remove these style blocks from `CampSite.uss` (lines 10–131):
- `#top-bar`
- `#top-header`
- `#profile-pic`
- `#player-name`
- `#player-name.visiting`
- `.icon-btn`
- `.debug-btn`
- `.settings-icon-btn`
- `.header-icon`
- `#top-header-spacer`
- `#date-time`
- `#resource-bar`
- `.resource-item`
- `.resource-icon`
- `#mana-display`
- `#water-display`
- `#mallum-display`

Keep `#camp-root` and `#campsite-viewport` and everything below them.

- [ ] **Step 2: Delete WeatherBar.uss**

```bash
rm Assets/UI/Styles/WeatherBar.uss
rm Assets/UI/Styles/WeatherBar.uss.meta
```

- [ ] **Step 3: Delete Forecast.uss**

```bash
rm Assets/UI/Styles/Forecast.uss
rm Assets/UI/Styles/Forecast.uss.meta
```

- [ ] **Step 4: Delete Settings.uss**

```bash
rm Assets/UI/Styles/Settings.uss
rm Assets/UI/Styles/Settings.uss.meta
```

- [ ] **Step 5: Remove style imports from CampFireRoot.uxml**

In `CampFireRoot.uxml`, remove the `<Style>` lines that reference `WeatherBar.uss`, `Forecast.uss`, and `Settings.uss`.

- [ ] **Step 6: Remove `#player-name` from Variables.uss font-display selector list**

In `Assets/UI/Styles/Variables.uss`, remove `#player-name` from the comma-separated selector list for the Fredoka display font (line 18). The player name now lives inside the profile bloom and doesn't need the display font override.

- [ ] **Step 7: Verify no console errors**

Check Unity console for missing style references or broken selectors using `read_console`.

- [ ] **Step 8: Commit**

```bash
git add -A Assets/UI/Styles/
git add Assets/UI/Documents/CampFireRoot.uxml
git commit -m "chore: remove old top-bar, weather-bar, forecast, and settings stylesheets"
```

---

### Task 4: Create ProfilePopupUI.cs

**Files:**
- Create: `Assets/Scripts/UI/ProfilePopupUI.cs`

This controller absorbs all `SettingsUI` logic and adds profile-specific features (player name, flame level, date/time, weather summary). It manages the bloom animation on the profile circle.

- [ ] **Step 1: Create ProfilePopupUI.cs**

```csharp
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Garden
{
    public class ProfilePopupUI : MonoBehaviour
    {
        private VisualElement hudProfile;
        private VisualElement profileBloom;
        private VisualElement bloomDismiss;
        private VisualElement profilePicLarge;
        private VisualElement hudProfilePic;

        private TextField displayNameInput;
        private Label friendCodeLabel;
        private Label flameLevelLabel;
        private Label dateTimeLabel;
        private Label weatherSummaryLabel;

        private Slider musicSlider;
        private Slider sfxSlider;
        private Label musicValue;
        private Label sfxValue;
        private DropdownField langDropdown;
        private Toggle vibrationToggle;

        private Label playerIdLabel;
        private Label serverLabel;
        private Label versionLabel;

        private Button deleteBtn;
        private VisualElement confirmRow;
        private Button confirmCancel;
        private Button confirmDelete;
        private Button debugBtn;

        private bool isOpen;

        public void Initialize(VisualElement root)
        {
            hudProfile = root.Q("hud-profile");
            profileBloom = root.Q("profile-bloom");
            bloomDismiss = root.Q("bloom-dismiss");
            hudProfilePic = root.Q("hud-profile-pic");
            profilePicLarge = root.Q("profile-pic-large");

            displayNameInput = root.Q<TextField>("profile-display-name");
            friendCodeLabel = root.Q<Label>("profile-friend-code");
            flameLevelLabel = root.Q<Label>("profile-flame-level");
            dateTimeLabel = root.Q<Label>("profile-date-time");
            weatherSummaryLabel = root.Q<Label>("profile-weather-summary");

            musicSlider = root.Q<Slider>("profile-music-slider");
            sfxSlider = root.Q<Slider>("profile-sfx-slider");
            musicValue = root.Q<Label>("profile-music-value");
            sfxValue = root.Q<Label>("profile-sfx-value");
            langDropdown = root.Q<DropdownField>("profile-language-dropdown");
            vibrationToggle = root.Q<Toggle>("profile-vibration-toggle");

            playerIdLabel = root.Q<Label>("profile-player-id");
            serverLabel = root.Q<Label>("profile-server");
            versionLabel = root.Q<Label>("profile-version");

            deleteBtn = root.Q<Button>("profile-delete-btn");
            confirmRow = root.Q<VisualElement>("profile-confirm-row");
            confirmCancel = root.Q<Button>("profile-confirm-cancel");
            confirmDelete = root.Q<Button>("profile-confirm-delete");
            debugBtn = root.Q<Button>("profile-debug-btn");

            // Profile pic click → toggle bloom
            hudProfile?.RegisterCallback<ClickEvent>(OnProfileClicked);
            bloomDismiss?.RegisterCallback<ClickEvent>(OnDismissClicked);

            // Load profile pic
            LoadProfilePics();

            // Settings: audio
            var data = SaveManager.Instance?.Data;
            if (data != null && musicSlider != null && sfxSlider != null)
            {
                musicSlider.value = data.musicVolume * 100f;
                sfxSlider.value = data.sfxVolume * 100f;
                musicValue.text = $"{data.musicVolume * 100f:F0}%";
                sfxValue.text = $"{data.sfxVolume * 100f:F0}%";
            }

            musicSlider?.RegisterValueChangedCallback(evt =>
            {
                float vol = evt.newValue / 100f;
                AudioManager.Instance?.SetMusicVolume(vol);
                if (musicValue != null) musicValue.text = $"{evt.newValue:F0}%";
                if (SaveManager.Instance?.Data != null)
                {
                    SaveManager.Instance.Data.musicVolume = vol;
                    SaveManager.Instance.Save();
                }
            });

            sfxSlider?.RegisterValueChangedCallback(evt =>
            {
                float vol = evt.newValue / 100f;
                AudioManager.Instance?.SetSFXVolume(vol);
                if (sfxValue != null) sfxValue.text = $"{evt.newValue:F0}%";
                if (SaveManager.Instance?.Data != null)
                {
                    SaveManager.Instance.Data.sfxVolume = vol;
                    SaveManager.Instance.Save();
                }
            });

            // Settings: vibration
            if (vibrationToggle != null && data != null)
            {
                vibrationToggle.value = data.vibrationEnabled;
                vibrationToggle.RegisterValueChangedCallback(evt =>
                {
                    if (SaveManager.Instance?.Data != null)
                    {
                        SaveManager.Instance.Data.vibrationEnabled = evt.newValue;
                        SaveManager.Instance.Save();
                    }
                });
            }

            // Settings: language
            if (langDropdown != null)
            {
                RefreshLanguageDropdown();
                langDropdown.RegisterValueChangedCallback(evt =>
                {
                    if (LocalizationService.Instance != null)
                        _ = LocalizationService.Instance.SwitchLocale(evt.newValue);
                });
            }

            // Account info
            var socialData = SocialSaveManager.Instance?.Data;
            if (playerIdLabel != null)
                playerIdLabel.text = !string.IsNullOrEmpty(socialData?.uid) ? socialData.uid : "---";
            if (serverLabel != null)
                serverLabel.text = ServerConfig.Current.name;
            if (versionLabel != null)
                versionLabel.text = Application.version;

            // Friend code
            if (friendCodeLabel != null && socialData != null)
                friendCodeLabel.text = $"Code: {socialData.friendCode ?? "---"}";

            // Display name
            if (displayNameInput != null)
            {
                var name = SocialSaveManager.Instance?.Data?.displayName;
                if (!string.IsNullOrEmpty(name))
                    displayNameInput.SetValueWithoutNotify(name);
                displayNameInput.RegisterValueChangedCallback(evt =>
                {
                    if (SocialService.Instance != null)
                        SocialService.Instance.UpdateDisplayName(evt.newValue);
                });
            }

            // Delete save
            if (deleteBtn != null)
            {
                deleteBtn.clicked += () =>
                {
                    deleteBtn.style.display = DisplayStyle.None;
                    if (confirmRow != null) confirmRow.style.display = DisplayStyle.Flex;
                };
            }

            if (confirmCancel != null)
            {
                confirmCancel.clicked += () =>
                {
                    if (confirmRow != null) confirmRow.style.display = DisplayStyle.None;
                    if (deleteBtn != null) deleteBtn.style.display = DisplayStyle.Flex;
                };
            }

            if (confirmDelete != null)
            {
                confirmDelete.clicked += () =>
                {
                    SaveManager.Instance?.DeleteSave();
                    SocialSaveManager.Instance?.DeleteSave();
                    SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                };
            }

            // Debug button
            if (debugBtn != null)
            {
                if (Application.isEditor || Debug.isDebugBuild)
                {
                    debugBtn.style.display = DisplayStyle.Flex;
                    debugBtn.clicked += OnDebugClicked;
                }
            }

            // Locale changes
            if (LocalizationService.Instance != null)
                LocalizationService.Instance.OnLocaleChanged += OnLocaleChanged;
        }

        private void OnDestroy()
        {
            if (LocalizationService.Instance != null)
                LocalizationService.Instance.OnLocaleChanged -= OnLocaleChanged;
        }

        private void Update()
        {
            if (isOpen && dateTimeLabel != null)
            {
                var now = GameTime.Now;
                dateTimeLabel.text = now.ToString("dd MMM  h:mm tt").ToUpper();
            }
        }

        public void RefreshLanguageDropdown()
        {
            if (langDropdown == null || LocalizationService.Instance == null) return;
            langDropdown.choices = LocalizationService.Instance.SupportedLocales;
            langDropdown.SetValueWithoutNotify(LocalizationService.Instance.CurrentLocale);
        }

        public void RefreshContent()
        {
            // Flame level
            if (flameLevelLabel != null && SaveManager.Instance?.Data != null)
                flameLevelLabel.text = $"Flame Level {SaveManager.Instance.Data.flameLevel}";

            // Weather summary
            if (weatherSummaryLabel != null && WeatherService.Instance != null)
            {
                var w = WeatherService.Instance.CurrentWeather;
                weatherSummaryLabel.text = $"{w.condition} {w.temperature:F0}\u00b0";
            }
        }

        private void LoadProfilePics()
        {
            var tex = SpriteService.Instance?.GetTexture("ui/profile");
            if (tex != null)
            {
                if (hudProfilePic != null)
                    hudProfilePic.style.backgroundImage = tex;
                if (profilePicLarge != null)
                    profilePicLarge.style.backgroundImage = tex;
            }
        }

        private void OnProfileClicked(ClickEvent evt)
        {
            evt.StopPropagation();
            ToggleBloom();
        }

        private void OnDismissClicked(ClickEvent evt)
        {
            if (isOpen) CloseBloom();
        }

        public void ToggleBloom()
        {
            if (isOpen)
                CloseBloom();
            else
                OpenBloom();
        }

        public void OpenBloom()
        {
            if (profileBloom == null) return;
            isOpen = true;
            RefreshContent();
            profileBloom.AddToClassList("bloom-open");
            bloomDismiss?.AddToClassList("bloom-dismiss-active");
        }

        public void CloseBloom()
        {
            if (profileBloom == null) return;
            isOpen = false;
            profileBloom.RemoveFromClassList("bloom-open");
            bloomDismiss?.RemoveFromClassList("bloom-dismiss-active");

            // Reset delete confirm state
            if (deleteBtn != null) deleteBtn.style.display = DisplayStyle.Flex;
            if (confirmRow != null) confirmRow.style.display = DisplayStyle.None;
        }

        private void OnDebugClicked()
        {
            CloseBloom();
            // Open debug overlay through CampFireUI
            var debugPanelElement = profileBloom?.panel?.visualTree?.Q("debug-panel");
            if (debugPanelElement != null)
                CampFireUI.Instance?.OpenOverlay("Debug", debugPanelElement);
        }

        private void OnLocaleChanged()
        {
            RefreshLanguageDropdown();
        }
    }
}
```

- [ ] **Step 2: Check compilation**

Use `read_console` MCP tool to verify no compile errors after Unity reloads.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/UI/ProfilePopupUI.cs
git commit -m "feat: add ProfilePopupUI — profile bloom popup absorbing SettingsUI"
```

---

### Task 5: Refactor WeatherBarUI.cs

**Files:**
- Modify: `Assets/Scripts/UI/WeatherBarUI.cs`

Retarget from old `#weather-bar` elements to `#hud-weather` button + `#forecast-bloom` card. Add bloom toggle animation.

- [ ] **Step 1: Rewrite WeatherBarUI.cs**

Replace the entire file content with:

```csharp
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class WeatherBarUI : MonoBehaviour
    {
        private VisualElement hudWeather;
        private VisualElement hudWeatherIcon;
        private VisualElement forecastBloom;
        private VisualElement bloomDismiss;
        private Label forecastTitle;
        private VisualElement forecastStats;
        private VisualElement forecastDays;
        private Label forecastSectionLabel;
        private Label dateTime;

        // Visiting name label (child of hud-profile)
        private Label visitingNameLabel;
        private VisualElement hudProfile;

        private bool isOpen;
        private bool iconsLoaded;

        private static readonly int[] MoonPhaseToSpriteIndex = { 5, 6, 7, 8, 1, 2, 3, 4 };

        public void Initialize(VisualElement root)
        {
            hudWeather = root.Q("hud-weather");
            hudWeatherIcon = root.Q("hud-weather-icon");
            forecastBloom = root.Q("forecast-bloom");
            bloomDismiss = root.Q("bloom-dismiss");
            forecastTitle = root.Q<Label>("forecast-bloom-title");
            forecastStats = root.Q("forecast-bloom-stats");
            forecastDays = root.Q("forecast-bloom-days");
            forecastSectionLabel = root.Q<Label>("forecast-section-label");
            hudProfile = root.Q("hud-profile");

            hudWeather?.RegisterCallback<ClickEvent>(OnWeatherClicked);
            bloomDismiss?.RegisterCallback<ClickEvent>(OnDismissClicked);

            if (WeatherService.Instance != null)
            {
                WeatherService.Instance.OnWeatherUpdated += UpdateWeatherIcon;
                WeatherService.Instance.OnForecastUpdated += PopulateForecast;
                UpdateWeatherIcon(WeatherService.Instance.CurrentWeather);
                if (WeatherService.Instance.Forecast.Count > 0)
                    PopulateForecast();
            }
        }

        private void OnDestroy()
        {
            if (WeatherService.Instance != null)
            {
                WeatherService.Instance.OnWeatherUpdated -= UpdateWeatherIcon;
                WeatherService.Instance.OnForecastUpdated -= PopulateForecast;
            }
        }

        private void OnWeatherClicked(ClickEvent evt)
        {
            evt.StopPropagation();
            ToggleBloom();
        }

        private void OnDismissClicked(ClickEvent evt)
        {
            if (isOpen) CloseBloom();
        }

        public void ToggleBloom()
        {
            if (isOpen) CloseBloom();
            else OpenBloom();
        }

        private void OpenBloom()
        {
            if (forecastBloom == null) return;
            isOpen = true;
            PopulateForecast();
            forecastBloom.AddToClassList("bloom-open");
            bloomDismiss?.AddToClassList("bloom-dismiss-active");
            if (hudWeatherIcon != null)
                hudWeatherIcon.style.rotate = new Rotate(180);
        }

        private void CloseBloom()
        {
            if (forecastBloom == null) return;
            isOpen = false;
            forecastBloom.RemoveFromClassList("bloom-open");
            bloomDismiss?.RemoveFromClassList("bloom-dismiss-active");
            if (hudWeatherIcon != null)
                hudWeatherIcon.style.rotate = new Rotate(0);
        }

        /// <summary>
        /// Called by external code when the dismiss backdrop is clicked
        /// and both bloom cards need to close.
        /// </summary>
        public bool IsOpen => isOpen;

        private void UpdateWeatherIcon(WeatherData weather)
        {
            if (hudWeatherIcon == null) return;
            var tex = GetWeatherIcon(weather.condition);
            if (tex != null)
                hudWeatherIcon.style.backgroundImage = tex;
        }

        private void PopulateForecast()
        {
            if (forecastStats == null || forecastDays == null) return;
            forecastStats.Clear();
            forecastDays.Clear();

            var weather = WeatherService.Instance?.CurrentWeather ?? default;

            // Title
            if (forecastTitle != null)
            {
                var condKey = $"ui.weather.{weather.condition.ToString().ToLower()}";
                forecastTitle.text = $"{Loc.Get("ui.weather.today", "Today")} \u2014 {Loc.Get(condKey, weather.condition.ToString())}";
            }

            // Today's stats
            AddStatRow(forecastStats, Loc.Get("ui.weather.temp", "Temp"), $"{weather.temperature:F0}\u00b0");
            AddStatRow(forecastStats, Loc.Get("ui.weather.humidity", "Humidity"), $"{weather.humidity:F0}%");
            AddStatRow(forecastStats, Loc.Get("ui.weather.wind", "Wind"), $"{weather.windSpeed:F1} m/s");

            var moonTex = GetMoonTexture((int)weather.moonPhase);
            if (moonTex != null)
                AddStatRow(forecastStats, Loc.Get("ui.weather.moon", "Moon"), "", moonTex);

            // Forecast section label
            if (forecastSectionLabel != null)
                forecastSectionLabel.text = Loc.Get("ui.weather.forecast", "Forecast");

            // Future days
            var forecast = WeatherService.Instance?.Forecast;
            if (forecast == null) return;

            float sunriseHour = weather.sunriseHour;
            float sunsetHour = weather.sunsetHour;

            foreach (var day in forecast)
            {
                var row = new VisualElement();
                row.AddToClassList("forecast-day-row");

                var dayLabelText = Loc.Get($"ui.weather.day_{day.dayLabel.ToLower()}", day.dayLabel);
                var dayLabel = new Label(dayLabelText.ToUpper());
                dayLabel.AddToClassList("forecast-day-name");
                row.Add(dayLabel);

                var icon = new VisualElement();
                icon.AddToClassList("forecast-day-icon");
                var dayTex = GetWeatherIcon(day.condition);
                if (dayTex != null)
                    icon.style.backgroundImage = dayTex;
                row.Add(icon);

                var tempLabel = new Label($"{day.tempHigh:F0}\u00b0/{day.tempLow:F0}\u00b0");
                tempLabel.AddToClassList("forecast-day-temp");
                row.Add(tempLabel);

                forecastDays.Add(row);
            }
        }

        private static void AddStatRow(VisualElement parent, string label, string value, Texture2D iconTex = null)
        {
            var row = new VisualElement();
            row.AddToClassList("forecast-stat-row");

            var lbl = new Label(label);
            lbl.AddToClassList("forecast-stat-label");
            row.Add(lbl);

            if (iconTex != null)
            {
                var icon = new VisualElement();
                icon.style.width = 24;
                icon.style.height = 24;
                icon.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
                icon.style.backgroundImage = iconTex;
                row.Add(icon);
            }
            else
            {
                var val = new Label(value);
                val.AddToClassList("forecast-stat-value");
                row.Add(val);
            }

            parent.Add(row);
        }

        public void SetVisitingName(string friendName)
        {
            if (hudProfile == null) return;

            if (friendName != null)
            {
                if (visitingNameLabel == null)
                {
                    visitingNameLabel = new Label();
                    visitingNameLabel.name = "hud-visiting-name";
                    hudProfile.Add(visitingNameLabel);
                }
                visitingNameLabel.text = $"{friendName}'s Camp";
                visitingNameLabel.style.display = DisplayStyle.Flex;
            }
            else
            {
                if (visitingNameLabel != null)
                    visitingNameLabel.style.display = DisplayStyle.None;
            }
        }

        private Texture2D GetMoonTexture(int phaseIndex)
        {
            int spriteIdx = MoonPhaseToSpriteIndex[phaseIndex] - 1;
            return SpriteService.Instance?.GetTexture($"moon/phase-{spriteIdx + 1}");
        }

        private static readonly string[] WeatherConditionKeys =
        {
            "ui/weather-clear", "ui/weather-cloudy", "ui/weather-rain",
            "ui/weather-storm", "ui/weather-snow"
        };

        private Texture2D GetWeatherIcon(WeatherCondition condition)
        {
            int idx = (int)condition;
            if (idx < 0 || idx >= WeatherConditionKeys.Length) return null;
            return SpriteService.Instance?.GetTexture(WeatherConditionKeys[idx]);
        }
    }
}
```

- [ ] **Step 2: Check compilation**

Use `read_console` to verify no compile errors.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/UI/WeatherBarUI.cs
git commit -m "feat: refactor WeatherBarUI to floating button + bloom forecast card"
```

---

### Task 6: Retarget ResourceDisplayUI.cs

**Files:**
- Modify: `Assets/Scripts/UI/ResourceDisplayUI.cs`

- [ ] **Step 1: Update element queries**

In `ResourceDisplayUI.cs`, change the `Initialize` method's element queries:

```csharp
// Old:
manaDisplay = root.Q<Label>("mana-display");
waterDisplay = root.Q<Label>("water-display");
mallumDisplay = root.Q<Label>("mallum-display");
manaIcon = root.Q("mana-icon");
waterIcon = root.Q("water-icon");
mallumIcon = root.Q("mallum-icon");

// New:
manaDisplay = root.Q<Label>("hud-mana");
waterDisplay = root.Q<Label>("hud-water");
mallumDisplay = root.Q<Label>("hud-mallum");
manaIcon = root.Q("hud-mana-icon");
waterIcon = root.Q("hud-water-icon");
mallumIcon = root.Q("hud-mallum-icon");
```

- [ ] **Step 2: Update sprite keys for smaller icons**

In the `Update` method, the icon sprite keys remain the same (`ui/resource-mana`, `ui/resource-water`, `ui/resource-mallum`). No change needed.

- [ ] **Step 3: Check compilation**

Use `read_console` to verify no compile errors.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/UI/ResourceDisplayUI.cs
git commit -m "feat: retarget ResourceDisplayUI to floating HUD element IDs"
```

---

### Task 7: Update CampFireUI.cs — wire new controllers, remove old ones

**Files:**
- Modify: `Assets/Scripts/UI/CampFireUI.cs`
- Delete: `Assets/Scripts/UI/SettingsUI.cs`

- [ ] **Step 1: Replace SettingsUI with ProfilePopupUI**

In `CampFireUI.cs`:

1. Replace the field declaration `private SettingsUI settingsUI;` with `private ProfilePopupUI profilePopup;`

2. Remove the field `private VisualElement settingsPanel;`

3. In `Start()`, replace:
```csharp
settingsUI = GetComponent<SettingsUI>();
settingsUI?.Initialize(root);
```
with:
```csharp
profilePopup = GetComponent<ProfilePopupUI>();
profilePopup?.Initialize(root);
```

4. Remove the settings button wiring block (lines 179–185):
```csharp
// Remove this entire block:
settingsBtn = root.Q<Button>("btn-settings");
if (settingsBtn != null)
{
    TryLoadSettingsIcon();
    settingsBtn.clicked += () => OpenOverlay(Loc.Get("ui.settings.title", "Settings"), settingsPanel);
}
```

5. Remove the debug button wiring block (lines 188–199):
```csharp
// Remove this entire block:
var debugBtn = root.Q<Button>("btn-debug");
if (debugBtn != null)
{
    ...
}
```

6. Remove the `settingsBtn` field, `_settingsIconLoaded` field, and `TryLoadSettingsIcon()` method entirely.

7. In `Update()`, remove the `TryLoadSettingsIcon()` call.

8. In `HideAllPanels()`, remove the line:
```csharp
if (settingsPanel != null) settingsPanel.style.display = DisplayStyle.None;
```

9. In `OnGameReady()`, replace `settingsUI?.RefreshLanguageDropdown()` with `profilePopup?.RefreshLanguageDropdown()`.

- [ ] **Step 2: Delete SettingsUI.cs**

```bash
rm Assets/Scripts/UI/SettingsUI.cs
rm Assets/Scripts/UI/SettingsUI.cs.meta
```

- [ ] **Step 3: Ensure DebugService is still added**

The debug button init in CampFireUI previously added `DebugService` to the GameObject. Move that logic into `ProfilePopupUI.OnDebugClicked` or ensure `DebugService` is added during `ProfilePopupUI.Initialize`. Actually, `DebugWeatherPanel` is still initialized separately in CampFireUI, so `DebugService` addition should stay in CampFireUI's `Start()`. Add after the existing `debugPanel?.Initialize(root)` line:

```csharp
if (Application.isEditor || Debug.isDebugBuild)
{
    if (GetComponent<DebugService>() == null)
        gameObject.AddComponent<DebugService>();
}
```

- [ ] **Step 4: Check compilation**

Use `read_console` to verify no compile errors.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/UI/CampFireUI.cs
git rm Assets/Scripts/UI/SettingsUI.cs
git commit -m "feat: wire ProfilePopupUI in CampFireUI, remove SettingsUI"
```

---

### Task 8: Update SafeAreaController.cs

**Files:**
- Modify: `Assets/Scripts/UI/SafeAreaController.cs`

- [ ] **Step 1: Replace top-bar safe area logic with floating-hud logic**

In `SafeAreaController.cs`, replace the `#top-bar` block (lines 30–35):

```csharp
// Old:
var topBar = root.Q("top-bar");
if (topBar != null)
{
    topBar.style.marginTop = -topBleed;
    topBar.style.paddingTop = topBleed;
}

// New — shift the floating HUD down by the safe area inset:
var floatingHud = root.Q("floating-hud");
if (floatingHud != null)
{
    floatingHud.style.top = 10 + topBleed;
}

// Also shift bloom cards down
var forecastBloom = root.Q("forecast-bloom");
if (forecastBloom != null)
    forecastBloom.style.top = 10 + topBleed;
var profileBloom = root.Q("profile-bloom");
if (profileBloom != null)
    profileBloom.style.top = 10 + topBleed;
```

- [ ] **Step 2: Check compilation**

Use `read_console` to verify no compile errors.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/UI/SafeAreaController.cs
git commit -m "feat: apply safe area insets to floating HUD instead of top-bar"
```

---

### Task 9: Add ProfilePopupUI component to UI GameObject

**Files:**
- Scene: `Assets/Scenes/Garden.unity`

The existing controllers (`WeatherBarUI`, `ResourceDisplayUI`, `SettingsUI`, etc.) are all MonoBehaviours on the `--- UI ---` GameObject. We need to add `ProfilePopupUI` and remove `SettingsUI`.

- [ ] **Step 1: Add ProfilePopupUI component**

Use Unity MCP `manage_components` tool:
```
action: "add"
game_object_name: "--- UI ---"
component_type: "Garden.ProfilePopupUI"
```

- [ ] **Step 2: Remove SettingsUI component**

Use Unity MCP `manage_components` tool:
```
action: "remove"
game_object_name: "--- UI ---"
component_type: "Garden.SettingsUI"
```

- [ ] **Step 3: Save scene**

Use Unity MCP `manage_scene` tool:
```
action: "save"
```

- [ ] **Step 4: Commit**

```bash
git add Assets/Scenes/Garden.unity
git commit -m "feat: add ProfilePopupUI component to UI GameObject, remove SettingsUI"
```

---

### Task 10: Bloom dismiss coordination — ensure only one bloom open at a time

**Files:**
- Modify: `Assets/Scripts/UI/CampFireUI.cs`

Both `WeatherBarUI` and `ProfilePopupUI` use the same `#bloom-dismiss` backdrop. We need to ensure opening one closes the other.

- [ ] **Step 1: Add bloom coordination in CampFireUI**

After initializing both controllers in `CampFireUI.Start()`, add:

```csharp
// Coordinate bloom popups — opening one closes the other
var bloomDismiss = root.Q("bloom-dismiss");
if (bloomDismiss != null)
{
    bloomDismiss.RegisterCallback<ClickEvent>(_ =>
    {
        if (weatherBar != null && weatherBar.IsOpen)
            weatherBar.ToggleBloom();
        if (profilePopup != null)
            profilePopup.CloseBloom();
    });
}
```

Wait — actually both controllers already register their own dismiss handlers. The issue is that the dismiss backdrop is shared. When one bloom opens, the other should close. Add coordination by having each controller's open method close the other:

In `CampFireUI.Start()`, after both are initialized:

```csharp
// Wire HUD profile click to also close weather bloom
var hudProfile = root.Q("hud-profile");
hudProfile?.RegisterCallback<ClickEvent>(_ =>
{
    if (weatherBar != null && weatherBar.IsOpen)
        weatherBar.ToggleBloom();
}, TrickleDown.TrickleDown);

var hudWeather = root.Q("hud-weather");
hudWeather?.RegisterCallback<ClickEvent>(_ =>
{
    profilePopup?.CloseBloom();
}, TrickleDown.TrickleDown);
```

- [ ] **Step 2: Check in Unity that tapping profile closes weather and vice versa**

Open both popups in play mode and verify the coordination.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/UI/CampFireUI.cs
git commit -m "feat: coordinate bloom popups — opening one closes the other"
```

---

### Task 11: Visual verification and polish

**Files:** None (testing only)

- [ ] **Step 1: Enter play mode and verify floating HUD appears**

Check that:
- Profile circle is visible top-left with profile pic
- Resource pill is centered with mana/water/mallum counts
- Weather circle is visible top-right with weather icon
- No solid bar background — game art extends to top

- [ ] **Step 2: Verify weather bloom animation**

Tap weather button:
- Circle should bloom into 280px card with springy animation
- Forecast content should fade in after shape expands
- Tapping outside or button again should close
- Weather icon should rotate while open

- [ ] **Step 3: Verify profile bloom animation**

Tap profile pic:
- Circle should bloom into 320px card
- All settings sections should be visible and functional
- Language dropdown, audio sliders, vibration toggle should work
- Delete save flow should work
- Debug button should appear in dev builds

- [ ] **Step 4: Verify safe area on device (or simulate notch)**

Check that floating elements respect safe area insets — not hidden under notch/status bar.

- [ ] **Step 5: Verify resource counts update**

Plant/harvest/water to change mana/water counts and verify the floating pill updates in real-time.

- [ ] **Step 6: Fix any visual issues found**

Adjust USS values as needed for spacing, sizing, colors, animation timing.

- [ ] **Step 7: Commit any fixes**

```bash
git add -A
git commit -m "fix: visual polish for floating HUD"
```
