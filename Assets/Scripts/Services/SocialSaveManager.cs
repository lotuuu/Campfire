using System;
using System.IO;
using UnityEngine;

namespace Garden
{
    public class SocialSaveManager : MonoBehaviour
    {
        public static SocialSaveManager Instance { get; private set; }

        public SocialData Data { get; private set; } = new();

        private string SavePath => Path.Combine(Application.persistentDataPath, "social.json");
        private bool _isDirty;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Load();
        }

        private void LateUpdate()
        {
            if (!_isDirty) return;
            _isDirty = false;
            Flush();
        }

        public void Save() => _isDirty = true;

        private void Flush()
        {
            var json = JsonUtility.ToJson(Data, true);
            File.WriteAllText(SavePath, json);
        }

        public void Load()
        {
            if (!File.Exists(SavePath)) { Data = new SocialData(); return; }

            try
            {
                var json = File.ReadAllText(SavePath);
                Data = JsonUtility.FromJson<SocialData>(json);
                if (Data == null) Data = new SocialData();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"SocialSaveManager: Failed to load — resetting. ({e.Message})");
                Data = new SocialData();
            }
        }

        public void DeleteSave()
        {
            _isDirty = false;
            if (File.Exists(SavePath)) File.Delete(SavePath);
            Data = new SocialData();
        }
    }
}
