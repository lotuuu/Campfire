using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class BottomNavUI : MonoBehaviour
    {
        public event Action OnApothekeClicked;
        public event Action OnLettersClicked;
        public event Action OnCraftClicked;

        public void Initialize(VisualElement root)
        {
            var btnApotheke = root.Q<Button>("btn-apotheke");
            var btnLetters = root.Q<Button>("btn-letters");
            var btnCraft = root.Q<Button>("btn-craft");

            btnApotheke?.RegisterCallback<ClickEvent>(_ => OnApothekeClicked?.Invoke());
            btnLetters?.RegisterCallback<ClickEvent>(_ => OnLettersClicked?.Invoke());
            btnCraft?.RegisterCallback<ClickEvent>(_ => OnCraftClicked?.Invoke());
        }
    }
}
