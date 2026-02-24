using System.Collections;
using UnityEngine;

namespace Garden
{
    public class WeatherOverlay : MonoBehaviour
    {
        [SerializeField] private RainOverlay rainOverlay;

        private void OnEnable()
        {
            if (WeatherService.Instance != null)
                WeatherService.Instance.OnWeatherUpdated += UpdateEffects;
        }

        private void OnDisable()
        {
            if (WeatherService.Instance != null)
                WeatherService.Instance.OnWeatherUpdated -= UpdateEffects;
        }

        private void Start()
        {
            rainOverlay?.Hide();

            if (WeatherService.Instance != null)
            {
                WeatherService.Instance.OnWeatherUpdated -= UpdateEffects;
                WeatherService.Instance.OnWeatherUpdated += UpdateEffects;
                UpdateEffects(WeatherService.Instance.CurrentWeather);
            }
            else
            {
                StartCoroutine(WaitForWeatherService());
            }
        }

        private IEnumerator WaitForWeatherService()
        {
            while (WeatherService.Instance == null)
                yield return null;
            WeatherService.Instance.OnWeatherUpdated -= UpdateEffects;
            WeatherService.Instance.OnWeatherUpdated += UpdateEffects;
            UpdateEffects(WeatherService.Instance.CurrentWeather);
        }

        private void UpdateEffects(WeatherData w)
        {
            switch (w.condition)
            {
                case WeatherCondition.Rain:
                    rainOverlay?.Show(storm: false);
                    break;
                case WeatherCondition.Storm:
                    rainOverlay?.Show(storm: true);
                    break;
                default:
                    rainOverlay?.Hide();
                    break;
            }
        }
    }
}
