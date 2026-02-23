using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class CurrencyDisplay : MonoBehaviour
    {
        private Label dewdropsText;
        private Label sunShardsText;
        private Label auraDustText;

        public void Initialize(VisualElement root)
        {
            dewdropsText = root.Q<Label>("dewdrops-text");
            sunShardsText = root.Q<Label>("sun-shards-text");
            auraDustText = root.Q<Label>("aura-dust-text");
        }

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
            if (cm == null || dewdropsText == null) return;
            dewdropsText.text = $"Dew: {cm.Dewdrops}";
            sunShardsText.text = $"Sun: {cm.SunShards}";
            auraDustText.text = $"Dust: {cm.AuraDust}";
        }
    }
}
