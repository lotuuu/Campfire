using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class BottomNavUI : MonoBehaviour
    {
        public event Action OnApothekeClicked;
        public event Action OnLettersClicked;
        public event Action OnQuestClicked;

        private Label questBadge;

        public void Initialize(VisualElement root)
        {
            var btnSeeds = root.Q<Button>("btn-seeds");
            var btnQuest = root.Q<Button>("btn-quest");
            var btnMail = root.Q<Button>("btn-mail");

            btnSeeds?.RegisterCallback<ClickEvent>(_ => OnApothekeClicked?.Invoke());
            btnQuest?.RegisterCallback<ClickEvent>(_ => OnQuestClicked?.Invoke());
            btnMail?.RegisterCallback<ClickEvent>(_ => OnLettersClicked?.Invoke());

            // Load nav icons
            SetIcon(root.Q("nav-icon-seeds"), "UI/Icons/nav-seeds");
            SetIcon(root.Q("nav-icon-quest"), "UI/Icons/quest-compass");
            SetIcon(root.Q("nav-icon-mail"), "UI/Icons/nav-mail");

            questBadge = root.Q<Label>("nav-quest-badge");
        }

        public void UpdateQuestBadge(int count)
        {
            if (questBadge == null) return;
            if (count > 0)
            {
                questBadge.text = count.ToString();
                questBadge.style.display = DisplayStyle.Flex;
            }
            else
            {
                questBadge.style.display = DisplayStyle.None;
            }
        }

        private static void SetIcon(VisualElement el, string resourcePath)
        {
            if (el == null) return;
            var tex = Resources.Load<Texture2D>(resourcePath);
            if (tex != null)
                el.style.backgroundImage = tex;
        }
    }
}
