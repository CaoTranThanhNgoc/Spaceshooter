using UnityEngine;
using UnityEditor;

namespace SpaceHawk.EditorTools
{
    /// <summary>Store-readiness settings applied by script so a fresh checkout or a wiped
    /// ProjectSettings ends up the same way: Android API range, App Bundle output and launcher
    /// icons. Safe to re-run.</summary>
    public static class ProjectConfigurator
    {
        [MenuItem("Tools/Space Hawk/17. Configure Android Project")]
        public static void Apply()
        {
            // The project had minSdk 36 (Android 16 only) - a handful of brand-new phones.
            // API 25 (Android 7.1) is the lowest this Unity version still supports and covers
            // well over 95% of active devices; the target stays at the latest level Google Play
            // requires for new uploads.
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;

            // Google Play only accepts Android App Bundles for new apps, not plain APKs.
            EditorUserBuildSettings.buildAppBundle = true;

            AppIconBuilder.Build();
            AssetDatabase.SaveAssets();
            Debug.Log("[ProjectConfigurator] Android settings applied (minSdk 25, App Bundle, icons).");
        }
    }
}
