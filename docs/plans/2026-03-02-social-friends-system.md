# Social Friends System Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add Firebase-backed friend codes, read-only village visiting, and seed/item gifting via the Letters panel.

**Architecture:** `SocialService` singleton owns all Firebase communication (Auth + Firestore). Cloud Functions (TypeScript) handle friend requests, gift validation, and scheduled cleanup. A separate `social.json` file caches social data locally alongside the existing `save.json`. The Letters panel becomes the social hub with Inbox, Friends, and Add Friend sub-views.

**Tech Stack:** Firebase Anonymous Auth, Cloud Firestore, Cloud Functions (TypeScript), Unity Firebase SDK (`com.google.firebase.auth` + `com.google.firebase.firestore`)

---

### Task 1: Install Firebase SDK and Configure Project

**Files:**
- Modify: `Packages/manifest.json`
- Create: `Assets/StreamingAssets/google-services.json` (Android config)
- Create: `Assets/Editor/GoogleService-Info.plist` (iOS config — can be placeholder initially)

**Step 1: Add Firebase packages to manifest.json**

Add these dependencies to `Packages/manifest.json`:
```json
"com.google.firebase.auth": "12.5.0",
"com.google.firebase.firestore": "12.5.0"
```

Note: These come from the Firebase Unity SDK. If UPM doesn't resolve them, download the SDK from https://firebase.google.com/download/unity and import the `.unitypackage` files for Auth and Firestore.

**Step 2: Create Firebase project and download config**

1. Go to Firebase Console → Create project "camp-fire"
2. Add an Android app (package name from `ProjectSettings/ProjectSettings.asset`)
3. Download `google-services.json` → place at `Assets/StreamingAssets/google-services.json`
4. Add an iOS app → download `GoogleService-Info.plist` → place at `Assets/Editor/GoogleService-Info.plist`

**Step 3: Verify compilation**

Run: Unity MCP `read_console` — check for no Firebase-related errors.

**Step 4: Commit**

```bash
git add Packages/manifest.json Assets/StreamingAssets/ Assets/Editor/
git commit -m "feat: add Firebase Auth and Firestore SDK"
```

---

### Task 2: Create SocialData and SocialSaveManager

**Files:**
- Create: `Assets/Scripts/Data/SocialData.cs`
- Create: `Assets/Scripts/Services/SocialSaveManager.cs`
- Test: `Assets/Tests/EditMode/TestSocialData.cs`

**Step 1: Write the failing test**

```csharp
using NUnit.Framework;
using Garden;

namespace Garden.Tests
{
    public class TestSocialData
    {
        [Test]
        public void NewSocialData_HasEmptyDefaults()
        {
            var data = new SocialData();
            Assert.IsNull(data.firebaseUid);
            Assert.IsNull(data.friendCode);
            Assert.AreEqual("Camper", data.displayName);
            Assert.IsNotNull(data.cachedFriends);
            Assert.AreEqual(0, data.cachedFriends.Count);
        }

        [Test]
        public void SocialData_SerializesRoundTrip()
        {
            var data = new SocialData
            {
                firebaseUid = "uid123",
                friendCode = "SPARK-7X2K",
                displayName = "My Camp"
            };
            data.cachedFriends.Add(new CachedFriend
            {
                uid = "friend1",
                displayName = "Friend Camp",
                friendCode = "BLAZE-4R1N"
            });

            string json = UnityEngine.JsonUtility.ToJson(data);
            var loaded = UnityEngine.JsonUtility.FromJson<SocialData>(json);

            Assert.AreEqual("uid123", loaded.firebaseUid);
            Assert.AreEqual("SPARK-7X2K", loaded.friendCode);
            Assert.AreEqual("My Camp", loaded.displayName);
            Assert.AreEqual(1, loaded.cachedFriends.Count);
            Assert.AreEqual("friend1", loaded.cachedFriends[0].uid);
        }
    }
}
```

**Step 2: Run test to verify it fails**

Run: Unity MCP `run_tests` with `mode: "EditMode"`
Expected: FAIL — `SocialData` and `CachedFriend` types not found.

**Step 3: Write SocialData**

Create `Assets/Scripts/Data/SocialData.cs`:
```csharp
using System;
using System.Collections.Generic;

namespace Garden
{
    [Serializable]
    public class SocialData
    {
        public string firebaseUid;
        public string friendCode;
        public string displayName = "Camper";
        public List<CachedFriend> cachedFriends = new();
        public int pendingGiftCount;
        public int pendingRequestCount;
    }

    [Serializable]
    public class CachedFriend
    {
        public string uid;
        public string displayName;
        public string friendCode;
    }
}
```

**Step 4: Run test to verify it passes**

Run: Unity MCP `run_tests` with `mode: "EditMode"`
Expected: PASS

**Step 5: Write SocialSaveManager**

Create `Assets/Scripts/Services/SocialSaveManager.cs`:
```csharp
using System;
using System.IO;
using UnityEngine;

namespace Garden
{
    public class SocialSaveManager : MonoBehaviour
    {
        public static SocialSaveManager Instance { get; private set; }

        public SocialData Data { get; private set; } = new();

        private string SavePath => Path.Combine(Application.persistentDataPath, "social.json");
        private bool _isDirty;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Load();
        }

        private void LateUpdate()
        {
            if (!_isDirty) return;
            _isDirty = false;
            Flush();
        }

        public void Save() => _isDirty = true;

        private void Flush()
        {
            var json = JsonUtility.ToJson(Data, true);
            File.WriteAllText(SavePath, json);
        }

        public void Load()
        {
            if (!File.Exists(SavePath)) { Data = new SocialData(); return; }
            try
            {
                var json = File.ReadAllText(SavePath);
                Data = JsonUtility.FromJson<SocialData>(json);
                if (Data == null) Data = new SocialData();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"SocialSaveManager: Failed to load — resetting. ({e.Message})");
                Data = new SocialData();
            }
        }

        public void DeleteSave()
        {
            _isDirty = false;
            if (File.Exists(SavePath)) File.Delete(SavePath);
            Data = new SocialData();
        }
    }
}
```

**Step 6: Commit**

```bash
git add Assets/Scripts/Data/SocialData.cs Assets/Scripts/Services/SocialSaveManager.cs Assets/Tests/EditMode/TestSocialData.cs
git commit -m "feat: add SocialData model and SocialSaveManager"
```

---

### Task 3: Create SocialService (Firebase Auth + Firestore Interface)

**Files:**
- Create: `Assets/Scripts/Services/SocialService.cs`

