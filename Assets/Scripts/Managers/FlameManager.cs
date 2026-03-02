using System;
using UnityEngine;

namespace Garden
{
    public class FlameManager : MonoBehaviour
    {
        public static FlameManager Instance { get; private set; }

        [SerializeField] private FlameConfig config;

        public FlameConfig Config => config;
        public int Level => SaveManager.Instance.Data.flameLevel;
        public float ManaPerSecond => config.GetManaPerSecond(Level);
        public int MaxEntities => config.GetMaxEntities(Level);

        public int CurrentEntityCount
        {
            get
            {
                var data = SaveManager.Instance.Data;
                return data.plots.Count + data.vases.Count + data.gardens.Count;
            }
        }

        public bool CanPlaceEntity => CurrentEntityCount < MaxEntities;

        public event Action OnFlameUpgraded;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Update()
        {
            SaveManager.Instance.Data.mana += ManaPerSecond * Time.deltaTime;
        }

        public bool CanUpgrade()
        {
            return Level < config.MaxLevel &&
                   CurrencyManager.Instance.CanAffordMana(config.GetUpgradeCost(Level));
        }

        public bool UpgradeFlame()
        {
            if (!CanUpgrade()) return false;
            if (!CurrencyManager.Instance.SpendMana(config.GetUpgradeCost(Level))) return false;
            SaveManager.Instance.Data.flameLevel++;
            SaveManager.Instance.Save();
            OnFlameUpgraded?.Invoke();
            return true;
        }
    }
}
