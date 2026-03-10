using System;
using UnityEngine;

namespace Garden
{
    public class DeepLinkService : MonoBehaviour
    {
        public static DeepLinkService Instance { get; private set; }

        public event Action<string> OnInviteCodeReceived;

        private string pendingInviteCode;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            Application.deepLinkActivated += OnDeepLinkActivated;

            // Check if app was launched via deep link
            if (!string.IsNullOrEmpty(Application.absoluteURL))
                OnDeepLinkActivated(Application.absoluteURL);
        }

        private void OnDestroy()
        {
            Application.deepLinkActivated -= OnDeepLinkActivated;
            if (Instance == this) Instance = null;
        }

        private void OnDeepLinkActivated(string url)
        {
            Debug.Log($"DeepLinkService: Received deep link: {url}");

            var code = ParseInviteCode(url);
            if (string.IsNullOrEmpty(code)) return;

            pendingInviteCode = code;
            OnInviteCodeReceived?.Invoke(code);

            // If social service is ready, process immediately
            if (SocialService.Instance != null && SocialService.Instance.IsSignedIn)
                ProcessPendingInvite();
        }

        private void Start()
        {
            if (SocialService.Instance != null)
                SocialService.Instance.OnSignedIn += ProcessPendingInvite;
        }

        private async void ProcessPendingInvite()
        {
            if (string.IsNullOrEmpty(pendingInviteCode)) return;

            var code = pendingInviteCode;
            pendingInviteCode = null;

            // Don't send a request to yourself
            var myCode = SocialService.Instance.FriendCode;
            if (!string.IsNullOrEmpty(myCode) &&
                string.Equals(myCode, code, StringComparison.OrdinalIgnoreCase))
            {
                Debug.Log("DeepLinkService: Ignoring invite — it's your own code");
                return;
            }

            Debug.Log($"DeepLinkService: Sending friend request to {code}");
            bool success = await SocialService.Instance.SendFriendRequest(code);
            Debug.Log(success
                ? $"DeepLinkService: Friend request sent to {code}"
                : $"DeepLinkService: Failed to send friend request to {code}");
        }

        private static string ParseInviteCode(string url)
        {
            // Expected: campfire://invite/SPARK-A3K9
            if (string.IsNullOrEmpty(url)) return null;

            const string prefix = "campfire://invite/";
            if (url.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return url.Substring(prefix.Length).Trim().TrimEnd('/');

            return null;
        }
    }
}
