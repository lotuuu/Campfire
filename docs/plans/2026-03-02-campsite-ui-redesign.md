# Campsite UI Redesign Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Restructure the main campsite UI to match the pixel art sketch — framed top bar with player name/date, 4-cell weather display, and SEEDS/CRAFT/MAIL bottom nav.

**Architecture:** In-place restructure of CampFireRoot.uxml, restyling of WeatherBar.uss/CampSite.uss/BottomNav.uss, and controller updates in WeatherBarUI.cs, CampFireUI.cs, BottomNavUI.cs, and ResourceDisplayUI.cs. No new files needed. No data model changes.

**Tech Stack:** Unity UI Toolkit (UXML, USS, C# MonoBehaviour controllers)

---

### Task 1: Restructure CampFireRoot.uxml — Top Bar

Replace the current `#weather-bar` and `#resource-bar` with a new framed `#top-bar` container containing a header row, weather row, and resource display.

**Files:**
- Modify: `Assets/UI/Documents/CampFireRoot.uxml:17-37`

**Step 1: Replace the weather-bar and resource-bar sections**

Replace lines 19-37 (the weather-bar, forecast-panel, and resource-bar) with this new structure:

```xml
        <!-- Top bar frame -->
        <ui:VisualElement name="top-bar">

            <!-- Header row: player name + date/time -->
            <ui:VisualElement name="top-header">
                <ui:Label name="player-name" text="Camper" />
                <ui:Button name="btn-debug" text="&#x2699;" />
                <ui:VisualElement name="top-header-spacer" />
                <ui:Label name="date-time" text="--" />
            </ui:VisualElement>

            <!-- Weather row: 4 cells -->
            <ui:VisualElement name="weather-bar">
                <ui:VisualElement name="weather-cell-condition" class="weather-cell">
                    <ui:Label name="weather-condition-label" text="--" />
                    <ui:Label name="weather-icon" text="--" />
                </ui:VisualElement>
                <ui:VisualElement name="weather-cell-humidity" class="weather-cell">
                    <ui:Label name="weather-humidity-icon" text="&#x1F4A7;" />
                    <ui:Label name="weather-humidity" text="--" />
                </ui:VisualElement>
                <ui:VisualElement name="weather-cell-temp" class="weather-cell">
                    <ui:Label name="weather-temp-icon" text="&#x1F321;" />
                    <ui:Label name="weather-temp" text="--" />
                </ui:VisualElement>
                <ui:VisualElement name="weather-cell-moon" class="weather-cell">
                    <ui:VisualElement name="weather-moon" />
                </ui:VisualElement>
            </ui:VisualElement>

            <!-- Resources (lower-right corner) -->
            <ui:VisualElement name="resource-bar">
                <ui:Label name="mana-display" text="0" />
                <ui:Label name="water-display" text="0" />
            </ui:VisualElement>

        </ui:VisualElement>

        <ui:VisualElement name="forecast-panel">
            <ui:VisualElement name="forecast-days" />
        </ui:VisualElement>
```

Note: `#gems-display` is removed from UXML (no gems in the sketch). The debug button moves into the header row as a gear icon.

**Step 2: Verify the UXML is valid**

Open Unity and check the console for UXML parse errors. The scene should load without errors.

**Step 3: Commit**

```
git add Assets/UI/Documents/CampFireRoot.uxml
git commit -m "refactor: restructure UXML top bar with header + 4-cell weather"
```

---

### Task 2: Restructure CampFireRoot.uxml — Bottom Nav

Rename and reorder the bottom nav buttons to match the sketch: SEEDS, CRAFT, MAIL.

**Files:**
- Modify: `Assets/UI/Documents/CampFireRoot.uxml:45-49`

**Step 1: Replace bottom nav buttons**

Replace the current bottom-nav section:

```xml
        <!-- Bottom navigation -->
        <ui:VisualElement name="bottom-nav">
            <ui:Button name="btn-seeds" class="nav-btn">
                <ui:Label text="SEEDS" class="nav-btn-label" />
                <ui:Label text="&#x1F331;" class="nav-btn-icon" />
            </ui:Button>
            <ui:Button name="btn-craft" class="nav-btn">
                <ui:Label text="CRAFT" class="nav-btn-label" />
                <ui:Label text="&#x1F525;" class="nav-btn-icon" />
            </ui:Button>
            <ui:Button name="btn-mail" class="nav-btn">
                <ui:Label text="MAIL" class="nav-btn-label" />
                <ui:Label text="&#x2709;" class="nav-btn-icon" />
            </ui:Button>
        </ui:VisualElement>
```

Each button now has a text label + icon placeholder as child Labels (vertical stack).

**Step 2: Commit**

```
git add Assets/UI/Documents/CampFireRoot.uxml
git commit -m "refactor: rename bottom nav to SEEDS/CRAFT/MAIL with icons"
```

---

### Task 3: Style the Top Bar Frame and Header

Add CSS for the new `#top-bar` container, `#top-header` row, and resource positioning.

**Files:**
- Modify: `Assets/UI/Styles/CampSite.uss:10-53` (replace resource-bar and btn-debug rules)

**Step 1: Replace the `#resource-bar`, `#mana-display`, `#water-display`, `#gems-display`, and `#btn-debug` rules**

Remove all existing resource-bar and btn-debug rules (lines 10-53) and replace with:

```css
/* ── Top bar frame ── */
#top-bar {
    flex-shrink: 0;
    padding: var(--spacing-sm);
    margin: var(--spacing-xs);
    background-color: rgba(30, 20, 10, 0.85);
    border-width: 3px;
    border-color: rgba(140, 100, 50, 0.5);
    border-radius: var(--radius-md);
}

/* ── Header row ── */
#top-header {
    flex-direction: row;
    align-items: center;
    margin-bottom: var(--spacing-xs);
}

#player-name {
    font-size: var(--font-lg);
    color: var(--color-text-bright);
    -unity-font-style: bold;
}

#btn-debug {
    font-size: var(--font-sm);
    color: var(--color-text-dim);
    background-color: transparent;
    border-width: 0;
    padding: 4px 8px;
    margin-left: var(--spacing-xs);
}

#top-header-spacer {
    flex-grow: 1;
}

#date-time {
    font-size: var(--font-sm);
    color: var(--color-text-dim);
}

/* ── Resource bar (inside top-bar, lower-right) ── */
#resource-bar {
    flex-direction: row;
    justify-content: flex-end;
    padding-top: var(--spacing-xxs);
}

#mana-display {
    font-size: var(--font-xs);
    color: var(--color-mana);
    margin-right: var(--spacing-md);
}

#water-display {
    font-size: var(--font-xs);
    color: var(--color-water);
}
```

**Step 2: Commit**

```
git add Assets/UI/Styles/CampSite.uss
git commit -m "style: add top bar frame, header row, and resource positioning"
```

---

### Task 4: Style the 4-Cell Weather Bar

Rewrite WeatherBar.uss for the new 4-cell layout where each weather element sits in a bordered cell.

**Files:**
- Modify: `Assets/UI/Styles/WeatherBar.uss` (full rewrite)

**Step 1: Replace entire file contents**

```css
/* WeatherBar.uss — 4-cell weather bar */

#weather-bar {
    flex-direction: row;
    flex-shrink: 0;
    margin-top: var(--spacing-xs);
}

.weather-cell {
    flex-grow: 1;
    flex-basis: 0;
    flex-direction: row;
    align-items: center;
    justify-content: center;
    padding: var(--spacing-xs) var(--spacing-xxs);
    background-color: rgba(50, 35, 18, 0.6);
    border-width: 2px;
    border-color: rgba(140, 100, 50, 0.4);
    border-radius: var(--radius-sm);
    margin-right: var(--spacing-xxs);
}

.weather-cell:last-child {
    margin-right: 0;
}

#weather-condition-label {
    font-size: var(--font-xs);
    color: var(--color-text);
    -unity-font-style: bold;
    margin-right: var(--spacing-xxs);
}

#weather-icon {
    font-size: var(--font-md);
    color: rgb(255, 220, 120);
}

#weather-humidity-icon {
    font-size: var(--font-sm);
    color: var(--color-water);
    margin-right: 2px;
}

#weather-humidity {
    font-size: var(--font-xs);
    color: var(--color-text);
    -unity-font-style: bold;
}

#weather-temp-icon {
    font-size: var(--font-sm);
    margin-right: 2px;
}

#weather-temp {
    font-size: var(--font-xs);
    color: var(--color-text);
    -unity-font-style: bold;
}

#weather-cell-moon {
    justify-content: center;
    align-items: center;
}

#weather-moon {
    width: 36px;
    height: 36px;
    -unity-background-scale-mode: scale-to-fit;
}
```

**Step 2: Commit**

```
git add Assets/UI/Styles/WeatherBar.uss
git commit -m "style: rewrite weather bar as 4-cell grid layout"
```

---

### Task 5: Style the Bottom Nav Buttons

Update BottomNav.uss so each button is a vertical stack of label + icon, matching the sketch's chunky bordered buttons.

**Files:**
- Modify: `Assets/UI/Styles/BottomNav.uss` (full rewrite)

**Step 1: Replace entire file contents**

```css
/* BottomNav.uss — Bottom navigation bar */

#bottom-nav {
    flex-direction: row;
    flex-shrink: 0;
    background-color: rgba(25, 16, 8, 0.95);
    border-top-width: 2px;
    border-top-color: rgba(140, 100, 50, 0.4);
    padding: var(--spacing-xs);
}

.nav-btn {
    flex-grow: 1;
    flex-basis: 0;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    min-height: 100px;
    background-color: rgba(60, 40, 20, 0.6);
    border-width: 2px;
    border-color: rgba(140, 100, 50, 0.4);
    border-radius: var(--radius-sm);
    margin: 0 var(--spacing-xxs);
    padding: var(--spacing-xs);
    transition-property: background-color;
    transition-duration: 0.15s;
}

.nav-btn:hover {
    background-color: rgba(80, 55, 30, 0.7);
}

.nav-btn:active {
    background-color: rgba(100, 70, 35, 0.8);
}

.nav-btn--active {
    border-color: rgb(255, 170, 50);
}

.nav-btn-label {
    font-size: var(--font-sm);
    color: var(--color-text);
    -unity-font-style: bold;
    -unity-text-align: middle-center;
}

.nav-btn-icon {
    font-size: var(--font-lg);
    -unity-text-align: middle-center;
    margin-top: var(--spacing-xxs);
}
```

**Step 2: Commit**

```
git add Assets/UI/Styles/BottomNav.uss
git commit -m "style: bottom nav buttons as bordered vertical stacks"
```

---

### Task 6: Update WeatherBarUI.cs for New Elements

Update the controller to populate the new weather cells (humidity, hi/lo temp, condition label) and the new header elements (player name, date/time).

**Files:**
- Modify: `Assets/Scripts/UI/WeatherBarUI.cs`

**Step 1: Rewrite WeatherBarUI.cs**

The controller needs new fields and an `Update()` method for the date/time clock:

```csharp
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class WeatherBarUI : MonoBehaviour
    {
        private Label weatherIcon;
        private Label weatherConditionLabel;
        private Label weatherHumidity;
        private Label weatherTemp;
        private VisualElement weatherMoon;
        private Label playerName;
        private Label dateTime;

        private VisualElement weatherBar;
        private VisualElement forecastPanel;
        private VisualElement forecastDays;
        private VisualElement campRoot;

        private static readonly int[] MoonPhaseToSpriteIndex = { 5, 6, 7, 8, 1, 2, 3, 4 };
        private Texture2D[] moonTextures;

        public void Initialize(VisualElement root)
        {
            weatherIcon = root.Q<Label>("weather-icon");
            weatherConditionLabel = root.Q<Label>("weather-condition-label");
            weatherHumidity = root.Q<Label>("weather-humidity");
            weatherTemp = root.Q<Label>("weather-temp");
            weatherMoon = root.Q("weather-moon");
            playerName = root.Q<Label>("player-name");
            dateTime = root.Q<Label>("date-time");

            moonTextures = new Texture2D[8];
            for (int i = 0; i < 8; i++)
                moonTextures[i] = Resources.Load<Texture2D>($"MoonPhases/Moon_Phase_{i + 1}");

            weatherBar = root.Q("weather-bar");
            forecastPanel = root.Q("forecast-panel");
            forecastDays = root.Q("forecast-days");
            campRoot = root.Q("camp-root");

            weatherBar?.RegisterCallback<ClickEvent>(OnWeatherBarClicked);
            campRoot?.RegisterCallback<ClickEvent>(OnRootClicked);

            // Player name from SocialService
            UpdatePlayerName();
            if (SocialService.Instance != null)
                SocialService.Instance.OnDisplayNameUpdated += OnDisplayNameUpdated;

            if (WeatherService.Instance != null)
            {
                WeatherService.Instance.OnWeatherUpdated += UpdateWeather;
                WeatherService.Instance.OnForecastUpdated += PopulateForecast;
                UpdateWeather(WeatherService.Instance.CurrentWeather);
                if (WeatherService.Instance.Forecast.Count > 0)
                    PopulateForecast();
            }
        }

        private void OnDestroy()
        {
            if (WeatherService.Instance != null)
            {
                WeatherService.Instance.OnWeatherUpdated -= UpdateWeather;
                WeatherService.Instance.OnForecastUpdated -= PopulateForecast;
            }
            if (SocialService.Instance != null)
                SocialService.Instance.OnDisplayNameUpdated -= OnDisplayNameUpdated;
        }

        private void Update()
        {
            if (dateTime != null)
            {
                var now = GameTime.Now;
                dateTime.text = now.ToString("dd MMM  h:mm tt").ToUpper();
            }
        }

        private void UpdatePlayerName()
        {
            if (playerName == null) return;
            var name = SocialSaveManager.Instance?.Data?.displayName;
            playerName.text = string.IsNullOrEmpty(name) ? "Camper" : name;
        }

        private void OnDisplayNameUpdated(string newName)
        {
            if (playerName != null)
                playerName.text = string.IsNullOrEmpty(newName) ? "Camper" : newName;
        }

        private void OnWeatherBarClicked(ClickEvent evt)
        {
            if (forecastPanel == null) return;
            forecastPanel.ToggleInClassList("forecast-visible");
            evt.StopPropagation();
        }

        private void OnRootClicked(ClickEvent evt)
        {
            if (forecastPanel == null) return;
            if (!forecastPanel.ClassListContains("forecast-visible")) return;

            var target = evt.target as VisualElement;
            while (target != null)
            {
                if (target == forecastPanel || target == weatherBar) return;
                target = target.parent;
            }

            forecastPanel.RemoveFromClassList("forecast-visible");
        }

        private void PopulateForecast()
        {
            if (forecastDays == null) return;
            forecastDays.Clear();

            var forecast = WeatherService.Instance?.Forecast;
            if (forecast == null) return;

            foreach (var day in forecast)
            {
                var col = new VisualElement();
                col.AddToClassList("forecast-day");

                var label = new Label(day.dayLabel);
                label.AddToClassList("forecast-day-label");
                col.Add(label);

                var icon = new Label(GetWeatherEmoji(day.condition));
                icon.AddToClassList("forecast-day-icon");
                col.Add(icon);

                var temp = new Label($"{day.tempHigh:F0}/{day.tempLow:F0}");
                temp.AddToClassList("forecast-day-temp");
                col.Add(temp);

                forecastDays.Add(col);
            }
        }

        private void UpdateWeather(WeatherData weather)
        {
            if (weatherIcon != null) weatherIcon.text = GetWeatherEmoji(weather.condition);
            if (weatherConditionLabel != null) weatherConditionLabel.text = weather.condition.ToString().ToUpper();
            if (weatherHumidity != null) weatherHumidity.text = $"{weather.humidity:F0}";
            if (weatherTemp != null) weatherTemp.text = $"{weather.temperature:F0}\u00b0";
            if (weatherMoon != null)
            {
                int spriteIdx = MoonPhaseToSpriteIndex[(int)weather.moonPhase] - 1;
                var tex = moonTextures[spriteIdx];
                if (tex != null)
                    weatherMoon.style.backgroundImage = tex;
            }
        }

        private static string GetWeatherEmoji(WeatherCondition c) => c switch
        {
            WeatherCondition.Clear => "\u2600",
            WeatherCondition.Cloudy => "\u2601",
            WeatherCondition.Rain => "\ud83c\udf27",
            WeatherCondition.Storm => "\u26c8",
            WeatherCondition.Snow => "\u2744",
            _ => "?"
        };
    }
}
```

Key changes from original:
- New fields: `weatherConditionLabel`, `weatherHumidity`, `playerName`, `dateTime`
- Removed: `weatherCondition` (was the old condition label)
- Added `Update()` method that updates the date/time clock every frame
- Added `UpdatePlayerName()` and `OnDisplayNameUpdated()` for player name from SocialService
- `UpdateWeather()` now populates humidity and shows condition text in uppercase

**Step 2: Verify compilation**

Check Unity console for compile errors.

**Step 3: Commit**

```
git add Assets/Scripts/UI/WeatherBarUI.cs
git commit -m "feat: update WeatherBarUI for 4-cell layout + player name + clock"
```

---

### Task 7: Update BottomNavUI.cs for Renamed Buttons

Update button name references from `btn-apotheke`/`btn-letters`/`btn-build` to `btn-seeds`/`btn-craft`/`btn-mail`.

**Files:**
- Modify: `Assets/Scripts/UI/BottomNavUI.cs`

**Step 1: Update button queries**

Replace the full file:

```csharp
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class BottomNavUI : MonoBehaviour
    {
        public event Action OnApothekeClicked;
        public event Action OnLettersClicked;
        public event Action OnBuildClicked;

        public void Initialize(VisualElement root)
        {
            var btnSeeds = root.Q<Button>("btn-seeds");
            var btnCraft = root.Q<Button>("btn-craft");
            var btnMail = root.Q<Button>("btn-mail");

            btnSeeds?.RegisterCallback<ClickEvent>(_ => OnApothekeClicked?.Invoke());
            btnCraft?.RegisterCallback<ClickEvent>(_ => OnBuildClicked?.Invoke());
            btnMail?.RegisterCallback<ClickEvent>(_ => OnLettersClicked?.Invoke());
        }
    }
}
```

Note: Event names stay the same (`OnApothekeClicked`, `OnLettersClicked`, `OnBuildClicked`) to avoid cascading changes in CampFireUI.cs. Only the UXML button name queries change. The mapping is:
- `btn-seeds` → fires `OnApothekeClicked` (opens Apotheke/Seeds panel)
- `btn-craft` → fires `OnBuildClicked` (opens Build/Craft panel)
- `btn-mail` → fires `OnLettersClicked` (opens Letters/Mail panel)

**Step 2: Verify compilation**

**Step 3: Commit**

```
git add Assets/Scripts/UI/BottomNavUI.cs
git commit -m "refactor: update BottomNavUI button refs for SEEDS/CRAFT/MAIL"
```

---

### Task 8: Update CampFireUI.cs — Move Debug Button Wiring

The debug button moved from `#resource-bar` into `#top-header`. The button name (`btn-debug`) stays the same, so the `root.Q<Button>("btn-debug")` query in CampFireUI still works. No code change needed for that.

However, we need to update the overlay titles to match the new nav labels.

**Files:**
- Modify: `Assets/Scripts/UI/CampFireUI.cs:81-83`

**Step 1: Update overlay title strings**

Change the overlay title strings:

```csharp
                bottomNav.OnApothekeClicked += () => OpenOverlay("Seeds", apothekePanel);
                bottomNav.OnLettersClicked += () => OpenOverlay("Mail", lettersPanel);
                bottomNav.OnBuildClicked += () => OpenOverlay("Craft", buildPanel);
```

**Step 2: Commit**

```
git add Assets/Scripts/UI/CampFireUI.cs
git commit -m "refactor: update overlay titles to Seeds/Craft/Mail"
```

---

### Task 9: Update ResourceDisplayUI.cs — Remove Gems

Remove the gems display reference since it's no longer in the UXML.

**Files:**
- Modify: `Assets/Scripts/UI/ResourceDisplayUI.cs`

**Step 1: Remove gems field and update display**

Replace the full file:

```csharp
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class ResourceDisplayUI : MonoBehaviour
    {
        private Label manaDisplay;
        private Label waterDisplay;

        public void Initialize(VisualElement root)
        {
            manaDisplay = root.Q<Label>("mana-display");
            waterDisplay = root.Q<Label>("water-display");

            if (CurrencyManager.Instance != null)
                CurrencyManager.Instance.OnCurrencyChanged += OnCurrencyChanged;

            UpdateDisplay();
        }

        private void OnDestroy()
        {
            if (CurrencyManager.Instance != null)
                CurrencyManager.Instance.OnCurrencyChanged -= OnCurrencyChanged;
        }

        private void OnCurrencyChanged(CurrencyType type, float oldVal, float newVal)
        {
            UpdateDisplay();
        }

        private void Update()
        {
            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            if (manaDisplay != null && SaveManager.Instance != null)
                manaDisplay.text = $"\u2726 {SaveManager.Instance.Data.mana:F0}";
            if (waterDisplay != null && CurrencyManager.Instance != null)
                waterDisplay.text = $"\U0001F4A7 {CurrencyManager.Instance.TotalWater}";
        }
    }
}
```

Changes: removed `gemsDisplay`, updated format to show icon + number (compact style for the top bar corner).

**Step 2: Verify compilation**

**Step 3: Commit**

```
git add Assets/Scripts/UI/ResourceDisplayUI.cs
git commit -m "refactor: remove gems from ResourceDisplayUI, compact format"
```

---

### Task 10: Visual Verification

Open Unity, load the Garden scene, and verify:
1. Top bar shows player name + gear + date/time in header row
2. 4 weather cells display correctly (condition, humidity, temp, moon)
3. Resources (mana, water) appear in lower-right of top bar
4. Campsite grid still renders correctly
5. Bottom nav shows SEEDS / CRAFT / MAIL with icons
6. Tapping SEEDS opens Apotheke panel, CRAFT opens Build, MAIL opens Letters
7. Debug gear button opens debug panel
8. Forecast panel still toggles on weather bar click
