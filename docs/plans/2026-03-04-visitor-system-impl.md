# Visitor System Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Replace VisitorSystem + MerchantManager with a unified, server-driven VisitorManager supporting three visitor types (Merchant, Gifter, Quester).

**Architecture:** Server decides which visitor appears each night via priority-based rules (scheduled events > milestones > weather > quest returns > random pool). Client calls `GET /visitors/tonight`, spawns visitor on hex grid, handles type-specific interactions (trade/gift/quest). Server is authoritative for quest state.

**Tech Stack:** Node.js/Express + PostgreSQL (server), Unity C# with UI Toolkit (client)

**Design doc:** `docs/plans/2026-03-04-visitor-system-redesign.md`

---

## Phase 1: Server — Database & API

### Task 1: Add visitor tables to migration

**Files:**
- Modify: `server/src/db/migrate.js` (append new tables after existing `CREATE INDEX idx_gifts_to`)

**Step 1: Add migration SQL**

Add these tables after the existing schema in `server/src/db/migrate.js`:

```sql
CREATE TABLE IF NOT EXISTS visitor_templates (
  id SERIAL PRIMARY KEY,
  visitor_id TEXT UNIQUE NOT NULL,
  name TEXT NOT NULL,
  portrait_id TEXT,
  type TEXT NOT NULL CHECK (type IN ('merchant', 'gifter', 'quester')),
  flame_level_min INTEGER NOT NULL DEFAULT 1,
  dialogue_pool JSONB NOT NULL DEFAULT '[]',
  offer_pool JSONB NOT NULL DEFAULT '[]',
  gift_pool JSONB NOT NULL DEFAULT '[]',
  quest_pool JSONB NOT NULL DEFAULT '[]',
  weight REAL NOT NULL DEFAULT 1.0
);

CREATE TABLE IF NOT EXISTS visitor_schedule (
  id SERIAL PRIMARY KEY,
  visitor_id TEXT NOT NULL REFERENCES visitor_templates(visitor_id),
  date DATE,
  visit_number INTEGER,
  weather_condition TEXT,
  priority INTEGER NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS idx_visitor_schedule_date ON visitor_schedule(date);
CREATE INDEX IF NOT EXISTS idx_visitor_schedule_visit ON visitor_schedule(visit_number);

CREATE TABLE IF NOT EXISTS visitor_quests (
  id SERIAL PRIMARY KEY,
  player_uid TEXT NOT NULL REFERENCES players(uid),
  visitor_id TEXT NOT NULL,
  request_item TEXT NOT NULL,
  request_count INTEGER NOT NULL,
  return_date_utc DATE NOT NULL,
  reward JSONB NOT NULL DEFAULT '{}',
  return_dialogue JSONB NOT NULL DEFAULT '[]',
  created_at TIMESTAMPTZ DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS idx_visitor_quests_player ON visitor_quests(player_uid);
CREATE INDEX IF NOT EXISTS idx_visitor_quests_return ON visitor_quests(return_date_utc);

CREATE TABLE IF NOT EXISTS player_visit_counts (
  player_uid TEXT UNIQUE NOT NULL REFERENCES players(uid),
  count INTEGER NOT NULL DEFAULT 0,
  last_visit_date DATE
);
```

**Step 2: Run migration**

Run: `cd server && npm run db:migrate`
Expected: Tables created successfully.

**Step 3: Commit**

```
git add server/src/db/migrate.js
git commit -m "feat(server): add visitor system database tables"
```

---

### Task 2: Create visitor routes — `GET /visitors/tonight`

**Files:**
- Create: `server/src/routes/visitors.js`
- Modify: `server/src/index.js` (add route mounting, ~line 37)

**Step 1: Create `server/src/routes/visitors.js`**

Follow the pattern from `server/src/routes/gifts.js`. The route needs:

