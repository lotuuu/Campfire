#if UNITY_IOS
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using System.IO;

namespace Garden.Editor
{
    public static class XcodeProjectPostProcess
    {
        // Edit these values before building.
        private const string TeamId = "PVQ68TVT7G";
        private const string BundleIdentifier = "com.yourcompany.garden";

        [PostProcessBuild(50)]
        public static void OnPostProcessBuild(BuildTarget target, string buildPath)
        {
            if (target != BuildTarget.iOS) return;

            string pbxPath = PBXProject.GetPBXProjectPath(buildPath);
            var project = new PBXProject();
            project.ReadFromFile(pbxPath);

            string mainTarget = project.GetUnityMainTargetGuid();
            string frameworkTarget = project.GetUnityFrameworkTargetGuid();

            // Set team ID on both targets.
            project.SetTeamId(mainTarget, TeamId);
            project.SetTeamId(frameworkTarget, TeamId);

            // Set bundle identifier on the main app target.
            project.SetBuildProperty(mainTarget, "PRODUCT_BUNDLE_IDENTIFIER", BundleIdentifier);

            // Enable automatic signing.
            project.SetBuildProperty(mainTarget, "CODE_SIGN_STYLE", "Automatic");
            project.SetBuildProperty(frameworkTarget, "CODE_SIGN_STYLE", "Automatic");

            project.WriteToFile(pbxPath);
        }
    }
}
#endif
