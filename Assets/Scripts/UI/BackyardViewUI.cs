using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class BackyardViewUI : MonoBehaviour
    {
        private const int BackyardEnvIndex = 0;

        [SerializeField] private BackyardIsometricView isometricView;

        public event Action<int, int> OnEmptySlotTapped;
        public event Action<int, int> OnMatureSlotTapped;

        private VisualElement terrariumPage;
        private readonly List<Button> slotButtons = new();
        private readonly List<Label> labels = new();
        private readonly List<VisualElement> progressFills = new();
        private readonly List<string> _lastLabelText = new();

        private Button _pickerBtn;
        private VisualElement _dropdown;
        private ConsumableType? _pendingType; // only set for slot-scoped apply mode

        private bool initialized;
        private bool pageActive;

        public void SetPageActive(bool active) => pageActive = active;

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
                int count = EnvironmentManager.Instance.GetActiveSlotCount(BackyardEnvIndex);
                for (int i = 0; i < count; i++)
                    AddSlotButton(i);
                EnvironmentManager.Instance.OnSlotUnlocked += OnSlotUnlocked;
            }

            BuildConsumablePicker();

            // Restore slot-scoped consumable visuals for any already-planted slots
            if (PlantManager.Instance != null && isometricView != null)
            {
                foreach (var slot in PlantManager.Instance.Slots)
                {
                    if (slot.environmentIndex != BackyardEnvIndex) continue;
                    foreach (var c in slot.appliedConsumables)
                        isometricView.SpawnSlotConsumableVisual(slot.slotIndex, c.type);
                }
            }

            // Restore env-scoped consumable visuals
            if (ConsumableManager.Instance != null && isometricView != null)
            {
                foreach (var c in ConsumableManager.Instance.GetEnvConsumables(BackyardEnvIndex))
                    isometricView.SpawnEnvConsumableVisual(c.type);
            }

            initialized = true;
            RefreshAllSlots();
        }

        private void BuildConsumablePicker()
        {
            _pickerBtn = new Button(ToggleDropdown);
            _pickerBtn.text = "🌿";
            _pickerBtn.AddToClassList("consumable-picker-btn");
            terrariumPage.Add(_pickerBtn);

            _dropdown = new VisualElement();
            _dropdown.AddToClassList("consumable-dropdown");
            _dropdown.style.display = DisplayStyle.None;
            terrariumPage.Add(_dropdown);
        }

        private void ToggleDropdown()
        {
            if (_pendingType.HasValue)
            {
                CancelApplyMode();
                return;
            }

            bool showing = _dropdown.style.display == DisplayStyle.Flex;
            if (showing)
            {
                _dropdown.style.display = DisplayStyle.None;
                return;
            }

            RefreshDropdown();
            _dropdown.style.display = DisplayStyle.Flex;
        }

        private void RefreshDropdown()
        {
            _dropdown.Clear();
            if (ConsumableManager.Instance == null) return;

            foreach (var c in ConsumableManager.Instance.AllConsumables)
            {
                int count = ConsumableManager.Instance.GetCount(c.type);
                if (count <= 0) continue;

                var row = new Button();
                row.AddToClassList("consumable-row");

                var nameLabel = new Label(c.displayName);
                nameLabel.AddToClassList("consumable-row-name");

                var countLabel = new Label($"x{count}");
                countLabel.AddToClassList("consumable-row-count");

                row.Add(nameLabel);
                row.Add(countLabel);

                var capturedType = c.type;
                var capturedIsEnvScoped = c.isEnvironmentScoped;
                row.clicked += () => OnConsumableRowTapped(capturedType, capturedIsEnvScoped);

                _dropdown.Add(row);
            }

            if (_dropdown.childCount == 0)
            {
                var empty = new Label("No consumables owned");
                empty.AddToClassList("consumable-row-name");
                empty.style.paddingTop = empty.style.paddingBottom =
                    empty.style.paddingLeft = empty.style.paddingRight = new StyleLength(8);
                _dropdown.Add(empty);
            }
        }

        private void OnConsumableRowTapped(ConsumableType type, bool isEnvironmentScoped)
        {
            _dropdown.style.display = DisplayStyle.None;

            if (isEnvironmentScoped)
            {
                // Apply immediately to entire Backyard — no slot selection needed
                if (ConsumableManager.Instance != null &&
                    ConsumableManager.Instance.ApplyToEnvironment(type, BackyardEnvIndex))
                {
                    isometricView?.SpawnEnvConsumableVisual(type);
                }
                return;
            }

            // Slot-scoped: enter apply mode so player can tap a slot
            _pendingType = type;
            foreach (var btn in slotButtons)
                btn.AddToClassList("backyard-slot-apply-mode");
        }

        private void CancelApplyMode()
        {
            _pendingType = null;
            foreach (var btn in slotButtons)
                btn.RemoveFromClassList("backyard-slot-apply-mode");
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
            btn.AddToClassList("backyard-slot-overlay");
            btn.style.position = Position.Absolute;

            var label = new Label("Tap to Plant");
            label.AddToClassList("backyard-slot-label");
            btn.Add(label);

            var progressBar = new VisualElement();
            progressBar.AddToClassList("backyard-progress-bar");
            var fill = new VisualElement();
            fill.AddToClassList("backyard-progress-fill");
            progressBar.Add(fill);
            btn.Add(progressBar);

            int idx = slotIndex;
            btn.RegisterCallback<ClickEvent>(_ => OnSlotClicked(idx));

            terrariumPage.Add(btn);
            slotButtons.Add(btn);
            labels.Add(label);
            _lastLabelText.Add(null);
            progressFills.Add(fill);
        }

        private void OnSlotUnlocked(int envIndex)
        {
            if (envIndex != BackyardEnvIndex) return;
            AddSlotButton(slotButtons.Count);
            RefreshAllSlots();
        }

        private void Update()
        {
            if (!initialized || !pageActive || isometricView == null || terrariumPage == null) return;

            for (int i = 0; i < slotButtons.Count; i++)
                PositionButton(i);

            if (PlantManager.Instance == null) return;

            for (int i = 0; i < slotButtons.Count; i++)
            {
                var slot = PlantManager.Instance.GetSlot(BackyardEnvIndex, i);
                if (slot == null) continue;

                if (slot.state == PlantState.Growing)
                {
                    float hours = PlantManager.Instance.GetRemainingHours(BackyardEnvIndex, i);
                    string text = hours > 1f ? $"{hours:F1}h" : $"{hours * 60f:F0}m";
                    if (i < labels.Count && labels[i] != null && text != _lastLabelText[i])
                    {
                        labels[i].text = text;
                        _lastLabelText[i] = text;
                    }
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

            var bl = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screenRect.x, screenRect.y));
            var tr = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screenRect.xMax, screenRect.yMax));

            float panelLeft   = Mathf.Min(bl.x, tr.x);
            float panelTop    = Mathf.Min(bl.y, tr.y);
            float panelWidth  = Mathf.Abs(tr.x - bl.x);
            float panelHeight = Mathf.Abs(bl.y - tr.y);

            var pageOrigin = terrariumPage.worldBound;
            if (pageOrigin.width <= 0) return;
            slotButtons[i].style.left   = panelLeft   - pageOrigin.x;
            slotButtons[i].style.top    = panelTop    - pageOrigin.y;
            slotButtons[i].style.width  = panelWidth;
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

            var slot = PlantManager.Instance.GetSlot(BackyardEnvIndex, i);
            if (slot == null) return;

            var label = i < labels.Count ? labels[i] : null;
            var fill  = i < progressFills.Count ? progressFills[i] : null;

            switch (slot.state)
            {
                case PlantState.Empty:
                    if (label != null) label.text = "Tap to Plant";
                    if (fill  != null) fill.style.width = new Length(0, LengthUnit.Percent);
                    slotButtons[i].RemoveFromClassList("backyard-slot-mature");
                    isometricView?.SetPlantVisual(i, PlantState.Empty, Color.clear);
                    isometricView?.ClearSlotConsumableVisuals(i);
                    break;

                case PlantState.Growing:
                    float hours = PlantManager.Instance.GetRemainingHours(BackyardEnvIndex, i);
                    if (label != null)
                        label.text = hours > 1f ? $"{hours:F1}h" : $"{hours * 60f:F0}m";
                    if (i < _lastLabelText.Count) _lastLabelText[i] = null;
                    if (fill != null)
                        fill.style.width = new Length(slot.growthProgress * 100f, LengthUnit.Percent);
                    slotButtons[i].RemoveFromClassList("backyard-slot-mature");
                    isometricView?.SetPlantVisual(i, PlantState.Growing,
                        slot.variant?.primaryColor ?? Color.green);
                    break;

                case PlantState.Mature:
                    if (label != null) label.text = "Harvest!";
                    if (fill  != null) fill.style.width = new Length(100, LengthUnit.Percent);
                    slotButtons[i].AddToClassList("backyard-slot-mature");
                    isometricView?.SetPlantVisual(i, PlantState.Mature,
                        slot.variant?.primaryColor ?? Color.green);
                    break;
            }
        }

        private void OnSlotClicked(int slotIndex)
        {
            // Slot-scoped apply mode: apply consumable to this slot
            if (_pendingType.HasValue)
            {
                var type = _pendingType.Value;
                CancelApplyMode();
                if (PlantManager.Instance != null &&
                    PlantManager.Instance.ApplyConsumable(type, BackyardEnvIndex, slotIndex))
                {
                    isometricView?.SpawnSlotConsumableVisual(slotIndex, type);
                }
                return;
            }

            // Normal interaction
            if (PlantManager.Instance == null) return;
            var slot = PlantManager.Instance.GetSlot(BackyardEnvIndex, slotIndex);
            if (slot == null) return;

            switch (slot.state)
            {
                case PlantState.Empty:  OnEmptySlotTapped?.Invoke(BackyardEnvIndex, slotIndex);  break;
                case PlantState.Mature: OnMatureSlotTapped?.Invoke(BackyardEnvIndex, slotIndex); break;
            }
        }

        private void OnSlotStateChanged(int envIndex, int slotIndex, PlantState state)
        {
            if (envIndex != BackyardEnvIndex) return;
            if (slotIndex >= 0 && slotIndex < slotButtons.Count)
                RefreshSlot(slotIndex);
        }

        private void OnSlotGrowthUpdated(int envIndex, int slotIndex, float progress)
        {
            if (envIndex != BackyardEnvIndex) return;
            if (slotIndex >= 0 && slotIndex < progressFills.Count && progressFills[slotIndex] != null)
                progressFills[slotIndex].style.width = new Length(progress * 100f, LengthUnit.Percent);
        }
    }
}
