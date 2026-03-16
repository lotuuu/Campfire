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
        /// Converts a seed item key like "basil_seed" to the plant slug "basil".
        /// </summary>
        public static string SeedToSpriteKey(string seedItemKey)
        {
            if (string.IsNullOrEmpty(seedItemKey)) return seedItemKey;
            return seedItemKey.EndsWith("_seed") ? seedItemKey[..^5] : seedItemKey;
        }

        /// <summary>
        /// Resolves any inventory item key to its sprite key under "items/".
        /// Uses server-authoritative ItemConfig.spriteKey when available,
        /// otherwise derives from item_key and category.
        /// </summary>
        public static string ItemToSpriteKey(string itemKey)
        {
            if (string.IsNullOrEmpty(itemKey)) return null;

            // Check for explicit sprite_key override from config
            var config = ConfigService.Instance?.GetItem(itemKey);
            if (config?.spriteKey != null) return config.spriteKey;

            // Derive from item_key and category
            if (config != null)
            {
                return config.category switch
                {
                    "seed" => $"items/{itemKey.Replace("_seed", "")}/seed",
                    "harvest" => $"items/{itemKey}/harvest",
                    "pigment" => $"items/{itemKey.Replace("_pigment", "")}/pigment",
                    _ => $"items/{itemKey}"
                };
            }

            // Fallback: direct mapping
            return $"items/{itemKey}";
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

        /// <summary>Downloads any sprites whose hashes differ from the server manifest.</summary>
        public async Task<bool> DownloadSprites(Dictionary<string, string> serverManifest)
        {
            if (serverManifest == null || serverManifest.Count == 0)
                return true;

            try
            {
                Directory.CreateDirectory(CacheDir);
                var localManifest = LoadLocalManifest();

                var toDownload = new List<string>();
                foreach (var kv in serverManifest)
                {
                    if (!localManifest.TryGetValue(kv.Key, out var localHash) || localHash != kv.Value)
                        toDownload.Add(kv.Key);
                }

                if (toDownload.Count > 0)
                {
                    BootTimer.Mark($"SpriteService: need to download {toDownload.Count} of {serverManifest.Count} sprites");
                    var dlSw = Stopwatch.StartNew();
                    await DownloadBatch(toDownload, serverManifest);
                    BootTimer.Mark($"SpriteService.DownloadBatch done ({dlSw.ElapsedMilliseconds}ms, {toDownload.Count} sprites)");
                    if (dlSw.ElapsedMilliseconds > SlowStepMs)
                        Debug.LogWarning($"[INIT SLOW] SpriteService.DownloadBatch ({toDownload.Count} sprites) took {dlSw.ElapsedMilliseconds}ms");
                }
                else
                {
                    BootTimer.Mark($"SpriteService: all {serverManifest.Count} sprites cached, no downloads needed");
                }

                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"SpriteService: download failed ({e.Message}).");
                return false;
            }
        }

        /// <summary>Loads all cached sprite PNGs into memory as Texture2D.</summary>
        public void LoadTextures()
        {
            var sw = Stopwatch.StartNew();
            LoadAllFromCache();
            BootTimer.Mark($"SpriteService.LoadAllFromCache done ({sw.ElapsedMilliseconds}ms, {_textures.Count} textures)");
            if (sw.ElapsedMilliseconds > SlowStepMs)
                Debug.LogWarning($"[INIT SLOW] SpriteService.LoadAllFromCache took {sw.ElapsedMilliseconds}ms ({_textures.Count} sprites)");
        }

        /// <summary>Downloads sprites then loads textures in one call (legacy convenience).</summary>
        public async Task<bool> SyncSprites(Dictionary<string, string> serverManifest)
        {
            var totalSw = Stopwatch.StartNew();
            var ok = await DownloadSprites(serverManifest);
            LoadTextures();
            Debug.Log($"[INIT] SpriteService.SyncSprites total: {totalSw.ElapsedMilliseconds}ms ({_textures.Count} sprites loaded)");
            return ok || _textures.Count > 0;
        }

        // ── Download ──

        private async Task DownloadBatch(List<string> keys, Dictionary<string, string> serverManifest)
        {
            var localManifest = LoadLocalManifest();

            // Try bundle download first, fall back to individual downloads
            var bundleSw = Stopwatch.StartNew();
            if (!await DownloadBundle(keys, serverManifest, localManifest))
            {
                BootTimer.Mark($"SpriteService: bundle failed ({bundleSw.ElapsedMilliseconds}ms), falling back to individual");
                await DownloadIndividual(keys, serverManifest, localManifest);
            }
            else
            {
                BootTimer.Mark($"SpriteService: bundle download succeeded ({bundleSw.ElapsedMilliseconds}ms)");
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
