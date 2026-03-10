# Sound System Design

## Overview

Full sound system for Camp Fire: background music loop, comprehensive SFX for all gameplay and UI interactions, with two-slider volume control (Music / SFX).

## Architecture

### AudioManager (Singleton MonoBehaviour)

Follows existing singleton pattern. Lives on the `--- UI ---` GameObject (or a dedicated `--- Audio ---` GameObject).

- Owns two `AudioSource` components: one for music (looping), one for SFX
- References an `AudioMixer` asset with two groups: **Music** and **SFX**
- Exposed mixer parameters: `MusicVolume` and `SFXVolume` (logarithmic dB, -80 to 0)
- SFX pool: 3-4 AudioSources for overlapping one-shot playback via `PlayOneShot()`
- Music starts automatically in `Start()`

**Public API:**
- `PlaySFX(string key)` — fire-and-forget SFX by key
- `PlayMusic()` / `StopMusic()`
- `SetMusicVolume(float 0-1)` / `SetSFXVolume(float 0-1)` — maps linear 0-1 to logarithmic dB

### SoundLibrary (ScriptableObject)

Located at `Assets/Resources/Config/SoundLibrary.asset`. Contains a serialized list of entries:

```csharp
[Serializable]
public class SoundEntry
{
    public string key;
    public AudioClip clip;
    [Range(0f, 1f)] public float volume = 1f;
    [Range(0.8f, 1.2f)] public float pitchVariation = 1f;
}
```

AudioManager loads this at `Awake()` and builds a `Dictionary<string, SoundEntry>` for O(1) lookup.

### AudioMixer Asset

`Assets/Audio/CampFireMixer.mixer` with groups:
- **Master**
  - **Music** (exposed param: `MusicVolume`)
  - **SFX** (exposed param: `SFXVolume`)

### Volume Persistence

Two new fields in `SaveData`:
- `musicVolume` (float, default 1.0)
- `sfxVolume` (float, default 1.0)

Restored on load, applied to mixer. The existing server-side `music` cosmetic field is synced as `musicVolume > 0`.

## SFX Event Catalog

### UI Sounds
| Key | Description |
|-----|-------------|
| `ui_tap` | Button press (all buttons) |
| `ui_panel_open` | Overlay panel slides up |
| `ui_panel_close` | Overlay panel slides down |

### Flame & Mana
| Key | Description |
|-----|-------------|
| `flame_collect_mana` | Collecting mana |
| `flame_upgrade` | Flame level up |

### Plots & Plants
| Key | Description |
|-----|-------------|
| `plot_craft` | Placing a new plot |
| `plot_plant` | Planting a seed |
| `plot_water` | Watering a plant |
| `plot_harvest` | Harvesting |

### Vases
| Key | Description |
|-----|-------------|
| `vase_craft` | Placing a new vase |
| `vase_fill_complete` | Vase finished filling |

### Mallums & Quests
| Key | Description |
|-----|-------------|
| `mallum_dispatch_water` | Sending Mallum to fetch water |
| `mallum_dispatch_quest` | Sending Mallum on quest |
| `quest_complete` | Quest finishes |
| `quest_collect_rewards` | Collecting quest rewards |

### Apotheke
| Key | Description |
|-----|-------------|
| `apotheke_mix` | Mixing a recipe |

### Garden
| Key | Description |
|-----|-------------|
| `garden_craft` | Placing a garden plot |
| `garden_harvest` | Collecting garden yield |

### Birds & Visitors
| Key | Description |
|-----|-------------|
| `bird_arrive` | Bird lands at camp |
| `bird_collect` | Collecting bird's seed drop |
| `visitor_arrive` | Visitor appears |

### Music
| Key | Description |
|-----|-------------|
| `music_main` | Ambient music loop (~30s) |

## Integration Points

Each manager/controller adds a single `AudioManager.Instance.PlaySFX("key")` call at the appropriate point:

