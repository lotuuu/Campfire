# Sound System Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add a complete sound system with background music, comprehensive SFX, and two-slider volume control.

**Architecture:** AudioMixer-based singleton `AudioManager` with `SoundLibrary` ScriptableObject mapping string keys to AudioClips. Two mixer groups (Music, SFX) with exposed volume parameters. Placeholder audio generated via Python3.

**Tech Stack:** Unity AudioMixer, AudioSource, ScriptableObject, Python3 WAV generation

---

### Task 1: Generate Placeholder Audio Assets

**Files:**
- Create: `Assets/Audio/SFX/*.wav` (18 files)
- Create: `Assets/Audio/Music/music_main.wav`
- Create: script `tools/generate_audio.py` (temporary, deleted after use)

**Step 1: Create audio directories**

```bash
mkdir -p Assets/Audio/SFX Assets/Audio/Music
```

**Step 2: Write and run Python WAV generator**

Create `tools/generate_audio.py` that uses the `wave` and `struct` stdlib modules to synthesize placeholder WAV files. Each sound uses a distinct synthesis approach (sine pops, noise bursts, sweeps, etc.) so they're distinguishable during testing.

Sound specifications:
- `ui_tap.wav` — 5ms sine pop at 2000Hz
- `ui_panel_open.wav` — 100ms rising two-tone (400→800Hz)
- `ui_panel_close.wav` — 100ms falling two-tone (800→400Hz)
- `flame_collect_mana.wav` — 200ms sine sweep 500→2000Hz
- `flame_upgrade.wav` — 500ms ascending arpeggio (C5-E5-G5)
- `plot_craft.wav` — 150ms low thud (120Hz) + click
- `plot_plant.wav` — 100ms soft plop (300Hz with decay)
- `plot_water.wav` — 200ms filtered noise burst
- `plot_harvest.wav` — 300ms cheerful two-note chime (E5-G5)
- `vase_craft.wav` — 150ms thud + click (same as plot_craft, slightly different pitch)
- `vase_fill_complete.wav` — 150ms water drip tone (descending 600→400Hz)
- `mallum_dispatch_water.wav` — 200ms whoosh (noise with envelope)
- `mallum_dispatch_quest.wav` — 200ms whoosh variant (slightly higher)
- `quest_complete.wav` — 400ms fanfare (C5-E5-G5 ascending)
- `quest_collect_rewards.wav` — 300ms coin jingle (fast sine pops)
- `apotheke_mix.wav` — 400ms bubbling (modulated sine)
- `bird_arrive.wav` — 150ms chirp (sine wobble 1500-2500Hz)
- `bird_collect.wav` — 200ms soft pickup chime (A5)
- `visitor_arrive.wav` — 300ms doorbell two-tone (E5-C5)
- `garden_craft.wav` — 150ms earthy thud (100Hz)
- `garden_harvest.wav` — 300ms harvest chime variant (C5-E5)
- `music_main.wav` — 30s gentle looping pad (layered sines with slow LFO, C3+E3+G3)

```bash
python3 tools/generate_audio.py
```

**Step 3: Delete generator script**

```bash
rm tools/generate_audio.py
```

**Step 4: Commit**

```bash
git add Assets/Audio/
git commit -m "feat(audio): add placeholder audio assets generated via Python synthesis"
```

---

### Task 2: Create AudioMixer Asset

**Files:**
- Create: `Assets/Audio/CampFireMixer.mixer`
- Create: `Assets/Audio/CampFireMixer.mixer.meta`

**Step 1: Create the AudioMixer YAML asset**

The mixer has three groups:
- **Master** (root)
  - **Music** (exposed param: `MusicVolume`)
  - **SFX** (exposed param: `SFXVolume`)

Write the `.mixer` file directly as Unity YAML. Use unique GUIDs for each group. Expose `MusicVolume` and `SFXVolume` parameters. Both default to 0 dB (full volume).

**Step 2: Create .meta file**

Standard Unity meta file with a unique GUID.

**Step 3: Commit**

