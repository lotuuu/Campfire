using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

namespace Garden
{
    [Serializable]
    public class EconomyAction
    {
        public string type;
        public string jsonBody;
    }

    [Serializable]
    public class EconomyQueue
    {
        public List<EconomyAction> actions = new();
    }

    [Serializable]
    public class EconomyState
    {
        public float mana;
        public int gems;
        public int flameLevel;
        public string lastManaCollectUtc;
        public List<InventoryItem> inventory;
    }

    [Serializable]
    public class SpendManaRequest
    {
        public float amount;
        public bool freeMode;
    }

    [Serializable]
    public class SpendGemsRequest
    {
        public int amount;
        public bool freeMode;
    }

    [Serializable]
    public class AddGemsRequest
    {
        public int amount;
    }

    [Serializable]
    public class AddSeedRequest
    {
        public string seed_name;
        public int count;
    }

    [Serializable]
    public class SpendSeedRequest
    {
        public string seed_name;
        public int count;
        public bool freeMode;
    }

    [Serializable]
    public class AddItemRequest
    {
        public string item_name;
        public int count;
    }

    [Serializable]
    public class SpendItemsRequest
    {
        public List<SpendItemEntry> items;
        public bool freeMode;
    }

    [Serializable]
    public class SpendItemEntry
    {
        public string item_name;
        public int count;
    }

    [Serializable]
    public class UpgradeFlameRequest
    {
        public List<SpendItemEntry> items;
        public bool freeMode;
    }

    public class EconomyService : MonoBehaviour
    {
        public static EconomyService Instance { get; private set; }

        public bool IsInitialized { get; private set; }
        public bool IsOnline { get; private set; }

        public event Action OnStateSynced;
        public event Action<string> OnInitFailed;

        private EconomyQueue _queue = new();
        private string _queuePath;
        private bool _isSyncing;

        private static string ServerBaseUrl => ServerConfig.BaseUrl;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _queuePath = System.IO.Path.Combine(Application.persistentDataPath, ServerConfig.SavePrefix + "economy_queue.json");
            LoadQueue();
        }

        private const long SlowStepMs = 500;

