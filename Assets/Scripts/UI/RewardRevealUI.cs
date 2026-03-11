using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class RewardRevealUI : MonoBehaviour
    {
        public static RewardRevealUI Instance { get; private set; }

        private VisualElement overlay;
        private Label titleLabel;
        private VisualElement cardsContainer;
        private Button collectBtn;

        private Action onCollect;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        public void Initialize(VisualElement root)
        {
            overlay = root.Q("reward-reveal-overlay");
            titleLabel = root.Q<Label>("reward-reveal-title");
            cardsContainer = root.Q("reward-reveal-cards");
            collectBtn = root.Q<Button>("reward-reveal-collect");

            collectBtn?.RegisterCallback<ClickEvent>(evt =>
            {
                evt.StopPropagation();
                Collect();
            });

            // Block clicks from falling through to elements behind
            overlay?.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
        }

        public void Show(string title, List<RewardEntry> rewards, Action onCollectCallback)
        {
            if (overlay == null || rewards == null || rewards.Count == 0)
            {
                onCollectCallback?.Invoke();
                return;
            }

            onCollect = onCollectCallback;
            titleLabel.text = title;
            cardsContainer.Clear();

            overlay.style.display = DisplayStyle.Flex;
            // Trigger fade-in on next frame so transition plays
            overlay.schedule.Execute(() => overlay.AddToClassList("reward-reveal--visible"));

            AudioManager.Instance?.PlaySFX("ui_panel_open");

            StartCoroutine(RevealCards(rewards));
        }

        public void Hide()
        {
            if (overlay == null) return;
            overlay.RemoveFromClassList("reward-reveal--visible");
            overlay.style.display = DisplayStyle.None;
            cardsContainer.Clear();
        }

        private void Collect()
        {
            AudioManager.Instance?.PlaySFX("ui_panel_close");
            var callback = onCollect;
            onCollect = null;
            Hide();
            callback?.Invoke();
        }

        private IEnumerator RevealCards(List<RewardEntry> rewards)
        {
            foreach (var reward in rewards)
            {
                var card = BuildCard(reward);
                cardsContainer.Add(card);

                // Stagger: wait a frame then trigger scale-in
                yield return null;
                card.AddToClassList("reward-card--visible");

                yield return new WaitForSeconds(0.1f);
            }
        }

        private VisualElement BuildCard(RewardEntry reward)
        {
            int tier = ConfigService.Instance?.GetSeed(reward.seedName)?.tier ?? 0;
            string tierClass = $"reward-card--tier{Mathf.Min(tier, 4)}";

            var card = new VisualElement();
            card.AddToClassList("reward-card");
            card.AddToClassList(tierClass);

            // Glow element behind card
            var glow = new VisualElement();
            glow.AddToClassList("reward-card-glow");
            card.Add(glow);

            // Seed sprite
            var sprite = new VisualElement();
            sprite.AddToClassList("reward-card-sprite");
            string spriteKey = $"items/{SpriteService.SeedToSpriteKey(reward.seedName)}/seed";
            var tex = SpriteService.Instance?.GetTexture(spriteKey);
            if (tex != null)
                sprite.style.backgroundImage = new StyleBackground(tex);
            card.Add(sprite);

            // Seed name
            var nameLabel = new Label(PlotManager.GetSeedDisplayName(reward.seedName));
            nameLabel.AddToClassList("reward-card-name");
            card.Add(nameLabel);

            // Count badge (only if > 1)
            if (reward.count > 1)
            {
                var countLabel = new Label($"x{reward.count}");
                countLabel.AddToClassList("reward-card-count");
                card.Add(countLabel);
            }

            return card;
        }
    }
}
