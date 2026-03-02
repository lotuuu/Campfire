# Garden — Game Design Document

## Overview

Garden is a hyper-contextual plant simulation game that uses real-world weather data to drive plant genetics, growth speed, and harvest quality. Built with Unity 6 (2D URP), it creates a unique experience where a player in a desert grows different plants than a player in a rainforest. The game blends active planting/harvesting with idle greenhouse income, wrapped in a progression system of unlockable environments and discoverable variants.

**Genre:** Plant simulation / Idle collector
**Platform:** Mobile (iOS/Android)
**Target framerate:** 120 FPS

---

## Core Gameplay Loop

```
Plant a seed  →  Weather determines the variant that grows
     ↑                        ↓
Unlock more      Growth speed boosted if weather matched
environments              ↓
     ↑            Harvest when mature
     |                    ↓
Spend Gold    ←─  Sell for Gold  ──OR──  Keep in Greenhouse
                                              ↓
                                    Passive Pollen income
                                              ↓
                                      Buy new seeds
```

### Step by Step

1. **Plant:** Player selects a seed from their inventory and taps an empty slot in their current environment.
2. **Resolve:** The genetics engine reads the current real-world weather and determines which variant grows. If the weather matches a variant's trigger condition, that variant grows at 1.25x speed. Otherwise, the default variant grows at 1.0x speed.
3. **Grow:** The plant accumulates time toward maturity. Growth speed is modified by the variant multiplier, environment bonus, and any applied consumables.
4. **Harvest:** When mature, the player taps the plant. A quality tier (D through S) is rolled, influenced by whether the weather matches the seed's preferred condition (Sync Shield).
5. **Choose:** The player sells the harvest for Gold or keeps it in the Greenhouse for passive Pollen generation.
6. **Spend:** Gold buys new environments and slots (Construction tab). Pollen buys new seeds (Shop tab).
7. **Discover:** Each new variant harvested is logged in the Codex, driving collection completion.

---

## Weather System

Garden's defining feature is that real-world weather directly drives gameplay. The game reads the player's GPS location and polls the OpenWeatherMap API every 15 minutes.

### Weather Properties

| Property | Range | Example Gameplay Effect |
|---|---|---|
| Temperature | Celsius | Glacial Astra triggers below 5°C |
| Humidity | 0–100% | Dew-Drop Astra triggers above 70% |
| Wind Speed | m/s | Gale-Force Astra triggers above 20 mph |
| Condition | Clear, Cloudy, Rain, Storm, Snow | Static Astra triggers during Thunderstorm |
| Time of Day | Day, Night, Golden Hour | Lunar Astra triggers at Night |
| Moon Phase | 8 phases (New → Full → New) | Void Astra triggers during New Moon |
| Calendar Event | Equinoxes, Lunar Eclipse | Equinox Astra triggers on Spring/Fall Equinox |

### Weather Data Flow

1. **WeatherService** is the single source of truth (`CurrentWeather`)
2. On device: requests GPS → calls OpenWeatherMap every 15 min → enriches with moon phase and calendar events
3. Consumers subscribe to `OnWeatherUpdated`: the resonance bar, weather overlay, and plant growth recalculations
4. In the Unity Editor: immediately enters debug mode with manual weather controls

---

## Genetics & Variant System

Each seed type has ~12 variants, each tied to specific weather conditions. The genetics engine resolves which variant grows at planting time.

### Resolution Algorithm

1. Sort all variants by priority (1 = highest, checked first)
2. For each variant with a non-null trigger, evaluate the trigger against current weather
3. **First match wins** → variant grows with 1.25x growth speed
4. **No match** → fallback to the default variant (null trigger) with 1.0x growth speed

### Trigger Conditions

A trigger condition is a set of independent boolean checks combined with AND logic. All enabled checks must pass for the trigger to match.

| Flag | Check |
|---|---|
| `useTemperature` | Temperature within min–max range |
| `useWeatherCondition` | At least one required condition matches (OR within this check) |
| `useWindSpeed` | Wind speed above minimum threshold |
| `useHumidity` | Humidity above minimum threshold |
| `useTimeOfDay` | Time of day matches (Day/Night/Golden Hour) |
| `useMoonPhase` | Moon phase matches exactly |
| `useCalendarEvent` | Calendar event matches exactly |

A trigger with no flags enabled evaluates to `true` (vacuous truth) — this is intentionally different from a `null` trigger, which causes the variant to be skipped entirely.

### Example: Astra Variants

