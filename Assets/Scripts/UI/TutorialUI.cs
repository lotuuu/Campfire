using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class TutorialUI : MonoBehaviour
    {
        private VisualElement hintBox;
        private Label hintSpeaker;
        private Label hintText;
        private VisualElement hintCentered;
        private Label hintCenteredSpeaker;
        private Label hintCenteredText;
        private VisualElement bottomNav;
        private VisualElement root;
        private bool isCentered;

        // Highlight pulse state
        private VisualElement currentHighlight;
        private float pulseTimer;
        private bool pulseBright;
        private const float PulseInterval = 0.8f;

        // Deferred highlight — retries until element appears (for dynamically created UI)
        private string pendingHighlightClass;

        // Persistent class-based highlight — survives DOM rebuilds
        private string activeHighlightClass;

        public void Initialize(VisualElement rootElement)
        {
            root = rootElement;
            hintBox = root.Q("tutorial-hint-box");
            hintSpeaker = root.Q<Label>("tutorial-hint-speaker");
            hintText = root.Q<Label>("tutorial-hint-text");
            hintCentered = root.Q("tutorial-hint-centered");
            hintCenteredSpeaker = root.Q<Label>("tutorial-hint-centered-speaker");
            hintCenteredText = root.Q<Label>("tutorial-hint-centered-text");
            bottomNav = root.Q("bottom-nav");
        }

        private void Update()
        {
            // Retry deferred highlight — switch to it once the element appears
            if (pendingHighlightClass != null)
            {
                var element = root.Q(className: pendingHighlightClass);
                if (element != null)
                {
                    // Clear previous highlight if any
                    if (currentHighlight != null)
                    {
                        currentHighlight.RemoveFromClassList("tutorial-highlight");
                        currentHighlight.RemoveFromClassList("tutorial-highlight-dim");
                    }
                    pendingHighlightClass = null;
                    currentHighlight = element;
                    pulseBright = true;
                    pulseTimer = 0f;
                    element.AddToClassList("tutorial-highlight");
                }
            }

            // Re-find element if it was destroyed by a DOM rebuild
            if (currentHighlight == null && activeHighlightClass != null)
            {
                var element = root.Q(className: activeHighlightClass);
                if (element != null)
                {
                    currentHighlight = element;
                    pulseBright = true;
                    pulseTimer = 0f;
                    element.AddToClassList("tutorial-highlight");
                }
            }

            if (currentHighlight == null) return;

            // Detect detached element (panel of root is null when removed from DOM)
            if (currentHighlight.panel == null)
            {
                currentHighlight = null;
                return;
            }

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

        public void ShowHint(string text, string speaker = "Spark of Ara", bool centered = false)
        {
            HideHint();
            isCentered = centered;

            if (centered)
            {
                if (hintCentered == null) return;
                if (hintCenteredSpeaker != null) hintCenteredSpeaker.text = speaker;
                if (hintCenteredText != null) hintCenteredText.text = text;
                hintCentered.style.display = DisplayStyle.Flex;
            }
            else
            {
                if (hintBox == null) return;
                if (hintSpeaker != null) hintSpeaker.text = speaker;
                if (hintText != null) hintText.text = text;
                hintBox.style.display = DisplayStyle.Flex;
                hintBox.BringToFront();
                if (bottomNav != null) bottomNav.style.display = DisplayStyle.None;
            }
        }

        public void HideHint()
        {
            if (hintBox != null)
                hintBox.style.display = DisplayStyle.None;
            if (hintCentered != null)
                hintCentered.style.display = DisplayStyle.None;
            if (bottomNav != null)
                bottomNav.style.display = DisplayStyle.Flex;
            isCentered = false;
        }

        public void HighlightElementByClass(string className)
        {
            ClearHighlight();
            activeHighlightClass = className;
            var element = root.Q(className: className);
            if (element == null)
            {
                // Element not yet in DOM — defer until it appears
                pendingHighlightClass = className;
                return;
            }
            currentHighlight = element;
            pulseBright = true;
            pulseTimer = 0f;
            element.AddToClassList("tutorial-highlight");
        }

        /// <summary>
        /// Queue a class-based highlight that activates when the element appears,
        /// replacing whatever is currently highlighted at that point.
        /// </summary>
        public void DeferHighlightByClass(string className)
        {
            activeHighlightClass = className;
            pendingHighlightClass = className;
        }

        public void HighlightElement(string elementName)
        {
            ClearHighlight();
            activeHighlightClass = null;
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
            pendingHighlightClass = null;
            activeHighlightClass = null;
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
