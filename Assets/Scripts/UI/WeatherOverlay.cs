using System.Collections;
using UnityEngine;

namespace Garden
{
    public class WeatherOverlay : MonoBehaviour
    {
        [SerializeField] private ParticleSystem rainEffect;
        [SerializeField] private ParticleSystem snowEffect;
        [SerializeField] private ParticleSystem windLines;
        [SerializeField] private float rainEmissionRate = 300f;
        [SerializeField] private float stormEmissionRate = 600f;
        [SerializeField] private float windSpeedThreshold = 5f;

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
            else
                StartCoroutine(WaitForWeatherService());
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
            StopAll();

            switch (w.condition)
            {
                case WeatherCondition.Rain:
                case WeatherCondition.Storm:
                    if (rainEffect != null)
                    {
                        var emission = rainEffect.emission;
                        emission.rateOverTime = w.condition == WeatherCondition.Storm ? stormEmissionRate : rainEmissionRate;
                        rainEffect.Play();
                    }
                    if (w.condition == WeatherCondition.Storm && windLines != null)
                        windLines.Play();
                    break;
                case WeatherCondition.Snow:
                    snowEffect?.Play();
                    break;
            }

            if (w.windSpeed > windSpeedThreshold && windLines != null && !windLines.isPlaying)
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
