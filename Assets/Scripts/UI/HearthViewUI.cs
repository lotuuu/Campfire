using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class HearthViewUI : MonoBehaviour
    {
        private const int HearthEnvIndex = 0;
        private const int SlotCount = 2;

        public event Action<int, int> OnEmptySlotTapped;
        public event Action<int, int> OnMatureSlotTapped;

        private Button[] slotButtons = new Button[SlotCount];
        private VisualElement[] soils = new VisualElement[SlotCount];
        private VisualElement[] swatches = new VisualElement[SlotCount];
        private Label[] labels = new Label[SlotCount];
        private VisualElement[] progressFills = new VisualElement[SlotCount];

        private bool initialized;

        public void Initialize(VisualElement root)
        {
            for (int i = 0; i < SlotCount; i++)
            {
                slotButtons[i] = root.Q<Button>($"hearth-slot-{i}");
                soils[i] = root.Q<VisualElement>($"hearth-soil-{i}");
                swatches[i] = root.Q<VisualElement>($"hearth-swatch-{i}");
                labels[i] = root.Q<Label>($"hearth-label-{i}");
                progressFills[i] = root.Q<VisualElement>($"hearth-progress-{i}");

                int slotIdx = i;
                slotButtons[i]?.RegisterCallback<ClickEvent>(evt => OnSlotClicked(slotIdx));
            }

            if (PlantManager.Instance != null)
            {
                PlantManager.Instance.OnSlotStateChanged += OnSlotStateChanged;
                PlantManager.Instance.OnSlotGrowthUpdated += OnSlotGrowthUpdated;
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
        }

        private void Update()
        {
            if (!initialized || PlantManager.Instance == null) return;

            for (int i = 0; i < SlotCount; i++)
            {
                var slot = PlantManager.Instance.GetSlot(HearthEnvIndex, i);
                if (slot == null) continue;

                if (slot.state == PlantState.Growing)
                {
                    float hours = PlantManager.Instance.GetRemainingHours(HearthEnvIndex, i);
                    if (labels[i] != null)
                        labels[i].text = hours > 1f ? $"{hours:F1}h" : $"{hours * 60f:F0}m";
                    if (progressFills[i] != null)
                        progressFills[i].style.width = new Length(slot.growthProgress * 100f, LengthUnit.Percent);
                }
                else if (slot.state == PlantState.Mature && swatches[i] != null)
                {
                    float pulse = 1f + 0.05f * Mathf.Sin(Time.time * 3f);
                    swatches[i].style.scale = new StyleScale(new Scale(new Vector3(pulse, pulse, 1f)));
                }
            }
        }

        public void RefreshAllSlots()
        {
            for (int i = 0; i < SlotCount; i++)
                RefreshSlot(i);
        }

        private void RefreshSlot(int i)
        {
            if (PlantManager.Instance == null) return;

            var slot = PlantManager.Instance.GetSlot(HearthEnvIndex, i);
            if (slot == null) return;

            bool hasButton = slotButtons[i] != null;

            switch (slot.state)
            {
                case PlantState.Empty:
                    if (soils[i] != null) soils[i].style.display = DisplayStyle.Flex;
                    if (swatches[i] != null) swatches[i].style.display = DisplayStyle.None;
                    if (labels[i] != null) labels[i].text = "Tap to Plant";
                    if (progressFills[i] != null) progressFills[i].style.width = new Length(0, LengthUnit.Percent);
                    if (hasButton)
                    {
                        slotButtons[i].RemoveFromClassList("hearth-slot-mature");
                    }
                    break;

                case PlantState.Growing:
                    if (soils[i] != null) soils[i].style.display = DisplayStyle.None;
                    if (swatches[i] != null)
                    {
                        swatches[i].style.display = DisplayStyle.Flex;
                        if (slot.variant != null)
                            swatches[i].style.backgroundColor = slot.variant.primaryColor;
                        swatches[i].style.scale = new StyleScale(new Scale(Vector3.one));
                    }
                    float hours = PlantManager.Instance.GetRemainingHours(HearthEnvIndex, i);
                    if (labels[i] != null)
                        labels[i].text = hours > 1f ? $"{hours:F1}h" : $"{hours * 60f:F0}m";
                    if (progressFills[i] != null)
                        progressFills[i].style.width = new Length(slot.growthProgress * 100f, LengthUnit.Percent);
                    if (hasButton)
                    {
                        slotButtons[i].RemoveFromClassList("hearth-slot-mature");
                    }
                    break;

                case PlantState.Mature:
                    if (soils[i] != null) soils[i].style.display = DisplayStyle.None;
                    if (swatches[i] != null)
                    {
                        swatches[i].style.display = DisplayStyle.Flex;
                        if (slot.variant != null)
                            swatches[i].style.backgroundColor = slot.variant.primaryColor;
                    }
                    if (labels[i] != null) labels[i].text = "Harvest!";
                    if (progressFills[i] != null) progressFills[i].style.width = new Length(100, LengthUnit.Percent);
                    if (hasButton)
                    {
                        slotButtons[i].AddToClassList("hearth-slot-mature");
                    }
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
                case PlantState.Empty:
                    OnEmptySlotTapped?.Invoke(HearthEnvIndex, slotIndex);
                    break;
                case PlantState.Mature:
                    OnMatureSlotTapped?.Invoke(HearthEnvIndex, slotIndex);
                    break;
            }
        }

        private void OnSlotStateChanged(int envIndex, int slotIndex, PlantState state)
        {
            if (envIndex != HearthEnvIndex) return;
            if (slotIndex >= 0 && slotIndex < SlotCount)
                RefreshSlot(slotIndex);
        }

        private void OnSlotGrowthUpdated(int envIndex, int slotIndex, float progress)
        {
            if (envIndex != HearthEnvIndex) return;
            if (slotIndex >= 0 && slotIndex < SlotCount)
            {
                if (progressFills[slotIndex] != null)
                    progressFills[slotIndex].style.width = new Length(progress * 100f, LengthUnit.Percent);
            }
        }
    }
}
