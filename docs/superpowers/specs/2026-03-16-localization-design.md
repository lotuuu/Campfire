# Localization System Design

## Context

Camp Fire has many hardcoded English strings across UI scripts and tutorial dialogue. Server-sourced content (quest names, item display names, seed names) is English-only. There is no localization infrastructure. This design adds a lightweight, server-authoritative localization system that fits the existing architecture.

## Goals

- All user-facing strings resolved through a single localization layer
- Translations served from the server — no client-side asset files
- No boot stall — device locale translations fetched as part of the existing configs request
- Lazy-load other locales on demand (language switch in settings)
- English fallback at every level (client and server) so nothing breaks with missing translations

## Non-Goals

- Pluralization/gender rules (can be added later)
- Right-to-left layout support
- Image/sprite localization
- Audio localization
- Localizing loading screen strings shown before translations are fetched (these remain English; `Loc.Get()` fallbacks handle this gracefully)

---

## Design

### 1. Client — `LocalizationService`

New singleton: `Assets/Scripts/Services/LocalizationService.cs`

**State:**
- `Dictionary<string, string> _strings` — current locale's key→text map
- `string CurrentLocale` — e.g. `"en"`, `"ja"`, `"de"`
- `event Action OnLocaleChanged` — fired after locale switch

**Instance methods:**
- `string Get(string key, string fallback)` — lookup `_strings[key]`, return fallback + log warning if missing
- `void LoadTranslations(Dictionary<string, string> data)` — bulk load from config response
- `async void FetchLocale(string locale)` — fetch translations for a different locale via lightweight endpoint, swap `_strings`, update `ConfigService` DTOs, fire event
- `string DetectDeviceLocale()` — maps `Application.systemLanguage` to locale code, defaults to `"en"`

**Static shorthand class `Loc`** (`Assets/Scripts/Utils/Loc.cs`):
```csharp
public static class Loc
{
    public static string Get(string key, string fallback) =>
        LocalizationService.Instance != null
            ? LocalizationService.Instance.Get(key, fallback)
            : fallback;
}
```

The null check ensures `Loc.Get()` works even before `LocalizationService` initializes (returns fallback). All callers use `Loc.Get()`, not the instance method directly.

### 2. Server — Translation Storage

**New `translations` table** (for client UI strings):

| Column | Type | Notes |
|--------|------|-------|
| id | integer | PK |
| locale | string | e.g. `"en"`, `"ja"` |
| key | string | e.g. `"ui.button.harvest"` |
| value | text | Translated string |
| | | Unique index on `(locale, key)` |

**Localized config DTO fields** (quest names, item names, etc.):

New `config_translations` table:

| Column | Type | Notes |
|--------|------|-------|
| id | integer | PK |
| locale | string | e.g. `"ja"` |
| translatable_type | string | `"item"`, `"quest"`, `"garden"` |
| translatable_key | string | Source record key (e.g. `"sprouts_seed"`, `"NearbyGathering"`) |
| field | string | `"display_name"`, `"description"` |
| value | text | Translated string |
| | | Unique index on `(locale, translatable_type, translatable_key, field)` |

Uses string keys (not integer FKs) to match how configs are actually keyed in `ConfigCache`/`game_configs`.

English values remain in the source tables as the baseline. Other locales stored in `config_translations`. Server joins at query time, falling back to English if no translation exists.

**ETS caching**: Translations cached in ETS alongside existing config cache for performance. Cache invalidated when translations are updated via admin.

### 3. Config Endpoint Changes

**Request:** `/api/game/configs?locale=xx`

New optional `locale` query parameter (default: `"en"`).

**Response additions:**
```json
{
  "supported_locales": ["en", "ja", "de"],
  "translations": {
    "ui.button.harvest": "Harvest",
    "ui.button.craft": "Craft",
    "tutorial.dialogue.seed_planted": "Your seed is planted and growing!",
    ...
  },
  "items": [
    {"item_key": "sprouts_seed", "display_name": "Sprouts Seed", ...}
  ],
  "quests": [
    {"quest_name": "Nearby Gathering", "description": "Gather seeds from...", ...}
  ]
}
```

- `supported_locales`: derived from distinct locales in the `translations` table (used to populate the language dropdown)
- `translations`: all client UI strings for the requested locale
- DTO string fields (`display_name`, `quest_name`, `description`) returned already localized

**New lightweight endpoint:** `GET /api/game/translations?locale=xx`

Returns only `translations` dict + localized DTO field overrides (not full configs). Used for language switching to avoid re-fetching all config data.

### 4. Boot Flow

1. `GameService` starts initialization
2. `LocalizationService` detects device locale (or reads saved preference from `SaveData`)
3. `ConfigService` fetches `/api/game/configs?locale=xx` — same single request as today, now includes `translations` and `supported_locales`
4. `ConfigService` passes translations dict to `LocalizationService.LoadTranslations()`
5. UI initializes — all `Loc.Get()` calls resolve from loaded translations
6. No additional request, no boot stall
7. Loading screen strings shown before step 4 use `Loc.Get()` fallbacks (English)

