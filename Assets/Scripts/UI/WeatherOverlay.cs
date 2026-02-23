using UnityEngine;

namespace Garden
{
    public class WeatherOverlay : MonoBehaviour
    {
        [SerializeField] private ParticleSystem rainEffect;
        [SerializeField] private ParticleSystem snowEffect;
        [SerializeField] private ParticleSystem windLines;

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
            StopAll();
            if (WeatherService.Instance != null)
                UpdateEffects(WeatherService.Instance.CurrentWeather);
        }

        private void UpdateEffects(WeatherData w)
        {
            StopAll();

            switch (w.condition)
            {
                case WeatherCondition.Rain:
                case WeatherCondition.Storm:
                    rainEffect?.Play();
                    if (w.condition == WeatherCondition.Storm && windLines != null)
                        windLines.Play();
                    break;
                case WeatherCondition.Snow:
                    snowEffect?.Play();
                    break;
            }

            if (w.windSpeed > 5f && windLines != null && !windLines.isPlaying)
                windLines.Play();
        }

        private void StopAll()
        {
            rainEffect?.Stop();
            snowEffect?.Stop();
            windLines?.Stop();
        }
    }
}