```javascript
const { Router } = require('express');
const pool = require('../db/pool');
const router = Router();

// GET /visitors/tonight
// Priority: scheduled date > visit_number milestone > quest return > random pool
router.get('/tonight', async (req, res) => {
  try {
    const { uid } = req.user;
    const today = new Date().toISOString().split('T')[0]; // YYYY-MM-DD

    // 1. Increment visit count (deduplicated by date)
    const visitResult = await pool.query(
      `INSERT INTO player_visit_counts (player_uid, count, last_visit_date)
       VALUES ($1, 1, $2)
       ON CONFLICT (player_uid) DO UPDATE
       SET count = CASE
         WHEN player_visit_counts.last_visit_date = $2 THEN player_visit_counts.count
         ELSE player_visit_counts.count + 1
       END,
       last_visit_date = $2
       RETURNING count`,
      [uid, today]
    );
    const visitCount = visitResult.rows[0].count;

    // 2. Check scheduled date visitors (highest priority)
    const scheduled = await pool.query(
      `SELECT vt.* FROM visitor_schedule vs
       JOIN visitor_templates vt ON vt.visitor_id = vs.visitor_id
       WHERE vs.date = $1
       ORDER BY vs.priority DESC LIMIT 1`,
      [today]
    );
    if (scheduled.rows.length > 0) {
      return res.json(buildVisitorPayload(scheduled.rows[0], visitCount));
    }

    // 3. Check visit_number milestones
    const milestone = await pool.query(
      `SELECT vt.* FROM visitor_schedule vs
       JOIN visitor_templates vt ON vt.visitor_id = vs.visitor_id
       WHERE vs.visit_number = $1
       ORDER BY vs.priority DESC LIMIT 1`,
      [visitCount]
    );
    if (milestone.rows.length > 0) {
      return res.json(buildVisitorPayload(milestone.rows[0], visitCount));
    }

    // 4. Check quest returns
    const questReturn = await pool.query(
      `SELECT * FROM visitor_quests
       WHERE player_uid = $1 AND return_date_utc <= $2
       ORDER BY return_date_utc ASC LIMIT 1`,
      [uid, today]
    );
    if (questReturn.rows.length > 0) {
      const quest = questReturn.rows[0];
      // Look up visitor template for portrait/name
      const template = await pool.query(
        `SELECT * FROM visitor_templates WHERE visitor_id = $1`,
        [quest.visitor_id]
      );
      const vt = template.rows[0] || {};
      return res.json({
        visitor_type: 'quester',
        visitor_id: quest.visitor_id,
        name: vt.name || quest.visitor_id,
        portrait_id: vt.portrait_id || null,
        dialogue: quest.return_dialogue || [],
        quest: {
          quest_id: quest.id,
          request_item: quest.request_item,
          request_count: quest.request_count,
          reward: quest.reward,
          is_return: true
        }
      });
    }

    // 5. Get player flame level from village snapshot for level gating
    const villageResult = await pool.query(
      `SELECT snapshot FROM villages WHERE player_uid = $1`, [uid]
    );
    const flameLevel = villageResult.rows[0]?.snapshot?.flameLevel || 1;

    // 6. Random pool (weighted, flame-level-gated)
    const pool_result = await pool.query(
      `SELECT * FROM visitor_templates WHERE flame_level_min <= $1`,
      [flameLevel]
    );
    if (pool_result.rows.length === 0) {
      return res.json({ visitor: null });
    }

    const picked = weightedRandom(pool_result.rows);
    return res.json(buildVisitorPayload(picked, visitCount));

  } catch (err) {
    console.error('GET /visitors/tonight error:', err);
    res.status(500).json({ error: 'Internal server error' });
  }
});

function weightedRandom(templates) {
  const totalWeight = templates.reduce((sum, t) => sum + t.weight, 0);
  let roll = Math.random() * totalWeight;
  for (const t of templates) {
    roll -= t.weight;
    if (roll <= 0) return t;
  }
  return templates[templates.length - 1];
}

function buildVisitorPayload(template, visitCount) {
  const type = template.type;
  const dialogue = rollDialogue(template.dialogue_pool);

  const payload = {
    visitor_type: type,
    visitor_id: template.visitor_id,
    name: template.name,
    portrait_id: template.portrait_id,
    dialogue
  };

  if (type === 'merchant') {
    payload.offers = rollOffers(template.offer_pool);
  } else if (type === 'gifter') {
    payload.gift = rollGift(template.gift_pool);
  } else if (type === 'quester') {
    payload.quest = rollQuest(template.quest_pool);
  }

  return payload;
}

function rollDialogue(pool) {
  if (!pool || pool.length === 0) return [];
  return pool[Math.floor(Math.random() * pool.length)];
}

function rollOffers(pool, count = 3) {
  if (!pool || pool.length === 0) return [];
  const shuffled = [...pool].sort(() => Math.random() - 0.5);
  return shuffled.slice(0, Math.min(count, shuffled.length));
}

function rollGift(pool) {
  if (!pool || pool.length === 0) return { type: 'seed', name: 'Chamomile', amount: 1 };
  return pool[Math.floor(Math.random() * pool.length)];
}

function rollQuest(pool) {
  if (!pool || pool.length === 0) return null;
  const quest = pool[Math.floor(Math.random() * pool.length)];
  return { ...quest, is_return: false };
}

module.exports = router;
```

**Step 2: Mount routes in `server/src/index.js`**

After line 37 (`app.use('/gifts', authMiddleware, require('./routes/gifts'));`), add:
```javascript
app.use('/visitors', authMiddleware, require('./routes/visitors'));
```

**Step 3: Commit**

```
git commit -m "feat(server): add GET /visitors/tonight with priority-based selection"
```

---

### Task 3: Add quest accept and complete endpoints

**Files:**
- Modify: `server/src/routes/visitors.js` (append routes)

**Step 1: Add `POST /visitors/quest/accept`**

