using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class PulseButton : MonoBehaviour
    {
        public event System.Action OnPulse;

        private Button button;

        public void Initialize(VisualElement root)
        {
            button = root.Q<Button>("pulse-button");
            button.clicked += HandleClick;
        }

        private void Start()
        {
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
            if (button == null || PlantManager.Instance == null) return;

            int mature = PlantManager.Instance.GetMatureCount();
            int growing = PlantManager.Instance.GetGrowingCount();

            if (mature > 0)
            {
                button.text = $"{mature} plant{(mature > 1 ? "s" : "")} ready!";
            }
            else if (growing > 0)
            {
                float hours = PlantManager.Instance.GetRemainingHours();
                if (hours > 1f)
                    button.text = $"{growing} growing \u2022 {hours:F1}h";
                else
                    button.text = $"{growing} growing \u2022 {hours * 60f:F0}m";
            }
        }

        private void HandleClick()
        {
            var pm = PlantManager.Instance;
            if (pm == null) return;

            OnPulse?.Invoke();
        }

        private void UpdateState()
        {
            if (button == null || PlantManager.Instance == null) return;

            var pm = PlantManager.Instance;
            int mature = pm.GetMatureCount();
            int growing = pm.GetGrowingCount();

            if (mature > 0)
                button.text = $"{mature} plant{(mature > 1 ? "s" : "")} ready!";
            else if (growing > 0)
                button.text = "Growing...";
            else
                button.text = "Plant a Seed";
        }
    }
}
