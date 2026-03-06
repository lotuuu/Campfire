using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

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
        public List<SeedInventoryEntry> seeds;
        public List<InventoryItem> items;
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

        private EconomyQueue _queue = new();
        private string _queuePath;
        private bool _isSyncing;

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
            _queuePath = System.IO.Path.Combine(Application.persistentDataPath, "economy_queue.json");
            LoadQueue();
        }

        public async void Initialize()
        {
            if (!SocialService.Instance.IsSignedIn) return;

            try
            {
                using var getReq = GetAuth("/economy/state");
                await SendAsync(getReq);

                if (getReq.responseCode == 200)
                {
                    var state = JsonUtility.FromJson<EconomyState>(getReq.downloadHandler.text);
                    ApplyServerState(state);
                    IsInitialized = true;
                    IsOnline = true;
                    OnStateSynced?.Invoke();
                    await DrainQueue();
                    return;
                }

                if (getReq.responseCode == 404)
                {
                    using var initReq = PostJson("/economy/init", "{}");
                    await SendAsync(initReq);

                    if (initReq.responseCode == 201)
                    {
                        var state = JsonUtility.FromJson<EconomyState>(initReq.downloadHandler.text);
                        ApplyServerState(state);
                        IsInitialized = true;
                        IsOnline = true;
                        OnStateSynced?.Invoke();
                        ClearQueue();
                        return;
                    }
                }

                Debug.LogWarning("EconomyService: Could not sync with server, running offline.");
                IsInitialized = true;
                IsOnline = false;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"EconomyService: Init failed ({e.Message}), running offline.");
                IsInitialized = true;
                IsOnline = false;
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

                // Sync from server after drain completes to reconcile state
                await SyncFromServer();
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
            var data = SaveManager.Instance.Data;
            data.mana = state.mana;
            data.gems = state.gems;
            data.flameLevel = state.flameLevel;

            data.seedInventory.Clear();
            if (state.seeds != null)
            {
                foreach (var s in state.seeds)
                    data.seedInventory.Add(new SeedInventoryEntry { seedName = s.seedName, count = s.count });
            }

            data.items.Clear();
            if (state.items != null)
            {
                foreach (var i in state.items)
                    data.items.Add(new InventoryItem { itemName = i.itemName, count = i.count });
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

        private void ClearQueue()
        {
            _queue.actions.Clear();
            SaveQueue();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused && IsOnline)
                _ = CollectMana();
        }

        private UnityWebRequest GetAuth(string path)
        {
            var request = UnityWebRequest.Get(ServerBaseUrl + path);
            request.downloadHandler = new DownloadHandlerBuffer();
            SetAuthHeader(request);
            return request;
        }

        private UnityWebRequest PostJson(string path, string json)
        {
            var request = new UnityWebRequest(ServerBaseUrl + path, "POST");
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