```javascript
router.post('/quest/accept', async (req, res) => {
  try {
    const { uid } = req.user;
    const { visitor_id, request_item, request_count, return_days, reward, return_dialogue } = req.body;

    if (!visitor_id || !request_item || !request_count || !return_days) {
      return res.status(400).json({ error: 'Missing required quest fields' });
    }

    const returnDate = new Date();
    returnDate.setDate(returnDate.getDate() + return_days);
    const returnDateStr = returnDate.toISOString().split('T')[0];

    const result = await pool.query(
      `INSERT INTO visitor_quests (player_uid, visitor_id, request_item, request_count, return_date_utc, reward, return_dialogue)
       VALUES ($1, $2, $3, $4, $5, $6, $7)
       RETURNING id, return_date_utc`,
      [uid, visitor_id, request_item, request_count, returnDateStr,
       JSON.stringify(reward || {}), JSON.stringify(return_dialogue || [])]
    );

    res.status(201).json({
      quest_id: result.rows[0].id,
      return_date: result.rows[0].return_date_utc
    });
  } catch (err) {
    console.error('POST /visitors/quest/accept error:', err);
    res.status(500).json({ error: 'Internal server error' });
  }
});
```

**Step 2: Add `POST /visitors/quest/complete`**

```javascript
router.post('/quest/complete', async (req, res) => {
  try {
    const { uid } = req.user;
    const { quest_id } = req.body;

    if (!quest_id) {
      return res.status(400).json({ error: 'Missing quest_id' });
    }

    const result = await pool.query(
      `DELETE FROM visitor_quests
       WHERE id = $1 AND player_uid = $2
       RETURNING reward`,
      [quest_id, uid]
    );

    if (result.rows.length === 0) {
      return res.status(404).json({ error: 'Quest not found' });
    }

    res.json({ reward: result.rows[0].reward });
  } catch (err) {
    console.error('POST /visitors/quest/complete error:', err);
    res.status(500).json({ error: 'Internal server error' });
  }
});
```

**Step 3: Commit**

```
git commit -m "feat(server): add quest accept and complete endpoints"
```

---

### Task 4: Seed initial visitor templates

**Files:**
- Create: `server/src/db/seed-visitors.js`

**Step 1: Create seed script**

Create a script that inserts starter visitor templates into the DB. Include at least one of each type (merchant, gifter, quester). Use the existing merchant data as reference for offer structure.

Merchant offer_pool format:
```json
[
  { "costs": [{"itemName": "Basil Leaf", "count": 2}], "rewardSeedName": "Lavender", "rewardCount": 1, "weight": 1.0 }
]
```

Gifter gift_pool format:
```json
[
  { "type": "seed", "name": "Chamomile", "amount": 2 },
  { "type": "water", "amount": 3 },
  { "type": "item", "name": "Basil Leaf", "amount": 1 }
]
```

Quester quest_pool format:
```json
[
  { "request_item": "Lavender Petal", "request_count": 3, "return_days": 7, "reward": {"type": "seed", "name": "Moonflower", "count": 2}, "return_dialogue": ["You found them!", "Here, take these rare seeds."] }
]
```

Also seed a `visitor_schedule` entry for visit #1 (a specific gifter for the first night).

**Step 2: Add npm script**

In `server/package.json`, add: `"db:seed-visitors": "node src/db/seed-visitors.js"`

**Step 3: Run seed**

Run: `cd server && npm run db:seed-visitors`

**Step 4: Commit**

```
git commit -m "feat(server): add visitor template seed data"
```

---

## Phase 2: Client — Data Types

### Task 5: Create VisitorSave and update SaveData

**Files:**
- Create: `Assets/Scripts/Data/VisitorSave.cs`
- Modify: `Assets/Scripts/Data/SaveData.cs` (lines 18, 27-29)
- Modify: `Assets/Scripts/Data/GameEnums.cs` (line 21)

**Step 1: Create `Assets/Scripts/Data/VisitorSave.cs`**

```csharp
using System;
using System.Collections.Generic;

namespace Garden
{
    public enum VisitorType { Merchant, Gifter, Quester }

    [Serializable]
    public class VisitorSave
    {
        public int gridX;
        public int gridY;
        public string visitorId;
        public string visitorName;
        public string portraitId;
        public VisitorType type;
        public List<string> dialogueLines = new();
        public bool dialogueSeen;
        public string appearedAtUtc;
        public string fetchedDateUtc; // prevents re-fetching same night

        // Merchant
        public List<MerchantOfferSave> offers = new();

        // Gifter
        public string giftType; // "seed", "water", "item"
        public string giftName;
        public int giftAmount;
        public bool giftClaimed;

        // Quester
        public int serverQuestId;
        public string requestItem;
        public int requestCount;
        public string returnDateUtc;
        public string rewardJson; // serialized reward from server
        public List<string> returnDialogue = new();
        public bool isReturnVisit;
        public bool questFulfilled;
    }

    [Serializable]
    public class ActiveVisitorQuest
    {
        public int serverQuestId;
        public string visitorId;
        public string visitorName;
        public string portraitId;
        public string requestItem;
        public int requestCount;
        public string returnDateUtc;
        public string rewardJson;
        public List<string> returnDialogue = new();
    }
}
```

