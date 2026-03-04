using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Garden
{
    public class SocialService : MonoBehaviour
    {
        public static SocialService Instance { get; private set; }

        // Set via build script or DevServerConfig resource at runtime
        public static readonly string ServerBaseUrl =
#if UNITY_EDITOR
            "http://localhost:3000";
#else
            DevServerConfig.BaseUrl;
#endif

        public event Action OnSignedIn;
        public event Action<List<FriendRequest>> OnFriendRequestsUpdated;
        public event Action<List<GiftMessage>> OnGiftsUpdated;
        public event Action<List<CachedFriend>> OnFriendListUpdated;
        public event Action<string> OnDisplayNameUpdated;

        public bool IsSignedIn { get; private set; }
        public string Uid => SocialSaveManager.Instance?.Data?.uid;
        public string FriendCode => SocialSaveManager.Instance?.Data?.friendCode;

        public string AuthToken => SocialSaveManager.Instance?.Data?.authToken;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            Initialize();
        }

        private async void Initialize()
        {
            var social = SocialSaveManager.Instance.Data;

            if (!string.IsNullOrEmpty(social.uid) && !string.IsNullOrEmpty(social.authToken))
            {
                IsSignedIn = true;
                OnSignedIn?.Invoke();
                return;
            }

            try
            {
                var body = JsonUtility.ToJson(new RegisterRequest
                {
                    displayName = social.displayName
                });

                using var request = PostJson("/auth/register", body, authenticated: false);
                await SendAsync(request);

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"SocialService: Registration failed: {request.error} — {request.downloadHandler.text}");
                    return;
                }

                var response = JsonUtility.FromJson<RegisterResponse>(request.downloadHandler.text);
                social.uid = response.uid;
                social.authToken = response.authToken;
                social.friendCode = response.friendCode;
                social.displayName = response.displayName;
                SocialSaveManager.Instance.Save();

                IsSignedIn = true;
                OnSignedIn?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogError($"SocialService: Initialize failed: {e.Message}");
            }
        }

        // ── Display Name ──

        private static readonly System.Text.RegularExpressions.Regex DisplayNameRegex =
            new(@"^[a-zA-Z0-9 ]+$");

        public static bool IsValidDisplayName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            var trimmed = name.Trim();
            return trimmed.Length >= 1 && trimmed.Length <= 20 && DisplayNameRegex.IsMatch(trimmed);
        }

        public async Task<bool> UpdateDisplayName(string newName)
        {
            if (!IsSignedIn || !IsValidDisplayName(newName)) return false;

            var trimmed = newName.Trim();
            try
            {
                var body = JsonUtility.ToJson(new UpdateDisplayNameRequest { displayName = trimmed });
                using var request = PutJson("/auth/display-name", body);
                await SendAsync(request);

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"SocialService: UpdateDisplayName failed: {request.error} — {request.downloadHandler.text}");
                    return false;
                }

                var response = JsonUtility.FromJson<UpdateDisplayNameResponse>(request.downloadHandler.text);
                SocialSaveManager.Instance.Data.displayName = response.displayName;
                SocialSaveManager.Instance.Save();
                OnDisplayNameUpdated?.Invoke(response.displayName);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"SocialService: UpdateDisplayName failed: {e.Message}");
                return false;
            }
        }

        // ── Friend Requests ──

        public async Task<bool> SendFriendRequest(string targetFriendCode)
        {
            if (!IsSignedIn || string.IsNullOrEmpty(targetFriendCode)) return false;

            try
            {
                var body = JsonUtility.ToJson(new SendFriendRequestBody { friendCode = targetFriendCode });
                using var request = PostJson("/friends/request", body);
                await SendAsync(request);

                if (request.result != UnityWebRequest.Result.Success)
                {
                    if (request.responseCode >= 500)
                        Debug.LogError($"SocialService: SendFriendRequest failed: {request.error} — {request.downloadHandler.text}");
                    return false;
                }

                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"SocialService: SendFriendRequest failed: {e.Message}");
                return false;
            }
        }

        public async Task<List<FriendRequest>> GetPendingRequests()
        {
            var requests = new List<FriendRequest>();
            if (!IsSignedIn) return requests;

            try
            {
                using var request = GetAuth("/friends/requests");
                await SendAsync(request);

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"SocialService: GetPendingRequests failed: {request.error}");
                    return requests;
                }

                var response = JsonUtility.FromJson<FriendRequestsResponse>(request.downloadHandler.text);
                if (response?.requests != null)
                {
                    foreach (var r in response.requests)
                    {
                        requests.Add(new FriendRequest
                        {
                            id = r.id,
                            fromUid = r.from_uid,
                            fromName = r.from_name,
                            status = r.status
                        });
                    }
                }

                OnFriendRequestsUpdated?.Invoke(requests);
            }
            catch (Exception e)
            {
                Debug.LogError($"SocialService: GetPendingRequests failed: {e.Message}");
            }

            return requests;
        }

        public async Task<bool> AcceptFriendRequest(string requestId)
        {
            if (!IsSignedIn) return false;

            try
            {
                using var request = PostJson($"/friends/accept/{requestId}", "{}");
                await SendAsync(request);

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"SocialService: AcceptFriendRequest failed: {request.error}");
                    return false;
                }

                await RefreshFriendList();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"SocialService: AcceptFriendRequest failed: {e.Message}");
                return false;
            }
        }

        public async Task<bool> DeclineFriendRequest(string requestId)
        {
            if (!IsSignedIn) return false;

            try
            {
                using var request = PostJson($"/friends/decline/{requestId}", "{}");
                await SendAsync(request);

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"SocialService: DeclineFriendRequest failed: {request.error}");
                    return false;
                }

                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"SocialService: DeclineFriendRequest failed: {e.Message}");
                return false;
            }
        }

        // ── Friend List ──

        public async Task RefreshFriendList()
        {
            if (!IsSignedIn) return;

            try
            {
                using var request = GetAuth("/friends");
                await SendAsync(request);

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"SocialService: RefreshFriendList failed: {request.error}");
                    return;
                }

                var response = JsonUtility.FromJson<FriendsResponse>(request.downloadHandler.text);
                var friends = new List<CachedFriend>();
                if (response?.friends != null)
                {
                    foreach (var f in response.friends)
                    {
                        friends.Add(new CachedFriend
                        {
                            uid = f.uid,
                            displayName = f.display_name,
                            friendCode = f.friend_code
                        });
                    }
                }

                SocialSaveManager.Instance.Data.cachedFriends = friends;
                SocialSaveManager.Instance.Save();
                OnFriendListUpdated?.Invoke(friends);
            }
            catch (Exception e)
            {
                Debug.LogError($"SocialService: RefreshFriendList failed: {e.Message}");
            }
        }

        public async Task<bool> RemoveFriend(string friendUid)
        {
            if (!IsSignedIn) return false;

            try
            {
                using var request = DeleteAuth($"/friends/{friendUid}");
                await SendAsync(request);

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"SocialService: RemoveFriend failed: {request.error}");
                    return false;
                }

                await RefreshFriendList();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"SocialService: RemoveFriend failed: {e.Message}");
                return false;
            }
        }

        // ── Village Snapshots ──

        public async Task PushVillageSnapshot()
        {
            if (!IsSignedIn) return;

            try
            {
                var data = SaveManager.Instance.Data;
                var snapshot = VillageSnapshot.FromSaveData(data, FlameManager.Instance.Level);
                var body = JsonUtility.ToJson(new VillageSnapshotRequest { snapshot = snapshot });

                using var request = PutJson("/village", body);
                await SendAsync(request);

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"SocialService: PushVillageSnapshot failed: {request.error}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"SocialService: PushVillageSnapshot failed: {e.Message}");
            }
        }

        public async Task<VillageSnapshot> FetchVillageSnapshot(string friendUid)
        {
            if (!IsSignedIn) return null;

            try
            {
                using var request = GetAuth($"/village/{friendUid}");
                await SendAsync(request);

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"SocialService: FetchVillageSnapshot failed: {request.error}");
                    return null;
                }

                var response = JsonUtility.FromJson<VillageSnapshotResponse>(request.downloadHandler.text);
                return response?.snapshot;
            }
            catch (Exception e)
            {
                Debug.LogError($"SocialService: FetchVillageSnapshot failed: {e.Message}");
                return null;
            }
        }

        // ── Gifts ──

        public async Task<bool> SendGift(string toUid, List<GiftItem> items)
        {
            if (!IsSignedIn || items == null || items.Count == 0 || items.Count > 3) return false;

            try
            {
                var body = JsonUtility.ToJson(new SendGiftRequest { toUid = toUid, items = items });
                using var request = PostJson("/gifts/send", body);
                await SendAsync(request);

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"SocialService: SendGift failed: {request.error} — {request.downloadHandler.text}");
                    return false;
                }

                DeductItemsLocally(items);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"SocialService: SendGift failed: {e.Message}");
                return false;
            }
        }

        public async Task<List<GiftMessage>> GetPendingGifts()
        {
            var gifts = new List<GiftMessage>();
            if (!IsSignedIn) return gifts;

            try
            {
                using var request = GetAuth("/gifts");
                await SendAsync(request);

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"SocialService: GetPendingGifts failed: {request.error}");
                    return gifts;
                }

                var response = JsonUtility.FromJson<GiftsResponse>(request.downloadHandler.text);
                if (response?.gifts != null)
                {
                    foreach (var g in response.gifts)
                    {
                        gifts.Add(new GiftMessage
                        {
                            id = g.id,
                            fromUid = g.from_uid,
                            fromName = g.from_name,
                            items = g.items ?? new List<GiftItem>()
                        });
                    }
                }

                OnGiftsUpdated?.Invoke(gifts);
            }
            catch (Exception e)
            {
                Debug.LogError($"SocialService: GetPendingGifts failed: {e.Message}");
            }

            return gifts;
        }

        public async Task<bool> ClaimGift(string giftId, List<GiftItem> items)
        {
            if (!IsSignedIn) return false;

            try
            {
                using var request = PostJson($"/gifts/claim/{giftId}", "{}");
                await SendAsync(request);

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"SocialService: ClaimGift failed: {request.error}");
                    return false;
                }

                AddItemsLocally(items);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"SocialService: ClaimGift failed: {e.Message}");
                return false;
            }
        }

        // ── Local Inventory Helpers ──

        private void DeductItemsLocally(List<GiftItem> items)
        {
            var data = SaveManager.Instance.Data;
            foreach (var item in items)
            {
                if (item.type == "seed")
                {
                    var entry = data.seedInventory.Find(s => s.seedName == item.name);
                    if (entry != null)
                    {
                        entry.count -= item.count;
                        if (entry.count <= 0) data.seedInventory.Remove(entry);
                    }
                }
                else
                {
                    var entry = data.items.Find(i => i.itemName == item.name);
                    if (entry != null)
                    {
                        entry.count -= item.count;
                        if (entry.count <= 0) data.items.Remove(entry);
                    }
                }
            }
            SaveManager.Instance.Save();
        }

        private void AddItemsLocally(List<GiftItem> items)
        {
            foreach (var item in items)
            {
                if (item.type == "seed")
                    ApothekeManager.Instance.AddSeed(item.name, item.count);
                else
                {
                    var data = SaveManager.Instance.Data;
                    var entry = data.items.Find(i => i.itemName == item.name);
                    if (entry != null)
                        entry.count += item.count;
                    else
                        data.items.Add(new InventoryItem { itemName = item.name, count = item.count });
                    SaveManager.Instance.Save();
                }
            }
        }

        // ── HTTP Helpers ──

        private static Task<UnityWebRequest> SendAsync(UnityWebRequest request)
        {
            var tcs = new TaskCompletionSource<UnityWebRequest>();
            var op = request.SendWebRequest();
            op.completed += _ => tcs.SetResult(request);
            return tcs.Task;
        }

        private UnityWebRequest GetAuth(string path)
        {
            var request = UnityWebRequest.Get(ServerBaseUrl + path);
            request.downloadHandler = new DownloadHandlerBuffer();
            SetAuthHeader(request);
            return request;
        }

        private UnityWebRequest PostJson(string path, string json, bool authenticated = true)
        {
            var request = new UnityWebRequest(ServerBaseUrl + path, "POST");
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            if (authenticated) SetAuthHeader(request);
            return request;
        }

        private UnityWebRequest PutJson(string path, string json)
        {
            var request = new UnityWebRequest(ServerBaseUrl + path, "PUT");
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            SetAuthHeader(request);
            return request;
        }

        private UnityWebRequest DeleteAuth(string path)
        {
            var request = UnityWebRequest.Delete(ServerBaseUrl + path);
            request.downloadHandler = new DownloadHandlerBuffer();
            SetAuthHeader(request);
            return request;
        }

        private void SetAuthHeader(UnityWebRequest request)
        {
            if (!string.IsNullOrEmpty(AuthToken))
                request.SetRequestHeader("Authorization", $"Bearer {AuthToken}");
        }

        // ── JSON Serialization Types (snake_case to match server responses) ──

        [Serializable]
        private class RegisterRequest
        {
            public string displayName;
        }

        [Serializable]
        private class RegisterResponse
        {
            public string uid;
            public string authToken;
            public string friendCode;
            public string displayName;
        }

        [Serializable]
        private class UpdateDisplayNameRequest
        {
            public string displayName;
        }

        [Serializable]
        private class UpdateDisplayNameResponse
        {
            public string displayName;
        }

        [Serializable]
        private class SendFriendRequestBody
        {
            public string friendCode;
        }

        [Serializable]
        private class FriendRequestEntry
        {
            public string id;
            public string from_uid;
            public string from_name;
            public string status;
        }

        [Serializable]
        private class FriendRequestsResponse
        {
            public List<FriendRequestEntry> requests;
        }

        [Serializable]
        private class FriendEntry
        {
            public string uid;
            public string display_name;
            public string friend_code;
        }

        [Serializable]
        private class FriendsResponse
        {
            public List<FriendEntry> friends;
        }

        [Serializable]
        private class VillageSnapshotRequest
        {
            public VillageSnapshot snapshot;
        }

        [Serializable]
        private class VillageSnapshotResponse
        {
            public VillageSnapshot snapshot;
        }

        [Serializable]
        private class SendGiftRequest
        {
            public string toUid;
            public List<GiftItem> items;
        }

        [Serializable]
        private class GiftEntry
        {
            public string id;
            public string from_uid;
            public string from_name;
            public List<GiftItem> items;
        }

        [Serializable]
        private class GiftsResponse
        {
            public List<GiftEntry> gifts;
        }
    }
}
