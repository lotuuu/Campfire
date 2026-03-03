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
        private string TmpPath => SavePath + ".tmp";
        private string BakPath => SavePath + ".bak";
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
            try
            {
                var json = JsonUtility.ToJson(Data, true);
                File.WriteAllText(TmpPath, json);

                if (File.Exists(SavePath))
                    File.Replace(TmpPath, SavePath, BakPath);
                else
                    File.Move(TmpPath, SavePath);
            }
            catch (Exception e)
            {
                Debug.LogError($"SocialSaveManager: Flush failed — {e.Message}");
            }
        }

        public void Load()
        {
            var data = TryLoadFrom(SavePath) ?? TryLoadFrom(BakPath);
            Data = data ?? new SocialData();
        }

        private SocialData TryLoadFrom(string path)
        {
            if (!File.Exists(path)) return null;

            try
            {
                var json = File.ReadAllText(path);
                var data = JsonUtility.FromJson<SocialData>(json);
                if (data != null) return data;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"SocialSaveManager: Failed to load from {Path.GetFileName(path)} — {e.Message}");
            }

            return null;
        }

        public void DeleteSave()
        {
            _isDirty = false;
            if (File.Exists(SavePath)) File.Delete(SavePath);
            if (File.Exists(BakPath)) File.Delete(BakPath);
            if (File.Exists(TmpPath)) File.Delete(TmpPath);
            Data = new SocialData();
        }
    }
}
