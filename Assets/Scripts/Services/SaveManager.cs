using System;
using System.IO;
using UnityEngine;

namespace Garden
{
    public class SaveManager : MonoBehaviour
    {
        public static SaveManager Instance { get; private set; }

        public SaveData Data { get; private set; } = new();

        private string SavePath => Path.Combine(Application.persistentDataPath, ServerConfig.SavePrefix + "save.json");
        private string TmpPath => SavePath + ".tmp";
        private string BakPath => SavePath + ".bak";
        private const float AutoSaveIntervalSeconds = 30f;
        private bool _isDirty;
        private float _autoSaveTimer;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Load();
        }

        private void Update()
        {
            _autoSaveTimer += Time.unscaledDeltaTime;
            if (_autoSaveTimer >= AutoSaveIntervalSeconds)
            {
                _autoSaveTimer = 0f;
                _isDirty = false;
                Flush();
            }
        }

        private void LateUpdate()
        {
            if (!_isDirty) return;
            _isDirty = false;
            Flush();
        }

        public void Save() => _isDirty = true;

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                _isDirty = false;
                Flush();
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                _isDirty = false;
                Flush();
            }
        }

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
                Debug.LogError($"SaveManager: Flush failed — {e.Message}");
            }

            // Push village snapshot to Firebase (fire-and-forget)
            if (SocialService.Instance != null && SocialService.Instance.IsSignedIn)
                _ = SocialService.Instance.PushVillageSnapshot();
        }

        public void Load()
        {
            var data = TryLoadFrom(SavePath) ?? TryLoadFrom(BakPath);
            Data = data ?? new SaveData();
        }

        private SaveData TryLoadFrom(string path)
        {
            if (!File.Exists(path)) return null;

            try
            {
                var json = File.ReadAllText(path);
                var data = JsonUtility.FromJson<SaveData>(json);
                if (data != null) return data;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"SaveManager: Failed to load from {Path.GetFileName(path)} — {e.Message}");
            }

            return null;
        }

        public void DeleteSave()
        {
            _isDirty = false;
            if (File.Exists(SavePath)) File.Delete(SavePath);
            if (File.Exists(BakPath)) File.Delete(BakPath);
            if (File.Exists(TmpPath)) File.Delete(TmpPath);
            Data = new SaveData();
        }
    }
}
