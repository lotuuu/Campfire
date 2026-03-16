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

            // Language
            var langDropdown = root.Q<DropdownField>("language-dropdown");
            if (langDropdown != null && LocalizationService.Instance != null)
            {
                langDropdown.choices = LocalizationService.Instance.SupportedLocales;
                langDropdown.value = LocalizationService.Instance.CurrentLocale;
                langDropdown.RegisterValueChangedCallback(evt =>
                {
                    _ = LocalizationService.Instance.SwitchLocale(evt.newValue);
                });
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
    }
}
