using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class ResourceDisplayUI : MonoBehaviour
    {
        private Label manaDisplay;
        private Label waterDisplay;
        private Label mallumDisplay;

        public void Initialize(VisualElement root)
        {
            manaDisplay = root.Q<Label>("mana-display");
            waterDisplay = root.Q<Label>("water-display");
            mallumDisplay = root.Q<Label>("mallum-display");

            // Load resource icons
            SetIcon(root.Q("mana-icon"), "UI/Icons/resource-mana");
            SetIcon(root.Q("water-icon"), "UI/Icons/resource-water");

            if (CurrencyManager.Instance != null)
                CurrencyManager.Instance.OnCurrencyChanged += OnCurrencyChanged;
            if (MallumManager.Instance != null)
                MallumManager.Instance.OnMallumsChanged += UpdateDisplay;

            UpdateDisplay();
        }

        private void OnDestroy()
        {
            if (CurrencyManager.Instance != null)
                CurrencyManager.Instance.OnCurrencyChanged -= OnCurrencyChanged;
            if (MallumManager.Instance != null)
                MallumManager.Instance.OnMallumsChanged -= UpdateDisplay;
        }

        private void OnCurrencyChanged(CurrencyType type, float oldVal, float newVal)
        {
            UpdateDisplay();
        }

        private void Update()
        {
            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            if (manaDisplay != null && SaveManager.Instance != null)
                manaDisplay.text = $"{SaveManager.Instance.Data.mana:F0}";
            if (waterDisplay != null && CurrencyManager.Instance != null)
                waterDisplay.text = $"{CurrencyManager.Instance.TotalWater}";
            if (mallumDisplay != null && MallumManager.Instance != null)
                mallumDisplay.text = $"{MallumManager.Instance.GetAvailableMallumCount()}/{MallumManager.Instance.GetTotalMallumCount()}";
        }

        private static void SetIcon(VisualElement el, string resourcePath)
        {
            if (el == null) return;
            var tex = Resources.Load<Texture2D>(resourcePath);
            if (tex != null)
                el.style.backgroundImage = tex;
        }
    }
}
