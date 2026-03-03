using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class DialogueUI : MonoBehaviour
    {
        private VisualElement overlay;
        private Label speakerLabel;
        private Label textLabel;
        private Label tapHint;

        private List<string> lines;
        private string speaker;
        private int currentIndex;
        private Action onComplete;

        public void Initialize(VisualElement root)
        {
            overlay = root.Q("dialogue-overlay");
            speakerLabel = root.Q<Label>("dialogue-speaker");
            textLabel = root.Q<Label>("dialogue-text");
            tapHint = root.Q<Label>("dialogue-tap-hint");

            overlay?.RegisterCallback<ClickEvent>(evt =>
            {
                evt.StopPropagation();
                Advance();
            });

            Hide();
        }

        public void Show(string speakerName, List<string> dialogueLines, Action onDialogueComplete)
        {
            if (dialogueLines == null || dialogueLines.Count == 0)
            {
                onDialogueComplete?.Invoke();
                return;
            }

            speaker = speakerName;
            lines = dialogueLines;
            currentIndex = 0;
            onComplete = onDialogueComplete;

            if (speakerLabel != null) speakerLabel.text = speaker;
            ShowCurrentLine();

            if (overlay != null) overlay.style.display = DisplayStyle.Flex;
        }

        public void Hide()
        {
            if (overlay != null) overlay.style.display = DisplayStyle.None;
        }

        private void Advance()
        {
            currentIndex++;
            if (currentIndex < lines.Count)
            {
                ShowCurrentLine();
            }
            else
            {
                Hide();
                onComplete?.Invoke();
            }
        }

        private void ShowCurrentLine()
        {
            if (textLabel != null)
                textLabel.text = lines[currentIndex];
            if (tapHint != null)
                tapHint.text = currentIndex < lines.Count - 1 ? "Tap to continue" : "Tap to close";
        }
    }
}
