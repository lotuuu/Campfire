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

        public void Initialize(VisualElement root)
        {
            _container = root.Q<VisualElement>("discovery-popup");
            _template = Resources.Load<VisualTreeAsset>("UI/Templates/DiscoveryPopup");
        }

        public void Show(VariantData variant)
        {
            _container.Clear();
            if (_template == null) { Debug.LogError("[DiscoveryPopupUI] Template not loaded — was Initialize() called?"); return; }

            var popup = _template.CloneTree();
            popup.style.flexGrow = 1;
            _container.Add(popup);
            _container.style.display = DisplayStyle.Flex;

            // Variant sprite
            var spriteContainer = popup.Q<VisualElement>("sprite-container");
            if (variant.variantSprite != null)
                spriteContainer.style.backgroundImage = new StyleBackground(variant.variantSprite);

            // Glow color from variant primary color
            popup.Q<VisualElement>("glow-bg").style.backgroundColor = new StyleColor(variant.primaryColor);

            // Text
            popup.Q<Label>("variant-name").text = variant.variantName;
            popup.Q<Label>("variant-description").text = variant.description;

            var rarityLabel = popup.Q<Label>("rarity-label");
            rarityLabel.text = variant.rarity.ToString().ToUpper();
            rarityLabel.AddToClassList($"rarity-{variant.rarity.ToString().ToLower()}");

            // Share button
            string capturedName = variant.variantName;
            popup.Q<Button>("share-button").clicked += () => StartCoroutine(ShareCoroutine(capturedName));

            // Tap card stops propagation — prevents card taps from dismissing the overlay
            popup.Q<VisualElement>("discovery-card").RegisterCallback<ClickEvent>(evt => evt.StopPropagation());

            // Tap overlay background to dismiss
            popup.Q<VisualElement>("discovery-overlay").RegisterCallback<ClickEvent>(_ => Dismiss());
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
            _container.Clear();
            _container.style.display = DisplayStyle.None;
            OnDismissed?.Invoke();
        }
    }
}
