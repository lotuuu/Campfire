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
        private Label socialBadge;

        private VisualElement iconSeeds, iconQuest, iconMail;
        private bool iconsLoaded;

        public void Initialize(VisualElement root)
        {
            var btnSeeds = root.Q<Button>("btn-seeds");
            var btnQuest = root.Q<Button>("btn-quest");
            var btnMail = root.Q<Button>("btn-mail");

            btnSeeds?.RegisterCallback<ClickEvent>(_ => { AudioManager.Instance?.PlaySFX("ui_tap"); OnApothekeClicked?.Invoke(); });
            btnQuest?.RegisterCallback<ClickEvent>(_ => { AudioManager.Instance?.PlaySFX("ui_tap"); OnQuestClicked?.Invoke(); });
            btnMail?.RegisterCallback<ClickEvent>(_ => { AudioManager.Instance?.PlaySFX("ui_tap"); OnLettersClicked?.Invoke(); });

            iconSeeds = root.Q("nav-icon-seeds");
            iconQuest = root.Q("nav-icon-quest");
            iconMail = root.Q("nav-icon-mail");

            questBadge = root.Q<Label>("nav-quest-badge");
            socialBadge = root.Q<Label>("nav-social-badge");
        }

        private void Update()
        {
            if (!iconsLoaded && SpriteService.Instance != null)
            {
                SetIcon(iconSeeds, "ui/nav-seeds");
                SetIcon(iconQuest, "ui/quest-compass");
                SetIcon(iconMail, "ui/nav-mail");
                iconsLoaded = iconSeeds?.style.backgroundImage.value.texture != null;
            }
        }

        public void UpdateQuestBadge(int count)
        {
            UpdateBadge(questBadge, count);
        }

        public void UpdateSocialBadge(int count)
        {
            UpdateBadge(socialBadge, count);
        }

        private static void UpdateBadge(Label badge, int count)
        {
            if (badge == null) return;
            if (count > 0)
            {
                badge.text = count.ToString();
                badge.style.display = DisplayStyle.Flex;
            }
            else
            {
                badge.style.display = DisplayStyle.None;
            }
        }

        private static void SetIcon(VisualElement el, string spriteKey)
        {
            if (el == null) return;
            var tex = SpriteService.Instance?.GetTexture(spriteKey);
            if (tex != null)
                el.style.backgroundImage = tex;
        }
    }
}