```bash
git add Assets/Audio/CampFireMixer.mixer Assets/Audio/CampFireMixer.mixer.meta
git commit -m "feat(audio): add AudioMixer with Music and SFX groups"
```

---

### Task 3: Create SoundLibrary ScriptableObject and AudioManager

**Files:**
- Create: `Assets/Scripts/Data/SoundLibrary.cs`
- Create: `Assets/Scripts/Services/AudioManager.cs`

**Step 1: Write SoundLibrary.cs**

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Garden
{
    [CreateAssetMenu(fileName = "SoundLibrary", menuName = "Garden/Sound Library")]
    public class SoundLibrary : ScriptableObject
    {
        public List<SoundEntry> entries = new();

        [Serializable]
        public class SoundEntry
        {
            public string key;
            public AudioClip clip;
            [Range(0f, 1f)] public float volume = 1f;
            [Range(0.8f, 1.2f)] public float pitchMin = 1f;
            [Range(0.8f, 1.2f)] public float pitchMax = 1f;
        }
    }
}
```

**Step 2: Write AudioManager.cs**

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace Garden
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [SerializeField] private AudioMixer mixer;
        [SerializeField] private SoundLibrary library;

        private AudioSource _musicSource;
        private AudioSource[] _sfxSources;
        private int _sfxIndex;
        private Dictionary<string, SoundLibrary.SoundEntry> _lookup;

        private const int SfxPoolSize = 4;
        private const float MinDb = -80f;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            _musicSource = gameObject.AddComponent<AudioSource>();
            _musicSource.loop = true;
            _musicSource.playOnAwake = false;
            if (mixer != null)
            {
                var musicGroup = mixer.FindMatchingGroups("Music");
                if (musicGroup.Length > 0) _musicSource.outputAudioMixerGroup = musicGroup[0];
            }

            _sfxSources = new AudioSource[SfxPoolSize];
            for (int i = 0; i < SfxPoolSize; i++)
            {
                _sfxSources[i] = gameObject.AddComponent<AudioSource>();
                _sfxSources[i].playOnAwake = false;
                if (mixer != null)
                {
                    var sfxGroup = mixer.FindMatchingGroups("SFX");
                    if (sfxGroup.Length > 0) _sfxSources[i].outputAudioMixerGroup = sfxGroup[0];
                }
            }

            BuildLookup();
        }

        private void Start()
        {
            // Restore volume from save
            var data = SaveManager.Instance?.Data;
            if (data != null)
            {
                SetMusicVolume(data.musicVolume);
                SetSFXVolume(data.sfxVolume);
            }

            // Auto-play music
            PlayMusic();
        }

        private void BuildLookup()
        {
            _lookup = new Dictionary<string, SoundLibrary.SoundEntry>();
            if (library == null) return;
            foreach (var entry in library.entries)
            {
                if (!string.IsNullOrEmpty(entry.key))
                    _lookup[entry.key] = entry;
            }
        }

        public void PlaySFX(string key)
        {
            if (_lookup == null || !_lookup.TryGetValue(key, out var entry)) return;
            if (entry.clip == null) return;

            var source = _sfxSources[_sfxIndex];
            _sfxIndex = (_sfxIndex + 1) % SfxPoolSize;

            source.pitch = Random.Range(entry.pitchMin, entry.pitchMax);
            source.PlayOneShot(entry.clip, entry.volume);
        }

        public void PlayMusic()
        {
            if (_lookup != null && _lookup.TryGetValue("music_main", out var entry) && entry.clip != null)
            {
                _musicSource.clip = entry.clip;
                _musicSource.volume = entry.volume;
                _musicSource.Play();
            }
        }

        public void StopMusic()
        {
            _musicSource.Stop();
        }

        public void SetMusicVolume(float volume01)
        {
            float db = volume01 > 0.0001f ? Mathf.Log10(volume01) * 20f : MinDb;
            mixer?.SetFloat("MusicVolume", db);
        }

        public void SetSFXVolume(float volume01)
        {
            float db = volume01 > 0.0001f ? Mathf.Log10(volume01) * 20f : MinDb;
            mixer?.SetFloat("SFXVolume", db);
        }
    }
}
```

