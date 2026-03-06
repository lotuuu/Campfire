using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class QuestButtonUI : MonoBehaviour
    {
        private Button floatBtn;
        private Label badge;
        private VisualElement iconEl;
        private bool iconLoaded;

        public void Initialize(VisualElement root)
        {
            floatBtn = root.Q<Button>("quest-float-btn");
            badge = root.Q<Label>("quest-badge");
            iconEl = root.Q("quest-float-icon");

            UpdateBadge();
        }

        private void Update()
        {
            if (!iconLoaded && iconEl != null && SpriteService.Instance != null)
            {
                var tex = SpriteService.Instance.GetTexture("ui/quest-compass");
                if (tex != null)
                {
                    iconEl.style.backgroundImage = tex;
                    iconLoaded = true;
                }
            }
        }

        public void UpdateBadge()
        {
            if (MallumManager.Instance == null)
            {
                badge.style.display = DisplayStyle.None;
                return;
            }

            int completed = MallumManager.Instance.GetCompletedQuestCount();
            if (completed > 0)
            {
                badge.text = completed.ToString();
                badge.style.display = DisplayStyle.Flex;
            }
            else
            {
                badge.style.display = DisplayStyle.None;
            }
        }
    }
}
