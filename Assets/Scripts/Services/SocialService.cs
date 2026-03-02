using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
#if FIREBASE_AVAILABLE
using Firebase.Auth;
using Firebase.Firestore;
using Firebase.Extensions;
#endif

#pragma warning disable CS1998 // Async method lacks 'await' — expected when FIREBASE_AVAILABLE is not defined

namespace Garden
{
    public class SocialService : MonoBehaviour
    {
        public static SocialService Instance { get; private set; }

        public event Action OnSignedIn;
        public event Action<List<FriendRequest>> OnFriendRequestsUpdated;
        public event Action<List<GiftMessage>> OnGiftsUpdated;
        public event Action<List<CachedFriend>> OnFriendListUpdated;

        public bool IsSignedIn { get; private set; }
        public string Uid => SocialSaveManager.Instance?.Data?.uid;
        public string FriendCode => SocialSaveManager.Instance?.Data?.friendCode;

#if FIREBASE_AVAILABLE
        private FirebaseAuth auth;
        private FirebaseFirestore db;
#endif

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            InitializeFirebase();
        }

        private async void InitializeFirebase()
        {
#if FIREBASE_AVAILABLE
            var dependencyStatus = await Firebase.FirebaseApp.CheckAndFixDependenciesAsync();
            if (dependencyStatus != Firebase.DependencyStatus.Available)
            {
                Debug.LogError($"SocialService: Firebase dependencies not available: {dependencyStatus}");
                return;
            }

            auth = FirebaseAuth.DefaultInstance;
            db = FirebaseFirestore.DefaultInstance;

            await SignIn();
#else
            Debug.Log("SocialService: Firebase not available — social features disabled.");
#endif
        }

        private async Task SignIn()
        {
#if FIREBASE_AVAILABLE
            var social = SocialSaveManager.Instance.Data;

            if (auth.CurrentUser != null)
            {
                social.uid = auth.CurrentUser.UserId;
                IsSignedIn = true;
                SocialSaveManager.Instance.Save();
                OnSignedIn?.Invoke();
                return;
            }

            try
            {
                var result = await auth.SignInAnonymouslyAsync();
                social.uid = result.User.UserId;
                IsSignedIn = true;
                SocialSaveManager.Instance.Save();
                OnSignedIn?.Invoke();
                await FetchPlayerProfile();
            }
            catch (Exception e)
            {
                Debug.LogError($"SocialService: Sign-in failed: {e.Message}");
            }
#endif
        }

        private async Task FetchPlayerProfile()
        {
#if FIREBASE_AVAILABLE
            var doc = await db.Collection("players").Document(Uid).GetSnapshotAsync();
            if (doc.Exists)
            {
                var social = SocialSaveManager.Instance.Data;
                social.friendCode = doc.GetValue<string>("friendCode");
                social.displayName = doc.GetValue<string>("displayName");
                SocialSaveManager.Instance.Save();
            }
#endif
        }

        // ── Friend Requests ──

        public async Task<bool> SendFriendRequest(string targetFriendCode)
        {
#if FIREBASE_AVAILABLE
            if (!IsSignedIn || string.IsNullOrEmpty(targetFriendCode)) return false;

            try
            {
                var query = await db.Collection("players")
                    .WhereEqualTo("friendCode", targetFriendCode)
                    .Limit(1)
                    .GetSnapshotAsync();

                if (query.Count == 0) return false;

                var targetDoc = query[0];
                string targetUid = targetDoc.Id;

                if (targetUid == Uid) return false;

                await db.Collection("friendRequests").AddAsync(new Dictionary<string, object>
                {
                    { "fromUid", Uid },
                    { "toUid", targetUid },
                    { "fromName", SocialSaveManager.Instance.Data.displayName },
                    { "status", "pending" },
                    { "createdAt", FieldValue.ServerTimestamp }
                });

                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"SocialService: SendFriendRequest failed: {e.Message}");
                return false;
            }
#else
            return false;
#endif
        }

        public async Task<List<FriendRequest>> GetPendingRequests()
        {
            var requests = new List<FriendRequest>();
#if FIREBASE_AVAILABLE
            if (!IsSignedIn) return requests;

            try
            {
                var query = await db.Collection("friendRequests")
                    .WhereEqualTo("toUid", Uid)
                    .WhereEqualTo("status", "pending")
                    .GetSnapshotAsync();

                foreach (var doc in query)
                {
                    requests.Add(new FriendRequest
                    {
                        id = doc.Id,
                        fromUid = doc.GetValue<string>("fromUid"),
                        fromName = doc.GetValue<string>("fromName"),
                        status = doc.GetValue<string>("status")
                    });
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"SocialService: GetPendingRequests failed: {e.Message}");
            }
#endif
            return requests;
        }

        public async Task<bool> AcceptFriendRequest(string requestId)
        {
#if FIREBASE_AVAILABLE
            if (!IsSignedIn) return false;
            try
            {
                await db.Collection("friendRequests").Document(requestId)
                    .UpdateAsync("status", "accepted");
                await RefreshFriendList();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"SocialService: AcceptFriendRequest failed: {e.Message}");
                return false;
            }
#else
            return false;
#endif
        }

        public async Task<bool> DeclineFriendRequest(string requestId)
        {
#if FIREBASE_AVAILABLE
            if (!IsSignedIn) return false;
            try
            {
                await db.Collection("friendRequests").Document(requestId)
                    .UpdateAsync("status", "declined");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"SocialService: DeclineFriendRequest failed: {e.Message}");
                return false;
            }
