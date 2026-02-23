using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Garden
{
    public class SeedSlotUI : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI countText;
        [SerializeField] private Button selectButton;

        private SeedData seed;
        private System.Action<SeedData> onSelected;

        public void Setup(SeedData data, int count, System.Action<SeedData> callback)
        {
            seed = data;
            onSelected = callback;
            nameText.text = data.seedName;
            countText.text = $"x{count}";
            if (data.icon != null) iconImage.sprite = data.icon;
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(() => onSelected?.Invoke(seed));
        }
    }
}