This is the central singleton that wraps all Firebase calls. Other code never touches Firebase directly.

**Step 1: Create SocialService**

Create `Assets/Scripts/Services/SocialService.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
#if FIREBASE_AVAILABLE
using Firebase.Auth;
using Firebase.Firestore;
using Firebase.Extensions;
#endif

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
        public string Uid => SocialSaveManager.Instance?.Data?.firebaseUid;
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
                social.firebaseUid = auth.CurrentUser.UserId;
                IsSignedIn = true;
                SocialSaveManager.Instance.Save();
                OnSignedIn?.Invoke();
                return;
            }

            try
            {
                var result = await auth.SignInAnonymouslyAsync();
                social.firebaseUid = result.User.UserId;
                IsSignedIn = true;
                SocialSaveManager.Instance.Save();
                OnSignedIn?.Invoke();

                // Fetch or create player profile (friend code assigned by Cloud Function)
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
            // If doc doesn't exist yet, the onUserCreated Cloud Function will create it
#endif
        }

        // ── Friend Requests ──

        public async Task<bool> SendFriendRequest(string targetFriendCode)
        {
#if FIREBASE_AVAILABLE
            if (!IsSignedIn || string.IsNullOrEmpty(targetFriendCode)) return false;

            try
            {
                // Look up target by friend code
                var query = await db.Collection("players")
                    .WhereEqualTo("friendCode", targetFriendCode)
                    .Limit(1)
                    .GetSnapshotAsync();

                if (query.Count == 0) return false;

                var targetDoc = query[0];
                string targetUid = targetDoc.Id;

                if (targetUid == Uid) return false; // Can't friend yourself

                // Create friend request
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
                // Cloud Function handles writing to both friend lists
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
                // Delete from both sides
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

                // Optimistic local deduction
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

                // Add items to local inventory
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
```

**Step 2: Verify compilation**

Run: Unity MCP `read_console` — check for no errors.

**Step 3: Commit**

```bash
git add Assets/Scripts/Services/SocialService.cs
git commit -m "feat: add SocialService with Firebase auth, friends, gifts, and village snapshots"
```

---

### Task 4: Create Supporting Data Types (FriendRequest, GiftMessage, GiftItem, VillageSnapshot)

**Files:**
- Create: `Assets/Scripts/Data/SocialTypes.cs`
- Test: `Assets/Tests/EditMode/TestVillageSnapshot.cs`

**Step 1: Write the failing test**

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using Garden;

namespace Garden.Tests
{
    public class TestVillageSnapshot
    {
        [Test]
        public void VillageSnapshot_FromSaveData_MapsCorrectly()
        {
            var saveData = new SaveData { flameLevel = 3 };
            saveData.plots.Add(new PlotSave
            {
                seedName = "Fern", state = PlotState.Growing, gridX = 1, gridY = 0
            });
            saveData.vases.Add(new VaseSave
            {
                currentWater = 3, capacity = 5, state = VaseState.Full, gridX = -1, gridY = 1
            });
            saveData.gardens.Add(new GardenSave
            {
                plantName = "Oak", mature = true, gridX = 0, gridY = -1
            });

            var snapshot = VillageSnapshot.FromSaveData(saveData, 3);

            Assert.AreEqual(3, snapshot.flameLevel);
            Assert.AreEqual(1, snapshot.plots.Count);
            Assert.AreEqual("Fern", snapshot.plots[0].seedName);
            Assert.AreEqual("Growing", snapshot.plots[0].state);
            Assert.AreEqual(1, snapshot.vases.Count);
            Assert.AreEqual(3, snapshot.vases[0].currentWater);
            Assert.AreEqual(1, snapshot.gardens.Count);
            Assert.AreEqual("Oak", snapshot.gardens[0].plantName);
            Assert.IsTrue(snapshot.gardens[0].mature);
        }

        [Test]
        public void VillageSnapshot_DictionaryRoundTrip()
        {
            var snapshot = new VillageSnapshot { flameLevel = 2 };
            snapshot.plots.Add(new SnapshotPlot
            {
                seedName = "Sunflower", state = "Mature", gridX = 1, gridY = 0
            });

            var dict = snapshot.ToDictionary();
            var loaded = VillageSnapshot.FromDictionary(dict);

            Assert.AreEqual(2, loaded.flameLevel);
            Assert.AreEqual(1, loaded.plots.Count);
            Assert.AreEqual("Sunflower", loaded.plots[0].seedName);
        }

        [Test]
        public void GiftItem_StoresTypeNameCount()
        {
            var item = new GiftItem { type = "seed", name = "Moonvine", count = 2 };
            Assert.AreEqual("seed", item.type);
            Assert.AreEqual("Moonvine", item.name);
            Assert.AreEqual(2, item.count);
        }
    }
}
```

**Step 2: Run test to verify it fails**

Run: Unity MCP `run_tests` with `mode: "EditMode"`
Expected: FAIL — types not found.

**Step 3: Write SocialTypes**

Create `Assets/Scripts/Data/SocialTypes.cs`:
```csharp
using System;
using System.Collections.Generic;

namespace Garden
{
    [Serializable]
    public class FriendRequest
    {
        public string id;
        public string fromUid;
        public string fromName;
        public string status; // "pending", "accepted", "declined"
    }

    [Serializable]
    public class GiftMessage
    {
        public string id;
        public string fromUid;
        public string fromName;
        public List<GiftItem> items = new();
    }

    [Serializable]
    public class GiftItem
    {
        public string type; // "seed" or "item"
        public string name;
        public int count;
    }

    // ── Village Snapshot Types ──

    [Serializable]
    public class VillageSnapshot
    {
        public int flameLevel;
        public List<SnapshotPlot> plots = new();
        public List<SnapshotVase> vases = new();
        public List<SnapshotGarden> gardens = new();

        public static VillageSnapshot FromSaveData(SaveData data, int flameLevel)
        {
            var snapshot = new VillageSnapshot { flameLevel = flameLevel };

            foreach (var p in data.plots)
            {
                snapshot.plots.Add(new SnapshotPlot
                {
                    seedName = p.seedName,
                    state = p.state.ToString(),
                    gridX = p.gridX,
                    gridY = p.gridY
                });
            }

            foreach (var v in data.vases)
            {
                snapshot.vases.Add(new SnapshotVase
                {
                    currentWater = v.currentWater,
                    capacity = v.capacity,
                    state = v.state.ToString(),
                    gridX = v.gridX,
                    gridY = v.gridY
                });
            }

            foreach (var g in data.gardens)
            {
                snapshot.gardens.Add(new SnapshotGarden
                {
                    plantName = g.plantName,
                    mature = g.mature,
                    gridX = g.gridX,
                    gridY = g.gridY
                });
            }

            return snapshot;
        }

