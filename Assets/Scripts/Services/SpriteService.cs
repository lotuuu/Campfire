using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

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

        private static string ServerBaseUrl => ServerConfig.BaseUrl;

        private const long SlowStepMs = 500;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            // Load cached sprites immediately so they're available for the loading screen
            var sw = Stopwatch.StartNew();
            LoadAllFromCache();
            if (sw.ElapsedMilliseconds > SlowStepMs)
                Debug.LogWarning($"[INIT SLOW] SpriteService.LoadAllFromCache (Awake) took {sw.ElapsedMilliseconds}ms ({_textures.Count} sprites)");
        }

        // ── Public API ──

        /// <summary>
        /// Converts a display name like "Sprouts Seed" to a sprite-friendly slug "sprouts".
        /// Strips trailing " Seed", lowercases, replaces spaces with hyphens.
        /// </summary>
        public static string SeedToSpriteKey(string seedName)
        {
            if (string.IsNullOrEmpty(seedName)) return seedName;
            var s = seedName.Trim();
            if (s.EndsWith(" Seed", System.StringComparison.OrdinalIgnoreCase))
                s = s.Substring(0, s.Length - 5);
            return s.ToLower().Replace(' ', '-');
        }

        /// <summary>
        /// Resolves any inventory item name to its sprite key under "items/".
        /// E.g. "Cress" → "items/cress/harvest", "Basil_pigment" → "items/basil/pigment",
        /// "Speed_Potion" → "items/speed_potion".
        /// </summary>
        public static string ItemToSpriteKey(string itemName)
        {
            if (string.IsNullOrEmpty(itemName)) return null;
            var lower = itemName.ToLower();
            if (lower.EndsWith("_pigment"))
            {
                var plant = lower.Substring(0, lower.Length - "_pigment".Length);
                return $"items/{plant}/pigment";
            }
            // Bare plant names (yields from harvesting) — check against known seeds
            if (ConfigService.Instance?.GetSeed(itemName) != null)
                return $"items/{lower}/harvest";
            return $"items/{lower}";
        }

        public Texture2D GetTexture(string key)
        {
            _textures.TryGetValue(key, out var tex);
            return tex;
        }

        /// <summary>
        /// Finds the best sprite for a prefix + percentage.
        /// Given prefix "hex/vase" and percent 0.2, searches for keys like
        /// hex/vase/0, hex/vase/10, hex/vase/100 and returns the one with
        /// the highest threshold ≤ 20 (i.e. hex/vase/10).
        /// </summary>
        public Texture2D GetTextureByPercentage(string prefix, float percent01)
        {
            int pct = Mathf.RoundToInt(percent01 * 100f);
            string bestKey = null;
            int bestThreshold = -1;

            foreach (var key in _textures.Keys)
            {
                if (key.Length <= prefix.Length + 1 || key[prefix.Length] != '/' || !key.StartsWith(prefix))
                    continue;
                var suffix = key.Substring(prefix.Length + 1);
                if (suffix.IndexOf('/') >= 0) continue;
                if (!int.TryParse(suffix, out int threshold)) continue;
                if (threshold <= pct && threshold > bestThreshold)
                {
                    bestThreshold = threshold;
                    bestKey = key;
                }
            }

            return bestKey != null ? _textures[bestKey] : null;
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
            var totalSw = Stopwatch.StartNew();

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
                    var dlSw = Stopwatch.StartNew();
                    await DownloadBatch(toDownload, serverManifest);
                    if (dlSw.ElapsedMilliseconds > SlowStepMs)
                        Debug.LogWarning($"[INIT SLOW] SpriteService.DownloadBatch ({toDownload.Count} sprites) took {dlSw.ElapsedMilliseconds}ms");
                }

                var cacheSw = Stopwatch.StartNew();
                LoadAllFromCache();
                if (cacheSw.ElapsedMilliseconds > SlowStepMs)
                    Debug.LogWarning($"[INIT SLOW] SpriteService.LoadAllFromCache took {cacheSw.ElapsedMilliseconds}ms ({_textures.Count} sprites)");

                Debug.Log($"[INIT] SpriteService.SyncSprites total: {totalSw.ElapsedMilliseconds}ms ({_textures.Count} sprites loaded)");
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
            var localManifest = LoadLocalManifest();

            // Try bundle download first, fall back to individual downloads
            if (!await DownloadBundle(keys, serverManifest, localManifest))
            {
                Debug.Log("SpriteService: bundle failed, falling back to individual downloads");
                await DownloadIndividual(keys, serverManifest, localManifest);
            }

            SaveLocalManifest(localManifest);
        }

        private async Task<bool> DownloadBundle(List<string> keys, Dictionary<string, string> serverManifest, Dictionary<string, string> localManifest)
        {
            var token = SocialSaveManager.Instance?.Data?.authToken;
            if (string.IsNullOrEmpty(token)) return false;

            var keysJson = new StringBuilder();
            keysJson.Append("{\"keys\":[");
            for (int i = 0; i < keys.Count; i++)
            {
                if (i > 0) keysJson.Append(",");
                keysJson.Append($"\"{keys[i]}\"");
            }
            keysJson.Append("]}");

            var url = $"{ServerBaseUrl}/game/sprites/bundle";
            using var req = new UnityWebRequest(url, "POST");
            var bodyBytes = System.Text.Encoding.UTF8.GetBytes(keysJson.ToString());
            req.uploadHandler = new UploadHandlerRaw(bodyBytes);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", $"Bearer {token}");

            var tcs = new TaskCompletionSource<bool>();
            var op = req.SendWebRequest();
            op.completed += _ => tcs.SetResult(true);
            await tcs.Task;

            if (req.responseCode != 200)
            {
                Debug.LogWarning($"SpriteService: bundle request failed (HTTP {req.responseCode})");
                return false;
            }

            try
            {
                var zipBytes = req.downloadHandler.data;
                using var zipStream = new System.IO.Compression.ZipArchive(
                    new MemoryStream(zipBytes), System.IO.Compression.ZipArchiveMode.Read);

                foreach (var entry in zipStream.Entries)
                {
                    if (entry.Length == 0) continue;

                    // Entry name is "key.png" — strip .png to get the sprite key
                    var entryName = entry.FullName;
                    var key = entryName.EndsWith(".png")
                        ? entryName.Substring(0, entryName.Length - 4)
                        : entryName;

                    var filePath = Path.Combine(CacheDir, key.Replace('/', Path.DirectorySeparatorChar) + ".png");
                    Directory.CreateDirectory(Path.GetDirectoryName(filePath));

                    using var entryStream = entry.Open();
                    using var fileStream = File.Create(filePath);
                    entryStream.CopyTo(fileStream);

                    if (serverManifest.TryGetValue(key, out var hash))
                        localManifest[key] = hash;
                }

                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"SpriteService: failed to extract bundle ({e.Message})");
                return false;
            }
        }

        private async Task DownloadIndividual(List<string> keys, Dictionary<string, string> serverManifest, Dictionary<string, string> localManifest)
        {
            const int batchSize = 8;

            for (int i = 0; i < keys.Count; i += batchSize)
            {
                var batch = keys.GetRange(i, Math.Min(batchSize, keys.Count - i));
                var tasks = new List<Task>();

                foreach (var key in batch)
                    tasks.Add(DownloadOne(key, serverManifest[key], localManifest));

                await Task.WhenAll(tasks);
            }
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
