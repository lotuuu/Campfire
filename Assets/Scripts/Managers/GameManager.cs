using UnityEngine;

namespace Garden
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            if (SaveManager.Instance.Data.seedInventory.Count == 0)
            {
                SeedRegistry.Instance.AddSeed("Astra", 5);
                SaveManager.Instance.Data.sunShards = 10;
                SaveManager.Instance.Data.dewdrops = 200;
                SaveManager.Instance.Save();
            }
        }
    }
}
