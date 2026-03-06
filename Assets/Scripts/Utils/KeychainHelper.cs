using System.Runtime.InteropServices;
using UnityEngine;

namespace Garden
{
    public static class KeychainHelper
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void _KeychainSet(string key, string value);

        [DllImport("__Internal")]
        private static extern string _KeychainGet(string key);
#endif

        public static void SetString(string key, string value)
        {
#if UNITY_IOS && !UNITY_EDITOR
            _KeychainSet(key, value);
#else
            Debug.Log($"KeychainHelper.SetString({key}) — no-op outside iOS device");
#endif
        }

        public static string GetString(string key)
        {
#if UNITY_IOS && !UNITY_EDITOR
            return _KeychainGet(key);
#else
            return null;
#endif
        }
    }
}
