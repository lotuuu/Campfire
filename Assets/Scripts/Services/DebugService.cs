using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Garden
{
    public class DebugService : MonoBehaviour
    {
        public static DebugService Instance { get; private set; }

        private static string ServerBaseUrl => ServerConfig.BaseUrl;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        // --- Remote Debug Logging ---
        private const float FlushIntervalSeconds = 5f;
        private const int MaxQueueSize = 50;

        private readonly List<RemoteLogEntry> _logQueue = new();
        private float _lastFlushTime;

        [Serializable]
        private class RemoteLogEntry
        {
            public string level;
            public string message;
            public string category;
            public string metadata;
        }

        public void LogRemoteError(string message, string category = "client", Dictionary<string, string> metadata = null)
        {
            QueueRemoteLog("error", message, category, metadata);
        }

        public void LogRemoteWarning(string message, string category = "client", Dictionary<string, string> metadata = null)
        {
            QueueRemoteLog("warning", message, category, metadata);
        }

        private void QueueRemoteLog(string level, string message, string category, Dictionary<string, string> metadata)
        {
            if (_logQueue.Count >= MaxQueueSize)
                _logQueue.RemoveAt(0);

            var metaJson = "{}";
            if (metadata != null && metadata.Count > 0)
            {
                var parts = new List<string>();
                foreach (var kv in metadata)
                    parts.Add($"\"{EscapeJson(kv.Key)}\":\"{EscapeJson(kv.Value)}\"");
                metaJson = "{" + string.Join(",", parts) + "}";
            }

            _logQueue.Add(new RemoteLogEntry
            {
                level = level,
                message = message,
                category = category,
                metadata = metaJson
            });
        }

        private void Update()
        {
            if (_logQueue.Count > 0 && Time.realtimeSinceStartup - _lastFlushTime >= FlushIntervalSeconds)
            {
                _lastFlushTime = Time.realtimeSinceStartup;
                FlushLogQueue();
            }
        }

        private async void FlushLogQueue()
        {
            var batch = new List<RemoteLogEntry>(_logQueue);
            _logQueue.Clear();

            foreach (var entry in batch)
            {
                var json = $"{{\"level\":\"{entry.level}\",\"message\":\"{EscapeJson(entry.message)}\",\"category\":\"{entry.category}\",\"metadata\":{entry.metadata}}}";
                await PostQuiet("/debug/log", json);
            }
        }

        private static string EscapeJson(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
        }

        public async Task<bool> SkipTime(int hours)
        {
            return await Post("/debug/skip-time", JsonUtility.ToJson(new SkipTimeReq { hours = hours }));
        }

        /// <summary>
        /// Skip time without triggering a full game state re-init.
        /// Used by time acceleration to avoid expensive re-fetches on every tick.
        /// </summary>
        public async Task<bool> SkipTimeQuiet(float hours)
        {
            return await PostQuiet("/debug/skip-time", JsonUtility.ToJson(new SkipTimeReqFloat { hours = hours }));
        }

        public async Task<bool> SetCurrency(float? mana = null, int? gems = null)
        {
            // Build JSON manually since JsonUtility doesn't handle nullable
            var parts = new List<string>();
            if (mana.HasValue) parts.Add($"\"mana\":{mana.Value}");
            if (gems.HasValue) parts.Add($"\"gems\":{gems.Value}");
            return await Post("/debug/set-currency", "{" + string.Join(",", parts) + "}");
        }

        public async Task<bool> GrantSeeds(string seedName, int count)
        {
            return await Post("/debug/grant-seeds",
                JsonUtility.ToJson(new GrantSeedsReq { seedName = seedName, count = count }));
        }

        public async Task<bool> GrantItems(string itemName, int count)
        {
            return await Post("/debug/grant-items",
                JsonUtility.ToJson(new GrantItemsReq { itemName = itemName, count = count }));
        }

        public async Task<bool> SpawnBird() => await Post("/debug/spawn-bird", "{}");

        public async Task<bool> CompleteQuests() => await Post("/debug/complete-quests", "{}");

        public async Task<bool> FillVases() => await Post("/debug/fill-vases", "{}");

        public async Task<bool> MaturePlots() => await Post("/debug/mature-plots", "{}");

        public async Task<bool> SetFlameLevel(int level)
        {
            return await Post("/debug/set-flame-level",
                JsonUtility.ToJson(new SetFlameLevelReq { level = level }));
        }

        public async Task<bool> ClearSave() => await PostQuiet("/debug/clear-save", "{}");

        public async Task<bool> ReceiveVisitor()
        {
            if (VisitorManager.Instance == null) return false;
            var data = SaveManager.Instance?.Data;
            if (data == null) return false;

            // Clear any existing visitor so we can fetch a fresh one
            VisitorManager.Instance.DebugKeepVisitor = true;
            data.currentVisitor = null;
            data.lastVisitorFetchDateUtc = null;

            string todayUtc = GameTime.UtcNow.Date.ToString("o");
            var visitor = await VisitorManager.Instance.FetchTonightVisitor(data, todayUtc);
            if (visitor == null)
            {
                Debug.LogWarning("[DebugService] No visitor available from server");
                return false;
            }

            data.currentVisitor = visitor;
            data.lastVisitorFetchDateUtc = todayUtc;
            SaveManager.Instance.Save();
            VisitorManager.Instance.NotifyVisitorArrived();
            Debug.Log($"[DebugService] Visitor received: {visitor.visitorName}");
            return true;
        }

        private async Task<bool> PostQuiet(string path, string json)
        {
            try
            {
                using var req = new UnityWebRequest(ServerBaseUrl + path, "POST");
                req.timeout = 15;
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                var token = SocialSaveManager.Instance?.Data?.authToken;
                if (!string.IsNullOrEmpty(token))
                    req.SetRequestHeader("Authorization", $"Bearer {token}");

                var tcs = new TaskCompletionSource<bool>();
                var op = req.SendWebRequest();
                op.completed += _ => tcs.SetResult(true);
                await tcs.Task;

                if (req.responseCode >= 200 && req.responseCode < 300)
                    return true;

                Debug.LogWarning($"[DebugService] {path} failed ({req.responseCode}): {req.downloadHandler.text}");
                return false;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[DebugService] {path} error: {e.Message}");
                return false;
            }
        }

        private async Task<bool> Post(string path, string json)
        {
            try
            {
                using var req = new UnityWebRequest(ServerBaseUrl + path, "POST");
                req.timeout = 15;
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                var token = SocialSaveManager.Instance?.Data?.authToken;
                if (!string.IsNullOrEmpty(token))
                    req.SetRequestHeader("Authorization", $"Bearer {token}");

                var tcs = new TaskCompletionSource<bool>();
                var op = req.SendWebRequest();
                op.completed += _ => tcs.SetResult(true);
                await tcs.Task;

                if (req.responseCode >= 200 && req.responseCode < 300)
                {
                    Debug.Log($"[DebugService] {path} OK: {req.downloadHandler.text}");
                    GameService.Instance?.Initialize();
                    return true;
                }

                Debug.LogWarning($"[DebugService] {path} failed ({req.responseCode}): {req.downloadHandler.text}");
                return false;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[DebugService] {path} error: {e.Message}");
                return false;
            }
        }

        [Serializable] private class SkipTimeReq { public int hours; }
        [Serializable] private class SkipTimeReqFloat { public float hours; }
        [Serializable] private class GrantSeedsReq { public string seedName; public int count; }
        [Serializable] private class GrantItemsReq { public string itemName; public int count; }
        [Serializable] private class SetFlameLevelReq { public int level; }
    }
}