        public Dictionary<string, object> ToDictionary()
        {
            var dict = new Dictionary<string, object>
            {
                { "flameLevel", flameLevel }
            };

            var plotList = new List<Dictionary<string, object>>();
            foreach (var p in plots)
            {
                plotList.Add(new Dictionary<string, object>
                {
                    { "seedName", p.seedName },
                    { "state", p.state },
                    { "gridX", p.gridX },
                    { "gridY", p.gridY }
                });
            }
            dict["plots"] = plotList;

            var vaseList = new List<Dictionary<string, object>>();
            foreach (var v in vases)
            {
                vaseList.Add(new Dictionary<string, object>
                {
                    { "currentWater", v.currentWater },
                    { "capacity", v.capacity },
                    { "state", v.state },
                    { "gridX", v.gridX },
                    { "gridY", v.gridY }
                });
            }
            dict["vases"] = vaseList;

            var gardenList = new List<Dictionary<string, object>>();
            foreach (var g in gardens)
            {
                gardenList.Add(new Dictionary<string, object>
                {
                    { "plantName", g.plantName },
                    { "mature", g.mature },
                    { "gridX", g.gridX },
                    { "gridY", g.gridY }
                });
            }
            dict["gardens"] = gardenList;

            return dict;
        }

        public static VillageSnapshot FromDictionary(Dictionary<string, object> dict)
        {
            var snapshot = new VillageSnapshot
            {
                flameLevel = Convert.ToInt32(dict["flameLevel"])
            };

            if (dict.TryGetValue("plots", out var plotsObj))
            {
                foreach (Dictionary<string, object> p in (List<object>)plotsObj)
                {
                    snapshot.plots.Add(new SnapshotPlot
                    {
                        seedName = p["seedName"]?.ToString(),
                        state = p["state"]?.ToString(),
                        gridX = Convert.ToInt32(p["gridX"]),
                        gridY = Convert.ToInt32(p["gridY"])
                    });
                }
            }

            if (dict.TryGetValue("vases", out var vasesObj))
            {
                foreach (Dictionary<string, object> v in (List<object>)vasesObj)
                {
                    snapshot.vases.Add(new SnapshotVase
                    {
                        currentWater = Convert.ToInt32(v["currentWater"]),
                        capacity = Convert.ToInt32(v["capacity"]),
                        state = v["state"]?.ToString(),
                        gridX = Convert.ToInt32(v["gridX"]),
                        gridY = Convert.ToInt32(v["gridY"])
                    });
                }
            }

            if (dict.TryGetValue("gardens", out var gardensObj))
            {
                foreach (Dictionary<string, object> g in (List<object>)gardensObj)
                {
                    snapshot.gardens.Add(new SnapshotGarden
                    {
                        plantName = g["plantName"]?.ToString(),
                        mature = Convert.ToBoolean(g["mature"]),
                        gridX = Convert.ToInt32(g["gridX"]),
                        gridY = Convert.ToInt32(g["gridY"])
                    });
                }
            }

            return snapshot;
        }
    }

    [Serializable]
    public class SnapshotPlot
    {
        public string seedName;
        public string state;
        public int gridX;
        public int gridY;
    }

    [Serializable]
    public class SnapshotVase
    {
        public int currentWater;
        public int capacity;
        public string state;
        public int gridX;
        public int gridY;
    }

    [Serializable]
    public class SnapshotGarden
    {
        public string plantName;
        public bool mature;
        public int gridX;
        public int gridY;
    }
}
```

**Step 4: Run test to verify it passes**

Run: Unity MCP `run_tests` with `mode: "EditMode"`
Expected: PASS

**Step 5: Commit**

```bash
git add Assets/Scripts/Data/SocialTypes.cs Assets/Tests/EditMode/TestVillageSnapshot.cs
git commit -m "feat: add social data types — FriendRequest, GiftMessage, VillageSnapshot"
```

---

### Task 5: Wire SocialService and SocialSaveManager into Scene

**Files:**
- Modify: Scene `Assets/Scenes/Garden.unity` (add components to existing managers GameObject)
- Modify: `Assets/Scripts/Managers/GameManager.cs` (trigger snapshot push on save)

**Step 1: Add SocialSaveManager and SocialService components**

Add `SocialSaveManager` and `SocialService` MonoBehaviours to the existing managers hierarchy in the scene. These go on a new child GameObject `"--- Social ---"` under the root, following the same pattern as other singletons.

Use Unity MCP:
```
manage_gameobject action=create name="--- Social ---" components_to_add=["SocialSaveManager","SocialService"]
```

**Step 2: Hook snapshot push into SaveManager.Flush**

Modify `Assets/Scripts/Services/SaveManager.cs` — after `Flush()` writes the JSON, also push the village snapshot:

Add to the end of the `Flush()` method in `SaveManager.cs`:
```csharp
private void Flush()
{
    var json = JsonUtility.ToJson(Data, true);
    File.WriteAllText(SavePath, json);

    // Push village snapshot to Firebase (fire-and-forget)
    if (SocialService.Instance != null && SocialService.Instance.IsSignedIn)
        _ = SocialService.Instance.PushVillageSnapshot();
}
```

**Step 3: Verify compilation and no errors**

Run: Unity MCP `read_console`

**Step 4: Commit**

```bash
git add Assets/Scenes/Garden.unity Assets/Scripts/Services/SaveManager.cs
git commit -m "feat: wire SocialService into scene, push village snapshot on save"
```

---

### Task 6: Create Cloud Functions Project

**Files:**
- Create: `firebase/functions/src/index.ts`
- Create: `firebase/functions/package.json`
- Create: `firebase/functions/tsconfig.json`
- Create: `firebase/firebase.json`
- Create: `firebase/firestore.rules`

**Step 1: Initialize Firebase project structure**

Create `firebase/` directory at project root:

```bash
mkdir -p firebase/functions/src
```

**Step 2: Create package.json**

Create `firebase/functions/package.json`:
```json
{
  "name": "camp-fire-functions",
  "main": "lib/index.js",
  "scripts": {
    "build": "tsc",
    "serve": "npm run build && firebase emulators:start --only functions",
    "deploy": "firebase deploy --only functions"
  },
  "engines": { "node": "20" },
  "dependencies": {
    "firebase-admin": "^12.0.0",
    "firebase-functions": "^5.0.0"
  },
  "devDependencies": {
    "typescript": "^5.4.0"
  }
}
```

**Step 3: Create tsconfig.json**

Create `firebase/functions/tsconfig.json`:
```json
{
  "compilerOptions": {
    "module": "commonjs",
    "noImplicitReturns": true,
    "noUnusedLocals": true,
    "outDir": "lib",
    "sourceMap": true,
    "strict": true,
    "target": "es2020"
  },
  "compileOnSave": true,
  "include": ["src"]
}
```

**Step 4: Create firebase.json**

Create `firebase/firebase.json`:
```json
{
  "functions": {
    "source": "functions"
  },
  "firestore": {
    "rules": "firestore.rules"
  }
}
```

**Step 5: Create Firestore security rules**

Create `firebase/firestore.rules`:
```
rules_version = '2';
service cloud.firestore {
  match /databases/{database}/documents {

    // Players can read their own profile, friends can read friend code lookups
    match /players/{userId} {
      allow read: if request.auth != null;
      allow write: if request.auth.uid == userId;
    }

    // Friend requests: sender can create, recipient can update status
    match /friendRequests/{requestId} {
      allow read: if request.auth != null &&
        (resource.data.fromUid == request.auth.uid || resource.data.toUid == request.auth.uid);
      allow create: if request.auth != null && request.resource.data.fromUid == request.auth.uid;
      allow update: if request.auth != null && resource.data.toUid == request.auth.uid;
    }

    // Friends list: only the owner reads/writes (Cloud Functions handle the friend's side)
    match /friends/{userId}/list/{friendId} {
      allow read: if request.auth.uid == userId;
      allow write: if false; // Only Cloud Functions write here
    }

    // Villages: owner writes, friends read
    match /villages/{userId} {
      allow write: if request.auth.uid == userId;
      allow read: if request.auth != null;
      // TODO: Tighten to friends-only once friend list is queryable
    }

    // Gifts: sender creates, recipient reads/updates
    match /gifts/{giftId} {
      allow read: if request.auth != null &&
        (resource.data.fromUid == request.auth.uid || resource.data.toUid == request.auth.uid);
      allow create: if request.auth != null && request.resource.data.fromUid == request.auth.uid;
      allow update: if request.auth != null && resource.data.toUid == request.auth.uid;
    }
  }
}
```

**Step 6: Create Cloud Functions**

Create `firebase/functions/src/index.ts`:
```typescript
import * as functions from "firebase-functions";
import * as admin from "firebase-admin";

