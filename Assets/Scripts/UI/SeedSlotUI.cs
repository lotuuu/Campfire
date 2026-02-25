using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public static class SeedSlotUI
    {
        public static VisualElement Create(VisualTreeAsset template, SeedData data, int count, System.Action<SeedData> callback)
        {
            var root = template.CloneTree();
            var slot = root.Q<Button>(className: "seed-slot");

            var nameLabel = root.Q<Label>(className: "seed-name");
            var countLabel = root.Q<Label>(className: "seed-count");
            var icon = root.Q<VisualElement>(className: "seed-icon");

            if (nameLabel != null) nameLabel.text = data.seedName;
            if (countLabel != null) countLabel.text = count < 0 ? "∞" : $"x{count}";
            if (icon != null && data.icon != null)
                icon.style.backgroundImage = new StyleBackground(data.icon);

            if (slot != null)
                slot.clicked += () => callback?.Invoke(data);

            return root;
        }
    }
}
