using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class RainOverlay : MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;

        private VisualElement _overlay;
        private RainDrop[] _drops;
        private int _activeCount;
        private bool _active;
        private float _dropWidth;
        private float _windAngle;
        private Color _dropColor;

        private const int k_StormCount = 180;

        private const float k_RainWidth  = 1.5f;
        private const float k_StormWidth = 2.0f;
        private const float k_RainAngle  = 0.12f;
        private const float k_StormAngle = 0.22f;

        private struct RainDrop
        {
            public float x;       // 0..1 normalized horizontal
            public float y;       // 0..1 normalized vertical (0 = top)
            public float speed;   // screen-heights per second
            public float alpha;
            public float length;  // fraction of screen height
        }

        private void Start()
        {
            if (uiDocument == null)
                uiDocument = FindFirstObjectByType<UIDocument>();

            _overlay = uiDocument.rootVisualElement.Q("rain-overlay");
            if (_overlay == null)
            {
                Debug.LogError("[RainOverlay] 'rain-overlay' VisualElement not found in UIDocument.", this);
                return;
            }
            _overlay.generateVisualContent += Paint;
            _drops = new RainDrop[k_StormCount];
        }

        private void OnDestroy()
        {
            if (_overlay != null)
                _overlay.generateVisualContent -= Paint;
        }

        public void Show(bool storm)
        {
            if (_overlay == null) return;
            _activeCount = storm ? k_StormCount : 80;
            _dropWidth   = storm ? k_StormWidth  : k_RainWidth;
            _windAngle   = storm ? k_StormAngle  : k_RainAngle;
            _dropColor   = storm
                ? new Color(0.71f, 0.82f, 1f)
                : new Color(0.78f, 0.88f, 1f);

            InitDrops(storm);
            _overlay.style.display = DisplayStyle.Flex;
            _active = true;
        }

        public void Hide()
        {
            if (_overlay == null) return;
            _active = false;
            _overlay.style.display = DisplayStyle.None;
        }

        private void InitDrops(bool storm)
        {
            float minLen = storm ? 0.07f : 0.05f;
            float maxLen = storm ? 0.12f : 0.09f;
            float minSpd = storm ? 1.0f  : 0.7f;
            float maxSpd = storm ? 1.6f  : 1.1f;
            float minAlp = storm ? 0.20f : 0.15f;
            float maxAlp = storm ? 0.55f : 0.40f;

            for (int i = 0; i < _activeCount; i++)
            {
                _drops[i] = new RainDrop
                {
                    x      = Random.value,
                    y      = Random.value,   // start spread across screen
                    speed  = Random.Range(minSpd, maxSpd),
                    alpha  = Random.Range(minAlp, maxAlp),
                    length = Random.Range(minLen, maxLen),
                };
            }
        }

        private void Update()
        {
            if (!_active) return;

            float dt = Time.deltaTime;
            for (int i = 0; i < _activeCount; i++)
            {
                _drops[i].y += _drops[i].speed * dt;
                if (_drops[i].y > 1f + _drops[i].length)
                    ResetDrop(ref _drops[i]);
            }
            _overlay.MarkDirtyRepaint();
        }

        private void ResetDrop(ref RainDrop d)
        {
            d.x = Random.value;
            d.y = -d.length;
        }

        private void Paint(MeshGenerationContext ctx)
        {
            if (!_active) return;

            float w = _overlay.resolvedStyle.width;
            float h = _overlay.resolvedStyle.height;
            if (w <= 0 || h <= 0) return;

            var p = ctx.painter2D;
            p.lineWidth = _dropWidth;
            p.lineCap   = LineCap.Round;

            for (int i = 0; i < _activeCount; i++)
            {
                ref var d = ref _drops[i];
                float x0  = d.x * w;
                float y0  = d.y * h;
                float len = d.length * h;
                float dx  = _windAngle * len;

                p.strokeColor = new Color(_dropColor.r, _dropColor.g, _dropColor.b, d.alpha);
                p.BeginPath();
                p.MoveTo(new Vector2(x0,      y0));
                p.LineTo(new Vector2(x0 + dx, y0 + len));
                p.Stroke();
            }
        }
    }
}
