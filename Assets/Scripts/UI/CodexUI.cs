using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Garden
{
    public class CodexUI : MonoBehaviour
    {
        [SerializeField] private Transform variantGrid;
        [SerializeField] private GameObject variantEntryPrefab;
        [SerializeField] private TextMeshProUGUI detailName;
        [SerializeField] private TextMeshProUGUI detailDescription;
        [SerializeField] private TextMeshProUGUI detailRarity;
        [SerializeField] private Image detailColorSwatch;
        [SerializeField] private Button closeButton;

        private void OnEnable()
        {
            RefreshCodex();
        }

        private void Start()
        {
            closeButton.onClick.AddListener(() => gameObject.SetActive(false));
        }

        private void RefreshCodex()
        {
            foreach (Transform child in variantGrid) Destroy(child.gameObject);

            var discovered = SaveManager.Instance.Data.discoveredVariants;

            foreach (var seed in SeedRegistry.Instance.AllSeeds)
            {
                foreach (var variant in seed.variants)
                {
                    var entry = Instantiate(variantEntryPrefab, variantGrid);
                    bool isDiscovered = discovered.Contains(variant.variantName);

                    var text = entry.GetComponentInChildren<TextMeshProUGUI>();
                    var image = entry.GetComponent<Image>();
                    var button = entry.GetComponent<Button>();

                    if (isDiscovered)
                    {
                        if (text != null) text.text = variant.variantName;
                        if (image != null) image.color = variant.primaryColor;
                    }
                    else
                    {
                        if (text != null) text.text = "???";
                        if (image != null) image.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
                    }

                    button?.onClick.AddListener(() => ShowDetail(variant, isDiscovered));
                }
            }
        }

        private void ShowDetail(VariantData variant, bool discovered)
        {
            if (discovered)
            {
                detailName.text = variant.variantName;
                detailDescription.text = variant.description;
                detailRarity.text = variant.rarity.ToString();
                detailColorSwatch.color = variant.primaryColor;
            }
            else
            {
                detailName.text = "Unknown Variant";
                detailDescription.text = variant.discoveryHint;
                detailRarity.text = "???";
                detailColorSwatch.color = Color.black;
            }
        }
    }
}
