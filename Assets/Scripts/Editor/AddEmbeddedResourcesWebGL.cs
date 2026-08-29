using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

#if UNITY_WEBGL

namespace FarmingKitEditor
{
    /// <summary>
    /// Ensures that useEmbeddedResources is toggled on build
    /// This is required to prevent errors related to Text Mesh Pro
    /// </summary>

    public class AddEmbeddedResourcesWebGL : IPreprocessBuildWithReport
    {
        private static bool hasInitialized = false;

        public int callbackOrder { get { return 0; } }
        public void OnPreprocessBuild(BuildReport report)
        {
            if (!hasInitialized)
            {
                PlayerSettings.WebGL.useEmbeddedResources = true;
                hasInitialized = true;
            }
        }
    }
}

#endif