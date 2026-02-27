using System.Collections.Generic;
using UnityEngine;

namespace Garden
{
    public class BackyardIsometricView : MonoBehaviour
    {
        // Per-type local transform for instantiated consumable visuals, indexed by (int)ConsumableType.
        // Env-scoped (Fan/Igloo/Heater/Cloud) are children of this root; slot-scoped use position as offset from tile center.
        private static readonly (Vector3 pos, Vector3 euler, Vector3 scale)[] ConsumableTransforms =
        {
            (new Vector3(0f,    0f,    0f), Vector3.zero,                new Vector3(0.05f, 0.05f, 0.05f)), // Fertilizer
            (new Vector3(0f,    0f,    0f), Vector3.zero,                new Vector3(0.05f, 0.05f, 0.05f)), // QualityDirt
            (new Vector3(1.94f,-0.85f, 0f), new Vector3(158f,-123f,-90f),new Vector3(0.5f,  0.5f,  0.5f)), // Fan
            (new Vector3(-0.31f,-0.18f,-1.67f),   new Vector3(17.94f,-142.4f,16.59f),new Vector3(2f,    2f,    2f)),    // Igloo
            (new Vector3(-3f,  -0.33f, 0f), new Vector3(0f, 61f, 0f),    new Vector3(0.005f,0.005f,0.005f)), // Heater
            (new Vector3(0.118f, 2.337f, 0.069f), new Vector3(0f, 40.3f, 0f),  new Vector3(5f,    5f,    5f)),    // Cloud
        };

        private Sprite tileSprite;
        [SerializeField] private string sortingLayerName = "Default";
        [SerializeField] private int baseSortingOrder = 0;
        [SerializeField] private Vector3 gridAnchor = new Vector3(0f, -0.3f, 0f);
        private const float PlantSpriteScale = 1.5f;

        private readonly List<GameObject> tiles = new();
        private readonly List<SpriteRenderer> _plantRenderers = new();
        [SerializeField] private UnityEngine.Object[] consumablePrefabs; // length 6, indexed by (int)ConsumableType
        [SerializeField] private Sprite emptyIndicatorSprite;

        // Local position/scale for the empty-slot ring, in tile-GO local space (pre-TileScale).
        // Y=0.32 centers the ring on the isometric top face (midpoint of upper sprite half).
        private static readonly Vector3 IndicatorLocalPos   = new Vector3(0f, 0.32f, -0.3f);
        private static readonly Vector3 IndicatorLocalScale = new Vector3(0.69f, 0.69f, 1f);
        private static readonly Color   IndicatorColor      = new Color(0.63f, 0.90f, 0.71f, 0.75f);

        // Slot-scoped consumable GOs: tile index → list of GOs (Fertilizer, QualityDirt)
        private readonly Dictionary<int, List<GameObject>> _slotConsumableGOs = new();

        // Env-scoped consumable GOs: type → single GO (Fan, Igloo, Heater, Cloud)
        private readonly Dictionary<ConsumableType, GameObject> _envConsumableGOs = new();

        private readonly List<GameObject> _emptyIndicatorGOs = new();

        // World-space slot labels and progress bars (children of tile GOs).
        private readonly List<TextMesh> _slotLabels = new();
        private readonly List<SpriteRenderer> _progressBgs = new();
        private readonly List<SpriteRenderer> _progressFills = new();
        private static Sprite _pixelSprite;

        private static readonly Vector3 ProgressLocalPos = new Vector3(0f, -0.05f, -1f);
        private static readonly Vector3 LabelLocalPos    = new Vector3(0f, -0.15f, -1f);
        private const float ProgressBarWidth  = 0.4f;
        private const float ProgressBarHeight = 0.04f;

        private Camera mainCam;
        private float slideOffsetX;

        private const int GridColumns = 2;
        private const float TileScale = 1.3f;

        private void Awake()
        {
            mainCam = Camera.main;
        }

        private void Start()
        {
            if (EnvironmentManager.Instance == null) return;
            EnvironmentManager.Instance.OnSlotUnlocked += OnSlotUnlocked;
            SetEnvironment(EnvironmentManager.Instance.ActiveEnvironmentIndex);
        }

