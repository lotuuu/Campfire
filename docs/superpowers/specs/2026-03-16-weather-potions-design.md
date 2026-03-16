# Weather Potions Design

## Overview

Weather potions are consumable items that modify the weather conditions a specific plot experiences during growth. When applied to a growing plot, a potion alters the raw weather data recorded in every snapshot for the rest of that growth cycle. This lets players compensate for unfavorable real-world weather without changing the actual weather system or GrowthRecipe evaluation.

Potions are acquired exclusively through visitors (gifts, trades, quests) — they cannot be crafted.

## Potion Definitions

| Item Key | Display Name | Type | Modifier |
|----------|-------------|------|----------|
| `hot_potion` | Hot Potion | additive | temperature += 15 |
| `cool_potion` | Cool Potion | additive | temperature -= 15 |
| `wind_potion` | Wind Potion | additive | windSpeed += 10 |
| `calm_potion` | Calm Potion | additive | windSpeed -= 10 |
| `humid_potion` | Humid Potion | additive | humidity += 30 |
| `dry_potion` | Dry Potion | additive | humidity -= 30 |
| `sun_potion` | Sun Potion | override | cloudCover = 0 (sunlight = 100%) |
| `shadow_potion` | Shadow Potion | override | cloudCover = 100 (sunlight = 0%) |
| `rain_potion` | Rain Potion | override | rain snapshot value = 1.0 |
| `impermeable_potion` | Impermeable Potion | override | rain snapshot value = 0.0 |
| `moon_potion` | Moon Potion | override | moonPhase = recipe's `ideal_min` value from the moon axis |

### Modifier Rules

- **Additive** modifiers stack. Two hot potions = +30°C. The `value` field is always the signed delta (cool stores `-15`, calm stores `-10`).
- **Override** modifiers set the value directly. If conflicting overrides exist on the same axis (e.g., rain + impermeable), the last one in the array wins.
- Clamping after all additive modifiers: humidity 0–100, windSpeed >= 0, cloudCover 0–100. Temperature is unclamped.

### Stacking

No limits. Players can apply any number of potions to a single plot. Multiple potions on different axes stack naturally. Multiple potions on the same axis follow the additive/override rules above.

## Prerequisite Fix: Rain Scoring Bug

`GrowthRecipe.evaluate` in `growth_recipe.ex` calculates rain fraction as:

```elixir
rain_count = length(Map.get(snapshots, "rain_snapshots", []))
rain_count / count
```

Since every snapshot appends a value (0.0 or 1.0) to `rain_snapshots`, `length/1` always equals `snapshot_count`, making the rain score always 1.0. The fix:

```elixir
rain_sum = Enum.sum(Map.get(snapshots, "rain_snapshots", []))
rain_sum / count
```

This must be fixed in both `build_axes` and `evaluate_per_axis`. The same bug exists in the client-side `GrowthRecipe.cs` — `rainSnapshots` is an `int` counting rain occurrences, which is correct, but confirm the server and client produce the same results after the fix.

## Data Model

### Migration

Add `potions` JSONB column to `player_plots`:

```elixir
alter table(:player_plots) do
  add :potions, :map, default: fragment("'[]'::jsonb"), null: false
end
```

Each entry in the array:

```json
{"type": "hot", "value": 15}
```

### PlayerPlot Schema

Add to schema:

```elixir
field :potions, {:array, :map}, default: []
```

Add `:potions` to the changeset cast list (line 28 of `player_plot.ex`).

### Reset on Harvest

In `Plots.harvest`, add `potions: []` to the reset changeset alongside `snapshots: %{}`, `fertilized: false`.

### Items Table

Add 11 new items to `seeds.exs`, all with category `"potion"`:

```
hot_potion, cool_potion, wind_potion, calm_potion,
humid_potion, dry_potion, sun_potion, shadow_potion,
rain_potion, impermeable_potion, moon_potion
```

No `recipe_configs` entries — potions are not craftable.

### Visitor Pools

Update visitor templates in `seeds.exs` to include weather potions in gift pools, merchant offer pools, and quest reward pools. Exact rates and costs are balance tuning, not specified here.

## Server Logic

### Potion Type Mapping

Module attribute in `Plots.ex`:

```elixir
@potion_types %{
  "hot_potion" => %{"type" => "hot", "value" => 15},
  "cool_potion" => %{"type" => "cool", "value" => -15},
  "wind_potion" => %{"type" => "wind", "value" => 10},
  "calm_potion" => %{"type" => "calm", "value" => -10},
  "humid_potion" => %{"type" => "humid", "value" => 30},
  "dry_potion" => %{"type" => "dry", "value" => -30},
  "sun_potion" => %{"type" => "sun"},
  "shadow_potion" => %{"type" => "shadow"},
  "rain_potion" => %{"type" => "rain"},
  "impermeable_potion" => %{"type" => "impermeable"},
  "moon_potion" => %{"type" => "moon"}
}
```

