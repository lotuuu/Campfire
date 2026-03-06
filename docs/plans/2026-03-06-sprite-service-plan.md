# SpriteService Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Serve all game sprites from the Phoenix backend so new visual content doesn't require a client build.

**Architecture:** Phoenix serves PNGs from `priv/static/assets/sprites/` and includes a sprite manifest (key->hash map) in the `/game/configs` response. Unity's `SpriteService` downloads missing/changed sprites at startup, caches to disk, and provides synchronous `GetTexture(key)`/`GetSprite(key)` access for all UI code.

**Tech Stack:** Elixir/Phoenix (server), Unity C# with UnityWebRequest (client), JSON manifest, PNG files.

---

## Task 1: Server — Sprite Manifest Module

Create a module that scans `priv/static/assets/sprites/` at startup, hashes each PNG, and serves the manifest via the configs endpoint.

**Files:**
- Create: `server/lib/camp_fire/sprite_manifest.ex`
- Modify: `server/lib/camp_fire_web/controllers/game_controller.ex:61-73`
- Modify: `server/lib/camp_fire/config_cache.ex:34-91`

**Step 1: Create SpriteManifest module**

```elixir
# server/lib/camp_fire/sprite_manifest.ex
defmodule CampFire.SpriteManifest do
  @moduledoc """
  Scans priv/static/assets/sprites/ and builds a key->hash manifest.
  Called at startup and cached in ETS via ConfigCache.
  """

  @sprites_dir "priv/static/assets/sprites"

  def build do
    base = Application.app_dir(:camp_fire, @sprites_dir)

    if File.dir?(base) do
      base
      |> scan_dir("")
      |> Map.new()
    else
      %{}
    end
  end

  defp scan_dir(base, prefix) do
    path = Path.join(base, prefix)

    path
    |> File.ls!()
    |> Enum.flat_map(fn entry ->
      full = Path.join(path, entry)
      rel = if prefix == "", do: entry, else: "#{prefix}/#{entry}"

      cond do
        File.dir?(full) ->
          scan_dir(base, rel)

        String.ends_with?(entry, ".png") ->
          key = String.replace_suffix(rel, ".png", "")
          hash = hash_file(full)
          [{key, hash}]

        true ->
          []
      end
    end)
  end

  defp hash_file(path) do
    path
    |> File.read!()
    |> then(&:crypto.hash(:md5, &1))
    |> Base.encode16(case: :lower)
    |> String.slice(0, 8)
  end
end
```

**Step 2: Wire manifest into ConfigCache**

In `server/lib/camp_fire/config_cache.ex`, add at the end of `load_all/0` (after line 90):

```elixir
    sprite_manifest = CampFire.SpriteManifest.build()
    :ets.insert(@table, {"sprite_manifest", sprite_manifest})
```

**Step 3: Include manifest in configs response**

In `server/lib/camp_fire_web/controllers/game_controller.ex`, modify `get_configs/2`:

After line 20 (`skin_configs = ...`), add:
```elixir
    sprite_manifest = ConfigCache.get("sprite_manifest") || %{}
```

In the `json(%{...})` response map (lines 63-73), add `sprites: sprite_manifest` to the map.

**Step 4: Create sprites directory with a test file**

```bash
mkdir -p server/priv/static/assets/sprites/ui
# Copy one existing icon as a test PNG (or create a placeholder)
```

**Step 5: Test manually**

Run: `cd server && mix phx.server`
Then: `curl -s localhost:4000/game/configs -H "Authorization: Bearer <token>" | jq .sprites`
Expected: JSON object with sprite keys and hashes (may be empty if no PNGs placed yet).

**Step 6: Commit**

```bash
git add server/lib/camp_fire/sprite_manifest.ex server/lib/camp_fire/config_cache.ex server/lib/camp_fire_web/controllers/game_controller.ex
git commit -m "feat(server): sprite manifest — scan and serve sprite hashes in configs"
```

---

## Task 2: Server — Place Initial Sprite PNGs

Export existing sprites from the Unity project and place them in `priv/static/assets/sprites/` following the key convention.

**Files:**
- Create: `server/priv/static/assets/sprites/` directory tree with PNGs