        private void OnDestroy()
        {
            if (EnvironmentManager.Instance != null)
                EnvironmentManager.Instance.OnSlotUnlocked -= OnSlotUnlocked;
        }

        public void SetEnvironment(int envIndex)
        {
            if (EnvironmentManager.Instance == null) return;
            var envs = EnvironmentManager.Instance.Environments;
            if (envIndex < 0 || envIndex >= envs.Count) return;

            tileSprite = envs[envIndex].tileSprite;
            if (tileSprite == null)
                Debug.LogWarning($"[BackyardIsometricView] No tileSprite assigned on {envs[envIndex].environmentName}.", this);

            int count = EnvironmentManager.Instance.GetActiveSlotCount(envIndex);
            RebuildGrid(count);
        }

        private void OnSlotUnlocked(int envIndex)
        {
            if (EnvironmentManager.Instance == null || envIndex != EnvironmentManager.Instance.ActiveEnvironmentIndex || tileSprite == null) return;
            SpawnTile(tiles.Count);
            RecenterGrid();
        }

        public void RebuildGrid(int count)
        {
            // SetActive(false) before Destroy so objects stop rendering immediately
            // (Unity defers Destroy to end-of-frame; without this, old tiles render
            // at wrong world positions for one frame after RecenterGrid repositions the transform)
            foreach (var kvp in _envConsumableGOs)
            {
                if (kvp.Value) { kvp.Value.SetActive(false); Destroy(kvp.Value); }
            }
            _envConsumableGOs.Clear();

            _slotConsumableGOs.Clear();

            foreach (var t in tiles)
            {
                if (t) { t.SetActive(false); Destroy(t); }
            }
            tiles.Clear();
            _plantRenderers.Clear();
            _emptyIndicatorGOs.Clear();
            _slotLabels.Clear();
            _progressBgs.Clear();
            _progressFills.Clear();

            for (int i = 0; i < count; i++)
                SpawnTile(i);

            RecenterGrid();
        }

        private void SpawnTile(int index)
        {
            var tileGO = new GameObject($"BackyardTile_{index}");
            tileGO.transform.SetParent(transform, false);
            tileGO.transform.localScale = Vector3.one * TileScale;

            var sr = tileGO.AddComponent<SpriteRenderer>();
            sr.sprite = tileSprite;
            sr.sortingLayerName = sortingLayerName;
            sr.sortingOrder = baseSortingOrder + index / GridColumns;

            // Plant visual: SpriteRenderer driven by SeedData.growthSprites
            var plantGO = new GameObject("PlantSprite");
            plantGO.transform.SetParent(tileGO.transform, false);
            plantGO.transform.localPosition = new Vector3(0f, 0.1f, -0.5f);
            plantGO.transform.localScale = Vector3.one * PlantSpriteScale;
            var plantSR = plantGO.AddComponent<SpriteRenderer>();
            plantSR.sortingLayerName = sortingLayerName;
            plantSR.sortingOrder = baseSortingOrder + index / GridColumns + 1;
            plantGO.SetActive(false);
            _plantRenderers.Add(plantSR);
            tiles.Add(tileGO);

            // Empty-slot indicator ring (world-space sprite child of tile GO)
            GameObject indicatorGO = null;
            if (emptyIndicatorSprite != null)
            {
                indicatorGO = new GameObject("EmptyIndicator");
                indicatorGO.transform.SetParent(tileGO.transform, false);
                indicatorGO.transform.localPosition = IndicatorLocalPos;
                indicatorGO.transform.localScale = IndicatorLocalScale;
                var isr = indicatorGO.AddComponent<SpriteRenderer>();
                isr.sprite = emptyIndicatorSprite;
                isr.sortingLayerName = sortingLayerName;
                isr.sortingOrder = baseSortingOrder + index / GridColumns + 1;
                isr.color = IndicatorColor;
                indicatorGO.SetActive(false);
            }
            _emptyIndicatorGOs.Add(indicatorGO);

            // World-space label (TextMesh)
            var labelGO = new GameObject("SlotLabel");
            labelGO.transform.SetParent(tileGO.transform, false);
            labelGO.transform.localPosition = LabelLocalPos;
            var tm = labelGO.AddComponent<TextMesh>();
            tm.text = "";
            tm.fontSize = 28;
            tm.characterSize = 0.06f;
            tm.anchor = TextAnchor.UpperCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = new Color(0.78f, 0.84f, 0.9f);
            var labelMR = labelGO.GetComponent<MeshRenderer>();
            labelMR.sortingLayerName = sortingLayerName;
            labelMR.sortingOrder = baseSortingOrder + 50;
            labelGO.SetActive(false);
            _slotLabels.Add(tm);

            // World-space progress bar (two sprites: bg + fill)
            EnsurePixelSprite();
            var barGO = new GameObject("ProgressBar");
            barGO.transform.SetParent(tileGO.transform, false);
            barGO.transform.localPosition = ProgressLocalPos;

            var bgGO = new GameObject("Bg");
            bgGO.transform.SetParent(barGO.transform, false);
            bgGO.transform.localScale = new Vector3(ProgressBarWidth, ProgressBarHeight, 1f);
            var bgSR = bgGO.AddComponent<SpriteRenderer>();
            bgSR.sprite = _pixelSprite;
            bgSR.color = new Color(0.08f, 0.14f, 0.2f, 0.5f);
            bgSR.sortingLayerName = sortingLayerName;
            bgSR.sortingOrder = baseSortingOrder + 49;
            _progressBgs.Add(bgSR);

            var fillGO = new GameObject("Fill");
            fillGO.transform.SetParent(barGO.transform, false);
            fillGO.transform.localScale = new Vector3(0f, ProgressBarHeight, 1f);
            var fillSR = fillGO.AddComponent<SpriteRenderer>();
            fillSR.sprite = _pixelSprite;
            fillSR.color = new Color(0.4f, 0.75f, 0.95f, 1f);
            fillSR.sortingLayerName = sortingLayerName;
            fillSR.sortingOrder = baseSortingOrder + 50;
            _progressFills.Add(fillSR);

            barGO.SetActive(false);

            PositionTile(index);
        }

