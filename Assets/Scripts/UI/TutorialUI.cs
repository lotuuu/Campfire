using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class TutorialUI : MonoBehaviour
    {
        private VisualElement hintBar;
        private Label hintSpeaker;
        private Label hintText;
        private VisualElement root;

        // Highlight pulse state
        private VisualElement currentHighlight;
        private float pulseTimer;
        private bool pulseBright;
        private const float PulseInterval = 0.8f;

        public void Initialize(VisualElement rootElement)
        {
            root = rootElement;
            hintBar = root.Q("tutorial-hint-bar");
            hintSpeaker = root.Q<Label>("tutorial-hint-speaker");
            hintText = root.Q<Label>("tutorial-hint-text");
        }

        private void Update()
        {
            if (currentHighlight == null) return;

            pulseTimer += Time.deltaTime;
            if (pulseTimer >= PulseInterval)
            {
                pulseTimer = 0f;
                pulseBright = !pulseBright;
                if (pulseBright)
                {
                    currentHighlight.RemoveFromClassList("tutorial-highlight-dim");
                    currentHighlight.AddToClassList("tutorial-highlight");
                }
                else
                {
                    currentHighlight.RemoveFromClassList("tutorial-highlight");
                    currentHighlight.AddToClassList("tutorial-highlight-dim");
                }
            }
        }

        public void ShowHint(string text, string speaker = "Spark of Ara")
        {
            if (hintBar == null) return;
            if (hintSpeaker != null) hintSpeaker.text = speaker;
            hintText.text = text;
            hintBar.style.display = DisplayStyle.Flex;
        }

        public void HideHint()
        {
            if (hintBar != null)
                hintBar.style.display = DisplayStyle.None;
        }

        public void HighlightElement(string elementName)
        {
            ClearHighlight();
            var element = root.Q(elementName);
            if (element == null) return;
            currentHighlight = element;
            pulseBright = true;
            pulseTimer = 0f;
            element.AddToClassList("tutorial-highlight");
        }

        public void HighlightElement(VisualElement element)
        {
            ClearHighlight();
            if (element == null) return;
            currentHighlight = element;
            pulseBright = true;
            pulseTimer = 0f;
            element.AddToClassList("tutorial-highlight");
        }

        public void ClearHighlight()
        {
            if (currentHighlight != null)
            {
                currentHighlight.RemoveFromClassList("tutorial-highlight");
                currentHighlight.RemoveFromClassList("tutorial-highlight-dim");
                currentHighlight = null;
            }
        }

        public void HideAll()
        {
            HideHint();
            ClearHighlight();
        }
    }
}
