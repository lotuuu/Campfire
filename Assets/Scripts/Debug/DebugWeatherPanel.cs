using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class DebugWeatherPanel : MonoBehaviour
    {
        private VisualElement panel;
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
            timeOverrideField = root.Q<TextField>("time-override-field");
            currentTimeLabel = root.Q<Label>("current-time-label");

            // Slider callbacks
            tempSlider?.RegisterValueChangedCallback(evt => tempValue.text = $"{evt.newValue:F0}\u00b0C");
            humiditySlider?.RegisterValueChangedCallback(evt => humidityValue.text = $"{evt.newValue:F0}%");
            windSlider?.RegisterValueChangedCallback(evt => windValue.text = $"{evt.newValue:F1} m/s");

            // Default dropdown indices
            if (conditionDropdown != null) conditionDropdown.index = 0;
            if (moonPhaseDropdown != null) moonPhaseDropdown.index = 0;
            if (timeOfDayDropdown != null) timeOfDayDropdown.index = 0;
            if (calendarEventDropdown != null) calendarEventDropdown.index = 0;

            // Wire buttons
            root.Q<Button>("apply-button")?.RegisterCallback<ClickEvent>(_ => ApplySettings());

            root.Q<Button>("blizzard-button")?.RegisterCallback<ClickEvent>(_ =>
                ApplyPreset(-5, 60, 15, 4, 0, 4));
            root.Q<Button>("thunderstorm-button")?.RegisterCallback<ClickEvent>(_ =>
                ApplyPreset(20, 90, 25, 3, 0, 3));
            root.Q<Button>("clear-night-button")?.RegisterCallback<ClickEvent>(_ =>
                ApplyPreset(15, 40, 2, 0, 1, 4));
            root.Q<Button>("golden-hour-button")?.RegisterCallback<ClickEvent>(_ =>
                ApplyPreset(22, 50, 5, 0, 2, 1));

            root.Q<Button>("time-skip-button")?.RegisterCallback<ClickEvent>(_ => SkipTime());
            root.Q<Button>("set-time-button")?.RegisterCallback<ClickEvent>(_ => SetTimeOverride());
            root.Q<Button>("reset-time-button")?.RegisterCallback<ClickEvent>(_ => ResetTimeOverride());
            var freeModeToggle = root.Q<Toggle>("free-mode-toggle");
            if (freeModeToggle != null)
            {
                freeModeToggle.value = CurrencyManager.FreeMode;
                freeModeToggle.RegisterValueChangedCallback(evt =>
                {
                    CurrencyManager.FreeMode = evt.newValue;
                    Debug.Log($"[Debug] Free Mode {(evt.newValue ? "ON" : "OFF")}");
                });
            }
            root.Q<Button>("max-currency-button")?.RegisterCallback<ClickEvent>(_ => MaxCurrency());
            root.Q<Button>("clear-save-button")?.RegisterCallback<ClickEvent>(_ => ClearSaveData());

            if (timeOverrideField != null)
                timeOverrideField.value = GameTime.Now.ToString("yyyy-MM-dd HH:mm");
        }

        private void Update()
        {
            if (currentTimeLabel != null && panel != null && panel.resolvedStyle.display == DisplayStyle.Flex)
            {
                var prefix = GameTime.IsOverridden ? "[OVERRIDDEN] " : "";
                currentTimeLabel.text = $"{prefix}{GameTime.Now:yyyy-MM-dd HH:mm:ss}";
            }
        }

        private void ApplyPreset(float temp, float humidity, float wind, int condIdx, int todIdx, int moonIdx)
        {
            if (tempSlider != null) tempSlider.value = temp;
            if (humiditySlider != null) humiditySlider.value = humidity;
            if (windSlider != null) windSlider.value = wind;
            if (conditionDropdown != null) conditionDropdown.index = condIdx;
            if (timeOfDayDropdown != null) timeOfDayDropdown.index = todIdx;
            if (moonPhaseDropdown != null) moonPhaseDropdown.index = moonIdx;
            ApplySettings();
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

        private void SkipTime()
        {
            int hours = Mathf.Max(1, timeSkipField != null ? timeSkipField.value : 1);
            var target = GameTime.Now.AddHours(hours);
            GameTime.SetOverride(target);
            Debug.Log($"[Debug] Skipped {hours} hour(s) forward.");
        }

        private void SetTimeOverride()
        {
            if (timeOverrideField == null) return;
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
            if (timeOverrideField != null)
                timeOverrideField.value = GameTime.Now.ToString("yyyy-MM-dd HH:mm");
            Debug.Log("[Debug] Time override cleared.");
        }

        private void MaxCurrency()
        {
            CurrencyManager.Instance?.GrantInfiniteGems();
            CurrencyManager.Instance?.AddMana(999999f);
            Debug.Log("[Debug] Currencies maxed out.");
        }

        private void ClearSaveData()
        {
            SaveManager.Instance.DeleteSave();
            Debug.Log("[Debug] Save data cleared. Reloading scene...");
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }
    }
}
