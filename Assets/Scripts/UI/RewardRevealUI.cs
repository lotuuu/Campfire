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

            StartCoroutine(RevealCards(MergeRewards(rewards)));
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

        private static List<RewardEntry> MergeRewards(List<RewardEntry> rewards)
        {
            var merged = new List<RewardEntry>();
            foreach (var r in rewards)
            {
                var existing = merged.Find(m => m.itemKey == r.itemKey);
                if (existing != null)
                    existing.count += r.count;
                else
                    merged.Add(new RewardEntry { itemKey = r.itemKey, count = r.count });
            }
            return merged;
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
            // Look up tier: reward.itemKey might be a seed key like "basil_seed"
            string plantName = SpriteService.SeedToSpriteKey(reward.itemKey);
            int tier = ConfigService.Instance?.GetSeed(plantName)?.tier ?? 0;
            string tierClass = $"reward-card--tier{Mathf.Min(tier, 4)}";

            var card = new VisualElement();
            card.AddToClassList("reward-card");
            card.AddToClassList(tierClass);

            // Glow element behind card
            var glow = new VisualElement();
            glow.AddToClassList("reward-card-glow");
            card.Add(glow);

            // Item sprite
            var sprite = new VisualElement();
            sprite.AddToClassList("reward-card-sprite");
            string spriteKey = SpriteService.ItemToSpriteKey(reward.itemKey);
            var tex = spriteKey != null ? SpriteService.Instance?.GetTexture(spriteKey) : null;
            if (tex != null)
                sprite.style.backgroundImage = new StyleBackground(tex);
            card.Add(sprite);

            // Item name
            var nameLabel = new Label(ConfigService.Instance.GetItemDisplayName(reward.itemKey));
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