        private void PositionTile(int index)
        {
            if (index >= tiles.Count || tileSprite == null) return;
            float ppu = tileSprite.pixelsPerUnit;
            float w = tileSprite.rect.width / ppu * TileScale;
            float h = tileSprite.rect.height / ppu * TileScale;
            int col = index % GridColumns;
            int row = index / GridColumns;
            // Isometric grid: east step = (+w/2, -h/4), south step = (-w/2, -h/4)
            tiles[index].transform.localPosition = new Vector3(
                (col - row) * w * 0.5f,
                (col + row) * -h * 0.25f,
                0f);
        }

        private void RecenterGrid()
        {
            if (tiles.Count == 0 || tileSprite == null) return;
            float ppu = tileSprite.pixelsPerUnit;
            float w = tileSprite.rect.width / ppu * TileScale;
            float h = tileSprite.rect.height / ppu * TileScale;
            int n = tiles.Count;
            // Average position of all tiles in the 2-column grid
            float sumX = 0f, sumY = 0f;
            for (int i = 0; i < n; i++)
            {
                int col = i % GridColumns;
                int row = i / GridColumns;
                sumX += (col - row) * w * 0.5f;
                sumY += (col + row) * -h * 0.25f;
            }
            transform.position = gridAnchor - new Vector3(sumX / n - slideOffsetX, sumY / n, 0f);
        }

        private static void EnsurePixelSprite()
        {
            if (_pixelSprite != null) return;
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            _pixelSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }

        public void SetSlotLabel(int index, string text, bool visible)
        {
            if (index < 0 || index >= _slotLabels.Count) return;
            var tm = _slotLabels[index];
            if (tm == null) return;
            tm.gameObject.SetActive(visible);
            tm.text = text ?? "";
        }

        public void SetSlotProgress(int index, float progress, bool visible)
        {
            if (index < 0 || index >= _progressBgs.Count) return;
            var bg = _progressBgs[index];
            if (bg == null) return;
            bg.transform.parent.gameObject.SetActive(visible);
            if (index < _progressFills.Count && _progressFills[index] != null)
            {
                float w = ProgressBarWidth * Mathf.Clamp01(progress);
                _progressFills[index].transform.localScale = new Vector3(w, ProgressBarHeight, 1f);
                _progressFills[index].transform.localPosition = new Vector3((w - ProgressBarWidth) * 0.5f, 0f, -0.01f);
            }
        }

