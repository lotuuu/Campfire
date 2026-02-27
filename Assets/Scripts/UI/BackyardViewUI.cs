using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Garden
{
    public class BackyardViewUI : MonoBehaviour
    {
        [SerializeField] private BackyardIsometricView isometricView;
        private Label backyardTitle;
        private int ActiveEnv => EnvironmentManager.Instance != null
            ? EnvironmentManager.Instance.ActiveEnvironmentIndex : 0;

        public event Action<int, int> OnEmptySlotTapped;
        public event Action<int, int> OnMatureSlotTapped;

        private VisualElement terrariumPage;
        private int _slotCount;

        private VisualElement _pickerContainer;
        private Button _pickerBtn;
        private VisualElement _iconsContainer;
        private ConsumableType? _pendingType; // only set for slot-scoped apply mode
        private ConsumableType? _pendingEnvConfirmType; // set while inline env-replace confirmation is showing

        // Overlay elements — taps are suppressed while any of these are visible.
        private VisualElement _satchelScrim;
        private VisualElement _harvestPopup;
        private VisualElement _discoveryPopup;

        private bool initialized;
        private bool pageActive;

        public void SetPageActive(bool active)
        {
            pageActive = active;
            if (active && initialized)
                RefreshAllSlots();
        }

        public void Initialize(VisualElement root)
        {
            terrariumPage = root.Q<VisualElement>("terrarium-page");
            backyardTitle = root.Q<Label>("backyard-title");

            _satchelScrim    = root.Q<VisualElement>("satchel-scrim");
            _harvestPopup    = root.Q<VisualElement>("harvest-popup");
            _discoveryPopup  = root.Q<VisualElement>("discovery-popup");

            if (PlantManager.Instance != null)
            {
                PlantManager.Instance.OnSlotStateChanged += OnSlotStateChanged;
                PlantManager.Instance.OnSlotGrowthUpdated += OnSlotGrowthUpdated;
            }

            if (EnvironmentManager.Instance != null)
            {
                EnvironmentManager.Instance.OnSlotUnlocked += OnSlotUnlocked;
                EnvironmentManager.Instance.OnActiveEnvironmentChanged += OnActiveEnvironmentChanged;
                BuildSlotsForEnv(ActiveEnv);
            }

            BuildConsumablePicker();
            RefreshPickerIndicator();
            RestoreConsumableVisuals(ActiveEnv);

            initialized = true;
            RefreshAllSlots();
            UpdateTitle();
        }

        private void OnActiveEnvironmentChanged(int envIndex)
        {
            RebuildForEnvironment(envIndex);
        }

        private void RebuildForEnvironment(int envIndex)
        {
            // Dismiss any in-flight env replacement confirmation before rebuilding
            CancelEnvConfirm();

            // Rebuild the iso view first so new tiles exist before we spawn slot buttons
            // and consumable visuals on them. This also makes ordering deterministic —
            // BackyardIsometricView no longer subscribes to OnActiveEnvironmentChanged;
            // we drive it explicitly here instead.
            isometricView?.SetEnvironment(envIndex);

            _slotCount = 0;
            BuildSlotsForEnv(envIndex);
            RestoreConsumableVisuals(envIndex);
            RefreshAllSlots();
            UpdateTitle();
            RefreshPickerIndicator();
        }

        private void BuildSlotsForEnv(int envIndex)
        {
            if (EnvironmentManager.Instance == null) return;
            _slotCount = EnvironmentManager.Instance.GetActiveSlotCount(envIndex);
        }

        private void RestoreConsumableVisuals(int envIndex)
        {
            if (isometricView == null) return;

            if (PlantManager.Instance != null)
            {
                foreach (var slot in PlantManager.Instance.Slots)
                {
                    if (slot.environmentIndex != envIndex) continue;
                    foreach (var c in slot.appliedConsumables)
                        isometricView.SpawnSlotConsumableVisual(slot.slotIndex, c.type);
                }
            }

            if (ConsumableManager.Instance != null)
            {
                foreach (var c in ConsumableManager.Instance.GetEnvConsumables(envIndex))
                    isometricView.SpawnEnvConsumableVisual(c.type);
            }
        }

        private void UpdateTitle()
        {
            if (backyardTitle == null || EnvironmentManager.Instance == null) return;
            var envs = EnvironmentManager.Instance.Environments;
            int idx = ActiveEnv;
            backyardTitle.text = (idx >= 0 && idx < envs.Count) ? envs[idx].environmentName : "Backyard";
        }

        private void BuildConsumablePicker()
        {
            _pickerContainer = new VisualElement();
            _pickerContainer.AddToClassList("consumable-picker");
            _pickerContainer.pickingMode = PickingMode.Ignore;
            terrariumPage.Add(_pickerContainer);

            _pickerBtn = new Button(ToggleDropdown);
            _pickerBtn.text = "";
            _pickerBtn.AddToClassList("consumable-picker-btn");
            _pickerContainer.Add(_pickerBtn);

            _iconsContainer = new VisualElement();
            _iconsContainer.AddToClassList("consumable-picker-icons");
            _pickerContainer.Add(_iconsContainer);
        }

        private void ToggleDropdown()
        {
            if (_pendingEnvConfirmType.HasValue)
            {
                CancelEnvConfirm();
                return;
            }
            if (_pendingType.HasValue)
            {
                CancelApplyMode();
                return;
            }

            bool open = _pickerContainer.ClassListContains("consumable-picker--open");
            if (open)
            {
                _pickerContainer.RemoveFromClassList("consumable-picker--open");
                return;
            }

            RefreshIcons();
            _pickerContainer.AddToClassList("consumable-picker--open");
        }

        private void RefreshIcons()
        {
            _iconsContainer.Clear();
            if (ConsumableManager.Instance == null) return;

            foreach (var c in ConsumableManager.Instance.AllConsumables)
            {
                int count = ConsumableManager.Instance.GetCount(c.type);
                if (count <= 0) continue;

                var btn = new Button();
                btn.AddToClassList("consumable-icon-btn");
                if (c.icon != null)
                    btn.style.backgroundImage = new StyleBackground(c.icon);

                var badge = new Label($"x{count}");
                badge.AddToClassList("consumable-icon-badge");
                btn.Add(badge);

                var capturedType = c.type;
                var capturedIsEnvScoped = c.isEnvironmentScoped;
                btn.clicked += () => OnConsumableRowTapped(capturedType, capturedIsEnvScoped);

                _iconsContainer.Add(btn);
            }
        }

        private void OnConsumableRowTapped(ConsumableType type, bool isEnvironmentScoped)
        {
            _pickerContainer.RemoveFromClassList("consumable-picker--open");

            if (isEnvironmentScoped)
            {
                var existingList = ConsumableManager.Instance?.GetEnvConsumables(ActiveEnv);
                if (existingList != null && existingList.Count > 0)
                {
                    ShowEnvReplaceConfirmation(type, existingList[0]);
                    return;
                }
                if (ConsumableManager.Instance != null &&
                    ConsumableManager.Instance.ApplyToEnvironment(type, ActiveEnv))
                {
                    isometricView?.SpawnEnvConsumableVisual(type);
                    RefreshPickerIndicator();
                }
                return;
            }

            // Slot-scoped: enter apply mode so player can tap a slot
            _pendingType = type;
        }

        private void CancelApplyMode()
        {
            _pendingType = null;
        }

        private void RefreshPickerIndicator()
        {
            if (_pickerBtn == null || ConsumableManager.Instance == null) return;
            bool occupied = ConsumableManager.Instance.GetEnvConsumables(ActiveEnv).Count > 0;
            if (occupied)
                _pickerBtn.AddToClassList("consumable-picker-btn--occupied");
            else
                _pickerBtn.RemoveFromClassList("consumable-picker-btn--occupied");
        }

        private void ShowEnvReplaceConfirmation(ConsumableType newType, ConsumableData existingData)
        {
            _pendingEnvConfirmType = newType;
            _iconsContainer.Clear();
            _pickerContainer.AddToClassList("consumable-picker--open");

            var label = new Label($"Replace {existingData.displayName}?");
            label.AddToClassList("consumable-confirm-label");
            _iconsContainer.Add(label);

            var confirmBtn = new Button(() => ConfirmEnvReplace(newType));
            confirmBtn.text = "Replace";
            confirmBtn.AddToClassList("consumable-confirm-btn");
            _iconsContainer.Add(confirmBtn);

            var cancelBtn = new Button(CancelEnvConfirm);
            cancelBtn.text = "Cancel";
            cancelBtn.AddToClassList("consumable-confirm-cancel-btn");
            _iconsContainer.Add(cancelBtn);
        }

        private void ConfirmEnvReplace(ConsumableType newType)
        {
            _pendingEnvConfirmType = null;
            _pickerContainer.RemoveFromClassList("consumable-picker--open");
            if (ConsumableManager.Instance != null &&
                ConsumableManager.Instance.ApplyToEnvironment(newType, ActiveEnv))
            {
                isometricView?.SpawnEnvConsumableVisual(newType);
            }
            RefreshPickerIndicator();
        }

        private void CancelEnvConfirm()
        {
            _pendingEnvConfirmType = null;
            _pickerContainer.RemoveFromClassList("consumable-picker--open");
        }

        public void CloseConsumablePicker()
        {
            CancelApplyMode();
            CancelEnvConfirm();
            _pickerContainer?.RemoveFromClassList("consumable-picker--open");
        }

        private void OnDestroy()
        {
            if (PlantManager.Instance != null)
            {
                PlantManager.Instance.OnSlotStateChanged -= OnSlotStateChanged;
                PlantManager.Instance.OnSlotGrowthUpdated -= OnSlotGrowthUpdated;
            }
            if (EnvironmentManager.Instance != null)
            {
                EnvironmentManager.Instance.OnSlotUnlocked -= OnSlotUnlocked;
                EnvironmentManager.Instance.OnActiveEnvironmentChanged -= OnActiveEnvironmentChanged;
            }
        }

        private void OnSlotUnlocked(int envIndex)
        {
            if (envIndex != ActiveEnv) return;
            _slotCount++;
            RefreshAllSlots();
        }

        private void Update()
        {
            if (!initialized || !pageActive || isometricView == null) return;

            // Direct screen-space hit test — bypasses UI Toolkit coordinate systems entirely.
            // Pointer.current unifies mouse (editor) and primary touch (mobile).
            bool tapped = false;
            Vector2 tapScreenPos = default;
            var pointer = Pointer.current;
            if (pointer != null && pointer.press.wasReleasedThisFrame)
            {
                tapped = true;
                tapScreenPos = pointer.position.ReadValue();
            }

            if (tapped)
            {
                bool overlayOpen =
                    _satchelScrim?.resolvedStyle.display   == DisplayStyle.Flex ||
                    _harvestPopup?.resolvedStyle.display   == DisplayStyle.Flex ||
                    _discoveryPopup?.resolvedStyle.display == DisplayStyle.Flex;

                if (!overlayOpen)
                {
                    // Test front tiles first (higher index = visually in front).
                    for (int i = _slotCount - 1; i >= 0; i--)
                    {
                        if (isometricView.GetTileScreenBounds(i).Contains(tapScreenPos))
                        {
                            OnSlotClicked(i);
                            break;
                        }
                    }
                }
            }

            if (PlantManager.Instance == null) return;

            for (int i = 0; i < _slotCount; i++)
            {
                var slot = PlantManager.Instance.GetSlot(ActiveEnv, i);
                if (slot == null) continue;

                if (slot.state == PlantState.Growing)
                {
                    float hours = PlantManager.Instance.GetRemainingHours(ActiveEnv, i);
                    string text = hours > 1f ? $"{hours:F1}h" : $"{hours * 60f:F0}m";
                    isometricView.SetSlotLabel(i, text, true);
                    isometricView.SetSlotProgress(i, slot.growthProgress, true);
                    isometricView.SetPlantSprite(i, GetGrowthSprite(slot));
                }
                else if (slot.state == PlantState.Mature)
                {
                    float pulse = 1f + 0.05f * Mathf.Sin(Time.time * 3f);
                    isometricView.SetPlantScale(i, pulse);
                }
            }
        }

        public void RefreshAllSlots()
        {
            for (int i = 0; i < _slotCount; i++)
                RefreshSlot(i);
        }

        private void RefreshSlot(int i)
        {
            if (PlantManager.Instance == null || i >= _slotCount) return;

            var slot = PlantManager.Instance.GetSlot(ActiveEnv, i);
            if (slot == null) return;

            switch (slot.state)
            {
                case PlantState.Empty:
                    isometricView?.SetEmptyIndicator(i, true);
                    isometricView?.SetSlotLabel(i, "", false);
                    isometricView?.SetSlotProgress(i, 0, false);
                    isometricView?.SetPlantVisual(i, PlantState.Empty, Color.clear);
                    isometricView?.ClearSlotConsumableVisuals(i);
                    break;

                case PlantState.Growing:
                    isometricView?.SetEmptyIndicator(i, false);
                    float hours = PlantManager.Instance.GetRemainingHours(ActiveEnv, i);
                    string text = hours > 1f ? $"{hours:F1}h" : $"{hours * 60f:F0}m";
                    isometricView?.SetSlotLabel(i, text, true);
                    isometricView?.SetSlotProgress(i, slot.growthProgress, true);
                    isometricView?.SetPlantVisual(i, PlantState.Growing,
                        slot.variant?.primaryColor ?? Color.green);
                    isometricView?.SetPlantSprite(i, GetGrowthSprite(slot));
                    break;

                case PlantState.Mature:
                    isometricView?.SetEmptyIndicator(i, false);
                    isometricView?.SetSlotLabel(i, "Harvest!", true);
                    isometricView?.SetSlotProgress(i, 1f, true);
                    isometricView?.SetPlantVisual(i, PlantState.Mature,
                        slot.variant?.primaryColor ?? Color.green);
                    {
                        var sprites = slot.seed?.growthSprites;
                        if (sprites != null && sprites.Length > 0)
                            isometricView?.SetPlantSprite(i, sprites[sprites.Length - 1]);
                    }
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
                    PlantManager.Instance.ApplyConsumable(type, ActiveEnv, slotIndex))
                {
                    isometricView?.SpawnSlotConsumableVisual(slotIndex, type);
                }
                return;
            }

            // Normal interaction
            if (PlantManager.Instance == null) return;
            var slot = PlantManager.Instance.GetSlot(ActiveEnv, slotIndex);
            if (slot == null) return;

            switch (slot.state)
            {
                case PlantState.Empty:  OnEmptySlotTapped?.Invoke(ActiveEnv, slotIndex);  break;
                case PlantState.Mature: OnMatureSlotTapped?.Invoke(ActiveEnv, slotIndex); break;
            }
        }

        private void OnSlotStateChanged(int envIndex, int slotIndex, PlantState state)
        {
            if (envIndex != ActiveEnv) return;
            if (slotIndex >= 0 && slotIndex < _slotCount)
                RefreshSlot(slotIndex);
        }

        private static Sprite GetGrowthSprite(PlantSlot slot)
        {
            var sprites = slot?.seed?.growthSprites;
            if (sprites == null || sprites.Length == 0) return null;
            int stage = Mathf.Clamp(Mathf.FloorToInt(slot.growthProgress * sprites.Length),
                0, sprites.Length - 1);
            return sprites[stage];
        }

        private void OnSlotGrowthUpdated(int envIndex, int slotIndex, float progress)
        {
            if (envIndex != ActiveEnv) return;
            isometricView?.SetSlotProgress(slotIndex, progress, true);
        }
    }
}
