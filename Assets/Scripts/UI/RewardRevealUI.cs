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

        private VisualElement root;
        private VisualElement overlay;
        private VisualElement titleGroup;
        private Label titleLabel;
        private Label subtitleLabel;
        private VisualElement cardsContainer;
        private Button collectBtn;

        private Action onCollect;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        public void Initialize(VisualElement rootElement)
        {
            root = rootElement;
            overlay = root.Q("reward-reveal-overlay");
            titleGroup = root.Q("reward-reveal-title-group");
            titleLabel = root.Q<Label>("reward-reveal-title");
            subtitleLabel = root.Q<Label>("reward-reveal-subtitle");
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

        public void Show(string title, string subtitle, List<RewardEntry> rewards, Action onCollectCallback)
        {
            if (overlay == null || rewards == null || rewards.Count == 0)
            {
                onCollectCallback?.Invoke();
                return;
            }

            onCollect = onCollectCallback;
            titleLabel.text = title;

            if (subtitleLabel != null)
            {
                bool hasSubtitle = !string.IsNullOrEmpty(subtitle);
                subtitleLabel.text = hasSubtitle ? subtitle.ToUpper() : "";
                subtitleLabel.style.display = hasSubtitle ? DisplayStyle.Flex : DisplayStyle.None;
            }

            cardsContainer.Clear();

            overlay.style.display = DisplayStyle.Flex;
            // Trigger fade-in on next frame so transition plays
            overlay.schedule.Execute(() => overlay.AddToClassList("reward-reveal--visible"));

            AudioManager.Instance?.PlaySFX("quest_reward_open");
            StartCoroutine(RevealCards(MergeRewards(rewards)));
        }

        // Backwards-compatible overload
        public void Show(string title, List<RewardEntry> rewards, Action onCollectCallback)
        {
            Show(title, null, rewards, onCollectCallback);
        }

        public void Hide()
        {
            if (overlay == null) return;
            overlay.RemoveFromClassList("reward-reveal--visible");
            overlay.style.display = DisplayStyle.None;
            cardsContainer.Clear();

            // Reset any inline opacity overrides from fly-out
            if (titleGroup != null) titleGroup.style.opacity = StyleKeyword.Null;
            if (subtitleLabel != null) subtitleLabel.style.opacity = StyleKeyword.Null;
            if (collectBtn != null)
            {
                collectBtn.style.opacity = StyleKeyword.Null;
                collectBtn.SetEnabled(true);
            }
        }

        private void Collect()
        {
            collectBtn.SetEnabled(false);
            StartCoroutine(FlyCardsAndClose());
        }

        private IEnumerator FlyCardsAndClose()
        {
            var targetBtn = root.Q("btn-seeds");

            // Fallback: if target not found, close immediately
            if (targetBtn == null)
            {
                CloseImmediate();
                yield break;
            }

            // Wait one frame to ensure layout is current
            yield return null;

            var targetBound = targetBtn.worldBound;
            float targetCX = targetBound.center.x;
            float targetCY = targetBound.center.y;

            // Fade out title, subtitle, and button
            if (titleGroup != null) titleGroup.style.opacity = 0;
            if (subtitleLabel != null) subtitleLabel.style.opacity = 0;
            collectBtn.style.opacity = 0;

            // Collect card elements
            var cards = new List<VisualElement>();
            foreach (var child in cardsContainer.Children())
            {
                if (child.ClassListContains("reward-card"))
                    cards.Add(child);
            }

            const float staggerDelay = 0.08f;
            const float transitionDuration = 0.35f;

            bool firstCard = true;
            foreach (var card in cards)
            {
                var cardBound = card.worldBound;
                float dx = targetCX - cardBound.center.x;
                float dy = targetCY - cardBound.center.y;

                card.style.translate = new StyleTranslate(
                    new Translate(new Length(dx, LengthUnit.Pixel), new Length(dy, LengthUnit.Pixel)));
                card.AddToClassList("reward-card--flying");

                if (firstCard)
                {
                    StartCoroutine(PlayDelayed("mallum_gear_up", transitionDuration));
                    firstCard = false;
                }

                yield return new WaitForSeconds(staggerDelay);
            }

            // Wait for the last card's transition to complete
            yield return new WaitForSeconds(transitionDuration);

            var callback = onCollect;
            onCollect = null;
            Hide();
            callback?.Invoke();
        }

        private IEnumerator PlayDelayed(string key, float delay)
        {
            yield return new WaitForSeconds(delay);
            AudioManager.Instance?.PlaySFX(key);
        }

        private void CloseImmediate()
        {
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

                yield return new WaitForSeconds(0.12f);
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

            // Glow ring behind card
            var glow = new VisualElement();
            glow.AddToClassList("reward-card-glow");
            card.Add(glow);

            // Inner glow overlay
            var innerGlow = new VisualElement();
            innerGlow.AddToClassList("reward-card-inner-glow");
            card.Add(innerGlow);

            // Item sprite
            var sprite = new VisualElement();
            sprite.AddToClassList("reward-card-sprite");
            string spriteKey = SpriteService.ItemToSpriteKey(reward.itemKey);
            var tex = spriteKey != null ? SpriteService.Instance?.GetTexture(spriteKey) : null;
            if (tex != null)
                sprite.style.backgroundImage = new StyleBackground(tex);
            card.Add(sprite);

            // Item name
            var nameLabel = new Label(ConfigService.Instance?.GetItemDisplayName(reward.itemKey) ?? reward.itemKey);
            nameLabel.AddToClassList("reward-card-name");
            card.Add(nameLabel);

            // Count — always shown
            var countLabel = new Label($"x{reward.count}");
            countLabel.AddToClassList("reward-card-count");
            card.Add(countLabel);

            return card;
        }
    }
}