        /// <summary>Shift all tiles horizontally to follow a page slide (world units).</summary>
        public void SetSlideOffset(float worldDeltaX)
        {
            if (Mathf.Approximately(slideOffsetX, worldDeltaX)) return;
            slideOffsetX = worldDeltaX;
            RecenterGrid();
        }

        /// <summary>Screen-space center of a tile in pixels (bottom-left origin, Y-up).</summary>
        public Vector2 GetTileScreenCenter(int index)
        {
            if (index < 0 || index >= tiles.Count || mainCam == null)
                return Vector2.zero;
            return mainCam.WorldToScreenPoint(tiles[index].transform.position);
        }

        /// <summary>
        /// Screen-space hit bounds for a tile in pixels (bottom-left origin, Y-up).
        /// Covers the full sprite so sides are hittable on a single tile. Overlap between
        /// adjacent tiles is resolved by DOM ordering (front tile is last child, wins first).
        /// </summary>
        public Rect GetTileScreenBounds(int index)
        {
            if (index < 0 || index >= tiles.Count || tileSprite == null || mainCam == null)
                return Rect.zero;
            var worldPos = tiles[index].transform.position;
            float ppu = tileSprite.pixelsPerUnit;
            float halfW = tileSprite.rect.width * 0.5f / ppu * TileScale;
            float halfH = tileSprite.rect.height * 0.5f / ppu * TileScale;
            var bl = (Vector2)mainCam.WorldToScreenPoint(worldPos + new Vector3(-halfW, -halfH));
            var tr = (Vector2)mainCam.WorldToScreenPoint(worldPos + new Vector3( halfW,  halfH));
            return new Rect(bl.x, bl.y, tr.x - bl.x, tr.y - bl.y);
        }

        /// <summary>Shows or hides the empty-slot indicator ring for a tile.</summary>
        public void SetEmptyIndicator(int index, bool visible)
        {
            if (index < 0 || index >= _emptyIndicatorGOs.Count) return;
            var go = _emptyIndicatorGOs[index];
            if (go != null) go.SetActive(visible);
        }

        public void SetPlantVisual(int index, PlantState state, Color color)
        {
            if (index < 0 || index >= _plantRenderers.Count) return;
            var sr = _plantRenderers[index];
            if (sr == null) return;
            sr.gameObject.SetActive(state != PlantState.Empty);
        }

        public void SetPlantSprite(int index, Sprite sprite)
        {
            if (index < 0 || index >= _plantRenderers.Count) return;
            var sr = _plantRenderers[index];
            if (sr != null) sr.sprite = sprite;
        }

        public void SetPlantScale(int index, float multiplier)
        {
            if (index < 0 || index >= _plantRenderers.Count) return;
            var sr = _plantRenderers[index];
            if (sr == null) return;
            sr.transform.localScale = Vector3.one * (PlantSpriteScale * multiplier);
        }

        /// <summary>Spawns a slot-scoped consumable visual as a child of the tile GO.</summary>
        public void SpawnSlotConsumableVisual(int slotIndex, ConsumableType type)
        {
            if (consumablePrefabs == null || (int)type >= consumablePrefabs.Length)
            {
                Debug.Log($"[BackyardIso] SpawnSlot: no prefab array or {type} out of range (len={consumablePrefabs?.Length})");
                return;
            }
            var prefab = consumablePrefabs[(int)type];
            if (prefab == null)
            {
                Debug.Log($"[BackyardIso] SpawnSlot: prefab for {type} is null — no art assigned");
                return;
            }
            if (slotIndex < 0 || slotIndex >= tiles.Count)
            {
                Debug.Log($"[BackyardIso] SpawnSlot: slotIndex {slotIndex} out of range (tiles={tiles.Count})");
                return;
            }

            if (!_slotConsumableGOs.ContainsKey(slotIndex))
                _slotConsumableGOs[slotIndex] = new List<GameObject>();

            int existing = _slotConsumableGOs[slotIndex].Count;
            var obj = Instantiate((UnityEngine.Object)prefab);
            var instance = obj as GameObject;
            if (instance == null)
            {
                Debug.LogWarning($"[BackyardIso] SpawnSlot: {type} asset is not a prefab — assign a prefab on BackyardIsometricView in the Inspector");
                if (obj != null) DestroyImmediate(obj);
                return;
            }
            instance.transform.SetParent(tiles[slotIndex].transform, false);
            var (pos, euler, scale) = (int)type < ConsumableTransforms.Length
                ? ConsumableTransforms[(int)type]
                : (new Vector3(0.25f, 0.15f, -0.55f), Vector3.zero, Vector3.one * 0.05f);
            instance.transform.localPosition = pos + new Vector3(existing * 0.18f, 0f, 0f);
            instance.transform.localEulerAngles = euler;
            instance.transform.localScale = scale;
            _slotConsumableGOs[slotIndex].Add(instance);
        }

