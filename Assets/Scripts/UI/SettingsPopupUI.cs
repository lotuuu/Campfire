using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class SettingsPopupUI : MonoBehaviour
    {
        private VisualElement hudMenu;
        private VisualElement settingsBackdrop;
        private VisualElement settingsPopup;

        private Slider musicSlider;
        private Slider sfxSlider;
        private Label musicValue;
        private Label sfxValue;
        private DropdownField langDropdown;
        private Toggle vibrationToggle;

        private bool isOpen;

        public bool IsOpen => isOpen;

        public void Initialize(VisualElement root)
        {
            hudMenu = root.Q("hud-menu");
            settingsBackdrop = root.Q("settings-backdrop");
            settingsPopup = root.Q("settings-popup");
            settingsBackdrop?.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.target == settingsBackdrop) CloseBloom();
            });

            musicSlider = root.Q<Slider>("settings-music-slider");
            sfxSlider = root.Q<Slider>("settings-sfx-slider");
            musicValue = root.Q<Label>("settings-music-value");
            sfxValue = root.Q<Label>("settings-sfx-value");
            langDropdown = root.Q<DropdownField>("settings-language-dropdown");
            vibrationToggle = root.Q<Toggle>("settings-vibration-toggle");

            hudMenu?.RegisterCallback<ClickEvent>(OnMenuClicked);

            // Audio
            var data = SaveManager.Instance?.Data;
            if (data != null && musicSlider != null && sfxSlider != null)
            {
                musicSlider.value = data.musicVolume * 100f;
                sfxSlider.value = data.sfxVolume * 100f;
                if (musicValue != null) musicValue.text = $"{data.musicVolume * 100f:F0}%";
                if (sfxValue != null) sfxValue.text = $"{data.sfxVolume * 100f:F0}%";
            }

            musicSlider?.RegisterValueChangedCallback(evt =>
            {
                float vol = evt.newValue / 100f;
                AudioManager.Instance?.SetMusicVolume(vol);
                if (musicValue != null) musicValue.text = $"{evt.newValue:F0}%";
                if (SaveManager.Instance?.Data != null)
                {
                    SaveManager.Instance.Data.musicVolume = vol;
                    SaveManager.Instance.Save();
                }
            });

            sfxSlider?.RegisterValueChangedCallback(evt =>
            {
                float vol = evt.newValue / 100f;
                AudioManager.Instance?.SetSFXVolume(vol);
                if (sfxValue != null) sfxValue.text = $"{evt.newValue:F0}%";
                if (SaveManager.Instance?.Data != null)
                {
                    SaveManager.Instance.Data.sfxVolume = vol;
                    SaveManager.Instance.Save();
                }
            });

            // Vibration
            if (vibrationToggle != null && data != null)
            {
                vibrationToggle.value = data.vibrationEnabled;
                vibrationToggle.RegisterValueChangedCallback(evt =>
                {
                    if (SaveManager.Instance?.Data != null)
                    {
                        SaveManager.Instance.Data.vibrationEnabled = evt.newValue;
                        SaveManager.Instance.Save();
                    }
                });
            }

            // Language
            if (langDropdown != null)
            {
                RefreshLanguageDropdown();
                langDropdown.RegisterValueChangedCallback(evt =>
                {
                    if (LocalizationService.Instance != null)
                        _ = LocalizationService.Instance.SwitchLocale(evt.newValue);
                });
            }

            if (LocalizationService.Instance != null)
                LocalizationService.Instance.OnLocaleChanged += OnLocaleChanged;
        }

        private void OnDestroy()
        {
            if (LocalizationService.Instance != null)
                LocalizationService.Instance.OnLocaleChanged -= OnLocaleChanged;
        }

        private void OnLocaleChanged()
        {
            RefreshLanguageDropdown();
        }

        public void RefreshLanguageDropdown()
        {
            if (langDropdown == null || LocalizationService.Instance == null) return;
            langDropdown.choices = LocalizationService.Instance.SupportedLocales;
            langDropdown.SetValueWithoutNotify(LocalizationService.Instance.CurrentLocale);
        }

        private void OnMenuClicked(ClickEvent evt)
        {
            evt.StopPropagation();
            if (isOpen) CloseBloom();
            else OpenBloom();
        }

        public void OpenBloom()
        {
            if (settingsBackdrop == null) return;
            isOpen = true;
            settingsBackdrop.style.display = DisplayStyle.Flex;
            if (settingsPopup != null)
            {
                settingsPopup.style.display = DisplayStyle.Flex;
                settingsPopup.schedule.Execute(() => settingsPopup.AddToClassList("popup-visible"));
            }
        }

        public void CloseBloom()
        {
            if (settingsBackdrop == null) return;
            isOpen = false;
            settingsPopup?.RemoveFromClassList("popup-visible");
            settingsBackdrop.style.display = DisplayStyle.None;
            if (settingsPopup != null) settingsPopup.style.display = DisplayStyle.None;
        }
    }
}
