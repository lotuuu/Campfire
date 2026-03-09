using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Garden
{
    /// <summary>
    /// Centralized server configuration. All services read ServerConfig.BaseUrl
    /// instead of maintaining their own #if UNITY_EDITOR blocks.
    /// The active server is persisted in PlayerPrefs. Each server gets its own
    /// player identity (auth token / save data) keyed by server id.
    /// </summary>
    public static class ServerConfig
    {
        [System.Serializable]
        public class ServerEntry
        {
            public string id;
            public string name;
            public string url;
        }

        private const string PrefKey = "selected_server";

        private static readonly List<ServerEntry> _servers = new()
        {
            new ServerEntry
            {
                id = "local", name = "Local",
#if UNITY_EDITOR
                url = "http://localhost:4000"
#else
                url = DevServerConfig.BaseUrl
#endif
            },
            new ServerEntry { id = "remote",  name = "Gigalixir",  url = "https://campfire.gigalixirapp.com" },
        };

        public static IReadOnlyList<ServerEntry> Servers => _servers;

        public static string SelectedId
        {
            get => PlayerPrefs.GetString(PrefKey, "local");
            private set
            {
                PlayerPrefs.SetString(PrefKey, value);
                PlayerPrefs.Save();
            }
        }

        public static ServerEntry Current => _servers.Find(s => s.id == SelectedId) ?? _servers[0];

        public static string BaseUrl => Current.url;

        /// <summary>
        /// Returns a PlayerPrefs-safe prefix for the current server,
        /// so each server keeps separate auth / save data.
        /// </summary>
        public static string SavePrefix => Current.id == "local" ? "" : Current.id + "_";

        public static void Select(string serverId)
        {
            if (serverId == SelectedId) return;
            SelectedId = serverId;
            // Reload the scene to reinitialize all services with the new server
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