admin.initializeApp();
const db = admin.firestore();

// Generate a unique friend code like "SPARK-7X2K"
function generateFriendCode(): string {
  const chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // No I,O,0,1 to avoid confusion
  const prefixes = ["SPARK", "BLAZE", "EMBER", "FLAME", "TORCH", "FLARE"];
  const prefix = prefixes[Math.floor(Math.random() * prefixes.length)];
  let suffix = "";
  for (let i = 0; i < 4; i++) {
    suffix += chars[Math.floor(Math.random() * chars.length)];
  }
  return `${prefix}-${suffix}`;
}

// On new user creation, generate friend code and create player profile
export const onUserCreated = functions.auth.user().onCreate(async (user) => {
  // Generate unique friend code (retry on collision)
  let friendCode: string;
  let attempts = 0;
  do {
    friendCode = generateFriendCode();
    const existing = await db.collection("players")
      .where("friendCode", "==", friendCode)
      .limit(1)
      .get();
    if (existing.empty) break;
    attempts++;
  } while (attempts < 10);

  await db.collection("players").doc(user.uid).set({
    friendCode,
    displayName: `Camper #${user.uid.substring(0, 4).toUpperCase()}`,
    createdAt: admin.firestore.FieldValue.serverTimestamp(),
    lastOnline: admin.firestore.FieldValue.serverTimestamp(),
  });
});

// When a friend request is accepted, add to both friend lists
export const onFriendRequestAccepted = functions.firestore
  .document("friendRequests/{requestId}")
  .onUpdate(async (change) => {
    const before = change.before.data();
    const after = change.after.data();

    if (before.status === "pending" && after.status === "accepted") {
      const fromUid = after.fromUid;
      const toUid = after.toUid;

      // Get both player profiles
      const [fromDoc, toDoc] = await Promise.all([
        db.collection("players").doc(fromUid).get(),
        db.collection("players").doc(toUid).get(),
      ]);

      const fromData = fromDoc.data();
      const toData = toDoc.data();

      // Check friend count limits (20 max)
      const [fromFriends, toFriends] = await Promise.all([
        db.collection("friends").doc(fromUid).collection("list").count().get(),
        db.collection("friends").doc(toUid).collection("list").count().get(),
      ]);

      if (fromFriends.data().count >= 20 || toFriends.data().count >= 20) {
        // Revert to pending if at limit
        await change.after.ref.update({ status: "pending" });
        return;
      }

      // Add to both friend lists
      const batch = db.batch();
      batch.set(
        db.collection("friends").doc(fromUid).collection("list").doc(toUid),
        {
          displayName: toData?.displayName || "Camper",
          friendCode: toData?.friendCode || "",
          addedAt: admin.firestore.FieldValue.serverTimestamp(),
        }
      );
      batch.set(
        db.collection("friends").doc(toUid).collection("list").doc(fromUid),
        {
          displayName: fromData?.displayName || "Camper",
          friendCode: fromData?.friendCode || "",
          addedAt: admin.firestore.FieldValue.serverTimestamp(),
        }
      );

      await batch.commit();
    }
  });

// Scheduled cleanup of expired gifts (older than 7 days)
export const cleanupExpiredGifts = functions.pubsub
  .schedule("every 24 hours")
  .onRun(async () => {
    const sevenDaysAgo = new Date();
    sevenDaysAgo.setDate(sevenDaysAgo.getDate() - 7);

    const expired = await db.collection("gifts")
      .where("status", "==", "pending")
      .where("createdAt", "<", admin.firestore.Timestamp.fromDate(sevenDaysAgo))
      .get();

    const batch = db.batch();
    expired.docs.forEach((doc) => batch.delete(doc.ref));
    await batch.commit();

    console.log(`Cleaned up ${expired.size} expired gifts`);
  });
