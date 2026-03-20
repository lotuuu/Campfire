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
            BootTimer.Mark("SaveManager.Awake — loading save");
            Load();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
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

        /// <summary>
        /// Flush any pending changes immediately, then disable further updates.
        /// Called before server switch to prevent writing to the wrong save file.
        /// </summary>
        public void FlushAndSuspend()
        {
            if (_isDirty)
            {
                _isDirty = false;
                Flush();
            }
            enabled = false;
        }

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

                // Verify the write succeeded before replacing the real save
                var written = File.ReadAllText(TmpPath);
                if (written != json)
                {
                    Debug.LogError("SaveManager: Tmp file verification failed, skipping replace.");
                    return;
                }

                if (File.Exists(SavePath))
                    File.Replace(TmpPath, SavePath, BakPath);
                else
                    File.Move(TmpPath, SavePath);
            }
            catch (Exception e)
            {
                Debug.LogError($"SaveManager: Flush failed — {e.Message}");
            }

            // Push village snapshot (fire-and-forget, PushVillageSnapshot has its own try/catch)
            if (SocialService.Instance != null && SocialService.Instance.IsSignedIn)
                _ = SocialService.Instance.PushVillageSnapshot();
        }

        public void Load()
        {
            var data = TryLoadFrom(SavePath) ?? TryLoadFrom(BakPath);
            Data = data ?? new SaveData();

            // Version gate: old saves use incompatible item keys — reset to fresh save
            if (Data.version < 2)
            {
                Debug.Log("[SaveManager] Save version too old, resetting to fresh save");
                Data = new SaveData();
            }

            // If tutorial was started but never completed, discard the stale local save
            // so the UI never renders old positions. Server wipe happens later in GameService.
            // Step 0 is excluded: brand-new saves default to 0 and should not be wiped.
            if (Data.tutorialStep > 0 && Data.tutorialStep < TutorialManager.StepComplete)
            {
                Debug.Log("[SaveManager] Tutorial incomplete on load — discarding local save");
                Data = new SaveData();
            }
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
