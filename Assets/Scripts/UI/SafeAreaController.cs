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
            campRoot.style.paddingTop = topBleed;
            campRoot.style.paddingBottom = bottomBleed;

            // Pull top-bar background up into the safe area with negative margin,
            // then add matching internal padding so content stays below the notch
            var topBar = root.Q("top-bar");
            if (topBar != null)
            {
                topBar.style.marginTop = -topBleed;
                topBar.style.paddingTop = topBleed;
            }

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
