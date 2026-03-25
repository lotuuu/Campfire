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
            float pixelsPerPoint = root.panel?.scaledPixelsPerPoint ?? 1f;

            float topBleed = (screenHeight - safeArea.yMax) / pixelsPerPoint;
            float bottomBleed = safeArea.y / pixelsPerPoint;

            // Use absolute point values — percentage padding resolves relative to
            // element width (CSS spec), which is wrong for vertical insets.
            // No top padding — campsite content extends into the Dynamic Island zone
            // since there's no top bar. The floating HUD still respects safe area.
            campRoot.style.paddingBottom = bottomBleed;

            // Side HUD elements (profile, menu) can extend into Dynamic Island zone
            // since they're at the edges. Only the center weather bar needs safe area offset.
            var weatherBarWrapper = root.Q("hud-weather-bar-wrapper");
            if (weatherBarWrapper != null)
                weatherBarWrapper.style.paddingTop = topBleed;
            var forecastBloom = root.Q("forecast-bloom");
            if (forecastBloom != null)
                forecastBloom.style.top = topBleed;
            var profileBloom = root.Q("profile-bloom");
            if (profileBloom != null)
                profileBloom.style.top = topBleed;

            // Pull bottom-nav background down into the bottom bleed area
            var bottomNav = root.Q("bottom-nav");
            if (bottomNav != null)
            {
                bottomNav.style.marginBottom = -bottomBleed;
                bottomNav.style.paddingBottom = bottomBleed;
            }

            // Same bleed treatment for tutorial hint and dialogue boxes
            var tutorialHintBox = root.Q("tutorial-hint-box");
            if (tutorialHintBox != null)
            {
                tutorialHintBox.style.marginBottom = -bottomBleed;
                tutorialHintBox.style.paddingBottom = bottomBleed;
            }

            var dialogueBox = root.Q("dialogue-box");
            if (dialogueBox != null)
            {
                dialogueBox.style.marginBottom = -bottomBleed;
                dialogueBox.style.paddingBottom = bottomBleed;
            }

            // After layout, measure the nav bar's actual height and apply it
            // to hint/dialogue boxes so they match exactly.
            if (bottomNav != null)
            {
                EventCallback<GeometryChangedEvent> onLayout = null;
                onLayout = evt =>
                {
                    float navHeight = bottomNav.resolvedStyle.height;
                    if (navHeight <= 0) return;

                    if (tutorialHintBox != null)
                        tutorialHintBox.style.height = navHeight;
                    if (dialogueBox != null)
                        dialogueBox.style.height = navHeight;

                    bottomNav.UnregisterCallback(onLayout);
                };
                bottomNav.RegisterCallback(onLayout);
            }
        }
    }
}
