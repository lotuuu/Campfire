using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class ResourceDisplayUI : MonoBehaviour
    {
        private Label manaDisplay;
        private Label waterDisplay;
        private Label mallumDisplay;

        private VisualElement manaIcon, waterIcon, mallumIcon;
        private bool iconsLoaded;

        public void Initialize(VisualElement root)
        {
            manaDisplay = root.Q<Label>("mana-display");
            waterDisplay = root.Q<Label>("water-display");
            mallumDisplay = root.Q<Label>("mallum-display");

            manaIcon = root.Q("mana-icon");
            waterIcon = root.Q("water-icon");
            mallumIcon = root.Q("mallum-icon");

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
            if (!iconsLoaded && SpriteService.Instance != null)
            {
                SetIcon(manaIcon, "ui/resource-mana");
                SetIcon(waterIcon, "ui/resource-water");
                SetIcon(mallumIcon, "ui/resource-mallum");
                iconsLoaded = manaIcon?.style.backgroundImage.value.texture != null;
            }
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

        private static void SetIcon(VisualElement el, string spriteKey)
        {
            if (el == null) return;
            var tex = SpriteService.Instance?.GetTexture(spriteKey);
            if (tex != null)
                el.style.backgroundImage = tex;
        }
    }
}
