# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Garden is a hyper-contextual plant simulation game using real-world weather data. Built with Unity 6 (6000.3.6f1), 2D URP. All runtime code lives under `Assets/Scripts/` in a single assembly (`Garden.asmdef`, namespace `Garden`).

## Running Tests

Tests are Unity EditMode tests in `Assets/Tests/EditMode/`. Run via:
- **Unity Test Runner**: Window > General > Test Runner > EditMode tab
- **Unity MCP**: Use `run_tests` tool with `mode: "EditMode"`

No external test runner or CI pipeline exists. The test assembly (`Garden.Tests.EditMode.asmdef`) references the `Garden` assembly and uses NUnit.

## Architecture

### Singleton Pattern

All managers and services are MonoBehaviour singletons with duplicate-destroy guards in `Awake()`. They are **scene-bound** (no `DontDestroyOnLoad`). Access via `ClassName.Instance`.

- **Services** (`Scripts/Services/`): `WeatherService`, `SaveManager`, `CurrencyManager` are MonoBehaviour singletons. `GeneticsEngine` and `HarvestEngine` are **pure static classes** (no Instance pattern).
- **Managers** (`Scripts/Managers/`): `PlantManager`, `GreenhouseManager`, `GameManager`, `SeedRegistry`, `SeedShopManager`, `EnvironmentManager` — all MonoBehaviour singletons owning runtime state.

### ScriptableObject Data Model

Data flows: `SeedData` → contains `List<VariantData>` → each variant has a `TriggerCondition`. Weather triggers variant resolution at plant/grow time.

- `SeedData`: seed properties, buy/sell prices, preferred weather, variant list
- `VariantData`: rarity, priority (1=highest), trigger condition, visuals
- `TriggerCondition`: serializable class (not SO) with independent boolean flags (`useTemperature`, `useWeatherCondition`, etc.)
- `EnvironmentData`: growth bonus with its own `TriggerCondition` for conditional activation
- `CurrencyConfig`: economy tuning (rewards per rarity, quality multipliers, costs)
- `WeatherData`: plain `[Serializable]` struct carrying all weather state

### Weather Data Flow

1. `WeatherService` is single source of truth (`CurrentWeather`)
2. **In Editor**: immediately sets `useDebugOverride = true`, skips GPS/API calls, fires `OnLocationResolved(true)`
3. **On device**: requests GPS → calls OpenWeatherMap every 15 min → enriches with moon phase + calendar events → fires `OnWeatherUpdated`
4. Consumers subscribe to `OnWeatherUpdated`: `ResonanceBar`, `LivingCanvasController`, `WeatherOverlay`
5. `PlantManager.Update()` re-evaluates growth multipliers every frame using current weather

`GameTime` wraps `DateTime` with a debug offset — all plant timers use `GameTime.UtcNow`, moon/time-of-day use `GameTime.Now`.

### Genetics/Variant Resolution

`GeneticsEngine.Resolve(SeedData, WeatherData)`:
1. Sort variants by priority ascending (1=first checked)
2. Skip variants with `trigger == null`; call `trigger.Evaluate(weather)` on the rest
3. First match wins → `growthSpeedMultiplier = 1.25f`
4. No match → fallback to last variant in sorted list → `multiplier = 1.0f`

Growth speed is also affected by environment bonus (additive): `totalMultiplier = variantMultiplier + envBonus`.

### Save System

`SaveManager` serializes `SaveData` to JSON at `Application.persistentDataPath/save.json` using `JsonUtility`. All references stored as **name strings**, resolved at load via `Resources.LoadAll<SeedData>("Seeds")`. Save-on-write: every mutation immediately calls `Save()`.

Backward compat: both v1 (`activePlant`) and v2 (`activeSlots`) fields are written. Restore prefers v2 if non-empty.

### UI Toolkit Architecture

