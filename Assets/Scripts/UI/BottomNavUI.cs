using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class BottomNavUI : MonoBehaviour
    {
        private Button[] tabs;
        private SwipeablePageView pageView;
        private int lockedTabIndex = 4;

        private static readonly string[] TabNames = {
            "tab-codex", "tab-shop", "tab-terrarium", "tab-greenhouse", "tab-locked"
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
        }

        private void OnTabClicked(int index)
        {
            if (index == lockedTabIndex) return;
            pageView.GoToPage(index);
        }

        private void UpdateActiveTab(int activeIndex)
        {
            for (int i = 0; i < tabs.Length; i++)
            {
                tabs[i].RemoveFromClassList("nav-tab--active");
            }
            if (activeIndex >= 0 && activeIndex < tabs.Length)
            {
                tabs[activeIndex].AddToClassList("nav-tab--active");
            }
        }

        private void OnDestroy()
        {
            if (pageView != null)
                pageView.OnPageChanged -= UpdateActiveTab;
        }
    }
}
