using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    /// <summary>
    /// Reads Screen.safeArea at runtime and pushes #top-bar and #bottom-nav
    /// inward so content clears the Dynamic Island, notch, and home indicator.
    /// Returns zero insets in the editor, so layout looks normal during development.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class SafeAreaController : MonoBehaviour
    {
        private UIDocument _doc;
        private VisualElement _topBar;
        private VisualElement _bottomNav;

        // Base padding from USS, captured once geometry resolves.
        private float _topBarBasePaddingTop;
        private float _bottomNavBasePaddingBottom;

        private void Awake()
        {
            _doc = GetComponent<UIDocument>();
        }

        private void Start()
        {
            var root = _doc.rootVisualElement;
            _topBar    = root.Q<VisualElement>("top-bar");
            _bottomNav = root.Q<VisualElement>("bottom-nav");

            // Wait until the layout pass has resolved so we can read base padding.
            root.RegisterCallback<GeometryChangedEvent>(OnGeometryReady);
        }

        private void OnGeometryReady(GeometryChangedEvent evt)
        {
            _doc.rootVisualElement.UnregisterCallback<GeometryChangedEvent>(OnGeometryReady);

            // Capture USS-computed base padding before we override with inline styles.
            if (_topBar    != null) _topBarBasePaddingTop       = _topBar.resolvedStyle.paddingTop;
            if (_bottomNav != null) _bottomNavBasePaddingBottom = _bottomNav.resolvedStyle.paddingBottom;

            ApplySafeArea();
        }

        private void ApplySafeArea()
        {
            var root     = _doc.rootVisualElement;
            var safeArea = Screen.safeArea;

            // Convert screen pixels → UI Toolkit units.
            float scaleY = root.resolvedStyle.height / Screen.height;

            float topInset    = (Screen.height - safeArea.yMax) * scaleY;
            float bottomInset = safeArea.yMin * scaleY;

            if (_topBar != null && topInset > 0)
                _topBar.style.paddingTop = _topBarBasePaddingTop + topInset;

            if (_bottomNav != null && bottomInset > 0)
                _bottomNav.style.paddingBottom = _bottomNavBasePaddingBottom + bottomInset;
        }

#if UNITY_EDITOR
        // Re-apply whenever the Game view safe area changes (Device Simulator).
        private Rect _lastSafeArea;

        private void Update()
        {
            if (Screen.safeArea != _lastSafeArea)
            {
                _lastSafeArea = Screen.safeArea;
                ApplySafeArea();
            }
        }
#endif
    }
}
