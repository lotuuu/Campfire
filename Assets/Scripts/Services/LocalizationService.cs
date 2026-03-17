using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Garden
{
    public class LocalizationService : MonoBehaviour
    {
        public static LocalizationService Instance { get; private set; }

        private Dictionary<string, string> _strings = new();
        public string CurrentLocale { get; private set; } = "en";
        public List<string> SupportedLocales { get; private set; } = new() { "en" };
        public event Action OnLocaleChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public bool IsLoaded { get; private set; }

        public string Get(string key, string fallback)
        {
            if (_strings.TryGetValue(key, out var value))
                return value;
            if (IsLoaded)
                Debug.LogWarning($"[Loc] Missing translation key: \"{key}\", using fallback: \"{fallback}\"");
            return fallback;
        }

        public void LoadTranslations(Dictionary<string, string> data, string locale)
        {
            _strings = data ?? new();
            CurrentLocale = locale;
            IsLoaded = true;
            OnLocaleChanged?.Invoke();
        }

        public void SetSupportedLocales(List<string> locales)
        {
            SupportedLocales = locales ?? new() { "en" };
        }

        public static string DetectDeviceLocale()
        {
            return Application.systemLanguage switch
            {
                SystemLanguage.Japanese => "ja",
                SystemLanguage.German => "de",
                SystemLanguage.French => "fr",
                SystemLanguage.Spanish => "es",
                SystemLanguage.Portuguese => "pt",
                SystemLanguage.Chinese => "zh",
                SystemLanguage.Korean => "ko",
                _ => "en"
            };
        }

        public async Task SwitchLocale(string locale)
        {
            var url = ServerConfig.BaseUrl + $"/game/translations?locale={locale}";
            using var req = UnityWebRequest.Get(url);
            req.downloadHandler = new DownloadHandlerBuffer();
            var token = SocialSaveManager.Instance?.Data?.authToken;
            if (!string.IsNullOrEmpty(token))
                req.SetRequestHeader("Authorization", $"Bearer {token}");

            var tcs = new TaskCompletionSource<bool>();
            var op = req.SendWebRequest();
            op.completed += _ => tcs.SetResult(true);
            await tcs.Task;

            if (req.responseCode != 200)
            {
                Debug.LogWarning($"[Loc] Failed to fetch locale {locale}: HTTP {req.responseCode}");
                return;
            }

            var root = MiniJson.Deserialize(req.downloadHandler.text) as Dictionary<string, object>;
            if (root == null) return;

            if (root.TryGetValue("translations", out var transObj) && transObj is Dictionary<string, object> trans)
            {
                var dict = new Dictionary<string, string>();
                foreach (var kv in trans)
                    dict[kv.Key] = kv.Value as string ?? "";
                _strings = dict;
            }

            if (root.TryGetValue("configOverrides", out var overridesObj))
                ConfigService.Instance?.ApplyLocaleOverrides(overridesObj as Dictionary<string, object>);

            CurrentLocale = locale;

            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.Data.locale = locale;
                SaveManager.Instance.Save();
            }

            OnLocaleChanged?.Invoke();
        }
    }
}