**Step 1: Identify source sprites in Unity**

The sprites currently live as Unity assets. Their source PNGs are at:
- Seed icons: look in the SeedData .asset files for the sprite reference GUID, then find the corresponding PNG
- UI icons: `Assets/Resources/UI/Icons/*.png`
- Weather icons: `Assets/Resources/UI/Icons/weather-*.png`
- Moon phases: `Assets/Resources/MoonPhases/`
- Visitor portraits: `Assets/Resources/Portraits/`

**Step 2: Create directory structure**

```bash
mkdir -p server/priv/static/assets/sprites/{seeds,quests,gardens,visitors,buildings,ui,portraits,moon}
```

**Step 3: Copy PNGs to server**

For each seed (Basil, Chamomile, Cress, Dahlia, Jasmine, Lavender, Marigold, Mint, Moonflower, Pansy, Poppy, Rosemary, Snowdrop, Sprouts):
- Find the icon PNG and copy to `server/priv/static/assets/sprites/seeds/{name}/icon.png`
- Find each growth stage PNG and copy to `server/priv/static/assets/sprites/seeds/{name}/growth-{n}.png`

For UI icons:
- Copy `Assets/Resources/UI/Icons/resource-mana.png` → `sprites/ui/resource-mana.png`
- Copy `Assets/Resources/UI/Icons/resource-water.png` → `sprites/ui/resource-water.png`
- Copy `Assets/Resources/UI/Icons/resource-mallum.png` → `sprites/ui/resource-mallum.png`
- Copy `Assets/Resources/UI/Icons/nav-seeds.png` → `sprites/ui/nav-seeds.png`
- Copy `Assets/Resources/UI/Icons/quest-compass.png` → `sprites/ui/quest-compass.png`
- Copy `Assets/Resources/UI/Icons/nav-mail.png` → `sprites/ui/nav-mail.png`
- Copy weather icons: `weather-clear.png`, `weather-cloudy.png`, `weather-rain.png`, `weather-storm.png`, `weather-snow.png` → `sprites/ui/weather-*.png`
- Copy `lorc-padlock.png` → `sprites/ui/lorc-padlock.png`

For moon phases:
- Copy `Moon_Phase_1.png` through `Moon_Phase_8.png` → `sprites/moon/phase-{n}.png`

For visitor portraits:
- Copy all from `Assets/Resources/Portraits/` → `sprites/portraits/{name}.png`

For garden plants:
- Find icon, growth stage, and mature PNGs for each GardenPlantData

**Important:** The PNGs must be the raw source images, NOT Unity .asset files. Look for the actual .png files that the .asset YAML references via GUID.

**Step 4: Verify manifest picks them up**

Run: `cd server && mix phx.server`
Then: `curl -s localhost:4000/game/configs -H "Authorization: Bearer <token>" | jq '.sprites | keys | length'`
Expected: A number matching the total sprites placed.

**Step 5: Verify static serving works**

Test: `curl -s -o /dev/null -w "%{http_code}" localhost:4000/assets/sprites/ui/resource-mana.png`
Expected: `200`

**Step 6: Commit**

```bash
git add server/priv/static/assets/sprites/
git commit -m "feat(server): add initial sprite PNGs for server-served sprites"
```

---

## Task 3: Client — SpriteService Core

Create the SpriteService singleton that downloads, caches, and serves sprites.

**Files:**
- Create: `Assets/Scripts/Services/SpriteService.cs`

