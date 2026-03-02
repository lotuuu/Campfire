using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class SafeAreaController : MonoBehaviour
    {
        private void Start()
        {
            var doc = GetComponentInChildren<UIDocument>();
            if (doc == null) return;
            var root = doc.rootVisualElement;
            var campRoot = root.Q("camp-root");
            if (campRoot == null) return;

            var safeArea = Screen.safeArea;
            var screenHeight = Screen.height;

            float topInset = (screenHeight - safeArea.yMax) / screenHeight * 100f;
            float bottomInset = safeArea.y / screenHeight * 100f;

            campRoot.style.paddingTop = new Length(topInset, LengthUnit.Percent);
            campRoot.style.paddingBottom = new Length(bottomInset, LengthUnit.Percent);
        }
    }
}
