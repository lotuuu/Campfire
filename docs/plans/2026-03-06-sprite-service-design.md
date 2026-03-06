# SpriteService: Server-Served Sprites

## Problem

Adding new visual content (visitors, seeds, items, buildings) requires a client build and app store update. Sprites are baked into Unity assets (ScriptableObjects, Resources folders). We want to decouple sprite content from client releases.

## Solution

All sprites are served as PNGs from the Phoenix backend. The Unity client downloads and caches them locally on first launch, then re-downloads only changed sprites on subsequent launches. After the loading screen, all sprites are in memory for synchronous access.

## Server Side

### Static file serving

Sprites live in `priv/static/assets/sprites/` organized by category:

```
priv/static/assets/sprites/
  seeds/
    basil/
      icon.png
      growth-0.png
      growth-1.png
      growth-2.png
    chamomile/
      icon.png
      growth-0.png
      ...
  quests/
    swamp-forage.png
    meadow-expedition.png
  gardens/
    oak/
      icon.png
      mature.png
  visitors/
    fox-spirit.png
  buildings/
    plot.png
    vase.png
  ui/
    resource-mana.png
    nav-seeds.png
    ...
```

Phoenix already serves static files from `priv/static/` with `assets` in `static_paths` -- no new routing needed.

### Sprite manifest

The `/game/configs` endpoint gains a `sprites` map:

```json
{
  "seeds": { ... },
  "quests": { ... },
  "sprites": {
    "seeds/basil/icon": "a1b2c3",
    "seeds/basil/growth-0": "d4e5f6",
    "seeds/basil/growth-1": "g7h8i9",
    "seeds/chamomile/icon": "j0k1l2",
    "quests/swamp-forage": "m3n4o5",
    "visitors/fox-spirit": "p6q7r8",
    "ui/resource-mana": "s9t0u1"
  }
}
```

Keys are path-based identifiers (no `.png` extension). Values are short content hashes for cache invalidation. The manifest is generated at server startup by hashing the files on disk.

## Client Side

### SpriteService

New singleton `SpriteService` in `Assets/Scripts/Services/`.

**Public API:**

```csharp
// Synchronous access (call only after init)
Texture2D GetTexture(string key)    // "seeds/basil/icon"
Sprite GetSprite(string key)        // wraps GetTexture in Sprite.Create

// Init (called by GameService)
Task<bool> SyncSprites()            // download missing/changed, returns success
```

**Cache structure** in `Application.persistentDataPath/sprite_cache/`:

```
sprite_cache/
  manifest.json          // { "seeds/basil/icon": "a1b2c3", ... }
  seeds/
    basil/
      icon.png
      growth-0.png
      ...
```

**Sync algorithm:**

1. Compare server manifest against local `manifest.json`
2. For each key: if missing locally or hash differs, add to download queue
3. Download all queued PNGs in parallel (batched, e.g. 8 concurrent)
4. Write each to disk, update local manifest
5. Load all cached PNGs into memory as Texture2D
6. Build `Dictionary<string, Texture2D>` for synchronous lookups

### Init flow change

```
GameService.Initialize()
  1. ConfigService.FetchConfigs()       // existing -- now includes sprite manifest
  2. SpriteService.SyncSprites()        // NEW
  3. Fetch /game/state                  // existing
```

### Offline / failure behavior

- If server unreachable: use whatever is in disk cache (stale but functional)
- If disk cache empty and server unreachable: GetTexture returns null, UI shows nothing
- Partial download failure: successfully downloaded sprites are cached, failed ones retry next launch

## Migration

### What changes

- `SeedData`: remove `icon` and `growthSprites` fields. SO keeps numeric/recipe fields (still used as ConfigService fallback).
- `GardenPlantData`: remove `icon` and `matureSprite` fields.
- `RecipeData`: remove `icon` field.
- All UI code that reads `seedData.icon` or `Resources.Load<Texture2D>("UI/Icons/...")` switches to `SpriteService.Instance.GetTexture(key)` or `GetSprite(key)`.

### What stays the same

- `SeedData` SOs still exist for fallback numeric config
- `ConfigService` still patches numeric values from server
- All game logic is unchanged

### Future

- Once all numeric config is reliably server-served, SeedData SOs can be removed entirely
- Growth sprites could become spritesheets or animated if needed -- same download mechanism

## Categories

| Category | Key pattern | Example |
|----------|------------|---------|
| Seed icons | `seeds/{name}/icon` | `seeds/basil/icon` |
| Seed growth stages | `seeds/{name}/growth-{n}` | `seeds/basil/growth-0` |
| Quest icons | `quests/{name}` | `quests/swamp-forage` |
| Garden icons | `gardens/{name}/icon` | `gardens/oak/icon` |
| Garden mature | `gardens/{name}/mature` | `gardens/oak/mature` |
| Visitor sprites | `visitors/{name}` | `visitors/fox-spirit` |
| Building sprites | `buildings/{name}` | `buildings/plot` |
| UI icons | `ui/{name}` | `ui/resource-mana` |
| Skin sprites | `skins/{name}` | `skins/mossy-plot` |
