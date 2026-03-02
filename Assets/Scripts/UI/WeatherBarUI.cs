using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class WeatherBarUI : MonoBehaviour
    {
        private Label weatherIcon;
        private Label weatherTemp;
        private Label weatherCondition;
        private VisualElement weatherMoon;

        private VisualElement weatherBar;
        private VisualElement forecastPanel;
        private VisualElement forecastDays;
        private VisualElement campRoot;

        // Moon_Phase_1=FullMoon .. Moon_Phase_5=NewMoon etc.
        private static readonly int[] MoonPhaseToSpriteIndex = { 5, 6, 7, 8, 1, 2, 3, 4 };
        private Texture2D[] moonTextures;

        public void Initialize(VisualElement root)
        {
            weatherIcon = root.Q<Label>("weather-icon");
            weatherTemp = root.Q<Label>("weather-temp");
            weatherCondition = root.Q<Label>("weather-condition");
            weatherMoon = root.Q("weather-moon");

            moonTextures = new Texture2D[8];
            for (int i = 0; i < 8; i++)
                moonTextures[i] = Resources.Load<Texture2D>($"MoonPhases/Moon_Phase_{i + 1}");

            weatherBar = root.Q("weather-bar");
            forecastPanel = root.Q("forecast-panel");
            forecastDays = root.Q("forecast-days");
            campRoot = root.Q("camp-root");

            weatherBar?.RegisterCallback<ClickEvent>(OnWeatherBarClicked);
            campRoot?.RegisterCallback<ClickEvent>(OnRootClicked);

            if (WeatherService.Instance != null)
            {
                WeatherService.Instance.OnWeatherUpdated += UpdateWeather;
                WeatherService.Instance.OnForecastUpdated += PopulateForecast;
                UpdateWeather(WeatherService.Instance.CurrentWeather);
                if (WeatherService.Instance.Forecast.Count > 0)
                    PopulateForecast();
            }
        }

        private void OnDestroy()
        {
            if (WeatherService.Instance != null)
            {
                WeatherService.Instance.OnWeatherUpdated -= UpdateWeather;
                WeatherService.Instance.OnForecastUpdated -= PopulateForecast;
            }
        }

        private void OnWeatherBarClicked(ClickEvent evt)
        {
            if (forecastPanel == null) return;
            forecastPanel.ToggleInClassList("forecast-visible");
            evt.StopPropagation();
        }

        private void OnRootClicked(ClickEvent evt)
        {
            if (forecastPanel == null) return;
            if (!forecastPanel.ClassListContains("forecast-visible")) return;

            // Don't close if clicking on the forecast panel itself
            var target = evt.target as VisualElement;
            while (target != null)
            {
                if (target == forecastPanel || target == weatherBar) return;
                target = target.parent;
            }

            forecastPanel.RemoveFromClassList("forecast-visible");
        }

        private void PopulateForecast()
        {
            if (forecastDays == null) return;
            forecastDays.Clear();

            var forecast = WeatherService.Instance?.Forecast;
            if (forecast == null) return;

            foreach (var day in forecast)
            {
                var col = new VisualElement();
                col.AddToClassList("forecast-day");

                var label = new Label(day.dayLabel);
                label.AddToClassList("forecast-day-label");
                col.Add(label);

                var icon = new Label(GetWeatherEmoji(day.condition));
                icon.AddToClassList("forecast-day-icon");
                col.Add(icon);

                var temp = new Label($"{day.tempHigh:F0}/{day.tempLow:F0}");
                temp.AddToClassList("forecast-day-temp");
                col.Add(temp);

                forecastDays.Add(col);
            }
        }

        private void UpdateWeather(WeatherData weather)
        {
            if (weatherIcon != null) weatherIcon.text = GetWeatherEmoji(weather.condition);
            if (weatherTemp != null) weatherTemp.text = $"{weather.temperature:F0}\u00b0C";
            if (weatherCondition != null) weatherCondition.text = weather.condition.ToString();
            if (weatherMoon != null)
            {
                int spriteIdx = MoonPhaseToSpriteIndex[(int)weather.moonPhase] - 1;
                var tex = moonTextures[spriteIdx];
                if (tex != null)
                    weatherMoon.style.backgroundImage = tex;
            }
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

    }
}