**Step 3: Commit**

```bash
git add Assets/Scripts/Data/SoundLibrary.cs Assets/Scripts/Services/AudioManager.cs
git commit -m "feat(audio): add AudioManager singleton and SoundLibrary ScriptableObject"
```

---

### Task 4: Create SoundLibrary Asset and Wire AudioManager into Scene

**Files:**
- Create: `Assets/Resources/Config/SoundLibrary.asset` (ScriptableObject YAML)
- Modify: `Assets/Scenes/Garden.unity` (add AudioManager component to --- UI --- or new --- Audio --- GameObject)

**Step 1: Create SoundLibrary.asset**

Write the ScriptableObject YAML directly with all 22 sound entries. Each entry maps a key string to a clip GUID (from the .meta files generated in Task 1). Set volume=1 and pitchMin/pitchMax=1 for all entries (defaults).

**Step 2: Add AudioManager to the scene**

Add a new GameObject `--- Audio ---` to Garden.unity with the `AudioManager` MonoBehaviour component. Wire its serialized fields:
- `mixer` → CampFireMixer.mixer (by GUID)
- `library` → SoundLibrary.asset (by GUID)

**Step 3: Commit**

```bash
git add Assets/Resources/Config/SoundLibrary.asset Assets/Scenes/Garden.unity
git commit -m "feat(audio): wire SoundLibrary asset and AudioManager into scene"
```

---

### Task 5: Add Volume Fields to SaveData

**Files:**
- Modify: `Assets/Scripts/Data/SaveData.cs:29` (add fields before closing brace)

**Step 1: Add volume fields**

Add after line 29 (`public string lastBirdCheckHourUtc;`):

```csharp
        public float musicVolume = 1f;
        public float sfxVolume = 1f;
```

**Step 2: Commit**

```bash
git add Assets/Scripts/Data/SaveData.cs
git commit -m "feat(audio): add musicVolume and sfxVolume to SaveData"
```

---

### Task 6: Add SFX Calls to Managers

**Files:**
- Modify: `Assets/Scripts/Managers/FlameManager.cs` (lines 133, UpgradeFlame)
- Modify: `Assets/Scripts/Managers/PlotManager.cs` (lines 250, 306, 346, 443)
- Modify: `Assets/Scripts/Managers/VaseManager.cs` (lines 94, 177)
- Modify: `Assets/Scripts/Managers/MallumManager.cs` (lines 242, 266, 188, 291)
- Modify: `Assets/Scripts/Managers/ApothekeManager.cs` (line 56)
- Modify: `Assets/Scripts/Managers/GardenManager.cs` (lines 215, 150)
- Modify: `Assets/Scripts/Managers/BirdManager.cs` (lines 49, 115)
- Modify: `Assets/Scripts/Managers/VisitorManager.cs` (line 96)

Each modification is a single line addition: `AudioManager.Instance?.PlaySFX("key");`

**Step 1: FlameManager — flame_upgrade**

In `UpgradeFlame()` at line 133, after `OnFlameUpgraded?.Invoke();`:
```csharp
            AudioManager.Instance?.PlaySFX("flame_upgrade");
```

Note: `flame_collect_mana` is triggered from the UI side (mana tap), not FlameManager.Update(). This will be wired in Task 7.

**Step 2: PlotManager — plot_craft, plot_plant, plot_water, plot_harvest**

After `OnPlotChanged?.Invoke(newIndex);` in `CraftPlot()` (line 250):
```csharp
            AudioManager.Instance?.PlaySFX("plot_craft");
```

After `OnPlotChanged?.Invoke(plotIndex);` in `Plant()` (line 306):
```csharp
            AudioManager.Instance?.PlaySFX("plot_plant");
```

After `OnPlotChanged?.Invoke(plotIndex);` in `Water()` (line 346):
```csharp
            AudioManager.Instance?.PlaySFX("plot_water");
```

After `OnHarvested?.Invoke(plotIndex, result);` in `Harvest()` (line 443):
```csharp
            AudioManager.Instance?.PlaySFX("plot_harvest");
```

