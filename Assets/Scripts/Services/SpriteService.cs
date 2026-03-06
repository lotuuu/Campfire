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