**Step 2: Update `SaveData.cs`**

Replace lines 18, 27-29:
```csharp
// Remove:
public string lastVisitorDateUtc;
public List<MerchantSave> merchants = new();
public string lastMerchantDateUtc;
public List<int> seenMerchantDialogues = new();

// Add:
public VisitorSave currentVisitor;
public List<ActiveVisitorQuest> activeQuests = new();
```

**Step 3: Update `GameEnums.cs` line 21**

Replace `NightMerchant` with `Visitor`:
```csharp
public enum CampBuildingType { None, Flame, Plot, Vase, Garden, Apotheke, MallumHouse, Bird, Visitor }
```

**Step 4: Compile and check console**

Run Unity compile via MCP `refresh_unity`. Check `read_console` for errors. Fix any references to the removed fields (there will be many — these are addressed in subsequent tasks).

**Step 5: Commit**

```
git commit -m "feat: add VisitorSave data types, update SaveData and CampBuildingType"
```

---

### Task 6: Delete old visitor/merchant data files

**Files:**
- Delete: `Assets/Scripts/Data/MerchantData.cs`
- Delete: `Assets/Scripts/Data/MerchantSave.cs`
- Keep: `TradeCost` class — move it out of `MerchantData.cs` into its own file or into `VisitorSave.cs` before deleting

**Step 1: Move `TradeCost` to `VisitorSave.cs`**

`TradeCost` is used by `MerchantOfferSave` which is still needed. Add to `VisitorSave.cs`:
```csharp
[Serializable]
public class TradeCost
{
    public string itemName;
    public int count;
}

[Serializable]
public class MerchantOfferSave
{
    public List<TradeCost> costs = new();
    public string rewardSeedName;
    public int rewardCount;
}
```

**Step 2: Delete `MerchantData.cs` and `MerchantSave.cs`**

Also delete any `.asset` files in `Assets/Resources/Merchants/` (the ScriptableObject instances).

**Step 3: Commit**

```
git commit -m "refactor: remove MerchantData/MerchantSave, consolidate into VisitorSave"
```

---

## Phase 3: Client — VisitorManager

### Task 7: Create VisitorManager

**Files:**
- Create: `Assets/Scripts/Managers/VisitorManager.cs`
- Delete: `Assets/Scripts/Managers/VisitorSystem.cs`
- Delete: `Assets/Scripts/Managers/MerchantManager.cs`

**Step 1: Create `Assets/Scripts/Managers/VisitorManager.cs`**

This is the core manager. It unifies VisitorSystem + MerchantManager. Key responsibilities:

```csharp
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Garden
{
    public class VisitorManager : MonoBehaviour
    {
        public static VisitorManager Instance { get; private set; }

        public event Action OnVisitorArrived;
        public event Action OnVisitorDeparted;

        private bool fetchInProgress;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            // Clean stale visitor on load (if night passed)
            var data = SaveManager.Instance?.Data;
            if (data?.currentVisitor != null)
            {
                if (!IsVisitorHour(GameTime.Now))
                {
                    DismissVisitor(data);
                    SaveManager.Instance.Save();
                }
            }
            // Clean expired quests
            CleanExpiredQuests(data, GameTime.UtcNow);
        }

        private void Update()
        {
            var data = SaveManager.Instance?.Data;
            if (data == null) return;

            var now = GameTime.Now;

            // Departure
            if (data.currentVisitor != null && !IsVisitorHour(now))
            {
                DismissVisitor(data);
                SaveManager.Instance.Save();
                OnVisitorDeparted?.Invoke();
                return;
            }

            // Arrival
            if (IsVisitorHour(now) && data.currentVisitor == null && !fetchInProgress)
            {
                string todayUtc = GameTime.UtcNow.Date.ToString("o");
                if (data.currentVisitor == null)
                {
                    // Check if already fetched today (visitor may have been dismissed after interaction)
                    // Use a separate field or rely on server dedup
                    FetchTonightVisitor(data, todayUtc);
                }
            }
        }

        public static bool IsVisitorHour(DateTime localTime)
        {
            return localTime.Hour >= 22;
        }

        // --- Server Communication ---

        private async void FetchTonightVisitor(SaveData data, string todayUtc)
        {
            fetchInProgress = true;
            try
            {
                var service = SocialService.Instance;
                if (service == null || string.IsNullOrEmpty(service.AuthToken)) return;

                var url = service.ServerBaseUrl + "/visitors/tonight";
                var request = UnityWebRequest.Get(url);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Authorization", $"Bearer {service.AuthToken}");

                var op = request.SendWebRequest();
                while (!op.isDone) await System.Threading.Tasks.Task.Yield();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"Failed to fetch visitor: {request.error}");
                    return;
                }

                var json = request.downloadHandler.text;
                var response = JsonUtility.FromJson<VisitorResponse>(json);
                if (string.IsNullOrEmpty(response.visitor_id)) return;

                int gridRadius = FlameManager.Instance != null
                    ? FlameManager.Instance.Config.GetGridSize(data.flameLevel)
                    : 2;
                var freeTiles = BirdManager.GetFreeTiles(data, gridRadius);
                if (freeTiles.Count == 0) return;

                var tile = freeTiles[UnityEngine.Random.Range(0, freeTiles.Count)];

                data.currentVisitor = BuildVisitorSave(response, tile.q, tile.r, todayUtc);
                SaveManager.Instance.Save();
                OnVisitorArrived?.Invoke();
            }
            finally
            {
                fetchInProgress = false;
            }
        }

        public static VisitorSave BuildVisitorSave(VisitorResponse response, int gridX, int gridY, string dateUtc)
        {
            var save = new VisitorSave
            {
                gridX = gridX,
                gridY = gridY,
                visitorId = response.visitor_id,
                visitorName = response.name,
                portraitId = response.portrait_id,
                dialogueLines = response.dialogue ?? new List<string>(),
                appearedAtUtc = GameTime.UtcNow.ToString("o"),
                fetchedDateUtc = dateUtc
            };

            switch (response.visitor_type)
            {
                case "merchant":
                    save.type = VisitorType.Merchant;
                    if (response.offers != null)
                    {
                        foreach (var o in response.offers)
                        {
                            save.offers.Add(new MerchantOfferSave
                            {
                                rewardSeedName = o.rewardSeedName,
                                rewardCount = o.rewardCount,
                                costs = o.costs ?? new List<TradeCost>()
                            });
                        }
                    }
                    break;
                case "gifter":
                    save.type = VisitorType.Gifter;
                    if (response.gift != null)
                    {
                        save.giftType = response.gift.type;
                        save.giftName = response.gift.name;
                        save.giftAmount = response.gift.amount;
                    }
                    break;
                case "quester":
                    save.type = VisitorType.Quester;
                    if (response.quest != null)
                    {
                        save.requestItem = response.quest.request_item;
                        save.requestCount = response.quest.request_count;
                        save.rewardJson = JsonUtility.ToJson(response.quest.reward);
                        save.returnDialogue = response.quest.return_dialogue ?? new List<string>();
                        save.isReturnVisit = response.quest.is_return;
                        if (response.quest.quest_id > 0)
                            save.serverQuestId = response.quest.quest_id;
                    }
                    break;
            }

            return save;
        }

        public static void DismissVisitor(SaveData data)
        {
            data.currentVisitor = null;
        }

        public static void CleanExpiredQuests(SaveData data, DateTime utcNow)
        {
            if (data?.activeQuests == null) return;
            // Quests expire 1 day after return date (grace for the return night)
            data.activeQuests.RemoveAll(q =>
            {
                if (string.IsNullOrEmpty(q.returnDateUtc)) return true;
                var returnDate = DateTime.Parse(q.returnDateUtc, null,
                    System.Globalization.DateTimeStyles.RoundtripKind);
                return utcNow.Date > returnDate.AddDays(1);
            });
        }

        // --- Gift Application ---

        public static void ApplyGift(VisitorSave visitor, SaveData data)
        {
            if (visitor.giftClaimed) return;
            visitor.giftClaimed = true;

            switch (visitor.giftType)
            {
                case "water":
                    foreach (var vase in data.vases)
                    {
                        int space = vase.capacity - vase.currentWater;
                        if (space > 0)
                        {
                            int fill = Math.Min(space, visitor.giftAmount);
                            vase.currentWater += fill;
                            visitor.giftAmount -= fill;
                            if (visitor.giftAmount <= 0) break;
                        }
                    }
                    break;
                case "seed":
                    ApothekeManager.Instance?.AddSeed(visitor.giftName, visitor.giftAmount);
                    break;
                case "item":
                    var entry = data.items.Find(i => i.itemName == visitor.giftName);
                    if (entry != null)
                        entry.count += visitor.giftAmount;
                    else
                        data.items.Add(new InventoryItem { itemName = visitor.giftName, count = visitor.giftAmount });
                    break;
            }
        }

        // --- Trade (reused from MerchantManager) ---

        public static bool CanAffordOffer(MerchantOfferSave offer, List<InventoryItem> items)
        {
            if (CurrencyManager.FreeMode) return true;
            foreach (var cost in offer.costs)
            {
                var item = items.Find(i => i.itemName == cost.itemName);
                if (item == null || item.count < cost.count) return false;
            }
            return true;
        }

        public static void ExecuteTrade(MerchantOfferSave offer, List<InventoryItem> items,
            List<SeedInventoryEntry> seedInventory)
        {
            if (!CurrencyManager.FreeMode)
            {
                foreach (var cost in offer.costs)
                {
                    var item = items.Find(i => i.itemName == cost.itemName);
                    item.count -= cost.count;
                    if (item.count <= 0) items.Remove(item);
                }
            }

            var seedEntry = seedInventory.Find(s => s.seedName == offer.rewardSeedName);
            if (seedEntry != null)
                seedEntry.count += offer.rewardCount;
            else
                seedInventory.Add(new SeedInventoryEntry
                    { seedName = offer.rewardSeedName, count = offer.rewardCount });
        }

        // --- Quest Accept/Complete (server calls) ---

        public async void AcceptQuest(VisitorSave visitor)
        {
            var service = SocialService.Instance;
            if (service == null) return;

            var body = JsonUtility.ToJson(new QuestAcceptRequest
            {
                visitor_id = visitor.visitorId,
                request_item = visitor.requestItem,
                request_count = visitor.requestCount,
                return_days = 7, // derived from quest data
                reward = visitor.rewardJson,
                return_dialogue = visitor.returnDialogue
            });

            var request = new UnityWebRequest(service.ServerBaseUrl + "/visitors/quest/accept", "POST");
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", $"Bearer {service.AuthToken}");

            var op = request.SendWebRequest();
            while (!op.isDone) await System.Threading.Tasks.Task.Yield();

            if (request.result == UnityWebRequest.Result.Success)
            {
                var response = JsonUtility.FromJson<QuestAcceptResponse>(request.downloadHandler.text);
                var data = SaveManager.Instance.Data;
                data.activeQuests.Add(new ActiveVisitorQuest
                {
                    serverQuestId = response.quest_id,
                    visitorId = visitor.visitorId,
                    visitorName = visitor.visitorName,
                    portraitId = visitor.portraitId,
                    requestItem = visitor.requestItem,
                    requestCount = visitor.requestCount,
                    returnDateUtc = response.return_date,
                    rewardJson = visitor.rewardJson,
                    returnDialogue = visitor.returnDialogue
                });
                SaveManager.Instance.Save();
            }
        }

        public async void CompleteQuest(VisitorSave visitor)
        {
            var service = SocialService.Instance;
            if (service == null) return;

            var body = JsonUtility.ToJson(new QuestCompleteRequest { quest_id = visitor.serverQuestId });
            var request = new UnityWebRequest(service.ServerBaseUrl + "/visitors/quest/complete", "POST");
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", $"Bearer {service.AuthToken}");

            var op = request.SendWebRequest();
            while (!op.isDone) await System.Threading.Tasks.Task.Yield();

            if (request.result == UnityWebRequest.Result.Success)
            {
                visitor.questFulfilled = true;
                // Remove from active quests
                var data = SaveManager.Instance.Data;
                data.activeQuests.RemoveAll(q => q.serverQuestId == visitor.serverQuestId);

                // Consume items from player inventory
                var item = data.items.Find(i => i.itemName == visitor.requestItem);
                if (item != null)
                {
                    item.count -= visitor.requestCount;
                    if (item.count <= 0) data.items.Remove(item);
                }

                // Apply reward (parse rewardJson)
                // The reward structure needs to be applied based on type
                SaveManager.Instance.Save();
            }
        }

        // --- JSON Types ---

        [Serializable]
        public class VisitorResponse
        {
            public string visitor_type;
            public string visitor_id;
            public string name;
            public string portrait_id;
            public List<string> dialogue;
            public List<OfferResponse> offers;
            public GiftResponse gift;
            public QuestResponse quest;
        }

        [Serializable]
        public class OfferResponse
        {
            public List<TradeCost> costs;
            public string rewardSeedName;
            public int rewardCount;
        }

        [Serializable]
        public class GiftResponse
        {
            public string type;
            public string name;
            public int amount;
        }

        [Serializable]
        public class QuestResponse
        {
            public int quest_id;
            public string request_item;
            public int request_count;
            public int return_days;
            public QuestReward reward;
            public List<string> return_dialogue;
            public bool is_return;
        }

        [Serializable]
        public class QuestReward
        {
            public string type;
            public string name;
            public int count;
        }

        [Serializable]
        private class QuestAcceptRequest
        {
            public string visitor_id;
            public string request_item;
            public int request_count;
            public int return_days;
            public string reward;
            public List<string> return_dialogue;
        }

        [Serializable]
        private class QuestAcceptResponse
        {
            public int quest_id;
            public string return_date;
        }

        [Serializable]
        private class QuestCompleteRequest
        {
            public int quest_id;
        }
    }
}
```