**Step 3: VaseManager — vase_craft, vase_fill_complete**

After `OnVasesChanged?.Invoke();` in `CraftVase()` (line 177):
```csharp
            AudioManager.Instance?.PlaySFX("vase_craft");
```

After `OnVasesChanged?.Invoke();` in `CheckFillCompletion()` (line 94):
```csharp
                AudioManager.Instance?.PlaySFX("vase_fill_complete");
```

**Step 4: MallumManager — dispatch and quest sounds**

After `OnMallumsChanged?.Invoke();` in `SendToFetchWater()` (line 242):
```csharp
            AudioManager.Instance?.PlaySFX("mallum_dispatch_water");
```

After `OnMallumsChanged?.Invoke();` in `SendOnQuest()` (line 266):
```csharp
            AudioManager.Instance?.PlaySFX("mallum_dispatch_quest");
```

After `CompleteQuest(mallum);` in Update() (line 188):
```csharp
                        AudioManager.Instance?.PlaySFX("quest_complete");
```

After `OnMallumsChanged?.Invoke();` in `CollectQuestRewards()` (line 291):
```csharp
            AudioManager.Instance?.PlaySFX("quest_collect_rewards");
```

**Step 5: ApothekeManager — apotheke_mix**

After `SaveManager.Instance.Save();` in `Mix()` (line 56):
```csharp
            AudioManager.Instance?.PlaySFX("apotheke_mix");
```

**Step 6: GardenManager — garden_craft, garden_harvest**

After `OnGardenChanged?.Invoke(data.gardens.Count - 1);` in `CraftEmptyGarden()` (line 215):
```csharp
            AudioManager.Instance?.PlaySFX("garden_craft");
```

After `OnYieldCollected?.Invoke(...)` in `CheckGrowthAndYields()` (line 150):
```csharp
                    AudioManager.Instance?.PlaySFX("garden_harvest");
```

**Step 7: BirdManager — bird_arrive, bird_collect**

After `OnBirdPlaced?.Invoke();` in `Update()` (line 49):
```csharp
                AudioManager.Instance?.PlaySFX("bird_arrive");
```

After `OnBirdCollected?.Invoke(bird);` in `CollectBirdFromServer()` (line 115):
```csharp
                AudioManager.Instance?.PlaySFX("bird_collect");
```

Also after `NotifyBirdCollected(collected);` in offline fallback (line 125):
```csharp
                AudioManager.Instance?.PlaySFX("bird_collect");
```

**Step 8: VisitorManager — visitor_arrive**

After `OnVisitorArrived?.Invoke();` in `FetchTonightVisitorAsync()` (line 96):
```csharp
                    AudioManager.Instance?.PlaySFX("visitor_arrive");
```

**Step 9: Commit**

```bash
git add Assets/Scripts/Managers/
git commit -m "feat(audio): add SFX triggers to all managers"
```

---

### Task 7: Add SFX Calls to UI Controllers

**Files:**
- Modify: `Assets/Scripts/UI/CampFireUI.cs` (OpenOverlay, CloseOverlay, mana tap)

**Step 1: Find and review CampFireUI for button wiring and overlay methods**

Read the full CampFireUI to identify:
- Where buttons are wired (for ui_tap)
- OpenOverlay (line 363) and CloseOverlay (line 371) for panel sounds
- Where mana collection is triggered from UI (for flame_collect_mana)

**Step 2: Add UI sounds**

In `OpenOverlay()` (line 363), at the start of the method:
```csharp
            AudioManager.Instance?.PlaySFX("ui_panel_open");
```

In `CloseOverlay()` (line 371), at the start of the method:
```csharp
            AudioManager.Instance?.PlaySFX("ui_panel_close");
```

For `ui_tap`: Add to relevant button click handlers. Examine CampFireUI and BottomNavUI to find where button callbacks are registered. Add `AudioManager.Instance?.PlaySFX("ui_tap");` at the start of each button handler.

For `flame_collect_mana`: Find where the mana tap/collect action is triggered from UI (likely in CampsiteViewUI or CampFireUI flame tap handler). Add `AudioManager.Instance?.PlaySFX("flame_collect_mana");` there.

