using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class BottomNavUI : MonoBehaviour
    {
        public event Action OnTerrariumReactivated;

        private const int TerrariumIndex = 2;
        private const float AnimDurationMs = 300f;

        private static readonly Color DimColor = new Color(0.549f, 0.902f, 0.941f, 0.18f);
        private static readonly Color ActiveColor = new Color(100f / 255f, 230f / 255f, 230f / 255f, 1f);

        private Button[] tabs;
        private SwipeablePageView pageView;
        private BarMorphElement barMorph;
        private IVisualElementScheduledItem animSchedule;
        private float animTarget;
        private float animStart;
        private long animStartTime;

        private static readonly string[] TabNames = {
            "tab-codex", "tab-shop", "tab-terrarium", "tab-greenhouse", "tab-construction"
        };

        public void Initialize(VisualElement root, SwipeablePageView pageView)
        {
            this.pageView = pageView;

            tabs = new Button[TabNames.Length];
            for (int i = 0; i < TabNames.Length; i++)
            {
                tabs[i] = root.Q<Button>(TabNames[i]);
                int index = i;
                tabs[i].clicked += () => OnTabClicked(index);
            }

            // Create BarMorphElement inside the terrarium tab's bar
            var terrariumBar = tabs[TerrariumIndex].Q(className: "nav-tab-bar");
            barMorph = new BarMorphElement();
            barMorph.style.position = Position.Absolute;
            barMorph.style.left = 0;
            barMorph.style.right = 0;
            barMorph.style.bottom = 0;
            barMorph.style.height = 20;
            barMorph.pickingMode = PickingMode.Ignore;
            terrariumBar.Add(barMorph);

            pageView.OnPageChanged += UpdateActiveTab;
            UpdateActiveTab(pageView.CurrentPageIndex);

            if (EnvironmentManager.Instance != null)
                EnvironmentManager.Instance.OnEnvironmentUnlocked += OnEnvironmentUnlocked;
        }

        private void OnTabClicked(int index)
        {
            if (index == TerrariumIndex && pageView.CurrentPageIndex == TerrariumIndex)
            {
                OnTerrariumReactivated?.Invoke();
                return;
            }
            pageView.GoToPage(index);
        }

        private void UpdateActiveTab(int activeIndex)
        {
            for (int i = 0; i < tabs.Length; i++)
            {
                tabs[i].RemoveFromClassList("nav-tab-active");
            }
            if (activeIndex >= 0 && activeIndex < tabs.Length)
            {
                tabs[activeIndex].AddToClassList("nav-tab-active");
            }

            // Update morph bar color
            bool terrariumActive = activeIndex == TerrariumIndex;
            barMorph.StrokeColor = terrariumActive ? ActiveColor : DimColor;

            UpdateExpandableState(activeIndex);
        }

        private void UpdateExpandableState(int activeIndex)
        {
            bool expandable = activeIndex == TerrariumIndex && HasMultipleUnlockedEnvironments();
            AnimateMorph(expandable ? 1f : 0f);
        }

        private void AnimateMorph(float target)
        {
            if (Mathf.Approximately(barMorph.Progress, target) &&
                Mathf.Approximately(animTarget, target))
                return;

            animStart = barMorph.Progress;
            animTarget = target;
            animStartTime = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;

            if (animSchedule != null) return; // already ticking

            animSchedule = barMorph.schedule.Execute(AnimTick).Every(16);
        }

        private void AnimTick()
        {
            long now = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
            float elapsed = now - animStartTime;
            float t = Mathf.Clamp01(elapsed / AnimDurationMs);

            // Ease-in-out
            t = t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;

            barMorph.Progress = Mathf.Lerp(animStart, animTarget, t);

            if (t >= 1f)
            {
                animSchedule.Pause();
                animSchedule = null;
            }
        }

        private bool HasMultipleUnlockedEnvironments()
        {
            if (EnvironmentManager.Instance == null) return false;
            int count = 0;
            for (int i = 0; i < EnvironmentManager.Instance.Environments.Count; i++)
            {
                if (EnvironmentManager.Instance.IsUnlocked(i) && ++count >= 2) return true;
            }
            return false;
        }

        private void OnEnvironmentUnlocked(int _) =>
            UpdateExpandableState(pageView?.CurrentPageIndex ?? -1);

        private void OnDestroy()
        {
            if (pageView != null)
                pageView.OnPageChanged -= UpdateActiveTab;
            if (EnvironmentManager.Instance != null)
                EnvironmentManager.Instance.OnEnvironmentUnlocked -= OnEnvironmentUnlocked;
        }
    }
}