**Step 1: Create SpriteService**

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Garden
{
    public class SpriteService : MonoBehaviour
    {
        public static SpriteService Instance { get; private set; }

        private Dictionary<string, Texture2D> _textures = new();
        private Dictionary<string, Sprite> _sprites = new();

        private static string CacheDir =>
            Path.Combine(Application.persistentDataPath, "sprite_cache");

        private static string ManifestPath =>
            Path.Combine(CacheDir, "manifest.json");

        private static string ServerBaseUrl =>
#if UNITY_EDITOR
            "http://localhost:4000";
#else
            DevServerConfig.BaseUrl;
#endif

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        // ── Public API ──

        public Texture2D GetTexture(string key)
        {
            _textures.TryGetValue(key, out var tex);
            return tex;
        }

        public Sprite GetSprite(string key)
        {
            if (_sprites.TryGetValue(key, out var cached)) return cached;

            var tex = GetTexture(key);
            if (tex == null) return null;

            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f), 100f);
            _sprites[key] = sprite;
            return sprite;
        }

        // ── Sync ──

        public async Task<bool> SyncSprites(Dictionary<string, string> serverManifest)
        {
            if (serverManifest == null || serverManifest.Count == 0)
            {
                LoadAllFromCache();
                return true;
            }

            try
            {
                Directory.CreateDirectory(CacheDir);
                var localManifest = LoadLocalManifest();

                // Find sprites that need downloading
                var toDownload = new List<string>();
                foreach (var kv in serverManifest)
                {
                    if (!localManifest.TryGetValue(kv.Key, out var localHash) || localHash != kv.Value)
                        toDownload.Add(kv.Key);
                }

                if (toDownload.Count > 0)
                {
                    Debug.Log($"SpriteService: downloading {toDownload.Count} sprites...");
                    await DownloadBatch(toDownload, serverManifest);
                }

                LoadAllFromCache();
                Debug.Log($"SpriteService: {_textures.Count} sprites loaded.");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"SpriteService: sync failed ({e.Message}), loading from cache.");
                LoadAllFromCache();
                return _textures.Count > 0;
            }
        }

        // ── Download ──

        private async Task DownloadBatch(List<string> keys, Dictionary<string, string> serverManifest)
        {
            const int batchSize = 8;
            var localManifest = LoadLocalManifest();

            for (int i = 0; i < keys.Count; i += batchSize)
            {
                var batch = keys.GetRange(i, Math.Min(batchSize, keys.Count - i));
                var tasks = new List<Task>();

                foreach (var key in batch)
                    tasks.Add(DownloadOne(key, serverManifest[key], localManifest));

                await Task.WhenAll(tasks);
            }

            SaveLocalManifest(localManifest);
        }

        private async Task DownloadOne(string key, string hash, Dictionary<string, string> localManifest)
        {
            var url = $"{ServerBaseUrl}/assets/sprites/{key}.png";
            using var req = UnityWebRequest.Get(url);
            req.downloadHandler = new DownloadHandlerBuffer();

            var tcs = new TaskCompletionSource<bool>();
            var op = req.SendWebRequest();
            op.completed += _ => tcs.SetResult(true);
            await tcs.Task;

            if (req.responseCode != 200)
            {
                Debug.LogWarning($"SpriteService: failed to download {key} (HTTP {req.responseCode})");
                return;
            }

            var filePath = Path.Combine(CacheDir, key.Replace('/', Path.DirectorySeparatorChar) + ".png");
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            File.WriteAllBytes(filePath, req.downloadHandler.data);
            localManifest[key] = hash;
        }

        // ── Cache I/O ──

        private void LoadAllFromCache()
        {
            _textures.Clear();
            _sprites.Clear();

            var manifest = LoadLocalManifest();
            foreach (var key in manifest.Keys)
            {
                var filePath = Path.Combine(CacheDir, key.Replace('/', Path.DirectorySeparatorChar) + ".png");
                if (!File.Exists(filePath)) continue;

                var bytes = File.ReadAllBytes(filePath);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Point;
                if (tex.LoadImage(bytes))
                    _textures[key] = tex;
            }
        }

        private Dictionary<string, string> LoadLocalManifest()
        {
            if (!File.Exists(ManifestPath))
                return new Dictionary<string, string>();

            var json = File.ReadAllText(ManifestPath);
            return ParseManifestJson(json);
        }

        private void SaveLocalManifest(Dictionary<string, string> manifest)
        {
            var sb = new StringBuilder();
            sb.Append("{");
            bool first = true;
            foreach (var kv in manifest)
            {
                if (!first) sb.Append(",");
                sb.Append($"\"{kv.Key}\":\"{kv.Value}\"");
                first = false;
            }
            sb.Append("}");
            File.WriteAllText(ManifestPath, sb.ToString());
        }

        private static Dictionary<string, string> ParseManifestJson(string json)
        {
            var result = new Dictionary<string, string>();
            var parsed = MiniJson.Deserialize(json) as Dictionary<string, object>;
            if (parsed == null) return result;

            foreach (var kv in parsed)
            {
                if (kv.Value is string s)
                    result[kv.Key] = s;
            }
            return result;
        }
    }
}
```

**Step 2: Commit**

```bash
git add Assets/Scripts/Services/SpriteService.cs
git commit -m "feat: add SpriteService — downloads, caches, and serves sprites from server"
```

---

## Task 4: Client — Parse Sprite Manifest in ConfigService

Add sprite manifest parsing to ConfigService so it's available for SpriteService.

**Files:**
- Modify: `Assets/Scripts/Services/ConfigService.cs:77-88` (add field), `237-376` (add to ParseResponse)

**Step 1: Add sprite manifest field and accessor**

In `ConfigService.cs`, after line 87 (`private Dictionary<string, object> _buildingCostConfig;`), add:

```csharp
        private Dictionary<string, string> _spriteManifest = new();
