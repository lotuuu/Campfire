using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class ResonanceBar : MonoBehaviour
    {
        private Label weatherText;

        public void Initialize(VisualElement root)
        {
            weatherText = root.Q<Label>("weather-text");
        }

        private void OnEnable()
        {
            if (WeatherService.Instance != null)
                WeatherService.Instance.OnWeatherUpdated += UpdateDisplay;
        }

        private void OnDisable()
        {
            if (WeatherService.Instance != null)
                WeatherService.Instance.OnWeatherUpdated -= UpdateDisplay;
        }

        private void Start()
        {
            if (WeatherService.Instance != null)
                UpdateDisplay(WeatherService.Instance.CurrentWeather);
        }

        private void UpdateDisplay(WeatherData w)
        {
            if (weatherText == null) return;
            string temp = $"{w.temperature:F0}\u00b0C";
            string condition = w.condition.ToString();
            string moon = FormatMoonPhase(w.moonPhase);
            weatherText.text = $"{temp}  \u2022  {condition}  \u2022  {moon}";
        }

        private string FormatMoonPhase(MoonPhase phase) => phase switch
        {
            MoonPhase.NewMoon => "New Moon",
            MoonPhase.WaxingCrescent => "Waxing Crescent",
            MoonPhase.FirstQuarter => "First Quarter",
            MoonPhase.WaxingGibbous => "Waxing Gibbous",
            MoonPhase.FullMoon => "Full Moon",
            MoonPhase.WaningGibbous => "Waning Gibbous",
            MoonPhase.LastQuarter => "Last Quarter",
            MoonPhase.WaningCrescent => "Waning Crescent",
            _ => phase.ToString()
        };
    }
}
