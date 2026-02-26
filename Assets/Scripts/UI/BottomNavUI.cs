using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class BottomNavUI : MonoBehaviour
    {
        public event Action OnTerrariumReactivated;

        private const int TerrariumIndex = 2;
        private const string ExpandableClass = "nav-tab-expandable";

        private Button[] tabs;
        private SwipeablePageView pageView;

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
            UpdateExpandableState(activeIndex);
        }

        private void UpdateExpandableState(int activeIndex)
        {
            bool expandable = activeIndex == TerrariumIndex && HasMultipleUnlockedEnvironments();
            if (expandable)
                tabs[TerrariumIndex].AddToClassList(ExpandableClass);
            else
                tabs[TerrariumIndex].RemoveFromClassList(ExpandableClass);
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
