# Garden MVP Design

**Date:** 2026-02-23
**Scope:** Full MVP + Debug Weather Simulation Tools
**Engine:** Unity 6 (6000.3.6f1) with 2D URP

## Decisions

- **Architecture:** ScriptableObject-driven with singleton services
- **Weather:** Real OpenWeatherMap API + debug override panel
- **Visuals:** Procedural 2D sprites with shader/particle effects per variant
- **UI:** uGUI with glassmorphism aesthetic (semi-transparent blurred panels)

## Data Architecture

### SeedData (ScriptableObject)
- `string seedName`
- `Sprite icon`
- `string description`
- `float baseGrowthHours` (4-72 range)
- `List<VariantData> variants`

### VariantData (ScriptableObject)
- `string variantName`
- `Rarity rarity` (Common, Uncommon, Rare, Epic, Legendary)
- `int priority` (1=Celestial, 2=Extreme, 3=Temporal, 4=Default)
- `TriggerCondition trigger` - serialized conditions:
  - Temperature range (min/max Celsius)
  - Weather types (Rain, Storm, Snow, Clear, Overcast)
  - Wind speed threshold
  - Time of day (Night, GoldenHour, Day)
  - Moon phase (NewMoon, Full, WaxingCrescent, etc.)
  - Calendar events (Equinox, Eclipse)
  - Humidity threshold
- Visual config:
  - `Color primaryColor, secondaryColor`
  - `Material variantMaterial` (shader effects)
  - `GameObject particleEffect` (glow, arcs, dust trails)
  - `string description` (codex flavor text)
  - `string discoveryHint` (cryptic hint for undiscovered)

### CurrencyConfig (ScriptableObject)
- Dewdrops earned per harvest (by rarity tier)
- Sun-Shards earned per achievement
- Aura Dust generation rate per greenhouse plant (by rarity)

## Core Systems

### WeatherService (Singleton)
- Fetches current weather from OpenWeatherMap free tier (`/data/2.5/weather`)
- Requires API key (stored in config, not in source)
- Polls every 15 minutes, caches result
- Exposes `WeatherData` struct:
  - `float temperature` (Celsius)
  - `float humidity` (0-100%)
  - `float windSpeed` (m/s)
  - `WeatherCondition condition` (Clear, Cloudy, Rain, Storm, Snow)
  - `float cloudCover` (0-100%)
  - `bool isNight` (derived from sunrise/sunset or local time)
  - `bool isGoldenHour` (derived from sunset time)
  - `MoonPhase moonPhase` (algorithmically calculated)
  - `CalendarEvent calendarEvent` (checked against known dates)
- Debug mode: all fields overridable via DebugWeatherPanel
- Events: `OnWeatherUpdated`

### GeneticsEngine (Static/Service)
- Input: `SeedData` + `WeatherData` at planting time
- Iterates variants sorted by priority (ascending = highest priority first)
- First variant whose `TriggerCondition` matches current weather wins
- Returns resolved `VariantData` + growth speed modifier
- Growth boost: +25% speed if weather still matches preferred state during growth

### PlantManager (Singleton)
- Manages single active plant slot
- States: Empty -> Planted -> Growing -> Mature
- Tracks: seed used, resolved variant, plant time, growth progress
- Real-time growth: calculates elapsed time + weather bonus accumulation
- Harvest action: moves plant to Greenhouse, awards Dewdrops

### GreenhouseManager (Singleton)
- Stores list of harvested plants (persistent)
- Each plant generates passive Aura Dust based on rarity
- Slot-limited (default 6, expandable with Sun-Shards)

### CurrencyManager (Singleton)
- Tracks: Dewdrops (soft), Sun-Shards (hard), Aura Dust (passive)
- Events: `OnCurrencyChanged(CurrencyType, int oldValue, int newValue)`
- Methods: `Add()`, `Spend()`, `CanAfford()`

### SaveManager (Singleton)
- JSON serialization to `Application.persistentDataPath/save.json`
- Auto-saves on key actions (plant, harvest, currency change)
- Saves: active plant state, greenhouse contents, currencies, discovered variants, seed inventory
- Loads on startup

## UI Screens

### HortusView (Main Screen)
- **Resonance Bar** (top): Pulsing bar showing current weather metadata
  - Format: "18C - Overcast - Waxing Crescent"
- **Plant Display** (center): 2D procedural plant visualization
  - Growth animation over time
  - Variant-specific colors, glow effects, particle systems
- **Pulse Button** (bottom center): Large circular button
  - If empty: opens Seed Satchel
  - If growing: shows ripple effect + remaining growth time
  - If mature: shows "Harvest" prompt
- **Weather Overlay**: Rain streaks, snow particles, wind lines on screen glass

### SeedSatchelPanel (Overlay)
- Grid of owned seeds with icons
- Selecting a seed shows probability preview
- High-probability variants glow based on current weather
- "Plant" button to start growth cycle

### FloraCodexPanel (Overlay)
- Per-seed variant grid
- Discovered variants: full art + stats
- Undiscovered: dark silhouette + cryptic hint on tap

### GreenhousePanel (Overlay)
- Grid of harvested plants in slots
- Each shows passive Aura Dust generation rate
- Total generation displayed at top

### DebugWeatherPanel (Dev Only)
- Toggle: Real API vs Simulated
- Sliders: Temperature (-20 to 50C), Humidity (0-100%), Wind (0-50 m/s)
- Dropdowns: Weather condition, Moon phase, Time of day
- Calendar event toggle (Equinox, Eclipse)
- "Apply" button pushes overrides to WeatherService
- Preset buttons: "Blizzard", "Thunderstorm", "Clear Night", "Golden Hour"

## Astra Seed Configuration

12 variants as defined in spec, configured as VariantData ScriptableObjects:

| Variant | Priority | Trigger |
|---------|----------|---------|
| Blood-Moon Astra | 1 | Lunar Eclipse |
| Equinox Astra | 1 | Spring/Fall Equinox |
| Static Astra | 2 | Storm condition |
| Glacial Astra | 2 | Temp < 5C |
| Petrified Astra | 2 | Temp > 38C |
| Gale-Force Astra | 2 | Wind > 8.9 m/s (20mph) |
| Void Astra | 2 | New Moon + Midnight (23:00-01:00) |
| Dew-Drop Astra | 3 | Rain or Humidity > 80% |
| Nebula Astra | 3 | Golden Hour |
| Lunar Astra | 3 | Nighttime |
| Solar Astra | 3 | Clear + Temp > 25C |
| Astra Base | 4 | Default fallback |

## File Structure

```
Assets/
  Scripts/
    Data/           SeedData.cs, VariantData.cs, CurrencyConfig.cs, enums
    Services/       WeatherService.cs, GeneticsEngine.cs, SaveManager.cs, CurrencyManager.cs
    Managers/       PlantManager.cs, GreenhouseManager.cs, GameManager.cs
    UI/             HortusUI.cs, SatchelUI.cs, CodexUI.cs, GreenhouseUI.cs, ResonanceBar.cs, PulseButton.cs, WeatherOverlay.cs
    Debug/          DebugWeatherPanel.cs
    Utils/          MoonPhaseCalculator.cs, TimeUtils.cs, CalendarEvents.cs
  Resources/
    Seeds/          Astra SeedData SO
    Variants/       12 Astra VariantData SOs
    Config/         CurrencyConfig SO
  Prefabs/
    UI/             Panel prefabs
    Plants/         Plant visual prefab with configurable components
    Effects/        Particle system prefabs (glow, rain, arcs, dust)
  Materials/
    Plants/         Variant-specific materials/shaders
  Scenes/
    MainScene.unity
```