**Step 2: Delete `VisitorSystem.cs` and `MerchantManager.cs`**

**Step 3: Compile and fix references**

There will be compile errors in files referencing `MerchantManager`, `VisitorSystem`, `MerchantData`, and `NightMerchant`. These are fixed in subsequent tasks.

**Step 4: Commit**

```
git commit -m "feat: add unified VisitorManager, remove VisitorSystem and MerchantManager"
```

---

## Phase 4: Client — UI Integration

### Task 8: Update CampsiteViewUI for visitors

**Files:**
- Modify: `Assets/Scripts/UI/CampsiteViewUI.cs`

**Step 1: Replace merchant references**

Key changes needed (reference line numbers from exploration):

1. **Line 72**: Change event `OnMerchantTapped` → `OnVisitorTapped`
   ```csharp
   public event Action OnVisitorTapped;
   ```

2. **Lines 109-113**: Subscribe to `VisitorManager` events instead of `MerchantManager`
   ```csharp
   if (VisitorManager.Instance != null)
   {
       VisitorManager.Instance.OnVisitorArrived += RebuildGrid;
       VisitorManager.Instance.OnVisitorDeparted += RebuildGrid;
   }
   ```

3. **Lines 251-253**: Replace merchant grid population
   ```csharp
   if (data.currentVisitor != null)
       occupied[(data.currentVisitor.gridX, data.currentVisitor.gridY)] = (CampBuildingType.Visitor, 0);
   ```