| Variant | Trigger | Rarity | Priority |
|---|---|---|---|
| Astra Base | null (fallback) | Common | 4 |
| Solar Astra | Clear + >25°C | Common | 2 |
| Lunar Astra | Night | Common | 3 |
| Nebula Astra | Golden Hour | Uncommon | 3 |
| Glacial Astra | <5°C | Rare | 2 |
| Dew-Drop Astra | Rain OR Humidity >70% | Rare | 2 |
| Gale-Force Astra | Wind >20 mph | Rare | 2 |
| Petrified Astra | >38°C | Rare | 2 |
| Static Astra | Thunderstorm | Epic | 1 |
| Void Astra | New Moon + Midnight | Epic | 1 |
| Blood-Moon Astra | Lunar Eclipse | Legendary | 1 |
| Equinox Astra | Spring/Fall Equinox | Legendary | 1 |

**Resolution example:** At midnight during a new moon, the engine checks priority-1 variants first. Static Astra requires a thunderstorm (no match). Void Astra requires new moon + midnight (match) → Void Astra grows at 1.25x speed.

---

## Plant Growth

### Growth Formula

```
effectiveMultiplier = variantMultiplier + environmentBonus + fertilizerBonus
effectiveGrowthHours = seed.baseGrowthHours / effectiveMultiplier
growthProgress = elapsedHours / effectiveGrowthHours
```

| Component | Value | Source |
|---|---|---|
| Variant multiplier | 1.0x (no match) or 1.25x (match) | Genetics resolution |
| Environment bonus | 0–0.5x | EnvironmentData, conditional on weather |
| Fertilizer bonus | 0 or 1.0x | Slot-scoped consumable |

Multipliers are additive. A matched variant in a bonus-active environment with fertilizer gives: `1.25 + 0.5 + 1.0 = 2.75x`, cutting a 1-hour base growth to ~22 minutes.

### Growth States

1. **Empty** — Slot available for planting
2. **Growing** — Progress accumulating each frame; plant sprite advances through growth stages
3. **Mature** — Ready to harvest; plant gently pulses

All plant timers use `GameTime.UtcNow` (a wrapper around `DateTime` with a debug offset). Growth progress is saved every 5 seconds.

---

## Harvest Quality

When a mature plant is harvested, a quality tier is rolled using weighted RNG.

### Quality Tiers

| Tier | Label | Base Probability | Sync Shield Probability | Sell Multiplier |
|---|---|---|---|---|
| D | Faded | 15% | 0% | 0.8x |
| C | Stable | 55% | 50% | 1.0x |
| B | Vibrant | 20% | 30% | 1.5x |
| A | Radiant | 8% | 15% | 2.2x |
| S | Eternal | 2% | 5% | 3.5x |

### Sync Shield

When the current weather condition matches the seed's `preferredWeather`, the Sync Shield activates:
- D-tier probability drops to 0%
- The removed 15% is redistributed upward
- S-tier chance increases from 2% to 5% (2.5x improvement)

The Sync Shield can also be triggered by applying a QualityDirt consumable to the plant's slot.

### Special Conditions

Individual seeds can define bonus conditions. For example, Cinder-Fern has +10% S-tier chance when temperature exceeds 25°C. These stack with the Sync Shield.

### Sell Value

```
goldEarned = seed.baseSellPrice × qualityMultiplier
```

A Rare seed with `baseSellPrice = 550` at A-tier: `550 × 2.2 = 1,210 Gold`.

---

## Economy

### Three Currencies

| Currency | Earn Method | Spend On | Starting Amount |
|---|---|---|---|
| **Gold** | Selling harvested plants | Environments, slots, greenhouse expansion | 75 |
| **Pollen** | Passive greenhouse income | Buying seeds in the Shop | 0 |
| **SunShards** | Achievements (future) | Consumables | 10 |

### Gold Earning Rates

Gold per harvest varies by seed rarity and quality tier. Base rewards per rarity:

| Rarity | Base Gold |
|---|---|
| Common | 10 |
| Uncommon | 25 |
| Rare | 50 |
| Epic | 100 |
| Legendary | 250 |

These are multiplied by the quality tier multiplier (0.8x–3.5x).

### Pollen Earning Rates

Greenhouse plants generate pollen passively per second, based on rarity:

| Rarity | Pollen/sec |
|---|---|
| Common | 0.5 |
| Uncommon | 1.5 |
| Rare | 4.0 |
| Epic | 10.0 |
| Legendary | 25.0 |

This rate is further multiplied by the plant's quality tier multiplier. A Rare B-tier plant generates `4.0 × 1.5 = 6.0 pollen/sec`.

---

## Greenhouse

