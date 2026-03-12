# Settings Screen Redesign Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Redesign the Settings overlay panel from a debug placeholder into a properly styled screen with Audio, Account, and Danger Zone sections.

**Architecture:** Three-section layout in the existing overlay system. New `Settings.uss` stylesheet replaces borrowed debug classes. SettingsUI.cs expanded to populate account info and wire inline delete confirmation. UXML markup replaced with proper semantic elements.

**Tech Stack:** Unity UI Toolkit (UXML, USS, C#)

**Spec:** `docs/superpowers/specs/2026-03-12-settings-screen-design.md`

---

## Chunk 1: Settings Screen Implementation

### Task 1: Create Settings.uss stylesheet

**Files:**
- Create: `Assets/UI/Styles/Settings.uss`

- [ ] **Step 1: Create the stylesheet**

Write `Assets/UI/Styles/Settings.uss` with all settings-specific styles:

```css
/* Settings.uss — Settings panel styling */

/* Section headers */
.settings-section-header {
    font-size: var(--font-xs);
    color: rgb(160, 140, 110);
    -unity-font-style: bold;
    letter-spacing: 3px;
    margin-top: var(--spacing-lg);
    margin-bottom: var(--spacing-sm);
    padding-bottom: var(--spacing-xs);
    border-bottom-width: 1px;
    border-bottom-color: rgba(180, 120, 60, 0.25);
}

/* First section header needs no top margin */
#settings-panel .settings-section-header:first-child {
    margin-top: 0;
}

/* Slider rows */
.settings-slider-row {
    flex-direction: row;
    align-items: center;
    margin-bottom: var(--spacing-md);
    min-height: 96px;
}

.settings-slider-label {
    width: 180px;
    color: rgb(160, 140, 110);
    font-size: var(--font-md);
}

.settings-slider-value {
    width: 100px;
    color: rgb(230, 210, 180);
    font-size: var(--font-md);
    -unity-text-align: middle-right;
    margin-left: var(--spacing-sm);
}

/* Slider track and dragger */
#settings-panel .unity-base-slider {
    flex-grow: 1;
    min-height: 80px;
}

#settings-panel .unity-base-slider .unity-base-slider__dragger {
    width: 64px;
    height: 64px;
    border-radius: 32px;
    background-color: rgb(255, 170, 60);
}

#settings-panel .unity-base-slider .unity-base-slider__tracker {
    height: 12px;
    background-color: rgba(180, 120, 60, 0.3);
    border-radius: 6px;
}

/* Info rows (key-value pairs) */
.settings-info-row {
    flex-direction: row;
    justify-content: space-between;
    align-items: center;
    min-height: 72px;
    padding-bottom: var(--spacing-xs);
}

.settings-info-label {
    color: rgb(160, 140, 110);
    font-size: var(--font-md);
}

.settings-info-value {
    color: rgb(230, 210, 180);
    font-size: var(--font-md);
    -unity-text-align: middle-right;
}

/* Delete button */
.settings-delete-btn {
    min-height: 96px;
    background-color: rgba(140, 40, 30, 0.7);
    border-width: 1px;
    border-color: rgba(200, 80, 60, 0.4);
    border-radius: var(--radius-sm);
    color: rgb(255, 220, 200);
    font-size: var(--font-md);
    -unity-text-align: middle-center;
    margin-top: var(--spacing-sm);
}

.settings-delete-btn:hover {
    background-color: rgba(160, 50, 35, 0.85);
}

/* Inline confirmation row */
.settings-confirm-row {
    flex-direction: row;
    align-items: center;
    margin-top: var(--spacing-sm);
}

.settings-confirm-label {
    flex-grow: 1;
    color: rgb(220, 90, 70);
    font-size: var(--font-md);
    -unity-font-style: bold;
}

.settings-confirm-cancel {
    min-height: 96px;
    min-width: 180px;
    background-color: rgba(60, 40, 20, 0.6);
    border-width: 1px;
    border-color: rgba(140, 100, 50, 0.3);
    border-radius: var(--radius-sm);
    color: rgb(220, 200, 160);
    font-size: var(--font-md);
    -unity-text-align: middle-center;
    margin-right: var(--spacing-sm);
}

.settings-confirm-cancel:hover {
    background-color: rgba(80, 50, 25, 0.7);
}

.settings-confirm-delete {
    min-height: 96px;
    min-width: 180px;
    background-color: rgba(180, 40, 30, 0.8);
    border-width: 1px;
    border-color: rgba(220, 80, 60, 0.5);
    border-radius: var(--radius-sm);
    color: rgb(255, 220, 200);
    font-size: var(--font-md);
    -unity-font-style: bold;
    -unity-text-align: middle-center;
}

.settings-confirm-delete:hover {
    background-color: rgba(200, 50, 35, 0.9);
}
```

- [ ] **Step 2: Add stylesheet reference to CampFireRoot.uxml**

In `Assets/UI/Documents/CampFireRoot.uxml`, add after the existing `<Style>` lines (after line 20, near the other style refs):

```xml
<Style src="project://database/Assets/UI/Styles/Settings.uss" />
```

- [ ] **Step 3: Commit**

```bash
git add Assets/UI/Styles/Settings.uss Assets/UI/Styles/Settings.uss.meta Assets/UI/Documents/CampFireRoot.uxml
git commit -m "feat(settings): add Settings.uss stylesheet"
```

---

### Task 2: Replace UXML markup for settings panel

**Files:**
- Modify: `Assets/UI/Documents/CampFireRoot.uxml` (lines 230-244)

- [ ] **Step 1: Replace the settings panel markup**

In `Assets/UI/Documents/CampFireRoot.uxml`, replace the entire `settings-panel` block (lines 230-244):

Old:
```xml
                    <!-- Settings panel -->
                    <ui:VisualElement name="settings-panel">
                        <ui:ScrollView name="settings-scroll" vertical-scroller-visibility="Hidden" horizontal-scroller-visibility="Hidden" touch-scroll-type="Clamped" elasticity="0">
                        <ui:VisualElement class="debug-row">
                            <ui:Label text="Music" class="debug-label" />
                            <ui:Slider name="music-slider" low-value="0" high-value="100" value="100" />
                            <ui:Label name="music-value" text="100%" class="debug-value" />
                        </ui:VisualElement>
                        <ui:VisualElement class="debug-row">
                            <ui:Label text="Sound FX" class="debug-label" />
                            <ui:Slider name="sfx-slider" low-value="0" high-value="100" value="100" />
                            <ui:Label name="sfx-value" text="100%" class="debug-value" />
                        </ui:VisualElement>
                        </ui:ScrollView>
                    </ui:VisualElement>
```

New:
```xml
                    <!-- Settings panel -->
                    <ui:VisualElement name="settings-panel">
                        <ui:ScrollView name="settings-scroll" vertical-scroller-visibility="Hidden" horizontal-scroller-visibility="Hidden" touch-scroll-type="Clamped" elasticity="0">

                        <!-- Audio section -->
                        <ui:Label text="AUDIO" class="settings-section-header" />
                        <ui:VisualElement class="settings-slider-row">
                            <ui:Label text="Music" class="settings-slider-label" />
                            <ui:Slider name="music-slider" low-value="0" high-value="100" value="100" />
                            <ui:Label name="music-value" text="100%" class="settings-slider-value" />
                        </ui:VisualElement>
                        <ui:VisualElement class="settings-slider-row">
                            <ui:Label text="Sound FX" class="settings-slider-label" />
                            <ui:Slider name="sfx-slider" low-value="0" high-value="100" value="100" />
                            <ui:Label name="sfx-value" text="100%" class="settings-slider-value" />
                        </ui:VisualElement>

                        <!-- Account section -->
                        <ui:Label text="ACCOUNT" class="settings-section-header" />
                        <ui:VisualElement class="settings-info-row">
                            <ui:Label text="Player" class="settings-info-label" />
                            <ui:Label name="settings-player-id" text="---" class="settings-info-value" />
                        </ui:VisualElement>
                        <ui:VisualElement class="settings-info-row">
                            <ui:Label text="Server" class="settings-info-label" />
                            <ui:Label name="settings-server" text="---" class="settings-info-value" />
                        </ui:VisualElement>
                        <ui:VisualElement class="settings-info-row">
                            <ui:Label text="Version" class="settings-info-label" />
                            <ui:Label name="settings-version" text="---" class="settings-info-value" />
                        </ui:VisualElement>

                        <!-- Danger zone section -->
                        <ui:Label text="DANGER ZONE" class="settings-section-header" />
                        <ui:Button name="settings-delete-btn" text="Delete Save Data" class="settings-delete-btn" />
                        <ui:VisualElement name="settings-confirm-row" class="settings-confirm-row" style="display: none;">
                            <ui:Label text="Are you sure?" class="settings-confirm-label" />
                            <ui:Button name="settings-confirm-cancel" text="Cancel" class="settings-confirm-cancel" />
                            <ui:Button name="settings-confirm-delete" text="Delete" class="settings-confirm-delete" />
                        </ui:VisualElement>

                        </ui:ScrollView>
                    </ui:VisualElement>
```

- [ ] **Step 2: Verify in Unity**

Use `read_console` MCP tool to check for any UXML parse errors after saving.

- [ ] **Step 3: Commit**

```bash
git add Assets/UI/Documents/CampFireRoot.uxml
git commit -m "feat(settings): replace settings panel UXML with proper sections"
```

---

### Task 3: Rewrite SettingsUI.cs with account info and delete flow

**Files:**
- Modify: `Assets/Scripts/UI/SettingsUI.cs`

- [ ] **Step 1: Rewrite SettingsUI.cs**

Replace the entire contents of `Assets/Scripts/UI/SettingsUI.cs`:

```csharp
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Garden
{
    public class SettingsUI : MonoBehaviour
    {
        private Slider _musicSlider;
        private Slider _sfxSlider;
        private Label _musicValue;
        private Label _sfxValue;

        private Label _playerIdLabel;
        private Label _serverLabel;
        private Label _versionLabel;

        private Button _deleteBtn;
        private VisualElement _confirmRow;
        private Button _confirmCancel;
        private Button _confirmDelete;

        public void Initialize(VisualElement root)
        {
            // Audio
            _musicSlider = root.Q<Slider>("music-slider");
            _sfxSlider = root.Q<Slider>("sfx-slider");
            _musicValue = root.Q<Label>("music-value");
            _sfxValue = root.Q<Label>("sfx-value");

            var data = SaveManager.Instance?.Data;
            if (data != null)
            {
                _musicSlider.value = data.musicVolume * 100f;
                _sfxSlider.value = data.sfxVolume * 100f;
                _musicValue.text = $"{data.musicVolume * 100f:F0}%";
                _sfxValue.text = $"{data.sfxVolume * 100f:F0}%";
            }

            _musicSlider.RegisterValueChangedCallback(evt =>
            {
                float vol = evt.newValue / 100f;
                AudioManager.Instance?.SetMusicVolume(vol);
                _musicValue.text = $"{evt.newValue:F0}%";
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
                _sfxValue.text = $"{evt.newValue:F0}%";
                if (SaveManager.Instance?.Data != null)
                {
                    SaveManager.Instance.Data.sfxVolume = vol;
                    SaveManager.Instance.Save();
                }
            });

            // Account info
            _playerIdLabel = root.Q<Label>("settings-player-id");
            _serverLabel = root.Q<Label>("settings-server");
            _versionLabel = root.Q<Label>("settings-version");

            var socialData = SocialSaveManager.Instance?.Data;
            _playerIdLabel.text = !string.IsNullOrEmpty(socialData?.uid) ? socialData.uid : "---";
            _serverLabel.text = ServerConfig.Current.name;
            _versionLabel.text = Application.version;

            // Delete save
            _deleteBtn = root.Q<Button>("settings-delete-btn");
            _confirmRow = root.Q<VisualElement>("settings-confirm-row");
            _confirmCancel = root.Q<Button>("settings-confirm-cancel");
            _confirmDelete = root.Q<Button>("settings-confirm-delete");

            _deleteBtn.clicked += () =>
            {
                _deleteBtn.style.display = DisplayStyle.None;
                _confirmRow.style.display = DisplayStyle.Flex;
            };

            _confirmCancel.clicked += () =>
            {
                _confirmRow.style.display = DisplayStyle.None;
                _deleteBtn.style.display = DisplayStyle.Flex;
            };

            _confirmDelete.clicked += () =>
            {
                SaveManager.Instance?.DeleteSave();
                SocialSaveManager.Instance?.DeleteSave();
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            };
        }
    }
}
```

- [ ] **Step 2: Verify compilation**

Use `read_console` MCP tool to check for compilation errors.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/UI/SettingsUI.cs
git commit -m "feat(settings): add account info and delete save flow"
```

---

### Task 4: Manual verification in Unity

- [ ] **Step 1: Open the game in Unity Editor and enter play mode**

Use `manage_editor` MCP tool to enter play mode.

- [ ] **Step 2: Open the Settings panel**

Tap the gear icon to open Settings. Verify:
- Three section headers visible: AUDIO, ACCOUNT, DANGER ZONE
- Music and SFX sliders work and display percentage
- Account info shows Player ID, Server name, and Version
- Styling matches the camp-fire warm palette

- [ ] **Step 3: Test delete confirmation flow**

Tap "Delete Save Data" → verify it swaps to "Are you sure?" with Cancel/Delete buttons.
Tap "Cancel" → verify it swaps back to the delete button.

- [ ] **Step 4: Exit play mode and commit any adjustments**

```bash
git add -A
git commit -m "fix(settings): visual adjustments from testing"
```

(Only if adjustments were needed.)
