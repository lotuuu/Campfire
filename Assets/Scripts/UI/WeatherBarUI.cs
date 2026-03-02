using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class WeatherBarUI : MonoBehaviour
    {
        private Label weatherIcon;
        private Label weatherTemp;
        private Label weatherCondition;
        private Label weatherMoon;

        public void Initialize(VisualElement root)
        {
            weatherIcon = root.Q<Label>("weather-icon");
            weatherTemp = root.Q<Label>("weather-temp");
            weatherCondition = root.Q<Label>("weather-condition");
            weatherMoon = root.Q<Label>("weather-moon");

            if (WeatherService.Instance != null)
            {
                WeatherService.Instance.OnWeatherUpdated += UpdateWeather;
                UpdateWeather(WeatherService.Instance.CurrentWeather);
            }
        }

        private void OnDestroy()
        {
            if (WeatherService.Instance != null)
                WeatherService.Instance.OnWeatherUpdated -= UpdateWeather;
        }

        private void UpdateWeather(WeatherData weather)
        {
            if (weatherIcon != null) weatherIcon.text = GetWeatherEmoji(weather.condition);
            if (weatherTemp != null) weatherTemp.text = $"{weather.temperature:F0}\u00b0C";
            if (weatherCondition != null) weatherCondition.text = weather.condition.ToString();
            if (weatherMoon != null) weatherMoon.text = GetMoonEmoji(weather.moonPhase);
        }

        private static string GetWeatherEmoji(WeatherCondition c) => c switch
        {
            WeatherCondition.Clear => "\u2600",
            WeatherCondition.Cloudy => "\u2601",
            WeatherCondition.Rain => "\ud83c\udf27",
            WeatherCondition.Storm => "\u26c8",
            WeatherCondition.Snow => "\u2744",
            _ => "?"
        };

        private static string GetMoonEmoji(MoonPhase m) => m switch
        {
            MoonPhase.NewMoon => "\ud83c\udf11",
            MoonPhase.WaxingCrescent => "\ud83c\udf12",
            MoonPhase.FirstQuarter => "\ud83c\udf13",
            MoonPhase.WaxingGibbous => "\ud83c\udf14",
            MoonPhase.FullMoon => "\ud83c\udf15",
            MoonPhase.WaningGibbous => "\ud83c\udf16",
            MoonPhase.LastQuarter => "\ud83c\udf17",
            MoonPhase.WaningCrescent => "\ud83c\udf18",
            _ => "?"
        };
    }
}
