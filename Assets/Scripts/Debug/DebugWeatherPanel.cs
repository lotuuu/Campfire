using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Garden
{
    public class DebugWeatherPanel : MonoBehaviour
    {
        [Header("Controls")]
        [SerializeField] private Toggle debugModeToggle;
        [SerializeField] private Slider tempSlider;
        [SerializeField] private Slider humiditySlider;
        [SerializeField] private Slider windSlider;
        [SerializeField] private TMP_Dropdown conditionDropdown;
        [SerializeField] private TMP_Dropdown moonPhaseDropdown;
        [SerializeField] private TMP_Dropdown timeOfDayDropdown;
        [SerializeField] private TMP_Dropdown calendarEventDropdown;

        [Header("Labels")]
        [SerializeField] private TextMeshProUGUI tempLabel;
        [SerializeField] private TextMeshProUGUI humidityLabel;
        [SerializeField] private TextMeshProUGUI windLabel;

        [Header("Preset Buttons")]
        [SerializeField] private Button blizzardButton;
        [SerializeField] private Button thunderstormButton;
        [SerializeField] private Button clearNightButton;
        [SerializeField] private Button goldenHourButton;
        [SerializeField] private Button applyButton;
        [SerializeField] private Button closeButton;

        private void Start()
        {
            tempSlider.minValue = -20; tempSlider.maxValue = 50;
            humiditySlider.minValue = 0; humiditySlider.maxValue = 100;
            windSlider.minValue = 0; windSlider.maxValue = 50;

            tempSlider.onValueChanged.AddListener(v => tempLabel.text = $"{v:F0}\u00b0C");
            humiditySlider.onValueChanged.AddListener(v => humidityLabel.text = $"{v:F0}%");
            windSlider.onValueChanged.AddListener(v => windLabel.text = $"{v:F1} m/s");

            applyButton.onClick.AddListener(ApplySettings);
            closeButton.onClick.AddListener(() => gameObject.SetActive(false));

            blizzardButton?.onClick.AddListener(() => ApplyPreset(-5, 60, 15, WeatherCondition.Snow, TimeOfDay.Day, MoonPhase.FullMoon));
            thunderstormButton?.onClick.AddListener(() => ApplyPreset(20, 90, 25, WeatherCondition.Storm, TimeOfDay.Day, MoonPhase.WaxingGibbous));
            clearNightButton?.onClick.AddListener(() => ApplyPreset(15, 40, 2, WeatherCondition.Clear, TimeOfDay.Night, MoonPhase.FullMoon));
            goldenHourButton?.onClick.AddListener(() => ApplyPreset(22, 50, 5, WeatherCondition.Clear, TimeOfDay.GoldenHour, MoonPhase.WaxingCrescent));

            tempSlider.value = 22;
            humiditySlider.value = 50;
            windSlider.value = 3;
        }

        private void ApplyPreset(float temp, float humidity, float wind, WeatherCondition cond, TimeOfDay tod, MoonPhase moon)
        {
            tempSlider.value = temp;
            humiditySlider.value = humidity;
            windSlider.value = wind;
            conditionDropdown.value = (int)cond;
            timeOfDayDropdown.value = (int)tod;
            moonPhaseDropdown.value = (int)moon;
            ApplySettings();
        }

        private void ApplySettings()
        {
            var weather = new WeatherData
            {
                temperature = tempSlider.value,
                humidity = humiditySlider.value,
                windSpeed = windSlider.value,
                condition = (WeatherCondition)conditionDropdown.value,
                timeOfDay = (TimeOfDay)timeOfDayDropdown.value,
                isNight = (TimeOfDay)timeOfDayDropdown.value == TimeOfDay.Night,
                isGoldenHour = (TimeOfDay)timeOfDayDropdown.value == TimeOfDay.GoldenHour,
                moonPhase = (MoonPhase)moonPhaseDropdown.value,
                calendarEvent = (CalendarEvent)calendarEventDropdown.value,
                cloudCover = conditionDropdown.value >= 1 ? 70f : 10f
            };

            WeatherService.Instance.SetDebugWeather(weather);
        }
    }
}
