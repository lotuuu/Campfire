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
            manaDisplay = root.Q<Label>("hud-mana");
            waterDisplay = root.Q<Label>("hud-water");
            mallumDisplay = root.Q<Label>("hud-mallum");

            manaIcon = root.Q("hud-mana-icon");
            waterIcon = root.Q("hud-water-icon");
            mallumIcon = root.Q("hud-mallum-icon");

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
                iconsLoaded = manaIcon?.style.backgroundImage.value.texture != null
                           && waterIcon?.style.backgroundImage.value.texture != null
                           && mallumIcon?.style.backgroundImage.value.texture != null;
            }

            // Mana updates every frame (accumulating), other resources are event-driven
            if (manaDisplay != null && SaveManager.Instance != null)
                manaDisplay.text = $"{SaveManager.Instance.Data.mana:F0}";
        }

        public void Refresh() => UpdateDisplay();

        private static readonly Color WaterNormal = new Color(100f/255, 180f/255, 240f/255);
        private static readonly Color WaterLow = new Color(240f/255, 200f/255, 60f/255);
        private static readonly Color WaterCritical = new Color(220f/255, 70f/255, 50f/255);

        private void UpdateDisplay()
        {
            if (manaDisplay != null && SaveManager.Instance != null)
                manaDisplay.text = $"{SaveManager.Instance.Data.mana:F0}";
            if (waterDisplay != null && SaveManager.Instance != null)
            {
                int current = 0, max = 0;
                foreach (var v in SaveManager.Instance.Data.vases)
                {
                    current += v.currentWater;
                    max += v.capacity;
                }
                waterDisplay.text = $"{current}/{max}";
                float pct = max > 0 ? (float)current / max : 1f;
                waterDisplay.style.color = pct <= 0.2f ? WaterCritical
                                         : pct <= 0.4f ? WaterLow
                                         : WaterNormal;
            }
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
