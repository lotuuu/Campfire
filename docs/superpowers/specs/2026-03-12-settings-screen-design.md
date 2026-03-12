# Settings Screen Redesign

## Overview

Redesign the Settings overlay panel from a bare-bones debug placeholder (two audio sliders borrowing debug-panel CSS) into a properly styled, full-featured settings screen with three sections: Audio, Account, and Danger Zone.

## Structure

The settings panel lives in the existing overlay system (slide-up panel via `CampFireUI.OpenOverlay()`). Three sections with uppercase headers and subtle dividers:

### 1. AUDIO
- Music volume slider (0–100%)
- SFX volume slider (0–100%)
- Same functionality as today — `AudioManager.SetMusicVolume/SetSFXVolume`, persist to `SaveData.musicVolume/sfxVolume`

### 2. ACCOUNT
Three read-only key-value info rows:
- **Player** — player ID from `SocialSaveManager` auth data
- **Server** — current server name from `ServerConfig`
- **Version** — `Application.version`

### 3. DANGER ZONE
- "Delete Save Data" button styled as danger (red-tinted)
- On tap: inline confirmation — button area swaps to show "Are you sure?" label with Cancel and Delete buttons side by side
- On confirm: wipe save files (both SaveManager and SocialSaveManager) and reload scene (same pattern as `ServerConfig.Select()`)
- Note: DialogueUI is a sequential text system without confirm/cancel buttons, so we use inline confirmation instead

## Visual Style

Clean sections — uppercase section headers with subtle bottom-border dividers. No cards or grouping containers. Matches the existing `debug-section-header` visual language.

### Styling Details
- **Own stylesheet**: `Assets/UI/Styles/Settings.uss` — stops borrowing `debug-row`/`debug-label` classes from Debug.uss
- **Section headers**: uppercase, small font (`--font-xs`), letter-spacing, bottom border `rgba(180,120,60,0.25)`
- **Sliders**: 64px orange dragger (touch-friendly), 12px semi-transparent tracker, percentage label right-aligned
- **Info rows**: dim label left, bright value right, simple flex row with bottom padding
- **Delete button**: full-width, `rgba(140,40,30,0.7)` background, `rgba(200,80,60,0.4)` border, light text

## Files Changed

| File | Change |
|------|--------|
| `Assets/UI/Documents/CampFireRoot.uxml` | Replace debug-row markup in settings-panel with proper section headers, info rows, and delete button |
| `Assets/Scripts/UI/SettingsUI.cs` | Populate account info rows, wire delete button to inline confirmation flow |
| `Assets/UI/Styles/Settings.uss` | New stylesheet with all settings-specific styles |
| `Assets/UI/Styles/Overlay.uss` | No changes needed — `#settings-scroll` already referenced |

## Delete Save Flow

1. User taps "Delete Save Data"
2. Button area swaps to inline confirmation: "Are you sure?" label + Cancel / Delete buttons side by side
3. Cancel → swap back to original delete button
4. Delete → call `SaveManager.Instance.DeleteSave()` + `SocialSaveManager.Instance.DeleteSave()` → reload scene via `SceneManager.LoadScene`

## What This Does NOT Include
- No invite code entry
- No contact/support link
- No sign-out (auto-register auth model)
- No notification settings
- No theme/accessibility options
