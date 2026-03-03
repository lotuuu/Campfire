using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class QuestButtonUI : MonoBehaviour
    {
        private Button floatBtn;
        private Label badge;

        public void Initialize(VisualElement root)
        {
            floatBtn = root.Q<Button>("quest-float-btn");
            badge = root.Q<Label>("quest-badge");

            var icon = root.Q("quest-float-icon");
            var tex = Resources.Load<Texture2D>("UI/Icons/quest-compass");
            if (icon != null && tex != null)
                icon.style.backgroundImage = tex;

            UpdateBadge();
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