```

**Step 7: Install dependencies and build**

```bash
cd firebase/functions && npm install && npm run build
```

**Step 8: Commit**

```bash
git add firebase/
git commit -m "feat: add Cloud Functions — friend codes, request handling, gift cleanup"
```

---

### Task 7: Rework Letters Panel UXML and USS

**Files:**
- Modify: `Assets/UI/Documents/CampFireRoot.uxml` (replace letters-panel contents)
- Modify: `Assets/UI/Styles/Letters.uss` (add styles for social UI)
- Create: `Assets/Resources/UI/Templates/FriendItem.uxml`
- Create: `Assets/Resources/UI/Templates/GiftItem.uxml`
- Create: `Assets/Resources/UI/Templates/FriendRequestItem.uxml`

**Step 1: Replace letters-panel in CampFireRoot.uxml**

Replace the `letters-panel` element (lines 74-76) in `Assets/UI/Documents/CampFireRoot.uxml` with:

```xml
<!-- Letters panel (social hub) -->
<ui:VisualElement name="letters-panel">
    <!-- Tab bar -->
    <ui:VisualElement name="letters-tabs">
        <ui:Button name="tab-inbox" class="letters-tab letters-tab--active" text="Inbox" />
        <ui:Button name="tab-friends" class="letters-tab" text="Friends" />
        <ui:Button name="tab-add" class="letters-tab" text="Add" />
    </ui:VisualElement>

    <!-- Inbox view -->
    <ui:VisualElement name="letters-inbox">
        <ui:Label name="inbox-empty" text="No new letters" class="letters-empty-text" />
        <ui:ScrollView name="inbox-list" />
    </ui:VisualElement>

    <!-- Friends list view -->
    <ui:VisualElement name="letters-friends">
        <ui:Label name="friends-empty" text="No friends yet" class="letters-empty-text" />
        <ui:ScrollView name="friends-list" />
    </ui:VisualElement>

    <!-- Add friend view -->
    <ui:VisualElement name="letters-add">
        <ui:Label name="my-friend-code" text="Your code: ---" class="friend-code-display" />
        <ui:TextField name="friend-code-input" label="Enter friend code" />
        <ui:Button name="btn-send-request" text="Send Request" class="letters-btn-primary" />
        <ui:Label name="add-friend-status" class="letters-status-text" />
    </ui:VisualElement>
</ui:VisualElement>
```

**Step 2: Create FriendItem.uxml template**

Create `Assets/Resources/UI/Templates/FriendItem.uxml`:
```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <ui:VisualElement class="friend-item">
        <ui:VisualElement class="friend-info">
            <ui:Label class="friend-name" />
            <ui:Label class="friend-code-small" />
        </ui:VisualElement>
        <ui:VisualElement class="friend-actions">
            <ui:Button class="btn-visit" text="Visit" />
            <ui:Button class="btn-send-gift" text="Gift" />
        </ui:VisualElement>
    </ui:VisualElement>
</ui:UXML>
```

**Step 3: Create GiftItem.uxml template**

Create `Assets/Resources/UI/Templates/GiftItem.uxml`:
```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <ui:VisualElement class="gift-item">
        <ui:VisualElement class="gift-info">
            <ui:Label class="gift-from" />
            <ui:Label class="gift-contents" />
        </ui:VisualElement>
        <ui:Button class="btn-claim-gift" text="Open" />
    </ui:VisualElement>
</ui:UXML>
```

**Step 4: Create FriendRequestItem.uxml template**

Create `Assets/Resources/UI/Templates/FriendRequestItem.uxml`:
```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <ui:VisualElement class="request-item">
        <ui:Label class="request-from" />
        <ui:VisualElement class="request-actions">
            <ui:Button class="btn-accept" text="Accept" />
            <ui:Button class="btn-decline" text="Decline" />
        </ui:VisualElement>
    </ui:VisualElement>