| Caller | Method | Sound Key |
|--------|--------|-----------|
| All buttons | click callbacks | `ui_tap` |
| `CampFireUI` | `ShowOverlay()` | `ui_panel_open` |
| `CampFireUI` | `HideOverlay()` | `ui_panel_close` |
| `FlameManager` | `CollectMana()` | `flame_collect_mana` |
| `FlameManager` | `UpgradeFlame()` | `flame_upgrade` |
| `PlotManager` | `CraftPlot()` | `plot_craft` |
| `PlotManager` | `Plant()` | `plot_plant` |
| `PlotManager` | `Water()` | `plot_water` |
| `PlotManager` | `Harvest()` | `plot_harvest` |
| `VaseManager` | `CraftVase()` | `vase_craft` |
| `VaseManager` | `OnFillComplete()` | `vase_fill_complete` |
| `MallumManager` | `SendToFetchWater()` | `mallum_dispatch_water` |
| `MallumManager` | `SendOnQuest()` | `mallum_dispatch_quest` |
| `MallumManager` | `CompleteQuest()` | `quest_complete` |
| `MallumManager` | `CollectQuestRewards()` | `quest_collect_rewards` |
| `ApothekeManager` | `Mix()` | `apotheke_mix` |
| `GardenManager` | `CraftGarden()` | `garden_craft` |
| `GardenManager` | `CollectYield()` | `garden_harvest` |
| `BirdManager` | bird arrival logic | `bird_arrive` |
| `BirdManager` | `CollectBirdDrop()` | `bird_collect` |
| `VisitorManager` | visitor arrival logic | `visitor_arrive` |

## Volume UI

Two sliders in a settings overlay panel (new minimal panel if none exists):
- Music volume slider (0-100%)
- SFX volume slider (0-100%)

Sliders call `AudioManager.SetMusicVolume()` / `SetSFXVolume()` and values persist via SaveManager.

## Placeholder Audio Assets

Generated via `ffmpeg` synthesis. Located at `Assets/Audio/Music/` and `Assets/Audio/SFX/`.

| Sound | Synthesis |
|-------|-----------|
| `ui_tap` | Short high click (5ms sine pop) |
| `ui_panel_open` | Rising two-tone (100ms) |
| `ui_panel_close` | Falling two-tone (100ms) |
| `flame_collect_mana` | Bright shimmer (sine sweep up, 200ms) |
| `flame_upgrade` | Ascending arpeggio (500ms) |
| `plot_craft` / `vase_craft` / `garden_craft` | Thud + click (150ms) |
| `plot_plant` | Soft plop (100ms) |
| `plot_water` | White noise burst filtered low (200ms) |
| `plot_harvest` / `garden_harvest` | Cheerful two-note chime (300ms) |
| `vase_fill_complete` | Water drip tone (150ms) |
| `mallum_dispatch_water/quest` | Whoosh (filtered noise sweep, 200ms) |
| `quest_complete` | Fanfare — three ascending tones (400ms) |
| `quest_collect_rewards` | Coin jingle (fast sine pops, 300ms) |
| `apotheke_mix` | Bubbling (modulated sine, 400ms) |
| `bird_arrive` | Short chirp (sine wobble, 150ms) |
| `bird_collect` | Soft pickup chime (200ms) |
| `visitor_arrive` | Doorbell two-tone (300ms) |
| `music_main` | Gentle looping pad (30s, layered sines with slow LFO) |

## File Locations

- `Assets/Audio/Music/` — music tracks
- `Assets/Audio/SFX/` — sound effect clips
- `Assets/Audio/CampFireMixer.mixer` — AudioMixer asset
- `Assets/Resources/Config/SoundLibrary.asset` — SoundLibrary ScriptableObject
- `Assets/Scripts/Services/AudioManager.cs` — AudioManager singleton
- `Assets/Scripts/Data/SoundLibrary.cs` — SoundLibrary + SoundEntry classes
