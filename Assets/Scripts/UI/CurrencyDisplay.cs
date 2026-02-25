using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class CurrencyDisplay : MonoBehaviour
    {
        private Label goldText;
        private Label sunShardsText;
        private Label auraDustText;

        public void Initialize(VisualElement root)
        {
            goldText = root.Q<Label>("gold-text");
            sunShardsText = root.Q<Label>("sun-shards-text");
            auraDustText = root.Q<Label>("aura-dust-text");

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
            if (cm == null || goldText == null) return;
            goldText.text = $"Gold: {cm.Gold}";
            sunShardsText.text = $"Sun: {cm.SunShards}";
            auraDustText.text = $"Dust: {cm.AuraDust}";
        }
    }
}
