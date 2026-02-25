using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class SnowOverlay : MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;

        private VisualElement _overlay;
        private Snowflake[] _flakes;
        private bool _active;
        private float _time;

        private const int k_Count = 120;
        private const float k_DriftAmp = 0.02f; // ±2% screen width sinusoidal sway

        private struct Snowflake
        {
            public float baseX;      // 0..1, horizontal center of oscillation
            public float y;          // 0..1, vertical position (0 = top)
            public float speed;      // screen-heights per second
            public float alpha;
            public float radius;     // pixels
            public float driftPhase; // per-flake phase offset (radians)
            public float driftFreq;  // oscillation cycles per second
        }

        private void Start()
        {
            if (uiDocument == null)
                uiDocument = FindFirstObjectByType<UIDocument>();

            _overlay = uiDocument.rootVisualElement.Q("snow-overlay");
            if (_overlay == null)
            {
                Debug.LogError("[SnowOverlay] 'snow-overlay' VisualElement not found in UIDocument.", this);
                return;
            }
            _overlay.generateVisualContent += Paint;
            _flakes = new Snowflake[k_Count];
        }

        private void OnDestroy()
        {
            if (_overlay != null)
                _overlay.generateVisualContent -= Paint;
        }

        public void Show()
        {
            if (_overlay == null) return;
            _time = 0f;
            InitFlakes();
            _overlay.style.display = DisplayStyle.Flex;
            _active = true;
        }

        public void Hide()
        {
            if (_overlay == null) return;
            _active = false;
            _overlay.style.display = DisplayStyle.None;
        }

        private void InitFlakes()
        {
            for (int i = 0; i < k_Count; i++)
            {
                _flakes[i] = new Snowflake
                {
                    baseX      = Random.value,
                    y          = Random.value,                   // start spread across screen
                    speed      = Random.Range(0.10f, 0.22f),
                    alpha      = Random.Range(0.35f, 0.75f),
                    radius     = Random.Range(2f, 5f),
                    driftPhase = Random.Range(0f, Mathf.PI * 2f),
                    driftFreq  = Random.Range(0.2f, 0.5f),
                };
            }
        }

        private void Update()
        {
            if (!_active) return;

            float dt = Time.deltaTime;
            _time += dt;

            for (int i = 0; i < k_Count; i++)
            {
                _flakes[i].y += _flakes[i].speed * dt;
                if (_flakes[i].y > 1.05f)
                    ResetFlake(ref _flakes[i]);
            }
            _overlay.MarkDirtyRepaint();
        }

        private void ResetFlake(ref Snowflake f)
        {
            f.baseX      = Random.value;
            f.y          = -0.05f;
            f.driftPhase = Random.Range(0f, Mathf.PI * 2f);
        }

        private void Paint(MeshGenerationContext ctx)
        {
            if (!_active) return;

            float w = _overlay.resolvedStyle.width;
            float h = _overlay.resolvedStyle.height;
            if (w <= 0 || h <= 0) return;

            var p = ctx.painter2D;

            for (int i = 0; i < k_Count; i++)
            {
                ref var f = ref _flakes[i];
                float driftX   = k_DriftAmp * Mathf.Sin(_time * f.driftFreq + f.driftPhase);
                float screenX  = (f.baseX + driftX) * w;
                float screenY  = f.y * h;

                p.fillColor = new Color(1f, 1f, 1f, f.alpha);
                p.BeginPath();
                p.Arc(new Vector2(screenX, screenY), f.radius,
                      Angle.Degrees(0), Angle.Degrees(360), ArcDirection.Clockwise);
                p.Fill();
            }
        }
    }
}
