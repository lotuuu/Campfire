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

            // Load nav icons
            SetIcon(root.Q("nav-icon-seeds"), "UI/Icons/nav-seeds");
            SetIcon(root.Q("nav-icon-craft"), "UI/Icons/nav-craft");
            SetIcon(root.Q("nav-icon-mail"), "UI/Icons/nav-mail");
        }

        private static void SetIcon(VisualElement el, string resourcePath)
        {
            if (el == null) return;
            var tex = Resources.Load<Texture2D>(resourcePath);
            if (tex != null)
                el.style.backgroundImage = tex;
        }
    }
}