The Greenhouse stores harvested plants for passive Pollen generation, with a decay mechanic that prevents indefinite value.

### Core Mechanics

- **Capacity:** Starts at 6 slots, expandable at 300 Gold per slot
- **Adding plants:** After harvest, choose "Keep" instead of "Sell"
- **Passive income:** Each plant generates pollen per second based on rarity × quality tier
- **Selling:** Tap a plant to see its current value and pollen rate, then sell for Gold

### Decay System

Plants in the greenhouse gradually decay through quality tiers over time:

| Starting Tier | Step Duration Factor | Example (1h base growth) |
|---|---|---|
| S | 2.0x | 120 minutes at S before dropping to A |
| A | 1.0x | 60 minutes at A |
| B | 0.5x | 30 minutes at B |
| C | 0.25x | 15 minutes at C |
| D | 0.125x | 7.5 minutes at D |

When D-tier expires, the plant withers and produces nothing. This creates a strategic choice: sell immediately for guaranteed Gold, or keep for Pollen income that diminishes over time.

**Example:** An S-tier Rare plant starts at 6.0 × 3.5 = 21 pollen/sec. After 2 hours it drops to A-tier (13.2 pollen/sec), then B (9.0), then C (6.0), then D (4.8), then gone. Total lifetime: ~3.75 hours.

---

## Environments & Construction

Players unlock themed environments through the Construction tab, each providing additional planting slots and conditional growth bonuses.

### Available Environments

| Environment | Base Slots | Max Slots | Unlock Cost | Growth Bonus |
|---|---|---|---|---|
| **Hearth** | 2 | 4 | Free (default) | +10% in temperate weather |
| **Balcony** | 2 | 4 | 100 Gold | Conditional |
| **Wild Patch** | 4 | 4 | ~1,000 Gold | Conditional |
| **Deep Conservatory** | 4 | 4 | ~5,000 Gold | High bonus, rare trigger |

### Slot Unlocking

Each environment starts with its base slot count. Additional slots cost 500 Gold each.

### Progression Gate

The Construction tab only reveals the next environment after the previous one is fully maxed out (all slots unlocked). This prevents players from spreading thin and encourages full investment in each environment.

### Environment Bonuses

Each environment has a `bonusCondition` (a TriggerCondition). When the current weather satisfies it, all plants in that environment receive a flat growth speed bonus (e.g., +0.1 to +0.5). Different environments favor different weather patterns, creating strategic reasons to plant certain seeds in certain locations.

### Active Environment

The Terrarium page shows one environment at a time. When multiple environments are unlocked, an environment switcher bar lets the player tap between them. Plants grow in all environments simultaneously regardless of which one is being viewed.

---

## Consumables

Consumables provide temporary boosts and can manipulate weather conditions locally within an environment.

### Slot-Scoped Consumables

Applied to individual plant slots. Consumed on harvest.

| Consumable | Effect |
|---|---|
| **Fertilizer** | +1.0x growth speed bonus |
| **QualityDirt** | Activates Sync Shield at harvest (guarantees quality boost) |

### Environment-Scoped Consumables

Applied to an entire environment. Only one active per environment at a time; replaced if a new one is applied.

| Consumable | Effect |
|---|---|
| **Fan** | Overrides wind speed (+X m/s) |
| **Igloo** | Reduces temperature (-X °C) |
| **Heater** | Increases temperature (+X °C) |
| **Cloud** | Forces weather condition to Rain |

Environment consumables modify the effective weather used for growth calculations and harvest evaluation. This lets players locally simulate weather conditions to trigger specific variants or environment bonuses.

### Purchase

Consumables are bought with SunShards in the Shop tab.

---

## Seed Catalog

### Seed Properties

Each seed defines:
- **Base growth hours** — How long the plant takes to mature at 1.0x speed
- **Buy price** — Cost in Pollen
- **Base sell price** — Gold earned at C-tier quality
- **Preferred weather** — Condition that activates Sync Shield
- **Variants** — ~12 per seed, tied to weather triggers
- **Special conditions** — Bonus quality modifiers for specific weather

### Available Seeds

| Seed | Buy Price | Base Sell | Growth Hours | Special |
|---|---|---|---|---|
| Quicksprout | Free | 50 | Short | Infinite supply, starter seed |
| Astra | 100 Pollen | 120 | Medium | Infinite supply, 12 variants |
| Cinder-Fern | 450 Pollen | 550 | Medium | +10% S-tier if >25°C |
| Mist-Vine | 800 Pollen | 1,000 | Medium | +10% S-tier if humidity >70% |
| Luna-Petal | 1,500 Pollen | 1,900 | Long | Only grows at night |
| Storm-Root | 3,000 Pollen | 4,000 | Long | +20% S-tier in wind/rain |