</ui:UXML>
```

**Step 5: Write Letters.uss styles**

Populate `Assets/UI/Styles/Letters.uss` with styles for tabs, friend items, gift items, request items, friend code display, input field, and status labels. Follow existing patterns from `Apotheke.uss` and `Craft.uss` for consistency (card-based list items, primary buttons, empty state text).

**Step 6: Commit**

```bash
git add Assets/UI/Documents/CampFireRoot.uxml Assets/UI/Styles/Letters.uss Assets/Resources/UI/Templates/FriendItem.uxml Assets/Resources/UI/Templates/GiftItem.uxml Assets/Resources/UI/Templates/FriendRequestItem.uxml
git commit -m "feat: Letters panel UXML with inbox, friends list, and add friend tabs"
```

---

### Task 8: Implement LettersUI Controller

**Files:**
- Modify: `Assets/Scripts/UI/LettersUI.cs` (full rewrite)

**Step 1: Implement LettersUI**

Replace the stub in `Assets/Scripts/UI/LettersUI.cs` with the full controller:

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class LettersUI : MonoBehaviour
    {
        private VisualElement inboxView;
        private VisualElement friendsView;
        private VisualElement addView;

        private Button tabInbox;
        private Button tabFriends;
        private Button tabAdd;

        private ScrollView inboxList;
        private ScrollView friendsList;
        private Label inboxEmpty;
        private Label friendsEmpty;

        private Label myFriendCode;
        private TextField friendCodeInput;
        private Button sendRequestBtn;
        private Label addFriendStatus;

        private VisualTreeAsset friendTemplate;
        private VisualTreeAsset giftTemplate;
        private VisualTreeAsset requestTemplate;

        public void Initialize(VisualElement root)
        {
            // Tab buttons
            tabInbox = root.Q<Button>("tab-inbox");
            tabFriends = root.Q<Button>("tab-friends");
            tabAdd = root.Q<Button>("tab-add");

            // Views
            inboxView = root.Q("letters-inbox");
            friendsView = root.Q("letters-friends");
            addView = root.Q("letters-add");

            // Inbox
            inboxList = root.Q<ScrollView>("inbox-list");
            inboxEmpty = root.Q<Label>("inbox-empty");

            // Friends
            friendsList = root.Q<ScrollView>("friends-list");
            friendsEmpty = root.Q<Label>("friends-empty");

            // Add friend
            myFriendCode = root.Q<Label>("my-friend-code");
            friendCodeInput = root.Q<TextField>("friend-code-input");
            sendRequestBtn = root.Q<Button>("btn-send-request");
            addFriendStatus = root.Q<Label>("add-friend-status");

            // Templates
            friendTemplate = Resources.Load<VisualTreeAsset>("UI/Templates/FriendItem");
            giftTemplate = Resources.Load<VisualTreeAsset>("UI/Templates/GiftItem");
            requestTemplate = Resources.Load<VisualTreeAsset>("UI/Templates/FriendRequestItem");

            // Wire tabs
            tabInbox?.RegisterCallback<ClickEvent>(_ => ShowTab("inbox"));
            tabFriends?.RegisterCallback<ClickEvent>(_ => ShowTab("friends"));
            tabAdd?.RegisterCallback<ClickEvent>(_ => ShowTab("add"));

            // Wire add friend button
            sendRequestBtn?.RegisterCallback<ClickEvent>(_ => OnSendFriendRequest());

            // Default to inbox
            ShowTab("inbox");

            // Subscribe to social events
            if (SocialService.Instance != null)
            {
                SocialService.Instance.OnSignedIn += OnSignedIn;
                SocialService.Instance.OnFriendListUpdated += OnFriendListUpdated;
            }
        }

        private void ShowTab(string tab)
        {
            inboxView.style.display = tab == "inbox" ? DisplayStyle.Flex : DisplayStyle.None;
            friendsView.style.display = tab == "friends" ? DisplayStyle.Flex : DisplayStyle.None;
            addView.style.display = tab == "add" ? DisplayStyle.Flex : DisplayStyle.None;

            tabInbox?.RemoveFromClassList("letters-tab--active");
            tabFriends?.RemoveFromClassList("letters-tab--active");
            tabAdd?.RemoveFromClassList("letters-tab--active");

            switch (tab)
            {
                case "inbox": tabInbox?.AddToClassList("letters-tab--active"); RefreshInbox(); break;
                case "friends": tabFriends?.AddToClassList("letters-tab--active"); RefreshFriends(); break;
                case "add": tabAdd?.AddToClassList("letters-tab--active"); RefreshAddView(); break;
            }
        }

        private void OnSignedIn()
        {
            RefreshAddView();
            RefreshInbox();
            RefreshFriends();
        }

        // ── Inbox ──

        private async void RefreshInbox()
        {
            if (SocialService.Instance == null || !SocialService.Instance.IsSignedIn)
            {
                if (inboxEmpty != null) inboxEmpty.style.display = DisplayStyle.Flex;
                return;
            }

            inboxList?.Clear();

            // Load friend requests
            var requests = await SocialService.Instance.GetPendingRequests();
            foreach (var req in requests)
            {
                if (requestTemplate == null) break;
                var el = requestTemplate.CloneTree();
                var nameLabel = el.Q<Label>(className: "request-from");
                if (nameLabel != null) nameLabel.text = $"{req.fromName} wants to be friends";

                var acceptBtn = el.Q<Button>(className: "btn-accept");
                var declineBtn = el.Q<Button>(className: "btn-decline");
                string reqId = req.id;

                acceptBtn?.RegisterCallback<ClickEvent>(async _ =>
                {
                    await SocialService.Instance.AcceptFriendRequest(reqId);
                    RefreshInbox();
                });
                declineBtn?.RegisterCallback<ClickEvent>(async _ =>
                {
                    await SocialService.Instance.DeclineFriendRequest(reqId);
                    RefreshInbox();
                });

                inboxList?.Add(el);
            }

            // Load pending gifts
            var gifts = await SocialService.Instance.GetPendingGifts();
            foreach (var gift in gifts)
            {
                if (giftTemplate == null) break;
                var el = giftTemplate.CloneTree();
                var fromLabel = el.Q<Label>(className: "gift-from");
                if (fromLabel != null) fromLabel.text = $"From {gift.fromName}";

                var contentsLabel = el.Q<Label>(className: "gift-contents");
                if (contentsLabel != null)
                {
                    var parts = new List<string>();
                    foreach (var item in gift.items)
                        parts.Add($"{item.name} x{item.count}");
                    contentsLabel.text = string.Join(", ", parts);
                }

                var claimBtn = el.Q<Button>(className: "btn-claim-gift");
                string giftId = gift.id;
                var giftItems = gift.items;

                claimBtn?.RegisterCallback<ClickEvent>(async _ =>
                {
                    await SocialService.Instance.ClaimGift(giftId, giftItems);
                    RefreshInbox();
                });

                inboxList?.Add(el);
            }

            bool isEmpty = requests.Count == 0 && gifts.Count == 0;
            if (inboxEmpty != null) inboxEmpty.style.display = isEmpty ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // ── Friends ──

        private void RefreshFriends()
        {
            friendsList?.Clear();

            var friends = SocialSaveManager.Instance?.Data?.cachedFriends;
            if (friends == null || friends.Count == 0)
            {
                if (friendsEmpty != null) friendsEmpty.style.display = DisplayStyle.Flex;
                return;
            }
            if (friendsEmpty != null) friendsEmpty.style.display = DisplayStyle.None;

            foreach (var friend in friends)
            {
                if (friendTemplate == null) break;
                var el = friendTemplate.CloneTree();

                var nameLabel = el.Q<Label>(className: "friend-name");
                if (nameLabel != null) nameLabel.text = friend.displayName;

                var codeLabel = el.Q<Label>(className: "friend-code-small");
                if (codeLabel != null) codeLabel.text = friend.friendCode;

                var visitBtn = el.Q<Button>(className: "btn-visit");
                var giftBtn = el.Q<Button>(className: "btn-send-gift");
                string friendUid = friend.uid;

                visitBtn?.RegisterCallback<ClickEvent>(_ => OnVisitFriend(friendUid));
                giftBtn?.RegisterCallback<ClickEvent>(_ => OnOpenGiftPicker(friendUid));

                friendsList?.Add(el);
            }
        }

        private void OnFriendListUpdated(List<CachedFriend> friends)
        {
            RefreshFriends();
        }

        // ── Add Friend ──

        private void RefreshAddView()
        {
            if (myFriendCode != null)
            {
                var code = SocialService.Instance?.FriendCode;
                myFriendCode.text = string.IsNullOrEmpty(code) ? "Your code: loading..." : $"Your code: {code}";
            }
            if (addFriendStatus != null) addFriendStatus.text = "";
        }

        private async void OnSendFriendRequest()
        {
            var code = friendCodeInput?.value?.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(code))
            {
                if (addFriendStatus != null) addFriendStatus.text = "Enter a friend code";
                return;
            }

            if (addFriendStatus != null) addFriendStatus.text = "Sending...";

            bool success = await SocialService.Instance.SendFriendRequest(code);
            if (addFriendStatus != null)
                addFriendStatus.text = success ? "Request sent!" : "Code not found or already friends";

            if (success && friendCodeInput != null)
                friendCodeInput.value = "";
        }

        // ── Visit & Gift (placeholder hooks) ──

        private async void OnVisitFriend(string friendUid)
        {
            var snapshot = await SocialService.Instance.FetchVillageSnapshot(friendUid);
            if (snapshot == null)
            {
                Debug.Log("LettersUI: No village snapshot available for this friend");
                return;
            }

            // Close overlay and switch campsite to read-only visit mode
            CampFireUI.Instance?.CloseOverlay();
            var campsiteView = GetComponent<CampsiteViewUI>();
            campsiteView?.EnterVisitMode(snapshot);
        }

        private void OnOpenGiftPicker(string friendUid)
        {
            // TODO: Task 10 — show gift item picker UI
            Debug.Log($"LettersUI: Gift picker for {friendUid} — not yet implemented");
        }
    }
}
```

