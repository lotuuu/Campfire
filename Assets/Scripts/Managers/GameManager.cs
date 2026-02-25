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
            Application.targetFrameRate = 120;
        }

        private void Start()
        {
            if (SaveManager.Instance.Data.seedInventory.Count == 0)
            {
                SeedRegistry.Instance.AddSeed("Quicksprout", 5);
                CurrencyManager.Instance.Add(CurrencyType.SunShards, 10);
                CurrencyManager.Instance.Add(CurrencyType.Gold, 200);
            }
        }
    }
}
