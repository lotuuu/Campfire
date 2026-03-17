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
        private DropdownField _langDropdown;

        private Label _headerLanguage, _headerAudio, _headerAccount, _headerDanger;
        private Label _labelMusic, _labelSfx, _labelPlayer, _labelServer, _labelVersion, _labelConfirm;

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

            // Localizable labels
            _headerLanguage = root.Q<Label>("settings-header-language");
            _headerAudio = root.Q<Label>("settings-header-audio");
            _headerAccount = root.Q<Label>("settings-header-account");
            _headerDanger = root.Q<Label>("settings-header-danger");
            _labelMusic = root.Q<Label>("settings-label-music");
            _labelSfx = root.Q<Label>("settings-label-sfx");
            _labelPlayer = root.Q<Label>("settings-label-player");
            _labelServer = root.Q<Label>("settings-label-server");
            _labelVersion = root.Q<Label>("settings-label-version");
            _labelConfirm = root.Q<Label>("settings-label-confirm");
            RefreshLabels();

            // Language
            _langDropdown = root.Q<DropdownField>("language-dropdown");
            if (_langDropdown != null)
            {
                RefreshLanguageDropdown();
                _langDropdown.RegisterValueChangedCallback(evt =>
                {
                    if (LocalizationService.Instance != null)
                        _ = LocalizationService.Instance.SwitchLocale(evt.newValue);
                });
                if (LocalizationService.Instance != null)
                    LocalizationService.Instance.OnLocaleChanged += OnLocaleChanged;
            }

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

        public void RefreshLanguageDropdown()
        {
            if (_langDropdown == null || LocalizationService.Instance == null) return;
            _langDropdown.choices = LocalizationService.Instance.SupportedLocales;
            _langDropdown.SetValueWithoutNotify(LocalizationService.Instance.CurrentLocale);
        }

        private void OnLocaleChanged()
        {
            RefreshLanguageDropdown();
            RefreshLabels();
        }

        private void RefreshLabels()
        {
            if (_headerLanguage != null) _headerLanguage.text = Loc.Get("ui.settings.language", "LANGUAGE");
            if (_headerAudio != null) _headerAudio.text = Loc.Get("ui.settings.audio", "AUDIO");
            if (_headerAccount != null) _headerAccount.text = Loc.Get("ui.settings.account", "ACCOUNT");
            if (_headerDanger != null) _headerDanger.text = Loc.Get("ui.settings.danger_zone", "DANGER ZONE");
            if (_labelMusic != null) _labelMusic.text = Loc.Get("ui.settings.music", "Music");
            if (_labelSfx != null) _labelSfx.text = Loc.Get("ui.settings.sfx", "Sound FX");
            if (_labelPlayer != null) _labelPlayer.text = Loc.Get("ui.settings.player", "Player");
            if (_labelServer != null) _labelServer.text = Loc.Get("ui.settings.server", "Server");
            if (_labelVersion != null) _labelVersion.text = Loc.Get("ui.settings.version", "Version");
            if (_labelConfirm != null) _labelConfirm.text = Loc.Get("ui.settings.confirm", "Are you sure?");
            if (_deleteBtn != null) _deleteBtn.text = Loc.Get("ui.settings.delete_save", "Delete Save Data");
            if (_confirmCancel != null) _confirmCancel.text = Loc.Get("ui.button.cancel", "Cancel");
            if (_confirmDelete != null) _confirmDelete.text = Loc.Get("ui.settings.delete", "Delete");
        }

        private void OnDestroy()
        {
            if (LocalizationService.Instance != null)
                LocalizationService.Instance.OnLocaleChanged -= OnLocaleChanged;
        }
    }
}
