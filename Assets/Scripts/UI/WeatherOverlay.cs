using System.Collections;
using UnityEngine;

namespace Garden
{
    public class WeatherOverlay : MonoBehaviour
    {
        [SerializeField] private RainOverlay rainOverlay;
        private Coroutine _waitCoroutine;

        private void OnEnable()
        {
            rainOverlay?.Hide();
            if (WeatherService.Instance != null)
            {
                WeatherService.Instance.OnWeatherUpdated += UpdateEffects;
                UpdateEffects(WeatherService.Instance.CurrentWeather);
            }
            else
            {
                _waitCoroutine = StartCoroutine(WaitForWeatherService());
            }
        }

        private void OnDisable()
        {
            if (_waitCoroutine != null) { StopCoroutine(_waitCoroutine); _waitCoroutine = null; }
            if (WeatherService.Instance != null)
                WeatherService.Instance.OnWeatherUpdated -= UpdateEffects;
        }

        private IEnumerator WaitForWeatherService()
        {
            while (WeatherService.Instance == null)
                yield return null;
            _waitCoroutine = null;
            WeatherService.Instance.OnWeatherUpdated += UpdateEffects;
            UpdateEffects(WeatherService.Instance.CurrentWeather);
        }

        private void UpdateEffects(WeatherData w)
        {
            switch (w.condition)
            {
                case WeatherCondition.Rain:  rainOverlay?.Show(storm: false); break;
                case WeatherCondition.Storm: rainOverlay?.Show(storm: true);  break;
                default:                     rainOverlay?.Hide();             break;
            }
        }
    }
}
