#if UNITY_IOS
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using System.IO;

namespace Garden.Editor
{
    public static class WeatherFetchBuildProcessor
    {
        [PostProcessBuild(100)]
        public static void OnPostProcessBuild(BuildTarget target, string buildPath)
        {
            if (target != BuildTarget.iOS) return;

            string plistPath = Path.Combine(buildPath, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);

            var root = plist.root;

            // Add fetch to UIBackgroundModes (create array if absent).
            const string key = "UIBackgroundModes";
            if (!root.values.ContainsKey(key))
                root.CreateArray(key);

            var modes = root[key].AsArray();

            // Only add if not already present.
            bool hasFetch = false;
            foreach (var item in modes.values)
            {
                if (item.AsString() == "fetch") { hasFetch = true; break; }
            }
            if (!hasFetch)
                modes.AddString("fetch");

            plist.WriteToFile(plistPath);
        }
    }
}
#endif
