using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class PulseButton : MonoBehaviour
    {
        public event System.Action OnPulse;

        private Button button;
        private Label buttonLabel;

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
            if (button == null) return;
            if (PlantManager.Instance != null && PlantManager.Instance.State == PlantState.Growing)
            {
                float hours = PlantManager.Instance.GetRemainingHours();
                if (hours > 1f)
                    button.text = $"{hours:F1}h remaining";
                else
                    button.text = $"{hours * 60f:F0}m remaining";
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
                    // Pulse animation via USS transition
                    if (button != null)
                    {
                        button.style.scale = new StyleScale(new Scale(new Vector3(1.1f, 1.1f, 1f)));
                        button.schedule.Execute(() =>
                            button.style.scale = new StyleScale(new Scale(Vector3.one))
                        ).ExecuteLater(200);
                    }
                    break;
                case PlantState.Mature:
                    pm.Harvest();
                    break;
            }
        }

        private void UpdateState()
        {
            if (button == null) return;
            var pm = PlantManager.Instance;
            if (pm == null) return;

            button.text = pm.State switch
            {
                PlantState.Empty => "Plant a Seed",
                PlantState.Growing => "Growing...",
                PlantState.Mature => "Harvest!",
                _ => ""
            };
        }
    }
}