```

Add public accessor after line 158:

```csharp
        public Dictionary<string, string> SpriteManifest => _spriteManifest;
```

**Step 2: Parse sprites from server response**

In `ParseResponse()`, after the building cost config block (after line 375), add:

```csharp
            // Sprite manifest
            if (root.TryGetValue("sprites", out var spritesObj) && spritesObj is Dictionary<string, object> sprites)
            {
                _spriteManifest.Clear();
                foreach (var kv in sprites)
                {
                    if (kv.Value is string hash)
                        _spriteManifest[kv.Key] = hash;
                }
            }
```

**Step 3: Commit**

```bash
git add Assets/Scripts/Services/ConfigService.cs
git commit -m "feat: parse sprite manifest from server configs"
```

---

## Task 5: Client — Wire SpriteService into GameService Init Flow

Add SpriteService.SyncSprites() between config fetch and game state fetch.

**Files:**
- Modify: `Assets/Scripts/Services/GameService.cs:34-71`

**Step 1: Add sprite sync after config fetch**

In `GameService.Initialize()`, after line 46 (`Debug.LogWarning("GameService: Config fetch failed...")`), add:

```csharp
                // Sync sprites from server
                if (SpriteService.Instance != null && ConfigService.Instance != null)
                {
                    await SpriteService.Instance.SyncSprites(ConfigService.Instance.SpriteManifest);
                }
```

**Step 2: Add SpriteService MonoBehaviour to the scene**

Ensure SpriteService is on the same GameObject as the other services (the `--- Services ---` or similar GameObject in the scene). This can be done via Unity editor or by adding it programmatically.

**Step 3: Commit**

```bash
git add Assets/Scripts/Services/GameService.cs
git commit -m "feat: wire SpriteService into game init flow"
```

---

## Task 6: Client — Migrate Seed Sprite References

Replace all `seedData.icon` and `seedData.growthSprites` usages with `SpriteService`.

**Files:**
- Modify: `Assets/Scripts/Data/SeedData.cs:14-16`
- Modify: `Assets/Scripts/UI/BuildCardHelper.cs:193-195`
- Modify: `Assets/Scripts/UI/ApothekeUI.cs:94-95`
- Modify: `Assets/Scripts/UI/CampsiteViewUI.cs:1328-1334`

**Step 1: Remove sprite fields from SeedData**

In `SeedData.cs`, remove:
```csharp
        [Header("Visuals")]
        public Sprite icon;
        public Sprite[] growthSprites;