**Step 2: Verify compilation**

Run: Unity MCP `read_console` — may show warning about `EnterVisitMode` not existing yet (addressed in Task 9).

**Step 3: Commit**

```bash
git add Assets/Scripts/UI/LettersUI.cs
git commit -m "feat: implement LettersUI with inbox, friends list, and add friend tabs"
```

---

### Task 9: Add Visit Mode to CampsiteViewUI

**Files:**
- Modify: `Assets/Scripts/UI/CampsiteViewUI.cs`

This adds a read-only "visit mode" that renders a friend's village snapshot instead of the player's own campsite.

**Step 1: Add visit mode state and methods to CampsiteViewUI**

Add to the `CampsiteMode` enum:
```csharp
private enum CampsiteMode { Normal, Placing, Watering, Visiting }
```

Add fields:
```csharp
private VillageSnapshot visitSnapshot;
private Button visitBackBtn;
```

Add `EnterVisitMode` method:
```csharp
public void EnterVisitMode(VillageSnapshot snapshot)
{
    mode = CampsiteMode.Visiting;
    visitSnapshot = snapshot;
    CloseInteractionPanel();
    RebuildGrid();
}

public void ExitVisitMode()
{
    mode = CampsiteMode.Normal;
    visitSnapshot = null;
    if (visitBackBtn != null)
    {
        visitBackBtn.RemoveFromHierarchy();
        visitBackBtn = null;
    }
    RebuildGrid();
}
```

**Step 2: Modify RebuildGrid to handle visit mode**

In `RebuildGrid()`, after `canvas.Clear()` and the cleanup block, add a branch:

```csharp
if (mode == CampsiteMode.Visiting && visitSnapshot != null)
{
    RebuildVisitGrid();
    return;
}
```

Add the `RebuildVisitGrid()` method:
```csharp
private void RebuildVisitGrid()
{
    int radius = visitSnapshot.flameLevel + 1; // Match the grid size formula from FlameConfig
    // Use same hex layout logic as RebuildGrid but read from visitSnapshot instead of SaveManager

    int rExtent = radius + ExtraRows;
    float minX = float.MaxValue, maxX = float.MinValue;
    float minY = float.MaxValue, maxY = float.MinValue;
    for (int q = -radius; q <= radius; q++)
    {
        int rMin = Mathf.Max(-rExtent, -q - rExtent);
        int rMax = Mathf.Min(rExtent, -q + rExtent);
        for (int r = rMin; r <= rMax; r++)
        {
            var center = HexGridUtil.HexToPixel(q, r, HexSize);
            if (center.x < minX) minX = center.x;
            if (center.x > maxX) maxX = center.x;
            if (center.y < minY) minY = center.y;
            if (center.y > maxY) maxY = center.y;
        }
    }

    float canvasWidth = (maxX - minX) + CellWidth + GridPadding * 2;
    float canvasHeight = (maxY - minY) + CellHeight + GridPadding * 2;
    canvas.style.width = canvasWidth;
    canvas.style.height = canvasHeight;

    float offsetX = -minX + GridPadding + CellWidth / 2f;
    float offsetY = -minY + GridPadding + CellHeight / 2f;

    // Build occupied lookup from snapshot
    var occupied = new Dictionary<(int, int), (CampBuildingType type, int index)>();
    occupied[(0, 0)] = (CampBuildingType.Flame, 0);
    for (int i = 0; i < visitSnapshot.plots.Count; i++)
        occupied[(visitSnapshot.plots[i].gridX, visitSnapshot.plots[i].gridY)] = (CampBuildingType.Plot, i);
    for (int i = 0; i < visitSnapshot.vases.Count; i++)
        occupied[(visitSnapshot.vases[i].gridX, visitSnapshot.vases[i].gridY)] = (CampBuildingType.Vase, i);
    for (int i = 0; i < visitSnapshot.gardens.Count; i++)
        occupied[(visitSnapshot.gardens[i].gridX, visitSnapshot.gardens[i].gridY)] = (CampBuildingType.Garden, i);

    for (int q = -radius; q <= radius; q++)
    {
        int rMin = Mathf.Max(-rExtent, -q - rExtent);
        int rMax = Mathf.Min(rExtent, -q + rExtent);
        for (int r = rMin; r <= rMax; r++)
        {
            var el = cellTemplate.CloneTree();
            var cell = el.Q(className: "grid-cell");
            if (cell == null) continue;

            var center = HexGridUtil.HexToPixel(q, r, HexSize);
            float x = center.x + offsetX - CellWidth / 2f;
            float y = center.y + offsetY - CellHeight / 2f;
            cell.style.left = x;
            cell.style.top = y;

            var label = cell.Q<Label>(className: "cell-label");
            var status = cell.Q<Label>(className: "cell-status");

            if (occupied.TryGetValue((q, r), out var info))
            {
                PopulateVisitCell(cell, label, status, info.type, info.index);
            }
            else
            {
                cell.AddToClassList("grid-cell--empty");
                if (label != null) label.text = "";
                if (status != null) status.text = "";
            }

            cell.generateVisualContent += DrawHexCell;
            cell.RegisterCallback<CustomStyleResolvedEvent>(_ => cell.MarkDirtyRepaint());
            canvas.Add(cell);
        }
    }

    // "Back to my camp" button
    visitBackBtn = new Button(ExitVisitMode) { text = "Back to My Camp" };
    visitBackBtn.name = "visit-back-btn";
    viewport.Add(visitBackBtn);

    var flameCenter = HexGridUtil.HexToPixel(0, 0, HexSize);
    panController.CenterOnPoint(flameCenter.x + offsetX, flameCenter.y + offsetY, canvasWidth, canvasHeight);
}

private void PopulateVisitCell(VisualElement cell, Label label, Label status,
    CampBuildingType type, int index)
{
    switch (type)
    {
        case CampBuildingType.Flame:
            cell.AddToClassList("grid-cell--flame");
            if (label != null) label.text = $"Lv.{visitSnapshot.flameLevel}";
            if (status != null) status.text = "Spark of Ara";
            break;
        case CampBuildingType.Plot:
            cell.AddToClassList("grid-cell--plot");
            var plot = visitSnapshot.plots[index];
            if (label != null) label.text = string.IsNullOrEmpty(plot.seedName) ? "Plot" : plot.seedName;
            if (status != null) status.text = plot.state;
            break;
        case CampBuildingType.Vase:
            cell.AddToClassList("grid-cell--vase");
            var vase = visitSnapshot.vases[index];
            if (label != null) label.text = $"{vase.currentWater}/{vase.capacity}";
            if (status != null) status.text = vase.state;
            break;
        case CampBuildingType.Garden:
            cell.AddToClassList("grid-cell--garden");
            var garden = visitSnapshot.gardens[index];
            if (label != null) label.text = garden.plantName ?? "Garden";
            if (status != null) status.text = garden.mature ? "Mature" : "Growing";
            break;
    }
}
```