        public async void Initialize()
        {
            if (SocialService.Instance == null || !SocialService.Instance.IsSignedIn) return;

            var totalSw = Stopwatch.StartNew();

            try
            {
                var sw = Stopwatch.StartNew();
                using var getReq = GetAuth("/economy/state");
                await SendAsync(getReq);
                if (sw.ElapsedMilliseconds > SlowStepMs)
                    Debug.LogWarning($"[INIT SLOW] GET /economy/state took {sw.ElapsedMilliseconds}ms");

                if (getReq.responseCode == 200)
                {
                    var state = JsonUtility.FromJson<EconomyState>(getReq.downloadHandler.text);
                    ApplyServerState(state);
                    IsInitialized = true;
                    IsOnline = true;
                    Debug.Log($"[INIT] EconomyService total: {totalSw.ElapsedMilliseconds}ms");
                    OnStateSynced?.Invoke();
                    await DrainQueue();
                    return;
                }

                if (getReq.responseCode == 404)
                {
                    sw.Restart();
                    using var initReq = PostJson("/economy/init", "{}");
                    await SendAsync(initReq);
                    if (sw.ElapsedMilliseconds > SlowStepMs)
                        Debug.LogWarning($"[INIT SLOW] POST /economy/init took {sw.ElapsedMilliseconds}ms");

                    if (initReq.responseCode == 201)
                    {
                        var state = JsonUtility.FromJson<EconomyState>(initReq.downloadHandler.text);
                        ApplyServerState(state);
                        IsInitialized = true;
                        IsOnline = true;
                        Debug.Log($"[INIT] EconomyService total: {totalSw.ElapsedMilliseconds}ms");
                        OnStateSynced?.Invoke();
                        ClearQueue();
                        return;
                    }
                }

                Debug.LogWarning("EconomyService: Could not sync with server.");
                IsInitialized = true;
                IsOnline = false;
                OnInitFailed?.Invoke("Could not sync economy with server");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"EconomyService: Init failed ({e.Message}).");
                IsInitialized = true;
                IsOnline = false;
                OnInitFailed?.Invoke("Could not sync economy with server");
            }
        }

        public void Enqueue(string actionType, string jsonBody)
        {
            _queue.actions.Add(new EconomyAction { type = actionType, jsonBody = jsonBody });
            SaveQueue();

            if (IsOnline && !_isSyncing)
                _ = DrainQueue();
        }

        public async Task CollectMana()
        {
            if (!IsOnline) return;

            try
            {
                using var req = PostJson("/economy/collect-mana", "{}");
                await SendAsync(req);

                if (req.responseCode == 200)
                {
                    var resp = JsonUtility.FromJson<ManaResponse>(req.downloadHandler.text);
                    SaveManager.Instance.Data.mana = resp.mana;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"EconomyService: CollectMana failed: {e.Message}");
            }
        }

        public async Task SyncFromServer()
        {
            try
            {
                using var req = GetAuth("/economy/state");
                await SendAsync(req);

                if (req.responseCode == 200)
                {
                    var state = JsonUtility.FromJson<EconomyState>(req.downloadHandler.text);
                    ApplyServerState(state);
                    OnStateSynced?.Invoke();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"EconomyService: SyncFromServer failed: {e.Message}");
            }
        }

        private const int MaxRetries = 3;

        private async Task DrainQueue()
        {
            if (_isSyncing) return;
            _isSyncing = true;

            try
            {
                while (_queue.actions.Count > 0)
                {
                    var action = _queue.actions[0];
                    bool success = false;

                    for (int attempt = 0; attempt < MaxRetries; attempt++)
                    {
                        success = await SendAction(action);
                        if (success) break;
                        if (attempt < MaxRetries - 1)
                            await Task.Delay(1000);
                    }

                    if (success)
                    {
                        _queue.actions.RemoveAt(0);
                        SaveQueue();
                    }
                    else
                    {
                        Debug.LogWarning($"EconomyService: Action {action.type} failed after {MaxRetries} retries, skipping.");
                        _queue.actions.RemoveAt(0);
                        SaveQueue();
                    }
                }

                // Do NOT SyncFromServer here — it overwrites local state with
                // potentially stale server data, causing race conditions with
                // GameService-based actions (harvests, flame upgrades, etc.).
            }
            finally
            {
                _isSyncing = false;
            }
        }

        private async Task<bool> SendAction(EconomyAction action)
        {
            try
            {
                using var req = PostJson($"/economy/{action.type}", action.jsonBody);
                await SendAsync(req);
                return req.responseCode >= 200 && req.responseCode < 300;
            }
            catch
            {
                IsOnline = false;
                return false;
            }
        }

        private void ApplyServerState(EconomyState state)
        {
            if (state == null) return;
            var data = SaveManager.Instance.Data;
            data.mana = state.mana;
            data.gems = state.gems;
            data.flameLevel = state.flameLevel;

            data.inventory.Clear();
            if (state.inventory != null)
            {
                foreach (var i in state.inventory)
                    data.inventory.Add(new InventoryItem { itemName = i.itemName, count = i.count });
            }

            SaveManager.Instance.Save();
        }

        private void LoadQueue()
        {
            try
            {
                if (System.IO.File.Exists(_queuePath))
                {
                    var json = System.IO.File.ReadAllText(_queuePath);
                    _queue = JsonUtility.FromJson<EconomyQueue>(json) ?? new EconomyQueue();
                }
            }
            catch { _queue = new EconomyQueue(); }
        }

        private void SaveQueue()
        {
            try
            {
                System.IO.File.WriteAllText(_queuePath, JsonUtility.ToJson(_queue));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"EconomyService: Failed to save queue: {e.Message}");
            }
        }

        public void ClearQueue()
        {
            _queue.actions.Clear();
            SaveQueue();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused && IsOnline)
                _ = CollectMana();
        }

        private const int RequestTimeoutSeconds = 15;

        private UnityWebRequest GetAuth(string path)
        {
            var request = UnityWebRequest.Get(ServerBaseUrl + path);
            request.timeout = RequestTimeoutSeconds;
            request.downloadHandler = new DownloadHandlerBuffer();
            SetAuthHeader(request);
            return request;
        }

        private UnityWebRequest PostJson(string path, string json)
        {
            var request = new UnityWebRequest(ServerBaseUrl + path, "POST");
            request.timeout = RequestTimeoutSeconds;
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            SetAuthHeader(request);
            return request;
        }

        private void SetAuthHeader(UnityWebRequest request)
        {
            var token = SocialSaveManager.Instance?.Data?.authToken;
            if (!string.IsNullOrEmpty(token))
                request.SetRequestHeader("Authorization", $"Bearer {token}");
        }

        private static Task<UnityWebRequest> SendAsync(UnityWebRequest request)
        {
            var tcs = new TaskCompletionSource<UnityWebRequest>();
            var op = request.SendWebRequest();
            op.completed += _ => tcs.SetResult(request);
            return tcs.Task;
        }

        [Serializable]
        private class ManaResponse { public float mana; }
    }
}
