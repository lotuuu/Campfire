using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Garden
{
    public class GreenhouseUI : MonoBehaviour
    {
        [SerializeField] private Transform plantGrid;
        [SerializeField] private GameObject plantSlotPrefab;
        [SerializeField] private TextMeshProUGUI dustRateText;
        [SerializeField] private TextMeshProUGUI slotsText;
        [SerializeField] private Button expandButton;
        [SerializeField] private Button closeButton;

        private void OnEnable()
        {
            RefreshDisplay();
        }

        private void Start()
        {
            closeButton.onClick.AddListener(() => gameObject.SetActive(false));
            expandButton.onClick.AddListener(OnExpand);
        }

        private void RefreshDisplay()
        {
            foreach (Transform child in plantGrid) Destroy(child.gameObject);

            var gm = GreenhouseManager.Instance;
            slotsText.text = $"{gm.Plants.Count} / {gm.MaxSlots}";
            dustRateText.text = $"+{gm.GetTotalDustPerHour():F1} Aura Dust/hr";

            foreach (var plant in gm.Plants)
            {
                var slot = Instantiate(plantSlotPrefab, plantGrid);
                var text = slot.GetComponentInChildren<TextMeshProUGUI>();
                var image = slot.GetComponent<Image>();
                if (text != null) text.text = plant.variantName;
                if (image != null) image.color = plant.primaryColor;
            }

            for (int i = gm.Plants.Count; i < gm.MaxSlots; i++)
            {
                var slot = Instantiate(plantSlotPrefab, plantGrid);
                var text = slot.GetComponentInChildren<TextMeshProUGUI>();
                var image = slot.GetComponent<Image>();
                if (text != null) text.text = "Empty";
                if (image != null) image.color = new Color(0.3f, 0.3f, 0.3f, 0.3f);
            }

            var config = CurrencyManager.Instance.Config;
            expandButton.interactable = CurrencyManager.Instance.CanAfford(
                CurrencyType.SunShards, config.slotCostSunShards);
        }

        private void OnExpand()
        {
            if (GreenhouseManager.Instance.ExpandSlots())
                RefreshDisplay();
        }
    }
}