### 5. Language Switching

- `SettingsUI` adds a language dropdown populated from `supported_locales` in the configs response
- On selection: `LocalizationService.FetchLocale("de")` fetches `/api/game/translations?locale=de` (lightweight endpoint)
- `_strings` swapped, `ConfigService` DTO string fields updated with localized overrides, `OnLocaleChanged` fired
- UI controllers listen to `OnLocaleChanged` and re-render text
- Selected locale persisted in `SaveData.locale` — overrides device detection on next boot

### 6. Key Naming Convention

Dot-separated, lowercase, grouped by context:

| Prefix | Example | Usage |
|--------|---------|-------|
| `ui.button.*` | `ui.button.harvest`, `ui.button.craft` | Button labels |
| `ui.label.*` | `ui.label.empty`, `ui.label.growing` | Status labels |
| `ui.build.*` | `ui.build.plot_name`, `ui.build.plot_desc` | Build panel |
| `ui.quest.*` | `ui.quest.complete`, `ui.quest.fetching_water` | Quest UI |
| `ui.loading.*` | `ui.loading.signing_in`, `ui.loading.slow` | Loading screens |
| `ui.visitor.*` | `ui.visitor.water`, `ui.visitor.seeds` | Visitor UI |
| `tutorial.dialogue.*` | `tutorial.dialogue.seed_planted` | Tutorial dialogue |
| `tutorial.hint.*` | `tutorial.hint.tap_to_plant` | Tutorial hints |

### 7. String Migration

Replace all hardcoded strings with `Loc.Get()` calls. Two patterns:

**Direct assignment:**
```csharp
// Before:
label.text = "Harvest";
// After:
label.text = Loc.Get("ui.button.harvest", "Harvest");
```

**Method argument:**
```csharp
// Before:
AddBuildOption("Plot", "Grow seeds", ...);
// After:
AddBuildOption(Loc.Get("ui.build.plot_name", "Plot"), Loc.Get("ui.build.plot_desc", "Grow seeds"), ...);
```

UXML static text (nav labels, etc.) set via code on init rather than hardcoded in markup.

Server DTO strings need no client-side change — `ConfigService` already stores whatever the server returns. The server just starts returning the localized version.

### 8. Admin UI

New LiveView page at `/admin/translations`:
- List/filter translations by locale and key prefix
- Edit individual translation values
- Bulk import via JSON/CSV upload
- Add new locale
- Invalidates ETS cache on save

---

## Key Files to Create/Modify

**New files:**
- `Assets/Scripts/Services/LocalizationService.cs` — singleton service
- `Assets/Scripts/Utils/Loc.cs` — static shorthand
- `server/lib/camp_fire/translations.ex` — Ecto context
- `server/lib/camp_fire/translations/translation.ex` — schema
- `server/lib/camp_fire/translations/config_translation.ex` — schema
- `server/priv/repo/migrations/*_create_translations.exs` — migration
- `server/lib/camp_fire_web/live/translations_live.ex` — admin page

**Modified files:**
- `Assets/Scripts/Services/ConfigService.cs` — parse `translations` + `supported_locales` from response, pass locale param
- `Assets/Scripts/Services/GameService.cs` — wire locale detection into boot flow
- `Assets/Scripts/Data/SaveData.cs` — add `locale` field
- `Assets/Scripts/UI/SettingsUI.cs` — add language dropdown
- `Assets/Scripts/UI/CampsiteViewUI.cs` — replace hardcoded strings with `Loc.Get()`
- `Assets/Scripts/UI/QuestUI.cs` — replace hardcoded strings
- `Assets/Scripts/UI/ApothekeUI.cs` — replace hardcoded strings
- `Assets/Scripts/UI/BuildUI.cs` — replace hardcoded strings
- `Assets/Scripts/UI/DialogueUI.cs` — replace hardcoded strings
- `Assets/Scripts/UI/CampFireUI.cs` — replace hardcoded strings
- `Assets/Scripts/UI/VisitorUI.cs` — replace hardcoded strings
- `Assets/Scripts/Managers/TutorialManager.cs` — replace hardcoded strings
- `server/lib/camp_fire_web/controllers/game_controller.ex` — accept locale param, add translations endpoint
- `server/lib/camp_fire/config_cache.ex` — locale-aware config assembly, ETS caching for translations
- `server/priv/repo/seeds.exs` — seed English translations

---

## Verification

1. **Unit tests**: `LocalizationService.Get()` returns correct value; logs warning on fallback; handles null/empty
2. **Server tests**: Configs endpoint returns translations for requested locale; falls back to English for missing translations; DTO fields localized; lightweight translations endpoint works
3. **Integration**: Boot game — all UI text renders identically to current (English fallback)
4. **Locale switch**: Seed a test locale with partial translations, switch in settings, verify UI updates and missing keys fall back with warnings
5. **Admin**: CRUD translations at `/admin/translations`, verify they appear in next configs fetch and ETS cache is invalidated
