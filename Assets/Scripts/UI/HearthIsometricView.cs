using System.Collections.Generic;
using UnityEngine;

namespace Garden
{
    public class HearthIsometricView : MonoBehaviour
    {
        [SerializeField] private Sprite tileSprite;
        [SerializeField] private string sortingLayerName = "Default";
        [SerializeField] private int baseSortingOrder = 0;
        [SerializeField] private Vector3 gridAnchor = new Vector3(0f, -0.3f, 0f);

        private readonly List<GameObject> tiles = new();
        private readonly List<SpriteRenderer> plantRenderers = new();
        private Camera mainCam;

        private const int HearthEnvIndex = 0;

        private void Awake()
        {
            mainCam = Camera.main;
        }

        private void Start()
        {
            if (tileSprite == null)
            {
                Debug.LogError("[HearthIsometricView] tileSprite is not assigned — grid will not render.", this);
                return;
            }
            if (EnvironmentManager.Instance == null) return;
            int count = EnvironmentManager.Instance.GetActiveSlotCount(HearthEnvIndex);
            RebuildGrid(count);
            EnvironmentManager.Instance.OnSlotUnlocked += OnSlotUnlocked;
        }

        private void OnDestroy()
        {
            if (EnvironmentManager.Instance != null)
                EnvironmentManager.Instance.OnSlotUnlocked -= OnSlotUnlocked;
        }

        private void OnSlotUnlocked(int envIndex)
        {
            if (envIndex != HearthEnvIndex || tileSprite == null) return;
            SpawnTile(tiles.Count);
            RecenterGrid();
        }

        public void RebuildGrid(int count)
        {
            foreach (var t in tiles) if (t) Destroy(t);
            tiles.Clear();
            plantRenderers.Clear();

            for (int i = 0; i < count; i++)
                SpawnTile(i);

            RecenterGrid();
        }

        private void SpawnTile(int index)
        {
            var tileGO = new GameObject($"HearthTile_{index}");
            tileGO.transform.SetParent(transform, false);

            var sr = tileGO.AddComponent<SpriteRenderer>();
            sr.sprite = tileSprite;
            sr.sortingLayerName = sortingLayerName;
            sr.sortingOrder = baseSortingOrder;

            // Plant visual: colored circle disc, child of tile
            var plantGO = new GameObject("PlantDisc");
            plantGO.transform.SetParent(tileGO.transform, false);
            plantGO.transform.localPosition = new Vector3(0f, 0.12f, 0f);

            var plantSr = plantGO.AddComponent<SpriteRenderer>();
            plantSr.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd");
            plantSr.sortingLayerName = sortingLayerName;
            plantSr.sortingOrder = baseSortingOrder + 1;
            plantSr.color = Color.clear;

            // Scale disc to ~35% of tile width
            if (tileSprite != null)
            {
                float tileW = tileSprite.rect.width / tileSprite.pixelsPerUnit;
                plantGO.transform.localScale = Vector3.one * (tileW * 0.35f);
            }

            plantGO.SetActive(false);
            tiles.Add(tileGO);
            plantRenderers.Add(plantSr);

            PositionTile(index);
        }

        private void PositionTile(int index)
        {
            if (index >= tiles.Count || tileSprite == null) return;
            float ppu = tileSprite.pixelsPerUnit;
            float w = tileSprite.rect.width / ppu;
            float h = tileSprite.rect.height / ppu;
            // Isometric east-row: each step = +w/2 right, -h/4 down
            tiles[index].transform.localPosition = new Vector3(index * w * 0.5f, index * -h * 0.25f, 0f);
        }

        private void RecenterGrid()
        {
            if (tiles.Count == 0 || tileSprite == null) return;
            float ppu = tileSprite.pixelsPerUnit;
            float w = tileSprite.rect.width / ppu;
            float h = tileSprite.rect.height / ppu;
            int n = tiles.Count;
            // Center of tile cluster in local space
            float localCenterX = (n - 1) * w * 0.25f;
            float localCenterY = (n - 1) * -h * 0.125f;
            transform.position = gridAnchor - new Vector3(localCenterX, localCenterY, 0f);
        }

        /// <summary>Screen-space center of a tile in pixels (bottom-left origin, Y-up).</summary>
        public Vector2 GetTileScreenCenter(int index)
        {
            if (index < 0 || index >= tiles.Count || mainCam == null)
                return Vector2.zero;
            return mainCam.WorldToScreenPoint(tiles[index].transform.position);
        }

        /// <summary>Screen-space bounds of a tile sprite in pixels (bottom-left origin, Y-up).</summary>
        public Rect GetTileScreenBounds(int index)
        {
            if (index < 0 || index >= tiles.Count || tileSprite == null || mainCam == null)
                return Rect.zero;
            var worldPos = tiles[index].transform.position;
            float ppu = tileSprite.pixelsPerUnit;
            float halfW = tileSprite.rect.width * 0.5f / ppu;
            float halfH = tileSprite.rect.height * 0.5f / ppu;
            var bl = (Vector2)mainCam.WorldToScreenPoint(worldPos + new Vector3(-halfW, -halfH));
            var tr = (Vector2)mainCam.WorldToScreenPoint(worldPos + new Vector3(halfW, halfH));
            return new Rect(bl.x, bl.y, tr.x - bl.x, tr.y - bl.y);
        }

        public void SetPlantVisual(int index, PlantState state, Color color)
        {
            if (index < 0 || index >= plantRenderers.Count) return;
            var go = plantRenderers[index].gameObject;
            if (state == PlantState.Empty)
            {
                go.SetActive(false);
                return;
            }
            go.SetActive(true);
            plantRenderers[index].color = color;
        }

        public void SetPlantScale(int index, float uniformScale)
        {
            if (index < 0 || index >= plantRenderers.Count) return;
            plantRenderers[index].transform.localScale = Vector3.one * uniformScale;
        }
    }
}
