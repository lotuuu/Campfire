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
            var btnSeeds = root.Q<Button>("btn-seeds");
            var btnCraft = root.Q<Button>("btn-craft");
            var btnMail = root.Q<Button>("btn-mail");

            btnSeeds?.RegisterCallback<ClickEvent>(_ => OnApothekeClicked?.Invoke());
            btnCraft?.RegisterCallback<ClickEvent>(_ => OnBuildClicked?.Invoke());
            btnMail?.RegisterCallback<ClickEvent>(_ => OnLettersClicked?.Invoke());
        }
    }
}
