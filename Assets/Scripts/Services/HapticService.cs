using UnityEngine;

namespace Garden
{
    public static class HapticService
    {
        public static void Vibrate()
        {
            if (SaveManager.Instance?.Data == null) return;
            if (!SaveManager.Instance.Data.vibrationEnabled) return;

#if UNITY_IOS || UNITY_ANDROID
            Handheld.Vibrate();
#endif
        }
    }
}
