using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class DebugWeatherPanel : MonoBehaviour
    {
        private VisualElement panel;
        private Toggle debugModeToggle;
        private Slider tempSlider;
        private Slider humiditySlider;
        private Slider windSlider;
        private DropdownField conditionDropdown;
        private DropdownField moonPhaseDropdown;
        private DropdownField timeOfDayDropdown;
        private DropdownField calendarEventDropdown;
        private Label tempValue;
        private Label humidityValue;
        private Label windValue;
        private IntegerField timeSkipField;
        private TextField timeOverrideField;
        private Label currentTimeLabel;

        public void Initialize(VisualElement root)
        {
            panel = root.Q<VisualElement>("debug-panel");
            debugModeToggle = root.Q<Toggle>("debug-mode-toggle");
            tempSlider = root.Q<Slider>("temp-slider");
            humiditySlider = root.Q<Slider>("humidity-slider");
            windSlider = root.Q<Slider>("wind-slider");
            conditionDropdown = root.Q<DropdownField>("condition-dropdown");
            moonPhaseDropdown = root.Q<DropdownField>("moon-phase-dropdown");
            timeOfDayDropdown = root.Q<DropdownField>("time-of-day-dropdown");
            calendarEventDropdown = root.Q<DropdownField>("calendar-event-dropdown");
            tempValue = root.Q<Label>("temp-value");
            humidityValue = root.Q<Label>("humidity-value");
            windValue = root.Q<Label>("wind-value");

            timeSkipField = root.Q<IntegerField>("time-skip-field");
            root.Q<Button>("time-skip-button").clicked += SkipTime;
            root.Q<Button>("max-currency-button").clicked += MaxCurrency;
            root.Q<Button>("clear-save-button").clicked += ClearSaveData;

            timeOverrideField = root.Q<TextField>("time-override-field");
            currentTimeLabel = root.Q<Label>("current-time-label");
            root.Q<Button>("set-time-button").clicked += SetTimeOverride;
            root.Q<Button>("reset-time-button").clicked += ResetTimeOverride;
            timeOverrideField.value = GameTime.Now.ToString("yyyy-MM-dd HH:mm");

            // Slider callbacks
            tempSlider.RegisterValueChangedCallback(evt => tempValue.text = $"{evt.newValue:F0}\u00b0C");
            humiditySlider.RegisterValueChangedCallback(evt => humidityValue.text = $"{evt.newValue:F0}%");
            windSlider.RegisterValueChangedCallback(evt => windValue.text = $"{evt.newValue:F1} m/s");

            // Set default dropdown indices
            conditionDropdown.index = 0;
            moonPhaseDropdown.index = 0;
            timeOfDayDropdown.index = 0;
            calendarEventDropdown.index = 0;

            // Buttons
            root.Q<Button>("debug-toggle")?.RegisterCallback<ClickEvent>(_ => Show());
            root.Q<Button>("apply-button").clicked += ApplySettings;
            root.Q<Button>("debug-close").clicked += Hide;
            root.Q<Button>("blizzard-button").clicked += () =>
                ApplyPreset(-5, 60, 15, 4, 0, 4);   // Snow, Day, FullMoon
            root.Q<Button>("thunderstorm-button").clicked += () =>
                ApplyPreset(20, 90, 25, 3, 0, 3);    // Storm, Day, WaxingGibbous
            root.Q<Button>("clear-night-button").clicked += () =>
                ApplyPreset(15, 40, 2, 0, 1, 4);     // Clear, Night, FullMoon
            root.Q<Button>("golden-hour-button").clicked += () =>
                ApplyPreset(22, 50, 5, 0, 2, 1);     // Clear, GoldenHour, WaxingCrescent
        }

        public void Show()
        {
            panel.style.display = DisplayStyle.Flex;
        }

        public void Hide()
        {
            panel.style.display = DisplayStyle.None;
        }

        private void ApplyPreset(float temp, float humidity, float wind, int condIdx, int todIdx, int moonIdx)
        {
            tempSlider.value = temp;
            humiditySlider.value = humidity;
            windSlider.value = wind;
            conditionDropdown.index = condIdx;
            timeOfDayDropdown.index = todIdx;
            moonPhaseDropdown.index = moonIdx;
            ApplySettings();
        }

        private void Update()
        {
            if (currentTimeLabel != null && panel.style.display == DisplayStyle.Flex)
            {
                var label = GameTime.IsOverridden ? "[OVERRIDDEN] " : "";
                currentTimeLabel.text = $"{label}{GameTime.Now:yyyy-MM-dd HH:mm:ss}";
            }
        }

        private void SetTimeOverride()
        {
            if (DateTime.TryParseExact(timeOverrideField.value, "yyyy-MM-dd HH:mm",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var target))
            {
                GameTime.SetOverride(target);
                Debug.Log($"[Debug] Time overridden to {target:yyyy-MM-dd HH:mm}");
            }
            else
            {
                Debug.LogWarning("[Debug] Invalid format. Use yyyy-MM-dd HH:mm");
            }
        }

        private void ResetTimeOverride()
        {
            GameTime.ClearOverride();
            timeOverrideField.value = GameTime.Now.ToString("yyyy-MM-dd HH:mm");
            Debug.Log("[Debug] Time override cleared.");
        }

        private void MaxCurrency()
        {
            var cm = CurrencyManager.Instance;
            cm.Add(CurrencyType.Dewdrops,  int.MaxValue - cm.Dewdrops);
            cm.Add(CurrencyType.SunShards, int.MaxValue - cm.SunShards);
            cm.Add(CurrencyType.AuraDust,  int.MaxValue - cm.AuraDust);
            Debug.Log("[Debug] All currencies set to int.MaxValue.");
        }

        private void ClearSaveData()
        {
            SaveManager.Instance.DeleteSave();
            Debug.Log("[Debug] Save data cleared. Reloading scene...");
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }

        private void SkipTime()
        {
            int hours = Mathf.Max(1, timeSkipField.value);
            PlantManager.Instance?.DebugAdvanceTime(hours);
            GreenhouseManager.Instance?.DebugAdvanceTime(hours);
            Debug.Log($"[Debug] Skipped {hours} hour(s) forward.");
        }

        private void ApplySettings()
        {
            var condition = (WeatherCondition)conditionDropdown.index;
            var timeOfDay = (TimeOfDay)timeOfDayDropdown.index;
            var moonPhase = (MoonPhase)moonPhaseDropdown.index;
            var calendarEvent = (CalendarEvent)calendarEventDropdown.index;

            var weather = new WeatherData
            {
                temperature = tempSlider.value,
                humidity = humiditySlider.value,
                windSpeed = windSlider.value,
                condition = condition,
                timeOfDay = timeOfDay,
                isNight = timeOfDay == TimeOfDay.Night,
                isGoldenHour = timeOfDay == TimeOfDay.GoldenHour,
                moonPhase = moonPhase,
                calendarEvent = calendarEvent,
                cloudCover = conditionDropdown.index >= 1 ? 70f : 10f
            };

            WeatherService.Instance.SetDebugWeather(weather);
        }
    }
}
