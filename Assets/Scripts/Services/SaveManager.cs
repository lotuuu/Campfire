using System;
using System.IO;
using UnityEngine;

namespace Garden
{
    public class SaveManager : MonoBehaviour
    {
        public static SaveManager Instance { get; private set; }

        public SaveData Data { get; private set; } = new();

        private string SavePath => Path.Combine(Application.persistentDataPath, "save.json");
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
            if (!File.Exists(SavePath)) { Data = new SaveData(); return; }

            try
            {
                var json = File.ReadAllText(SavePath);
                Data = JsonUtility.FromJson<SaveData>(json);
                if (Data == null) Data = new SaveData();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"SaveManager: Failed to load save — resetting. ({e.Message})");
                Data = new SaveData();
            }
        }

        public void DeleteSave()
        {
            _isDirty = false;
            if (File.Exists(SavePath)) File.Delete(SavePath);
            Data = new SaveData();
        }
    }
}