**Step 3: Verify compilation**

Run: Unity MCP `read_console`

**Step 4: Commit**

```bash
git add Assets/Scripts/UI/CampsiteViewUI.cs
git commit -m "feat: add read-only visit mode to CampsiteViewUI for viewing friend villages"
```

---

### Task 10: Implement Gift Picker UI

**Files:**
- Create: `Assets/Resources/UI/Templates/GiftPickerItem.uxml`
- Modify: `Assets/Scripts/UI/LettersUI.cs` (add gift picker logic)
- Modify: `Assets/UI/Documents/CampFireRoot.uxml` (add gift picker overlay within letters panel)

**Step 1: Create GiftPickerItem.uxml**

Create `Assets/Resources/UI/Templates/GiftPickerItem.uxml`:
```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <ui:VisualElement class="gift-picker-item">
        <ui:Label class="picker-item-name" />
        <ui:Label class="picker-item-count" />
        <ui:Button class="btn-add-to-gift" text="+" />
    </ui:VisualElement>
</ui:UXML>
```

**Step 2: Add gift picker elements to letters-panel in CampFireRoot.uxml**

Inside `letters-panel`, after `letters-add`, add:
```xml
<!-- Gift picker (shown when sending a gift) -->
<ui:VisualElement name="letters-gift-picker" style="display: none;">
    <ui:Label name="gift-picker-title" text="Send Gift" class="letters-section-title" />
    <ui:Label name="gift-picker-to" text="To: ---" />
    <ui:ScrollView name="gift-picker-inventory" />
    <ui:VisualElement name="gift-picker-selected">
        <ui:Label name="gift-selected-label" text="Selected: none" />
    </ui:VisualElement>
    <ui:VisualElement name="gift-picker-actions">
        <ui:Button name="btn-confirm-gift" text="Send" class="letters-btn-primary" />
        <ui:Button name="btn-cancel-gift" text="Cancel" />
    </ui:VisualElement>
</ui:VisualElement>
```

**Step 3: Add gift picker logic to LettersUI**

Add fields and methods to `LettersUI.cs` for:
- Caching gift picker elements (`giftPickerView`, `giftPickerInventory`, `giftSelectedLabel`, etc.)
- `OnOpenGiftPicker(friendUid)` — populates inventory list with seeds and items, each with "+" button
- Selected items tracked in `List<GiftItem> selectedGiftItems` (max 3)
- `OnConfirmGift()` — calls `SocialService.Instance.SendGift(targetUid, selectedGiftItems)`, shows result, returns to friends tab
- `OnCancelGift()` — hides gift picker, shows friends tab

The gift picker shows all seeds and items from `SaveManager.Instance.Data.seedInventory` and `SaveManager.Instance.Data.items` with counts. Tapping "+" adds one to the gift (up to the player's available count, max 3 items total).

**Step 4: Verify compilation**

Run: Unity MCP `read_console`

**Step 5: Commit**

```bash
git add Assets/Resources/UI/Templates/GiftPickerItem.uxml Assets/Scripts/UI/LettersUI.cs Assets/UI/Documents/CampFireRoot.uxml
git commit -m "feat: add gift picker UI for selecting seeds/items to send to friends"
```

---

### Task 11: Integration Testing and Polish

**Files:**
- Modify: `Assets/Tests/EditMode/TestSocialData.cs` (add edge case tests)
- Modify: `Assets/Tests/EditMode/TestVillageSnapshot.cs` (add edge case tests)

**Step 1: Add edge case tests**

Add to `TestSocialData.cs`:
```csharp
[Test]
public void SocialData_EmptyFriendList_SerializesCleanly()
{
    var data = new SocialData { firebaseUid = "test" };
    string json = JsonUtility.ToJson(data);
    var loaded = JsonUtility.FromJson<SocialData>(json);
    Assert.IsNotNull(loaded.cachedFriends);
    Assert.AreEqual(0, loaded.cachedFriends.Count);
}
```

Add to `TestVillageSnapshot.cs`:
```csharp
[Test]
public void VillageSnapshot_EmptySaveData_ProducesEmptySnapshot()
{
    var saveData = new SaveData();
    var snapshot = VillageSnapshot.FromSaveData(saveData, 1);
    Assert.AreEqual(1, snapshot.flameLevel);
    Assert.AreEqual(0, snapshot.plots.Count);
    Assert.AreEqual(0, snapshot.vases.Count);
    Assert.AreEqual(0, snapshot.gardens.Count);
}

[Test]
public void GiftItem_SeedAndItemTypes()
{
    var seed = new GiftItem { type = "seed", name = "Fern", count = 3 };
    var item = new GiftItem { type = "item", name = "Fertilizer", count = 1 };
    Assert.AreEqual("seed", seed.type);
    Assert.AreEqual("item", item.type);
}
```

**Step 2: Run all tests**

Run: Unity MCP `run_tests` with `mode: "EditMode"`
Expected: All tests PASS

**Step 3: Commit**

```bash
git add Assets/Tests/EditMode/
git commit -m "test: add edge case tests for social data types and village snapshots"
```

---

### Task 12: Deploy Cloud Functions and Final Verification

**Step 1: Deploy Firestore rules**

```bash
cd firebase && firebase deploy --only firestore:rules
```

**Step 2: Deploy Cloud Functions**

```bash
cd firebase && firebase deploy --only functions
```

**Step 3: Test end-to-end in Unity Editor**

1. Enter Play mode
2. Open Letters panel → Add tab → verify friend code displays
3. Check Firebase Console → `players` collection has a document with the friend code

**Step 4: Final commit**

```bash
git add -A
git commit -m "feat: complete social friends system — friends, visits, gifts via Letters"
```