Seeds marked as "infinite" are always available at no cost. Others are purchased from inventory and consumed when planted.

---

## Codex (Discovery Log)

The Codex tracks all variants across all seeds in a browsable grid, serving as the game's collection mechanic.

### Discovery States

**Undiscovered:** Entry shows "??? · [Rarity]" with a lock icon and shadowed silhouette. Tapping reveals a hint about the trigger condition.

**Discovered:** Entry shows the full plant sprite, variant name, and rarity color. Tapping opens a detail panel with the full description.

### Discovery Trigger

When a plant is harvested, the game checks if its variant has been seen before. If it's new:
1. The variant is added to `discoveredVariants` in save data
2. A special discovery popup appears before the normal harvest result
3. The Codex entry updates to show the full variant

Discovery hints guide players toward the right weather conditions without revealing exact thresholds, encouraging experimentation and patience.

---

## Progression

### Early Game (First 1–2 Hours)

- Start with Quicksprout (free) and 5 Astra seeds, 75 Gold, 10 SunShards
- Plant first seeds in the Hearth (2 slots)
- Discover 2–3 variants based on current time of day and weather
- Learn the sell vs. keep decision through first harvests
- Fill greenhouse with first plants, observe passive Pollen generation
- Buy first new seeds from the Shop with accumulated Pollen

### Mid Game (Hours 3–10)

- Unlock the Balcony environment (100 Gold), doubling planting capacity
- Begin unlocking additional slots (500 Gold each)
- Discover that environment bonuses meaningfully affect growth speed
- Accumulate enough Pollen for expensive seeds (Cinder-Fern, Mist-Vine)
- Start noticing the Sync Shield and quality tier system
- Begin targeting specific weather windows for rare variants

### Late Game (Hours 10+)

- Unlock 3rd and 4th environments (increasingly expensive)
- Maximize slot counts across all environments
- Use consumables strategically to simulate weather for rare variants
- Chase Epic and Legendary variants that require specific moon phases or calendar events
- Greenhouse becomes the primary Pollen engine, scaling with more rare/epic plants
- Complete the Codex by collecting all variants across all seeds

### Endgame Hooks

- **Collection completion:** Discovering every variant across every seed
- **Quality chasing:** Achieving S-tier harvests of Legendary variants
- **Economy optimization:** Building the most efficient Greenhouse for passive income
- **Seasonal content:** Calendar-event variants (Equinox, Lunar Eclipse) create natural moments to return

---

## UI Structure

The interface is organized as a 5-page swipeable carousel with a bottom navigation bar.

| Page | Tab | Purpose |
|---|---|---|
| 0 | Codex | Browse discovered and undiscovered variants |
| 1 | Shop | Buy seeds (Pollen) and consumables (SunShards) |
| 2 | Terrarium | View and interact with plants in the active environment |
| 3 | Greenhouse | Manage stored plants generating passive Pollen |
| 4 | Construction | Unlock environments and expand slot capacity |

### Key UI Elements

- **Resonance Bar** (top): Displays current weather conditions, temperature, and active effects
- **Currency Display** (top): Shows Gold, Pollen, and SunShards
- **Satchel** (overlay): Seed selection drawer that opens when tapping an empty slot
- **Harvest Popup** (modal): Shows quality result with Sell/Keep buttons
- **Discovery Popup** (modal): Celebrates first-time variant discoveries
- **Environment Switcher** (Terrarium): Tabs for switching between unlocked environments
- **Consumable Picker** (Terrarium): Icon bar for selecting and applying consumables

---

## Visual Identity

- **Aesthetic:** Lofi-bioluminescence — soft edges, glowing plant veins, warm atmosphere
- **UI Style:** Glassmorphism with translucent panels and blurred backgrounds
- **Rendering:** 2D isometric grid with depth sorting by row
- **Plant Visuals:** 5+ growth stages per seed, with rarity affecting visual richness
- **Quality Indicators:** D=desaturated, C=standard, B=bright glow, A=floating particles, S=pulsing bioluminescence

---

## Save System

All game state is serialized to JSON at `Application.persistentDataPath/save.json`. Save-on-write: every mutation immediately persists. The save file tracks:

- Currency balances (Gold, Pollen, SunShards)
- Active plant slots across all environments
- Greenhouse plants with decay state
- Discovered variants list
- Seed and consumable inventories
- Unlocked environments and slot counts
- Active environment selection