### Endpoint: `POST /game/plot/apply-potion`

Request body:

```json
{"plotId": 42, "potionItemKey": "hot_potion"}
```

Handler — `Plots.apply_potion(player_uid, plot_id, potion_item_key)`:

1. Look up `potion_item_key` in `@potion_types`. Return `{:error, :unknown_potion}` if not found.
2. Validate plot exists, is owned by player, state is `"growing"`.
3. Call `Economy.spend_item(player_uid, potion_item_key, 1)` — fail if insufficient.
4. Append the potion map to the plot's `potions` array.
5. Update and return the plot.

### Snapshot Modifier

In `Plots.record_snapshot/2`, after loading the plot (line 310) and before building `updated_snapshots`:

1. Read `plot.potions` (defaults to `[]`).
2. Apply each potion modifier in order to the incoming `weather_data` map to produce `effective_weather`:
   - `"hot"` / `"cool"`: add `value` to `"temperature"`, no clamp
   - `"wind"` / `"calm"`: add `value` to `"wind_speed"`, clamp >= 0
   - `"humid"` / `"dry"`: add `value` to `"humidity"`, clamp 0–100
   - `"sun"`: set `"cloud_cover"` to 0
   - `"shadow"`: set `"cloud_cover"` to 100
   - `"rain"`: set `"is_raining"` to true
   - `"impermeable"`: set `"is_raining"` to false
   - `"moon"`: look up the plot's seed config via `plot.seed_item_id`, get `recipe["moon"]["ideal_min"]`, set `"moon_phase"` to that value
3. Use `effective_weather` instead of raw `weather_data` when building `updated_snapshots`.

Extract this into a private function `apply_potions(weather_data, potions, plot)` for clarity.

### Edge Case: Initial Snapshot

When a seed is planted, `record_initial_snapshot` fires before any potions can be applied. This is expected — the first snapshot uses real weather. Potions only affect snapshots recorded after application.

## Client Logic

### GameService

Add `ApplyPotion(int plotServerId, string potionItemKey)` — calls `POST /game/plot/apply-potion`, returns success/error.

### PlotManager

Add `ApplyPotion(int plotIndex, string potionItemKey)`:

1. Validate plot is in growing state.
2. Call `GameService.Instance.ApplyPotion(serverId, potionItemKey)`.
3. On success: add potion item key to local `PlotSave.potions`, fire `OnPlotChanged`.
4. On failure: resync via `GameService.Instance.ResyncFullState()`.

### PlotSave

Add `List<string> potions` field — list of applied potion item keys for display.

### GameStateResponse

The server returns `potions` as the stored JSONB (list of `{"type", "value"}` maps). The client DTO maps this to `List<string>` of item keys by reversing the type mapping (e.g., `"hot"` → `"hot_potion"`). Add a `potionItemKeys` field to the server plot serialization in `GameController` that does this mapping server-side for simplicity — return both `potions` (raw) and `potionItemKeys` (list of strings) in the plot response.

### UI

When a player taps a growing plot in the interaction panel:

- Show available weather potions from inventory alongside existing actions (water, fertilize, speed).
- Tapping a potion calls `PlotManager.ApplyPotion`.
- Applied potions are displayed as icons on the plot detail view.

No changes to harvest UI, quality display, or recipe visualization — they already show effective values from snapshots.

## What Does NOT Change

- **GrowthRecipe** (server and client) — unchanged, evaluates snapshots as before
- **Harvest evaluation** — unchanged, reads already-modified snapshots
- **Harvest preview** — unchanged, shows modified values naturally
- **WeatherService** — unchanged, real weather is unaffected
- **Fertilizer system** — orthogonal, fertilizer boosts drops; potions affect quality score

## Testing

Server tests in `test/camp_fire/game/`:

- `apply_potion` — validates ownership, growing state, unknown potion rejection, item spending, potions array append
- `apply_potions` helper — additive modifiers shift values with correct clamping, override modifiers set values directly, stacking works
- Moon potion — resolves `ideal_min` from seed config recipe's moon axis
- Rain scoring fix — verify `Enum.sum` produces correct rain fraction
- Harvest reset — potions cleared after harvest

Client tests in `Assets/Tests/EditMode/`:

- `PlotSave` serialization round-trip with potions list
