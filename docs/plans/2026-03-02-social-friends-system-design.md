# Social Friends System Design

## Overview

Add friend system, village visiting, and gift sending to Camp Fire. Uses Firebase (Auth + Firestore + Cloud Functions) as the backend. The social layer sits alongside the existing local save system without modifying it.

## Backend Stack

- **Firebase Anonymous Auth**: player identity on first launch, no sign-up friction. Upgradeable to Google/Apple sign-in later.
- **Cloud Firestore**: player profiles, friend lists, village snapshots, gift inbox, event configs (future).
- **Cloud Functions (TypeScript)**: friend request processing, gift validation/transfer, scheduled cleanup, and future timed event scheduling.
- **Unity packages**: `com.google.firebase.auth` + `com.google.firebase.firestore`

## Player Identity

Firebase Anonymous Auth creates a UID on first launch. A Cloud Function (`onUserCreated`) generates a unique friend code.

**Firestore**: `players/{uid}`
```json
{
  "friendCode": "SPARK-7X2K",
  "displayName": "Campsite #4821",
  "createdAt": "timestamp",
  "lastOnline": "timestamp"
}
```

Friend codes are short, shareable strings (format: `XXXX-XXXX`, alphanumeric). Generated server-side, indexed for uniqueness.

## Friend System

Players add friends by entering a friend code in the Letters panel.

**Flow**: Enter code → Cloud Function looks up target by code → creates friend request → target sees it in their inbox.

**Firestore**: `friendRequests/{autoId}`
```json
{
  "fromUid": "abc123",
  "toUid": "def456",
  "fromName": "Campsite #4821",
  "status": "pending",
  "createdAt": "timestamp"
}
```

**Firestore**: `friends/{uid}/list/{friendUid}`
```json
{
  "displayName": "Campsite #4821",
  "friendCode": "SPARK-7X2K",
  "addedAt": "timestamp"
}
```

On accept, a Cloud Function writes to both players' `friends` subcollections (symmetric). Declining updates status to `"declined"`.

**Limits**: 20 friends max per player.

## Village Snapshots (Visiting)

On game save, a lightweight village snapshot is pushed to Firestore. Friends can view it read-only.

**Firestore**: `villages/{uid}`
```json
{
  "flameLevel": 3,
  "plots": [
    { "seedName": "Sunflower", "state": "Growing", "gridX": 1, "gridY": 0 }
  ],
  "gardens": [
    { "plantName": "Oak", "mature": true, "gridX": 0, "gridY": -1 }
  ],
  "vases": [
    { "currentWater": 3, "capacity": 5, "state": "Full", "gridX": 2, "gridY": 0 }
  ],
  "totalManaEarned": 1250.0,
  "updatedAt": "timestamp"
}
```

**Key decisions**:
- No currency values exposed (prevents social comparison on wealth)
- Snapshot updates on save, not real-time
- Firestore security rules: only owner writes, friends read
- Reuses `CampsiteViewUI` rendering in read-only mode with "Back to my camp" button

## Gift System

Gifts flow through the Letters panel. Players send seeds and harvested items to friends.

**Sending flow**:
1. Pick a friend → "Send Gift"
2. Select seeds/items from inventory (max 3 items per gift)
3. Cloud Function validates sender has the items, deducts, creates gift document
4. Local SaveData deducts optimistically (Cloud Function is source of truth; rolls back on failure)

**Receiving flow**:
1. Open Letters → see pending gifts in inbox
2. Tap to open → items added to local inventory → gift marked claimed in Firestore

**Firestore**: `gifts/{autoId}`
```json
{
  "fromUid": "abc123",
  "toUid": "def456",
  "fromName": "Campsite #4821",
  "items": [
    { "type": "seed", "name": "Moonvine", "count": 1 },
    { "type": "item", "name": "Fertilizer", "count": 2 }
  ],
  "status": "pending",
  "createdAt": "timestamp",
  "claimedAt": null
}
```

**Limits**:
- Max 3 items per gift
- Max 5 gifts per day per sender
- Unclaimed gifts expire after 7 days (scheduled Cloud Function cleanup)

**Inventory sync**: Cloud Function reads a lightweight "inventory summary" pushed alongside the village snapshot to validate sends. Full `SaveData` never leaves the device.

## New C# Architecture

### SocialService (singleton MonoBehaviour)

Owns all Firebase interaction. Other managers never touch Firebase directly.

- `SignIn()` — anonymous auth on startup
- `GetFriendCode()` / `SendFriendRequest(code)` / `AcceptRequest(id)` / `DeclineRequest(id)`
- `GetFriendList()` / `RemoveFriend(uid)`
- `FetchVillageSnapshot(uid)` / `PushVillageSnapshot()`
- `SendGift(uid, items)` / `ClaimGift(id)` / `GetPendingGifts()`

### SocialData (local cache)

Separate from `SaveData` — persisted to `social.json` alongside `save.json`.

- `firebaseUid`, `friendCode`, `displayName`
- Cached friend list (for offline display)
- Pending gift/request counts

### Cloud Functions (TypeScript)

- `onUserCreated` — generates friend code on new auth
- `sendFriendRequest` — validates and creates request
- `acceptFriendRequest` — writes to both friend lists
- `sendGift` — validates inventory, deducts, creates gift doc
- `claimGift` — marks claimed
- `cleanupExpiredGifts` — scheduled daily, deletes 7-day-old unclaimed gifts

## Letters UI Rework

The stubbed `LettersUI` becomes the social hub with three sub-views:

1. **Inbox** — pending gifts and friend requests (badge count on Letters nav button)
2. **Friends** — friend list with "Visit" and "Send Gift" actions per friend
3. **Add Friend** — text field to enter a friend code

## Future Considerations

- **Timed events**: Cloud Functions + Firestore event configs. A scheduled function activates/deactivates events; clients read active events on launch. This infrastructure (Cloud Functions, Firestore, auth) is shared with the social system.
- **Account linking**: Upgrade from anonymous to Google/Apple sign-in for account recovery.
- **Cross-device sync**: Could extend village snapshots into full cloud saves later.
