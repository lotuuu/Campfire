# Merchant Dialogue System Design

## Overview

When the player first taps a merchant tile each night, a VN-style dialogue sequence plays before the trade panel opens. Subsequent taps that same night skip the dialogue and go straight to trading. The merchant has a pool of random conversations (each being 2-3 lines), and one is picked per visit.

## Data Model

### MerchantData Extensions

Add `List<MerchantDialogue> dialoguePool` to `MerchantData`. Each `MerchantDialogue` contains a `List<string> lines` (2-3 lines per conversation).

### MerchantSave Extensions

- `List<string> dialogueLines` — the rolled dialogue for this visit (persisted across app backgrounding)
- `bool dialogueSeen` — set to `true` after first interaction; subsequent taps skip dialogue

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

~6 random conversations of 2-3 lines each, stored in the `MerchantData` ScriptableObject asset's `dialoguePool` field. One conversation is randomly selected when the merchant spawns and saved into `MerchantSave.dialogueLines`.
