# Merchant Dialogue System Design

## Overview

When the player first taps a merchant tile each night, a VN-style dialogue sequence plays before the trade panel opens. Subsequent taps that same night skip the dialogue and go straight to trading. The merchant has a pool of random conversations (each being 2-3 lines), and one is picked per visit.

Conversations are tracked as "seen" across sessions. A previously-seen conversation won't be shown again until all conversations have been seen, at which point it falls back to picking from already-seen ones.

## Data Model

### MerchantData Extensions

Add `List<MerchantDialogue> dialoguePool` to `MerchantData`. Each `MerchantDialogue` contains a `List<string> lines` (2-3 lines per conversation).

### MerchantSave Extensions

- `List<string> dialogueLines` — the rolled dialogue for this visit (persisted across app backgrounding)
- `bool dialogueSeen` — set to `true` after first interaction this night; subsequent taps skip dialogue

### SaveData Extensions

- `List<int> seenMerchantDialogues` — indices into `MerchantData.dialoguePool` that have been shown. Persists across nights. When all indices are seen, the list is cleared and conversations recycle.

## Dialogue Selection Logic (static, testable)

1. Build list of unseen indices: all indices in `dialoguePool` not in `seenMerchantDialogues`
2. If unseen list is empty, clear `seenMerchantDialogues` and use the full pool
3. Pick a random unseen index
4. Store the selected dialogue lines in `MerchantSave.dialogueLines`
5. Add the index to `seenMerchantDialogues`

## UI

Full-screen dialogue overlay with:
- Dark scrim background
- Bottom-anchored text box spanning full width (VN-style)
- Merchant name label at top of text box
- Dialogue text that updates on each tap
- Tap anywhere to advance; after last line, one more tap dismisses and opens trade panel

New files:
- `Assets/Scripts/UI/DialogueUI.cs` — MonoBehaviour controller
- `dialogue-panel` element in `CampFireRoot.uxml`
- `Assets/UI/Styles/Dialogue.uss` — styling

## Flow

1. Player taps merchant tile
2. `CampsiteViewUI` fires `OnMerchantTapped(index)`
3. `CampFireUI` checks `merchantSave.dialogueSeen`:
   - `false`: opens dialogue overlay, shows lines one by one via tap-to-advance, then sets `dialogueSeen = true`, saves, opens trade panel
   - `true`: opens trade panel directly

## Dialogue Content

~6 random conversations of 2-3 lines each, stored in the `MerchantData` ScriptableObject asset's `dialoguePool` field. One conversation is randomly selected when the merchant spawns (preferring unseen ones) and saved into `MerchantSave.dialogueLines`.
