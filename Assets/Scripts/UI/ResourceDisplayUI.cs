using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class ResourceDisplayUI : MonoBehaviour
    {
        private Label manaDisplay;
        private Label waterDisplay;

        public void Initialize(VisualElement root)
        {
            manaDisplay = root.Q<Label>("mana-display");
            waterDisplay = root.Q<Label>("water-display");

            if (CurrencyManager.Instance != null)
                CurrencyManager.Instance.OnCurrencyChanged += OnCurrencyChanged;

            UpdateDisplay();
        }

        private void OnDestroy()
        {
            if (CurrencyManager.Instance != null)
                CurrencyManager.Instance.OnCurrencyChanged -= OnCurrencyChanged;
        }

        private void OnCurrencyChanged(CurrencyType type, float oldVal, float newVal)
        {
            UpdateDisplay();
        }

        private void Update()
        {
            // Mana updates every frame from FlameManager
            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            if (manaDisplay != null && SaveManager.Instance != null)
                manaDisplay.text = $"{SaveManager.Instance.Data.mana:F0} Mana";
            if (waterDisplay != null && CurrencyManager.Instance != null)
                waterDisplay.text = $"{CurrencyManager.Instance.TotalWater} Water";
        }
    }
}
