using UnityEngine;
using TMPro;

namespace Garden
{
    public class CurrencyDisplay : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI dewdropsText;
        [SerializeField] private TextMeshProUGUI sunShardsText;
        [SerializeField] private TextMeshProUGUI auraDustText;

        private void OnEnable()
        {
            if (CurrencyManager.Instance != null)
                CurrencyManager.Instance.OnCurrencyChanged += OnChanged;
            Refresh();
        }

        private void Start()
        {
            if (CurrencyManager.Instance != null)
                CurrencyManager.Instance.OnCurrencyChanged += OnChanged;
            Refresh();
        }

        private void OnDisable()
        {
            if (CurrencyManager.Instance != null)
                CurrencyManager.Instance.OnCurrencyChanged -= OnChanged;
        }

        private void OnChanged(CurrencyType type, int oldVal, int newVal) => Refresh();

        private void Refresh()
        {
            var cm = CurrencyManager.Instance;
            if (cm == null) return;
            dewdropsText.text = $"Dew: {cm.Dewdrops}";
            sunShardsText.text = $"Sun: {cm.SunShards}";
            auraDustText.text = $"Dust: {cm.AuraDust}";
        }
    }
}
