using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class DialogueUI : MonoBehaviour
    {
        private VisualElement overlay;
        private VisualElement portraitElement;
        private VisualElement dialogueBox;
        private Label speakerLabel;
        private Label textLabel;
        private Label tapHint;
        private VisualElement bottomNav;

        private List<string> lines;
        private string speaker;
        private string speakerLocKey;
        private int currentIndex;
        private Action onComplete;

        // Typewriter state
        private const float CharsPerSecond = 30f;
        private const int CharsPerTick = 3;
        private string fullLineText;
        private int revealedChars;
        private float charTimer;
        private bool isAnimating;
        private int nextTickAt;

        public void Initialize(VisualElement root)
        {
            overlay = root.Q("dialogue-overlay");
            portraitElement = root.Q("dialogue-speaker-portrait");
            dialogueBox = root.Q("dialogue-box");
            speakerLabel = root.Q<Label>("dialogue-speaker");
            textLabel = root.Q<Label>("dialogue-text");
            tapHint = root.Q<Label>("dialogue-tap-hint");
            bottomNav = root.Q("bottom-nav");

            // Tap anywhere on the dim backdrop to advance
            overlay?.RegisterCallback<ClickEvent>(evt =>
            {
                evt.StopPropagation();
                Advance();
            });

            // Tap on the dialogue box to advance
            dialogueBox?.RegisterCallback<ClickEvent>(evt =>
            {
                evt.StopPropagation();
                Advance();
            });

            if (LocalizationService.Instance != null)
                LocalizationService.Instance.OnLocaleChanged += RefreshLocale;

            Hide();
        }

        public void Show(string speakerName, List<string> dialogueLines, Action onDialogueComplete, Texture2D portrait = null, string speakerKey = null)
        {
            if (dialogueLines == null || dialogueLines.Count == 0)
            {
                onDialogueComplete?.Invoke();
                return;
            }

            speaker = speakerName;
            speakerLocKey = speakerKey;
            lines = dialogueLines;
            currentIndex = 0;
            onComplete = onDialogueComplete;

            if (speakerLabel != null) speakerLabel.text = speaker;
            if (portraitElement != null)
            {
                if (portrait != null)
                {
                    portraitElement.style.backgroundImage = new StyleBackground(portrait);
                    portraitElement.style.display = DisplayStyle.Flex;
                }
                else
                {
                    portraitElement.style.display = DisplayStyle.None;
                }
            }
            ShowCurrentLine();

            if (overlay != null) overlay.style.display = DisplayStyle.Flex;
            if (dialogueBox != null)
            {
                dialogueBox.style.display = DisplayStyle.Flex;
                dialogueBox.BringToFront();
            }
            if (bottomNav != null) bottomNav.style.display = DisplayStyle.None;
        }

        public void Hide(bool silent = false)
        {
            if (!silent && overlay != null && overlay.style.display != DisplayStyle.None)
                AudioManager.Instance?.PlaySFX("ui_panel_close");
            if (overlay != null) overlay.style.display = DisplayStyle.None;
            if (dialogueBox != null) dialogueBox.style.display = DisplayStyle.None;
            if (bottomNav != null) bottomNav.style.display = DisplayStyle.Flex;
            isAnimating = false;
        }

        private void Update()
        {
            if (!isAnimating) return;

            charTimer += Time.deltaTime * CharsPerSecond;
            int newChars = Mathf.FloorToInt(charTimer);
            if (newChars <= 0) return;

            charTimer -= newChars;
            int prevChars = revealedChars;
            revealedChars = Mathf.Min(revealedChars + newChars, fullLineText.Length);

            if (textLabel != null)
                textLabel.text = fullLineText.Substring(0, revealedChars);

            if (revealedChars >= nextTickAt && prevChars < nextTickAt)
            {
                AudioManager.Instance?.PlaySFX("dialogue_tick");
                nextTickAt += CharsPerTick;
            }

            if (revealedChars >= fullLineText.Length)
                FinishAnimation();
        }

        private void Advance()
        {
            if (isAnimating)
            {
                // Tap during animation: reveal full line instantly
                FinishAnimation();
                if (textLabel != null)
                    textLabel.text = fullLineText;
                return;
            }

            currentIndex++;
            if (currentIndex < lines.Count)
            {
                ShowCurrentLine();
            }
            else
            {
                Hide(silent: onComplete != null);
                onComplete?.Invoke();
            }
        }

        private void ShowCurrentLine()
        {
            fullLineText = lines[currentIndex];
            revealedChars = 0;
            charTimer = 0f;
            nextTickAt = CharsPerTick;
            isAnimating = true;

            if (textLabel != null)
                textLabel.text = "";
            if (tapHint != null)
                tapHint.style.opacity = 0f;
        }

        private void FinishAnimation()
        {
            isAnimating = false;
            if (tapHint != null)
            {
                tapHint.text = currentIndex < lines.Count - 1
                    ? Loc.Get("ui.dialogue.tap_continue", "Tap to continue")
                    : Loc.Get("ui.dialogue.tap_close", "Tap to close");
                tapHint.style.opacity = 1f;
            }
        }

        private void RefreshLocale()
        {
            if (overlay == null || overlay.style.display == DisplayStyle.None) return;
            if (!string.IsNullOrEmpty(speakerLocKey) && speakerLabel != null)
                speakerLabel.text = Loc.Get(speakerLocKey, speaker);
            if (tapHint != null && !isAnimating && lines != null && currentIndex < lines.Count)
                tapHint.text = currentIndex < lines.Count - 1 ? Loc.Get("ui.dialogue.tap_continue", "Tap to continue") : Loc.Get("ui.dialogue.tap_close", "Tap to close");
        }

        private void OnDestroy()
        {
            if (LocalizationService.Instance != null)
                LocalizationService.Instance.OnLocaleChanged -= RefreshLocale;
        }
    }
}