```

**Step 2: Update BuildCardHelper.cs**

Where it loads `seedData.icon` (~line 193-195), replace with:
```csharp
var icon = SpriteService.Instance?.GetSprite($"seeds/{seedData.seedName.ToLower()}/icon");
```

**Step 3: Update ApothekeUI.cs**

Where it uses `seedData.icon` (~line 94-95), replace with:
```csharp
var icon = SpriteService.Instance?.GetSprite($"seeds/{seedData.seedName.ToLower()}/icon");
```

**Step 4: Update CampsiteViewUI.cs**

Where it uses `seed.icon` (~line 1328-1334), replace with:
```csharp
var icon = SpriteService.Instance?.GetSprite($"seeds/{seed.seedName.ToLower()}/icon");
```

Where it uses growth sprites for plot rendering, replace `seedData.growthSprites[index]` with:
```csharp
SpriteService.Instance?.GetSprite($"seeds/{seedData.seedName.ToLower()}/growth-{index}")
```

**Step 5: Compile and verify no references to seedData.icon or seedData.growthSprites remain**

Run: Unity MCP `read_console` to check for compilation errors.

**Step 6: Commit**

```bash
git add Assets/Scripts/Data/SeedData.cs Assets/Scripts/UI/BuildCardHelper.cs Assets/Scripts/UI/ApothekeUI.cs Assets/Scripts/UI/CampsiteViewUI.cs
git commit -m "refactor: migrate seed sprites from SeedData SOs to SpriteService"
```

---

## Task 7: Client — Migrate GardenPlantData Sprite References

Replace `GardenPlantData.icon`, `growthSprites`, and `matureSprite` with SpriteService lookups.

**Files:**
- Modify: `Assets/Scripts/Data/GardenPlantData.cs:28-30`
- Modify: `Assets/Scripts/UI/BuildCardHelper.cs:198-200`
- Modify: `Assets/Scripts/UI/CampsiteViewUI.cs:1667` (and anywhere garden sprites are used)

**Step 1: Remove sprite fields from GardenPlantData**

In `GardenPlantData.cs`, remove:
```csharp
        public Sprite icon;
        public Sprite[] growthSprites;
        public Sprite matureSprite;
```

**Step 2: Update UI code**

Replace `plantData.icon` with:
```csharp
SpriteService.Instance?.GetSprite($"gardens/{plantData.plantName.ToLower()}/icon")
```

Replace `plantData.matureSprite` with:
```csharp
SpriteService.Instance?.GetSprite($"gardens/{plantData.plantName.ToLower()}/mature")
```

Replace `plantData.growthSprites[index]` with:
```csharp
SpriteService.Instance?.GetSprite($"gardens/{plantData.plantName.ToLower()}/growth-{index}")
```

**Step 3: Compile check**

Run: Unity MCP `read_console` for errors.

**Step 4: Commit**

```bash
git add Assets/Scripts/Data/GardenPlantData.cs Assets/Scripts/UI/BuildCardHelper.cs Assets/Scripts/UI/CampsiteViewUI.cs
git commit -m "refactor: migrate garden plant sprites to SpriteService"
```

---

## Task 8: Client — Migrate RecipeData Icon

**Files:**
- Modify: `Assets/Scripts/Data/RecipeData.cs:26`

**Step 1: Remove icon field**

In `RecipeData.cs`, remove:
```csharp
        public Sprite icon;