**Step 3: Commit**

```bash
git add Assets/Scripts/UI/
git commit -m "feat(audio): add UI sound effects (tap, panel open/close, mana collect)"
```

---

### Task 8: Add Settings Panel with Volume Sliders

**Files:**
- Modify: `Assets/UI/Documents/CampFireRoot.uxml` (add settings panel)
- Modify: `Assets/UI/Styles/` (add settings panel styles if needed)
- Create: `Assets/Scripts/UI/SettingsUI.cs` (settings panel controller)
- Modify: `Assets/Scripts/UI/CampFireUI.cs` (wire settings panel + button)

**Step 1: Add settings panel to CampFireRoot.uxml**

Add a settings panel within the overlay container (following the pattern of existing panels like apotheke, build, etc.):

```xml
<VisualElement name="settings-panel" class="overlay-panel" style="display: none;">
    <VisualElement class="settings-row">
        <Label text="Music" />
        <Slider name="music-slider" low-value="0" high-value="100" value="100" />
    </VisualElement>
    <VisualElement class="settings-row">
        <Label text="Sound Effects" />
        <Slider name="sfx-slider" low-value="0" high-value="100" value="100" />
    </VisualElement>
</VisualElement>
```

**Step 2: Write SettingsUI.cs**

```csharp
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class SettingsUI : MonoBehaviour
    {
        private Slider _musicSlider;
        private Slider _sfxSlider;

        public void Initialize(VisualElement root)
        {
            _musicSlider = root.Q<Slider>("music-slider");
            _sfxSlider = root.Q<Slider>("sfx-slider");

            var data = SaveManager.Instance?.Data;
            if (data != null)
            {
                _musicSlider.value = data.musicVolume * 100f;
                _sfxSlider.value = data.sfxVolume * 100f;
            }

            _musicSlider.RegisterValueChangedCallback(evt =>
            {
                float vol = evt.newValue / 100f;
                AudioManager.Instance?.SetMusicVolume(vol);
                if (SaveManager.Instance?.Data != null)
                {
                    SaveManager.Instance.Data.musicVolume = vol;
                    SaveManager.Instance.Save();
                }
            });

            _sfxSlider.RegisterValueChangedCallback(evt =>
            {
                float vol = evt.newValue / 100f;
                AudioManager.Instance?.SetSFXVolume(vol);
                if (SaveManager.Instance?.Data != null)
                {
                    SaveManager.Instance.Data.sfxVolume = vol;
                    SaveManager.Instance.Save();
                }
            });
        }
    }
}
```

**Step 3: Wire into CampFireUI**

- Add `settingsPanel` VisualElement field, query it in initialization
- Add `SettingsUI` component initialization
- Add settings button to bottom nav or top bar (check existing pattern for where it fits best)
- Wire button to `OpenOverlay("Settings", settingsPanel)`
- Add `settingsPanel` to `HideAllPanels()`

**Step 4: Commit**

```bash
git add Assets/Scripts/UI/SettingsUI.cs Assets/Scripts/UI/CampFireUI.cs Assets/UI/Documents/CampFireRoot.uxml
git commit -m "feat(audio): add settings panel with music and SFX volume sliders"
```

---

### Task 9: Verify and Test

**Step 1: Check compilation**

Open Unity and verify no compilation errors. Check the Console for any issues.

**Step 2: Verify AudioManager initializes**

Enter Play mode. Check that:
- AudioManager.Instance is not null
- Music starts playing automatically
- No errors in Console

**Step 3: Test SFX playback**

Perform gameplay actions and verify sounds trigger:
- Tap a button (ui_tap)
- Open/close an overlay panel
- Collect mana
- Plant, water, harvest

**Step 4: Test volume sliders**

Open settings panel. Verify:
- Music slider changes music volume in real time
- SFX slider changes SFX volume in real time
- Values persist after closing and reopening settings

**Step 5: Commit any fixes**

```bash
git add -A && git commit -m "fix(audio): address issues found during testing"
```
