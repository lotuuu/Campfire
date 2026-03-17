using UnityEngine;
using UnityEngine.UIElements;
using DG.Tweening;
using System;

namespace Garden
{
    /// <summary>
    /// Diagonal wipe transition using UI Toolkit VisualElements with DOTween.
    /// Multiple image layers sweep diagonally across the screen at different speeds
    /// for a parallax effect. Call Play() with midpoint and completion callbacks.
    /// </summary>
    public class TransitionWipe : MonoBehaviour
    {
        public static TransitionWipe Instance { get; private set; }

        [System.Serializable]
        public class LayerConfig
        {
            public string elementName;
            public float speed = 1f;
        }

        static readonly Vector2 StartOffset = new Vector2(-120f, -120f);
        static readonly Vector2 EndOffset = new Vector2(120f, 120f);
        const float Duration = 0.8f;
        const float SwitchPoint = 0.781f;
        const float GlobalSpeed = 0.7f;

        static readonly LayerConfig[] LayerConfigs = new[]
        {
            new LayerConfig { elementName = "wipe-layer-0", speed = 1.1f },
            new LayerConfig { elementName = "wipe-layer-1", speed = 0.65f },
            new LayerConfig { elementName = "wipe-layer-2", speed = 1.0f },
            new LayerConfig { elementName = "wipe-layer-3", speed = 0.8f },
        };

        private VisualElement container;
        private VisualElement[] layerElements;
        private Sequence currentSequence;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        public void Initialize(VisualElement root)
        {
            container = root.Q("wipe-container");
            if (container == null) return;

            layerElements = new VisualElement[LayerConfigs.Length];
            for (int i = 0; i < LayerConfigs.Length; i++)
                layerElements[i] = container.Q(LayerConfigs[i].elementName);
        }

        public void Play(Action onMidPoint, Action onComplete)
        {
            currentSequence?.Kill();

            if (container == null || layerElements == null)
            {
                onMidPoint?.Invoke();
                onComplete?.Invoke();
                return;
            }

            container.style.display = DisplayStyle.Flex;

            // Position all layers at start (off-screen top-left)
            for (int i = 0; i < layerElements.Length; i++)
            {
                if (layerElements[i] == null) continue;
                layerElements[i].style.translate =
                    new StyleTranslate(new Translate(
                        new Length(StartOffset.x, LengthUnit.Percent),
                        new Length(StartOffset.y, LengthUnit.Percent)));
            }

            var seq = DOTween.Sequence();
            seq.SetUpdate(true);

            for (int i = 0; i < layerElements.Length; i++)
            {
                if (layerElements[i] == null) continue;
                float s = LayerConfigs[i].speed * GlobalSpeed;
                float dur = Duration / Mathf.Max(s, 0.01f);
                var el = layerElements[i];

                // Tween translate from start% to end%
                float startX = StartOffset.x, startY = StartOffset.y;
                float endX = EndOffset.x, endY = EndOffset.y;
                var tween = DOTween.To(
                    () => 0f,
                    t =>
                    {
                        float x = Mathf.Lerp(startX, endX, t);
                        float y = Mathf.Lerp(startY, endY, t);
                        el.style.translate = new StyleTranslate(
                            new Translate(new Length(x, LengthUnit.Percent), new Length(y, LengthUnit.Percent)));
                    },
                    1f, dur
                ).SetEase(Ease.Linear);

                seq.Insert(0f, tween);
            }

            seq.InsertCallback(Duration * SwitchPoint, () => onMidPoint?.Invoke());

            seq.OnComplete(() =>
            {
                container.style.display = DisplayStyle.None;
                onComplete?.Invoke();
            });

            currentSequence = seq;
        }
    }
}
