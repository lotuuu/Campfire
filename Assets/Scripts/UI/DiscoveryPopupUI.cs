using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class DiscoveryPopupUI : MonoBehaviour
    {
        public event Action OnDismissed;

        private VisualElement _container;
        private VisualTreeAsset _template;
        private bool _dismissing;

        public void Initialize(VisualElement root)
        {
            _container = root.Q<VisualElement>("discovery-popup");
            _template = Resources.Load<VisualTreeAsset>("UI/Templates/DiscoveryPopup");
        }

        public void Show(VariantData variant)
        {
            _container.Clear();
            _dismissing = false;
            if (_template == null) { Debug.LogError("[DiscoveryPopupUI] Template not loaded — was Initialize() called?"); return; }

            var popup = _template.CloneTree();
            popup.style.flexGrow = 1;
            _container.Add(popup);
            _container.style.display = DisplayStyle.Flex;

            // Variant sprite
            var spriteContainer = popup.Q<VisualElement>("sprite-container");
            if (variant.variantSprite != null)
                spriteContainer.style.backgroundImage = new StyleBackground(variant.variantSprite);

            // Glow colors from variant primary color
            var variantColor = variant.primaryColor;
            popup.Q<VisualElement>("card-radiance").style.backgroundColor = new StyleColor(variantColor);
            popup.Q<VisualElement>("sprite-glow").style.backgroundColor = new StyleColor(variantColor);

            // Tint sprite ring with variant color
            var ring = popup.Q<VisualElement>("sprite-ring");
            var ringColor = new StyleColor(WithAlpha(variantColor, 0.25f));
            ring.style.borderTopColor = ringColor;
            ring.style.borderBottomColor = ringColor;
            ring.style.borderLeftColor = ringColor;
            ring.style.borderRightColor = ringColor;

            // Text
            popup.Q<Label>("variant-name").text = variant.variantName;
            popup.Q<Label>("variant-description").text = variant.description;

            // Rarity badge
            string rarityKey = variant.rarity.ToString().ToLower();
            popup.Q<VisualElement>("rarity-badge").AddToClassList($"badge-{rarityKey}");
            popup.Q<Label>("rarity-label").text = variant.rarity.ToString().ToUpper();

            // Share button
            string capturedName = variant.variantName;
            popup.Q<Button>("share-button").clicked += () => StartCoroutine(ShareCoroutine(capturedName));

            // Tap card stops propagation — prevents card taps from dismissing the overlay
            popup.Q<VisualElement>("discovery-card").RegisterCallback<ClickEvent>(evt => evt.StopPropagation());

            // Tap overlay background to dismiss
            popup.Q<VisualElement>("discovery-overlay").RegisterCallback<ClickEvent>(_ => Dismiss());

            // Entrance animation
            PlayEntranceAnimation(popup);

            // Breathing glow on sprite
            var spriteGlow = popup.Q<VisualElement>("sprite-glow");
            spriteGlow.schedule.Execute(() =>
            {
                float pulse = 0.12f + 0.04f * Mathf.Sin(Time.time * 1.8f);
                spriteGlow.style.opacity = pulse;
            }).Every(33);
        }

        private void PlayEntranceAnimation(VisualElement popup)
        {
            var card = popup.Q("discovery-card");
            var header = popup.Q(className: "discovery-header");
            var spriteStage = popup.Q("sprite-stage");
            var nameLabel = popup.Q("variant-name");
            var badge = popup.Q("rarity-badge");
            var divider = popup.Q(className: "ornament-divider");
            var description = popup.Q("variant-description");
            var actions = popup.Q(className: "discovery-actions");

            // Initial hidden states
            card.style.opacity = 0f;
            card.style.scale = new StyleScale(new Scale(new Vector3(0.92f, 0.92f, 1f)));

            var staggered = new[] { header, spriteStage, nameLabel, badge, divider, description, actions };
            foreach (var el in staggered)
                el.style.opacity = 0f;

            spriteStage.style.scale = new StyleScale(new Scale(new Vector3(0.85f, 0.85f, 1f)));

            // Card entrance
            card.schedule.Execute(() =>
            {
                card.style.opacity = 1f;
                card.style.scale = new StyleScale(new Scale(Vector3.one));
            }).ExecuteLater(50);

            // Staggered child reveals
            const long baseDelay = 200;
            const long stagger = 100;
            for (int i = 0; i < staggered.Length; i++)
            {
                var el = staggered[i];
                long delay = baseDelay + stagger * i;
                if (el == spriteStage)
                {
                    el.schedule.Execute(() =>
                    {
                        el.style.opacity = 1f;
                        el.style.scale = new StyleScale(new Scale(Vector3.one));
                    }).ExecuteLater(delay);
                }
                else
                {
                    el.schedule.Execute(() => el.style.opacity = 1f).ExecuteLater(delay);
                }
            }
        }

        private IEnumerator ShareCoroutine(string variantName)
        {
            yield return new WaitForEndOfFrame();

            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            string path = Path.Combine(Application.temporaryCachePath, "discovery.png");
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Destroy(texture);

            new NativeShare()
                .AddFile(path)
                .SetText($"I just discovered {variantName} in Garden! 🌱")
                .Share();
        }

        private void Dismiss()
        {
            if (_dismissing) return;
            _dismissing = true;

            var overlay = _container.Q("discovery-overlay");
            if (overlay == null) { FinishDismiss(); return; }

            overlay.style.opacity = 0f;
            overlay.schedule.Execute(FinishDismiss).ExecuteLater(300);
        }

        private void FinishDismiss()
        {
            _container.Clear();
            _container.style.display = DisplayStyle.None;
            _dismissing = false;
            OnDismissed?.Invoke();
        }

        private static Color WithAlpha(Color c, float a) => new(c.r, c.g, c.b, a);
    }
}
