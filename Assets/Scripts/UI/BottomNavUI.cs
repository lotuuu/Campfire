using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class BottomNavUI : MonoBehaviour
    {
        public event Action OnApothekeClicked;
        public event Action OnLettersClicked;
        public event Action OnBuildClicked;

        public void Initialize(VisualElement root)
        {
            var btnApotheke = root.Q<Button>("btn-apotheke");
            var btnLetters = root.Q<Button>("btn-letters");
            var btnBuild = root.Q<Button>("btn-build");

            btnApotheke?.RegisterCallback<ClickEvent>(_ => OnApothekeClicked?.Invoke());
            btnLetters?.RegisterCallback<ClickEvent>(_ => OnLettersClicked?.Invoke());
            btnBuild?.RegisterCallback<ClickEvent>(_ => OnBuildClicked?.Invoke());
        }
    }
}
