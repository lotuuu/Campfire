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

            // Keep original safe area padding on camp-root so layout is unchanged
            campRoot.style.paddingTop = new Length(topInset, LengthUnit.Percent);
            campRoot.style.paddingBottom = new Length(bottomInset, LengthUnit.Percent);

            // Pull top-bar background up into the safe area with negative margin,
            // then add matching internal padding so content stays below the notch
            var topBar = root.Q("top-bar");
            if (topBar != null)
            {
                float pixelsPerPoint = root.panel?.scaledPixelsPerPoint ?? 1f;
                float bleed = (screenHeight - safeArea.yMax) / pixelsPerPoint;
                topBar.style.marginTop = -bleed;
                topBar.style.paddingTop = bleed;
            }
        }
    }
}
