using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class EnvironmentSwitcherBar : MonoBehaviour
    {
        public event Action<int> OnEnvironmentSelected;

        private VisualElement bar;
        private bool isVisible;

        public void Initialize(VisualElement root)
        {
            bar = root.Q<VisualElement>("env-switcher-bar");
            if (bar == null)
            {
                Debug.LogError("[EnvironmentSwitcherBar] #env-switcher-bar not found in UXML.");
                return;
            }

            bar.style.display = DisplayStyle.None;

            if (EnvironmentManager.Instance != null)
                EnvironmentManager.Instance.OnEnvironmentUnlocked += OnEnvironmentUnlocked;
        }

        private void OnDestroy()
        {
            if (EnvironmentManager.Instance != null)
                EnvironmentManager.Instance.OnEnvironmentUnlocked -= OnEnvironmentUnlocked;
        }

        private void OnEnvironmentUnlocked(int _)
        {
            if (isVisible) RebuildPills();
        }

        public void Toggle()
        {
            if (isVisible) Hide();
            else Show();
        }

        public void Show()
        {
            if (bar == null) return;
            RebuildPills();
            if (bar.childCount == 0) return; // nothing to show (0 or 1 env)

            isVisible = true;
            bar.style.display = DisplayStyle.Flex;

            // Slide in: start hidden (class sets translate 100%), remove it next frame
            bar.RemoveFromClassList("env-switcher-bar--visible");
            bar.AddToClassList("env-switcher-bar--hidden");
            bar.schedule.Execute(() =>
            {
                bar.RemoveFromClassList("env-switcher-bar--hidden");
                bar.AddToClassList("env-switcher-bar--visible");
            }).ExecuteLater(16);
        }

        public void Hide()
        {
            if (bar == null || !isVisible) return;
            isVisible = false;

            bar.RemoveFromClassList("env-switcher-bar--visible");
            bar.AddToClassList("env-switcher-bar--hidden");
            bar.RegisterCallback<TransitionEndEvent>(OnHideTransitionEnd);
        }

        private void OnHideTransitionEnd(TransitionEndEvent evt)
        {
            bar.UnregisterCallback<TransitionEndEvent>(OnHideTransitionEnd);
            if (!isVisible)
                bar.style.display = DisplayStyle.None;
        }

        private void RebuildPills()
        {
            bar.Clear();
            if (EnvironmentManager.Instance == null) return;

            var envs = EnvironmentManager.Instance.Environments;
            int activeEnv = EnvironmentManager.Instance.ActiveEnvironmentIndex;
            int unlockedCount = 0;

            for (int i = 0; i < envs.Count; i++)
            {
                if (!EnvironmentManager.Instance.IsUnlocked(i)) continue;
                unlockedCount++;

                var pill = new Button();
                pill.AddToClassList("env-pill");
                pill.text = envs[i].environmentName;

                if (i == activeEnv)
                    pill.AddToClassList("env-pill--active");

                int captured = i;
                pill.clicked += () =>
                {
                    OnEnvironmentSelected?.Invoke(captured);
                };

                bar.Add(pill);
            }

            // If only 1 env is unlocked, nothing to switch — clear the bar
            if (unlockedCount <= 1)
                bar.Clear();
        }
    }
}
