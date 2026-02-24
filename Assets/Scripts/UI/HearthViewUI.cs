using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class HearthViewUI : MonoBehaviour
    {
        private const int HearthEnvIndex = 0;

        [SerializeField] private HearthIsometricView isometricView;

        public event Action<int, int> OnEmptySlotTapped;
        public event Action<int, int> OnMatureSlotTapped;

        private VisualElement terrariumPage;
        private readonly List<Button> slotButtons = new();
        private readonly List<Label> labels = new();
        private readonly List<VisualElement> progressFills = new();

        private bool initialized;

        public void Initialize(VisualElement root)
        {
            terrariumPage = root.Q<VisualElement>("terrarium-page");

            if (PlantManager.Instance != null)
            {
                PlantManager.Instance.OnSlotStateChanged += OnSlotStateChanged;
                PlantManager.Instance.OnSlotGrowthUpdated += OnSlotGrowthUpdated;
            }

            if (EnvironmentManager.Instance != null)
            {
                int count = EnvironmentManager.Instance.GetActiveSlotCount(HearthEnvIndex);
                for (int i = 0; i < count; i++)
                    AddSlotButton(i);
                EnvironmentManager.Instance.OnSlotUnlocked += OnSlotUnlocked;
            }

            initialized = true;
            RefreshAllSlots();
        }

        private void OnDestroy()
        {
            if (PlantManager.Instance != null)
            {
                PlantManager.Instance.OnSlotStateChanged -= OnSlotStateChanged;
                PlantManager.Instance.OnSlotGrowthUpdated -= OnSlotGrowthUpdated;
            }
            if (EnvironmentManager.Instance != null)
                EnvironmentManager.Instance.OnSlotUnlocked -= OnSlotUnlocked;
        }

        private void AddSlotButton(int slotIndex)
        {
            var btn = new Button();
            btn.AddToClassList("hearth-slot-overlay");
            btn.style.position = Position.Absolute;

            var label = new Label("Tap to Plant");
            label.AddToClassList("hearth-slot-label");
            btn.Add(label);

            var progressBar = new VisualElement();
            progressBar.AddToClassList("hearth-progress-bar");
            var fill = new VisualElement();
            fill.AddToClassList("hearth-progress-fill");
            progressBar.Add(fill);
            btn.Add(progressBar);

            int idx = slotIndex;
            btn.RegisterCallback<ClickEvent>(_ => OnSlotClicked(idx));

            terrariumPage.Add(btn);
            slotButtons.Add(btn);
            labels.Add(label);
            progressFills.Add(fill);
        }

        private void OnSlotUnlocked(int envIndex)
        {
            if (envIndex != HearthEnvIndex) return;
            AddSlotButton(slotButtons.Count);
            RefreshAllSlots();
        }

        private void Update()
        {
            if (!initialized || isometricView == null || terrariumPage == null) return;

            // Re-project tile positions each frame (handles screen resize)
            for (int i = 0; i < slotButtons.Count; i++)
                PositionButton(i);

            if (PlantManager.Instance == null) return;

            for (int i = 0; i < slotButtons.Count; i++)
            {
                var slot = PlantManager.Instance.GetSlot(HearthEnvIndex, i);
                if (slot == null) continue;

                if (slot.state == PlantState.Growing)
                {
                    float hours = PlantManager.Instance.GetRemainingHours(HearthEnvIndex, i);
                    if (i < labels.Count && labels[i] != null)
                        labels[i].text = hours > 1f ? $"{hours:F1}h" : $"{hours * 60f:F0}m";
                }
                else if (slot.state == PlantState.Mature)
                {
                    float pulse = 1f + 0.05f * Mathf.Sin(Time.time * 3f);
                    isometricView.SetPlantScale(i, pulse);
                }
            }
        }

        private void PositionButton(int i)
        {
            if (i >= slotButtons.Count || terrariumPage?.panel == null) return;

            var screenRect = isometricView.GetTileScreenBounds(i);
            var panel = terrariumPage.panel;

            // Convert screen-space corners (bottom-left origin) to panel space (top-left origin)
            var bl = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screenRect.x, screenRect.y));
            var tr = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screenRect.xMax, screenRect.yMax));

            // In panel space Y increases downward, so tr.y < bl.y (tr is visually higher)
            float panelLeft = Mathf.Min(bl.x, tr.x);
            float panelTop = Mathf.Min(bl.y, tr.y);
            float panelWidth = Mathf.Abs(tr.x - bl.x);
            float panelHeight = Mathf.Abs(bl.y - tr.y);

            // Make coords relative to terrariumPage
            var pageOrigin = terrariumPage.worldBound;
            slotButtons[i].style.left = panelLeft - pageOrigin.x;
            slotButtons[i].style.top = panelTop - pageOrigin.y;
            slotButtons[i].style.width = panelWidth;
            slotButtons[i].style.height = panelHeight;
        }

        public void RefreshAllSlots()
        {
            for (int i = 0; i < slotButtons.Count; i++)
                RefreshSlot(i);
        }

        private void RefreshSlot(int i)
        {
            if (PlantManager.Instance == null || i >= slotButtons.Count) return;

            var slot = PlantManager.Instance.GetSlot(HearthEnvIndex, i);
            if (slot == null) return;

            var label = i < labels.Count ? labels[i] : null;
            var fill = i < progressFills.Count ? progressFills[i] : null;

            switch (slot.state)
            {
                case PlantState.Empty:
                    if (label != null) label.text = "Tap to Plant";
                    if (fill != null) fill.style.width = new Length(0, LengthUnit.Percent);
                    slotButtons[i].RemoveFromClassList("hearth-slot-mature");
                    isometricView?.SetPlantVisual(i, PlantState.Empty, Color.clear);
                    break;

                case PlantState.Growing:
                    float hours = PlantManager.Instance.GetRemainingHours(HearthEnvIndex, i);
                    if (label != null)
                        label.text = hours > 1f ? $"{hours:F1}h" : $"{hours * 60f:F0}m";
                    if (fill != null)
                        fill.style.width = new Length(slot.growthProgress * 100f, LengthUnit.Percent);
                    slotButtons[i].RemoveFromClassList("hearth-slot-mature");
                    isometricView?.SetPlantVisual(i, PlantState.Growing, slot.variant?.primaryColor ?? Color.green);
                    break;

                case PlantState.Mature:
                    if (label != null) label.text = "Harvest!";
                    if (fill != null) fill.style.width = new Length(100, LengthUnit.Percent);
                    slotButtons[i].AddToClassList("hearth-slot-mature");
                    isometricView?.SetPlantVisual(i, PlantState.Mature, slot.variant?.primaryColor ?? Color.green);
                    break;
            }
        }

        private void OnSlotClicked(int slotIndex)
        {
            if (PlantManager.Instance == null) return;
            var slot = PlantManager.Instance.GetSlot(HearthEnvIndex, slotIndex);
            if (slot == null) return;

            switch (slot.state)
            {
                case PlantState.Empty: OnEmptySlotTapped?.Invoke(HearthEnvIndex, slotIndex); break;
                case PlantState.Mature: OnMatureSlotTapped?.Invoke(HearthEnvIndex, slotIndex); break;
            }
        }

        private void OnSlotStateChanged(int envIndex, int slotIndex, PlantState state)
        {
            if (envIndex != HearthEnvIndex) return;
            if (slotIndex >= 0 && slotIndex < slotButtons.Count)
                RefreshSlot(slotIndex);
        }

        private void OnSlotGrowthUpdated(int envIndex, int slotIndex, float progress)
        {
            if (envIndex != HearthEnvIndex) return;
            if (slotIndex >= 0 && slotIndex < progressFills.Count && progressFills[slotIndex] != null)
                progressFills[slotIndex].style.width = new Length(progress * 100f, LengthUnit.Percent);
        }
    }
}