4. **Line 317**: Update non-movable check: `CampBuildingType.NightMerchant` → `CampBuildingType.Visitor`

5. **Lines 452-457**: Update `PopulateOccupiedCell` merchant case:
   ```csharp
   case CampBuildingType.Visitor:
       cell.AddToClassList("grid-cell--visitor");
       var visitor = SaveManager.Instance.Data.currentVisitor;
       if (label != null) label.text = visitor.visitorName;
       if (status != null)
       {
           switch (visitor.type)
           {
               case VisitorType.Merchant: status.text = $"{visitor.offers.Count} trades"; break;
               case VisitorType.Gifter: status.text = visitor.giftClaimed ? "Thanked" : "Gift"; break;
               case VisitorType.Quester: status.text = visitor.isReturnVisit ? "Returned!" : "Quest"; break;
           }
       }
       break;
   ```

6. **Lines 488-492**: Update tap handler:
   ```csharp
   if (type == CampBuildingType.Visitor)
   {
       OnVisitorTapped?.Invoke();
       return;
   }
   ```

**Step 2: Also update any USS class references**

In the stylesheet, rename `.grid-cell--merchant` to `.grid-cell--visitor` (or add `.grid-cell--visitor` as alias).

**Step 3: Commit**

```
git commit -m "refactor: update CampsiteViewUI for unified visitor system"
```

---

### Task 9: Refactor MerchantUI → VisitorUI

**Files:**
- Rename/rewrite: `Assets/Scripts/UI/MerchantUI.cs` → `Assets/Scripts/UI/VisitorUI.cs`
- Modify: `Assets/UI/Documents/CampFireRoot.uxml` (lines 203-207)

**Step 1: Update UXML**

Replace the merchant panel (lines 203-207) with a more flexible visitor panel:
```xml
<ui:VisualElement name="visitor-panel">
    <ui:Label name="visitor-flavor" class="merchant-flavor" />
    <!-- Merchant sub-panel -->
    <ui:VisualElement name="visitor-merchant-section">
        <ui:VisualElement name="visitor-offer-list" />
    </ui:VisualElement>
    <!-- Gifter sub-panel -->
    <ui:VisualElement name="visitor-gifter-section">
        <ui:Label name="visitor-gift-text" class="gift-text" />
        <ui:Button name="visitor-claim-gift-btn" text="Accept Gift" class="action-button" />
    </ui:VisualElement>
    <!-- Quester sub-panel -->
    <ui:VisualElement name="visitor-quester-section">
        <ui:Label name="visitor-quest-text" class="quest-text" />
        <ui:Button name="visitor-accept-quest-btn" text="Accept Quest" class="action-button" />
        <ui:Button name="visitor-turnin-quest-btn" text="Turn In" class="action-button" />
    </ui:VisualElement>
</ui:VisualElement>
```

**Step 2: Create `VisitorUI.cs`**

Replace `MerchantUI.cs` with `VisitorUI.cs`. Key structure:

- `Initialize(VisualElement root)` — cache all panel elements
- `ShowVisitor()` — reads `SaveData.currentVisitor`, shows the correct sub-panel, hides others
- `RefreshMerchantOffers()` — reuse existing MerchantUI offer rendering logic (trade rows from template)
- `RefreshGifterPanel()` — show gift description + claim button
- `RefreshQuesterPanel()` — show quest request or turn-in based on `isReturnVisit`

Wire button callbacks:
- Claim gift button → `VisitorManager.ApplyGift()`, save, refresh
- Accept quest button → `VisitorManager.Instance.AcceptQuest()`, save, refresh
- Turn-in quest button → check inventory, call `VisitorManager.Instance.CompleteQuest()`, save, refresh

**Step 3: Delete `MerchantUI.cs`**

**Step 4: Commit**

```
git commit -m "feat: replace MerchantUI with VisitorUI supporting all visitor types"
```

---

### Task 10: Rewire CampFireUI

**Files:**
- Modify: `Assets/Scripts/UI/CampFireUI.cs`

**Step 1: Replace merchant references**

Key changes:

1. **Line 23**: `private MerchantUI merchantUI;` → `private VisitorUI visitorUI;`
2. **Line 35**: `private VisualElement merchantPanel;` → `private VisualElement visitorPanel;`
3. **Lines 69-72**: Initialize VisitorUI instead of MerchantUI
4. **Line 84**: `merchantPanel = root.Q("merchant-panel");` → `visitorPanel = root.Q("visitor-panel");`
5. **Lines 151-182**: Replace `OnMerchantTapped` handler with `OnVisitorTapped`:

