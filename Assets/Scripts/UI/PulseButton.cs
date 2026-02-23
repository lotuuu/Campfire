using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Garden
{
    public class PulseButton : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TextMeshProUGUI label;
        [SerializeField] private Image pulseRing;

        public event System.Action OnPulse;

        private void Start()
        {
            button.onClick.AddListener(HandleClick);
            UpdateState();
            if (PlantManager.Instance != null)
                PlantManager.Instance.OnPlantStateChanged += UpdateState;
        }

        private void OnDestroy()
        {
            if (PlantManager.Instance != null)
                PlantManager.Instance.OnPlantStateChanged -= UpdateState;
        }

        private void Update()
        {
            if (PlantManager.Instance != null && PlantManager.Instance.State == PlantState.Growing)
            {
                float hours = PlantManager.Instance.GetRemainingHours();
                if (hours > 1f)
                    label.text = $"{hours:F1}h remaining";
                else
                    label.text = $"{hours * 60f:F0}m remaining";
            }
        }

        private void HandleClick()
        {
            var pm = PlantManager.Instance;
            if (pm == null) return;

            switch (pm.State)
            {
                case PlantState.Empty:
                    OnPulse?.Invoke();
                    break;
                case PlantState.Growing:
                    if (pulseRing != null)
                        pulseRing.GetComponent<Animator>()?.SetTrigger("Pulse");
                    break;
                case PlantState.Mature:
                    pm.Harvest();
                    break;
            }
        }

        private void UpdateState()
        {
            var pm = PlantManager.Instance;
            if (pm == null) return;

            label.text = pm.State switch
            {
                PlantState.Empty => "Plant a Seed",
                PlantState.Growing => "Growing...",
                PlantState.Mature => "Harvest!",
                _ => ""
            };
        }
    }
}