- Single `UIDocument` on `"--- UI ---"` GameObject with `GardenRoot.uxml` as root
- All panels defined inline in UXML, toggled via `style.display = DisplayStyle.None / Flex`
- Controllers are MonoBehaviours on the same GameObject, initialized via `Initialize(VisualElement root)` where they cache element refs with `root.Q<>()`
- Dynamic list items use `VisualTreeAsset.CloneTree()` from templates in `Assets/Resources/UI/Templates/`
- Stylesheets in `Assets/UI/Styles/`; `Variables.uss` defines shared CSS custom properties
- Location gate blocks all input until weather location resolves (skipped in editor)

## Critical Gotchas

**`trigger == null` vs empty `TriggerCondition`**: A `null` trigger means GeneticsEngine skips the variant (only reached as last-resort fallback). An empty `TriggerCondition` with no `use*` flags returns `true` (vacuous truth) and would match everything. Base variants must have `trigger = null`, not an empty condition.

**UXML templates live under `Resources/`**: Dynamic list templates are in `Assets/Resources/UI/Templates/` (loaded via `Resources.Load<VisualTreeAsset>()`), not `Assets/UI/Templates/`.

**`SeedSlotUI` is a static factory class**, not a MonoBehaviour — unusual among the UI controllers.

**`PlantManager.DebugAdvanceTime(hours)`** backdates `slot.plantTime` rather than advancing `GameTime` — these are different mechanisms.

**Three currencies**: Dewdrops (primary, from selling), SunShards (premium, for expansion), AuraDust (passive, from greenhouse). `CurrencyManager` reads/writes directly into `SaveManager.Instance.Data`.

## Key File Locations

- Runtime scripts: `Assets/Scripts/{Data,Services,Managers,UI,Utils,Debug}/`
- Seed assets: `Assets/Resources/Seeds/*.asset`
- Variant assets: `Assets/Resources/Variants/` (subdirs per seed type)
- Config: `Assets/Resources/Config/CurrencyConfig.asset`
- Environments: `Assets/Resources/Config/Environments/*.asset`
- Root UXML: `Assets/UI/Documents/GardenRoot.uxml`
- Stylesheets: `Assets/UI/Styles/*.uss`
- Templates: `Assets/Resources/UI/Templates/*.uxml`
- Tests: `Assets/Tests/EditMode/`
- Scene: `Assets/Scenes/SampleScene.unity`

## Unity Development

When editing Unity prefab/asset values, ALWAYS edit the serialized .asset/.prefab YAML files directly — never rely on changing C# code defaults, as Unity's serialized field values take precedence over code defaults.

When the Unity MCP tool is unavailable or unreliable (especially for asset rename/move operations), fall back immediately to direct filesystem operations (Bash mv + manual .meta file handling) rather than retrying MCP repeatedly.

After implementing any visual/VFX/animation feature in Unity, always verify it will actually render by checking: sorting layers, sorting orders relative to Canvas/HUD, and that the rendering approach is appropriate (e.g., don't use UI.Image for world-space trail effects).

When creating or modifying weapons/items/abilities that interact with player physics (velocity, movement, size), always check for conflicts with PlayerMovement's FixedUpdate loop, existing coroutines, and PlayerAnimator's per-frame overrides before implementing.

### Physics & Movement

Use reasonable, conservative initial values for physics parameters (speed, force, friction, boost multipliers). For reference: typical swipeBoost ~2-4, maxSpeed ~5-8, friction ~0.5-1.5. Never set extreme values like 10-15 without explicit user request.

## Workflow Preferences

When the user reports a specific bug and indicates they already know the cause, apply the minimal targeted fix immediately. Do NOT launch broad debugging investigations or over-investigate unless asked.

For git commits, split changes into logical atomic commits by feature/fix area. When asked to 'commit', check for unrelated changes and offer to split them.

When implementing a plan or feature, always generate ALL required artifacts (code, asset files, audio files, prefab wiring) in one pass. Do not stop at just writing code and wait for the user to say 'do it' for the rest.