```csharp
if (campsiteView != null)
    campsiteView.OnVisitorTapped += () =>
    {
        var data = SaveManager.Instance?.Data;
        if (data?.currentVisitor == null) return;
        var visitor = data.currentVisitor;

        if (!visitor.dialogueSeen && visitor.dialogueLines.Count > 0 && dialogueUI != null)
        {
            Texture2D portrait = null;
            if (!string.IsNullOrEmpty(visitor.portraitId))
                portrait = Resources.Load<Texture2D>($"Portraits/{visitor.portraitId}");

            dialogueUI.Show(visitor.visitorName, visitor.dialogueLines, () =>
            {
                visitor.dialogueSeen = true;
                SaveManager.Instance.Save();
                visitorUI?.ShowVisitor();
                OpenOverlay(visitor.visitorName, visitorPanel);
            }, portrait);
        }
        else
        {
            visitorUI?.ShowVisitor();
            OpenOverlay(visitor.visitorName, visitorPanel);
        }
    };
```

6. **Line 235**: Hide `visitorPanel` in `HideAllPanels()`

**Step 2: Compile and verify**

Run Unity compile. Check console for errors.

**Step 3: Commit**

```
git commit -m "refactor: rewire CampFireUI for unified visitor system"
```

---

## Phase 5: Cleanup & Testing

### Task 11: Clean up remaining references

**Files:**
- Grep for any remaining references to `MerchantManager`, `VisitorSystem`, `MerchantData`, `NightMerchant`, `lastVisitorDateUtc`, `lastMerchantDateUtc`, `seenMerchantDialogues`
- Fix all compilation errors

This includes:
- `GameManager.cs` (if it references VisitorSystem or MerchantManager in initialization)
- `BirdManager.cs` (if it references NightMerchant in free tile calculation)
- Any test files
- Scene references (the Unity scene may have VisitorSystem/MerchantManager components on GameObjects — these need to be replaced with VisitorManager)

**Step 1: Grep and fix all references**

**Step 2: Update Unity scene**

Remove `VisitorSystem` and `MerchantManager` MonoBehaviour components from the scene. Add `VisitorManager` component to the managers GameObject.

**Step 3: Delete `Assets/Resources/Merchants/` folder** (old ScriptableObject assets)

**Step 4: Full compile check**

**Step 5: Commit**

```
git commit -m "chore: clean up all old merchant/visitor references"
```

---

### Task 12: Write tests

**Files:**
- Create: `Assets/Tests/EditMode/TestVisitorManager.cs`
- Delete: `Assets/Tests/EditMode/TestVisitorSystem.cs`
- Delete: `Assets/Tests/EditMode/TestMerchantManager.cs`

**Step 1: Write tests for static helpers**

Test the static methods on VisitorManager (same pattern as old tests):

```csharp
[Test] public void IsVisitorHour_Before22_ReturnsFalse()
[Test] public void IsVisitorHour_At22_ReturnsTrue()
[Test] public void IsVisitorHour_At23_ReturnsTrue()

[Test] public void BuildVisitorSave_Merchant_SetsOffersCorrectly()
[Test] public void BuildVisitorSave_Gifter_SetsGiftFields()
[Test] public void BuildVisitorSave_Quester_SetsQuestFields()

[Test] public void ApplyGift_Water_DistributesAcrossVases()
[Test] public void ApplyGift_Seed_AddsToinventory()
[Test] public void ApplyGift_Item_AddsToItems()
[Test] public void ApplyGift_AlreadyClaimed_DoesNothing()

[Test] public void CanAffordOffer_Sufficient_ReturnsTrue()
[Test] public void CanAffordOffer_Insufficient_ReturnsFalse()
[Test] public void ExecuteTrade_ConsumesItemsAndAddsSeed()

[Test] public void CleanExpiredQuests_RemovesOldQuests()
[Test] public void CleanExpiredQuests_KeepsFutureQuests()

[Test] public void DismissVisitor_ClearsCurrentVisitor()
```

**Step 2: Run tests**

Run via Unity MCP `run_tests` with `mode: "EditMode"`.

**Step 3: Delete old test files**

**Step 4: Commit**

```
git commit -m "test: add TestVisitorManager, remove old merchant/visitor tests"
```

---

### Task 13: Add SocialService.ServerBaseUrl and AuthToken accessibility

**Files:**
- Modify: `Assets/Scripts/Services/SocialService.cs` (if `ServerBaseUrl` and `AuthToken` are not already public)

Check if `SocialService.ServerBaseUrl` and `SocialService.AuthToken` are accessible. VisitorManager needs to call them. If they're private, make them `public` (or add `internal` getters). The existing code pattern in SocialService should guide this.

**Step 1: Verify accessibility, make public if needed**

**Step 2: Commit (if changes needed)**

```
git commit -m "refactor: expose SocialService server URL and auth token for visitor API calls"
```