        /// <summary>Destroys all slot-scoped consumable GOs for a tile (called on harvest/empty).</summary>
        public void ClearSlotConsumableVisuals(int slotIndex)
        {
            if (!_slotConsumableGOs.TryGetValue(slotIndex, out var gos)) return;
            foreach (var go in gos) if (go) Destroy(go);
            gos.Clear();
            _slotConsumableGOs.Remove(slotIndex);
        }

        /// <summary>
        /// Spawns an env-scoped consumable visual as a child of this transform at a fixed position.
        /// Replaces any existing GO of the same type.
        /// </summary>
        public void SpawnEnvConsumableVisual(ConsumableType type)
        {
            if (consumablePrefabs == null || (int)type >= consumablePrefabs.Length)
            {
                Debug.Log($"[BackyardIso] SpawnEnv: no prefab array or {type} out of range (len={consumablePrefabs?.Length})");
                return;
            }
            var prefab = consumablePrefabs[(int)type];
            if (prefab == null)
            {
                Debug.Log($"[BackyardIso] SpawnEnv: prefab for {type} is null — not assigned in Inspector");
                return;
            }

            // Clear all env consumable GOs — only one allowed at a time
            ClearAllEnvConsumableVisuals();

            var obj = Instantiate((UnityEngine.Object)prefab);
            var instance = obj as GameObject;
            if (instance == null)
            {
                Debug.LogWarning($"[BackyardIso] SpawnEnv: {type} asset is not a prefab — assign a prefab on BackyardIsometricView in the Inspector");
                if (obj != null) DestroyImmediate(obj);
                return;
            }
            instance.transform.SetParent(transform, false);
            var (pos, euler, scale) = (int)type < ConsumableTransforms.Length
                ? ConsumableTransforms[(int)type]
                : (Vector3.zero, Vector3.zero, Vector3.one * 0.05f);
            instance.transform.localPosition = pos;
            instance.transform.localEulerAngles = euler;
            instance.transform.localScale = scale;
            ApplyMaterialOverrides(type, instance);
            _envConsumableGOs[type] = instance;
        }

        private void ApplyMaterialOverrides(ConsumableType type, GameObject instance)
        {
            if (type == ConsumableType.Igloo)
            {
                foreach (var r in instance.GetComponentsInChildren<Renderer>())
                {
                    // Render above all tile sprites
                    r.sortingLayerName = sortingLayerName;
                    r.sortingOrder = 999;

                    foreach (var mat in r.materials)
                    {
                        mat.SetFloat("_Surface", 1f);          // URP: 0=Opaque 1=Transparent
                        mat.SetFloat("_ZWrite", 0f);
                        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                        mat.renderQueue = 3000;
                        var c = mat.color;
                        c.a = 0.55f;
                        mat.color = c;
                    }
                }
            }
        }

        /// <summary>Removes an env-scoped consumable GO.</summary>
        public void ClearEnvConsumableVisual(ConsumableType type)
        {
            if (_envConsumableGOs.TryGetValue(type, out var go))
            {
                if (go) Destroy(go);
                _envConsumableGOs.Remove(type);
            }
        }

        /// <summary>Destroys all env-scoped consumable GOs (called before spawning a replacement).</summary>
        public void ClearAllEnvConsumableVisuals()
        {
            foreach (var kvp in _envConsumableGOs)
                if (kvp.Value) { kvp.Value.SetActive(false); Destroy(kvp.Value); }
            _envConsumableGOs.Clear();
        }
    }
}