#else
            return false;
#endif
        }

        // ── Friend List ──

        public async Task RefreshFriendList()
        {
#if FIREBASE_AVAILABLE
            if (!IsSignedIn) return;
            try
            {
                var query = await db.Collection("friends").Document(Uid)
                    .Collection("list").GetSnapshotAsync();

                var friends = new List<CachedFriend>();
                foreach (var doc in query)
                {
                    friends.Add(new CachedFriend
                    {
                        uid = doc.Id,
                        displayName = doc.GetValue<string>("displayName"),
                        friendCode = doc.GetValue<string>("friendCode")
                    });
                }

                SocialSaveManager.Instance.Data.cachedFriends = friends;
                SocialSaveManager.Instance.Save();
                OnFriendListUpdated?.Invoke(friends);
            }
            catch (Exception e)
            {
                Debug.LogError($"SocialService: RefreshFriendList failed: {e.Message}");
            }
#endif
        }

        public async Task<bool> RemoveFriend(string friendUid)
        {
#if FIREBASE_AVAILABLE
            if (!IsSignedIn) return false;
            try
            {
                await db.Collection("friends").Document(Uid)
                    .Collection("list").Document(friendUid).DeleteAsync();
                await db.Collection("friends").Document(friendUid)
                    .Collection("list").Document(Uid).DeleteAsync();
                await RefreshFriendList();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"SocialService: RemoveFriend failed: {e.Message}");
                return false;
            }
#else
            return false;
#endif
        }

        // ── Village Snapshots ──

        public async Task PushVillageSnapshot()
        {
#if FIREBASE_AVAILABLE
            if (!IsSignedIn) return;
            try
            {
                var data = SaveManager.Instance.Data;
                var snapshot = VillageSnapshot.FromSaveData(data, FlameManager.Instance.Level);
                await db.Collection("villages").Document(Uid)
                    .SetAsync(snapshot.ToDictionary());
            }
            catch (Exception e)
            {
                Debug.LogError($"SocialService: PushVillageSnapshot failed: {e.Message}");
            }
#endif
        }

        public async Task<VillageSnapshot> FetchVillageSnapshot(string friendUid)
        {
#if FIREBASE_AVAILABLE
            if (!IsSignedIn) return null;
            try
            {
                var doc = await db.Collection("villages").Document(friendUid).GetSnapshotAsync();
                if (!doc.Exists) return null;
                return VillageSnapshot.FromDictionary(doc.ToDictionary());
            }
            catch (Exception e)
            {
                Debug.LogError($"SocialService: FetchVillageSnapshot failed: {e.Message}");
                return null;
            }
#else
            return null;
#endif
        }

        // ── Gifts ──

        public async Task<bool> SendGift(string toUid, List<GiftItem> items)
        {
#if FIREBASE_AVAILABLE
            if (!IsSignedIn || items == null || items.Count == 0 || items.Count > 3) return false;

            try
            {
                var itemDicts = new List<Dictionary<string, object>>();
                foreach (var item in items)
                {
                    itemDicts.Add(new Dictionary<string, object>
                    {
                        { "type", item.type },
                        { "name", item.name },
                        { "count", item.count }
                    });
                }

                await db.Collection("gifts").AddAsync(new Dictionary<string, object>
                {
                    { "fromUid", Uid },
                    { "toUid", toUid },
                    { "fromName", SocialSaveManager.Instance.Data.displayName },
                    { "items", itemDicts },
                    { "status", "pending" },
                    { "createdAt", FieldValue.ServerTimestamp }
                });

                DeductItemsLocally(items);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"SocialService: SendGift failed: {e.Message}");
                return false;
            }
#else
            return false;
#endif
        }

        public async Task<List<GiftMessage>> GetPendingGifts()
        {
            var gifts = new List<GiftMessage>();
#if FIREBASE_AVAILABLE
            if (!IsSignedIn) return gifts;

            try
            {
                var query = await db.Collection("gifts")
                    .WhereEqualTo("toUid", Uid)
                    .WhereEqualTo("status", "pending")
                    .GetSnapshotAsync();

                foreach (var doc in query)
                {
                    var gift = new GiftMessage
                    {
                        id = doc.Id,
                        fromUid = doc.GetValue<string>("fromUid"),
                        fromName = doc.GetValue<string>("fromName"),
                        items = new List<GiftItem>()
                    };

                    var itemsList = doc.GetValue<List<object>>("items");
                    foreach (Dictionary<string, object> itemDict in itemsList)
                    {
                        gift.items.Add(new GiftItem
                        {
                            type = itemDict["type"].ToString(),
                            name = itemDict["name"].ToString(),
                            count = Convert.ToInt32(itemDict["count"])
                        });
                    }

                    gifts.Add(gift);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"SocialService: GetPendingGifts failed: {e.Message}");
            }
#endif
            return gifts;
        }

        public async Task<bool> ClaimGift(string giftId, List<GiftItem> items)
        {
#if FIREBASE_AVAILABLE
            if (!IsSignedIn) return false;
            try
            {
                await db.Collection("gifts").Document(giftId).UpdateAsync(new Dictionary<string, object>
                {
                    { "status", "claimed" },
                    { "claimedAt", FieldValue.ServerTimestamp }
                });
                AddItemsLocally(items);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"SocialService: ClaimGift failed: {e.Message}");
                return false;
            }
#else
            return false;
#endif
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
    }
}
