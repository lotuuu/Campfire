using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class SettingsUI : MonoBehaviour
    {
        private Slider _musicSlider;
        private Slider _sfxSlider;
        private Label _musicValue;
        private Label _sfxValue;

        public void Initialize(VisualElement root)
        {
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
        }
    }
}