```

**Step 2: Update any UI references (if any)**

Search for `recipeData.icon` — the exploration showed this field is present but likely unused in UI. If references exist, replace with `SpriteService.Instance?.GetSprite(...)`.

**Step 3: Commit**

```bash
git add Assets/Scripts/Data/RecipeData.cs
git commit -m "refactor: remove unused icon field from RecipeData"
```

---

## Task 9: Client — Migrate Hardcoded Resources.Load UI Icons

Replace all `Resources.Load<Texture2D>("UI/Icons/...")` calls with SpriteService lookups.

**Files:**
- Modify: `Assets/Scripts/UI/BottomNavUI.cs:27-29, 62`
- Modify: `Assets/Scripts/UI/ResourceDisplayUI.cs:19-21, 62`
- Modify: `Assets/Scripts/UI/WeatherBarUI.cs:40, 45-49, 357`
- Modify: `Assets/Scripts/UI/QuestButtonUI.cs:17`
- Modify: `Assets/Scripts/UI/CampsiteViewUI.cs:1738, 1952`
- Modify: `Assets/Scripts/UI/CampFireUI.cs:161`
- Modify: `Assets/Scripts/UI/BuildCardHelper.cs:183`

**Step 1: Update BottomNavUI.cs**

Replace `Resources.Load<Texture2D>("UI/Icons/nav-seeds")` etc. with:
```csharp
SpriteService.Instance?.GetTexture("ui/nav-seeds")
```

Same for `quest-compass` and `nav-mail`.

**Step 2: Update ResourceDisplayUI.cs**

Replace `Resources.Load<Texture2D>("UI/Icons/resource-mana")` etc. with:
```csharp
SpriteService.Instance?.GetTexture("ui/resource-mana")
```

Same for `resource-water` and `resource-mallum`.

**Step 3: Update WeatherBarUI.cs**

Replace moon phase loading `Resources.Load<Texture2D>($"MoonPhases/Moon_Phase_{i+1}")` with:
```csharp
SpriteService.Instance?.GetTexture($"moon/phase-{i + 1}")
```

Replace weather icons `Resources.Load<Texture2D>("UI/Icons/weather-clear")` etc. with:
```csharp
SpriteService.Instance?.GetTexture("ui/weather-clear")
```

**Step 4: Update QuestButtonUI.cs**

Replace `Resources.Load<Texture2D>("UI/Icons/quest-compass")` with:
```csharp
SpriteService.Instance?.GetTexture("ui/quest-compass")
```

**Step 5: Update CampsiteViewUI.cs**

Replace `Resources.Load<Texture2D>("UI/Icons/lorc-padlock")` with:
```csharp
SpriteService.Instance?.GetTexture("ui/lorc-padlock")
```

Replace `Resources.Load<Texture2D>($"UI/Icons/Items/{skin.costItemName}")` with:
```csharp
SpriteService.Instance?.GetTexture($"ui/items/{skin.costItemName.ToLower()}")
```

**Step 6: Update CampFireUI.cs**

Replace `Resources.Load<Texture2D>($"Portraits/{visitor.portraitId}")` with:
```csharp
SpriteService.Instance?.GetTexture($"portraits/{visitor.portraitId}")
```

**Step 7: Update BuildCardHelper.cs**

Replace `Resources.Load<Texture2D>("UI/Icons/resource-mana")` with:
```csharp
SpriteService.Instance?.GetTexture("ui/resource-mana")
```

**Step 8: Compile check**

Run: Unity MCP `read_console` for errors.

**Step 9: Commit**

```bash
git add Assets/Scripts/UI/
git commit -m "refactor: migrate all UI icon loading from Resources to SpriteService"
```

---

## Task 10: Client — Add SpriteService to Scene

Ensure SpriteService MonoBehaviour exists on the services GameObject in the scene.

**Files:**
- Modify: `Assets/Scenes/Garden.unity` (add SpriteService component)

**Step 1: Add component**

Use Unity MCP `manage_components` to add `SpriteService` to the services GameObject (same one that has `ConfigService`, `GameService`, etc.).

**Step 2: Verify in play mode**

Enter play mode and check console for:
- `SpriteService: downloading N sprites...`
- `SpriteService: N sprites loaded.`

**Step 3: Commit**

```bash
git add Assets/Scenes/Garden.unity
git commit -m "feat: add SpriteService component to scene"
```

---

## Task 11: Run Tests and Final Verification

**Step 1: Run all EditMode tests**

Run: Unity MCP `run_tests` with `mode: "EditMode"`
Expected: All existing tests pass (no regressions from removing sprite fields).

**Step 2: Check for remaining Resources.Load sprite calls**

Search for: `Resources.Load<Texture2D>` and `Resources.Load<Sprite>` in `Assets/Scripts/`
Expected: Only non-sprite loads remain (VisualTreeAsset templates, etc.). The hex grid `Resources.LoadAll<Sprite>("Sprites/TX_HexagonTest")` in `CampsiteViewUI.cs:90` can stay for now (tilemap sprites, not content sprites).

**Step 3: Play mode smoke test**

Enter play mode and verify:
- All seed icons appear in Apotheke/Build panels
- Resource icons (mana, water, mallum) show in top bar
- Weather icons render
- Moon phases render
- Nav bar icons work

**Step 4: Commit any fixes**

```bash
git commit -m "fix: address issues found during sprite migration verification"
```
