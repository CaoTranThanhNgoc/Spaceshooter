using System.Threading;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace SpaceHawk.EditorTools
{
    /// <summary>One-off installer for the Unity Gaming Services packages the Leaderboard feature
    /// needs. Adds each by name (no pinned version) so Unity's Package Manager resolves whatever
    /// is actually current in the registry, rather than us guessing a version string that might
    /// not exist. Safe to re-run - Client.Add on an already-installed package is a no-op success.</summary>
    public static class InstallUGSPackages
    {
        private static readonly string[] Packages =
        {
            "com.unity.services.core",
            "com.unity.services.authentication",
            "com.unity.services.leaderboards",
            "com.unity.services.cloudsave",
        };

        private const int TimeoutSeconds = 120;

        [MenuItem("Tools/Space Hawk/Install UGS Packages")]
        public static void Install()
        {
            foreach (string package in Packages)
            {
                Debug.Log($"[InstallUGSPackages] Requesting {package}...");
                AddRequest request = Client.Add(package);

                float waited = 0f;
                const float step = 0.25f;
                while (!request.IsCompleted && waited < TimeoutSeconds)
                {
                    Thread.Sleep((int)(step * 1000));
                    waited += step;
                }

                if (!request.IsCompleted)
                {
                    Debug.LogError($"[InstallUGSPackages] Timed out waiting for {package} after {TimeoutSeconds}s.");
                    continue;
                }

                if (request.Status == StatusCode.Success)
                    Debug.Log($"[InstallUGSPackages] Installed {request.Result.packageId}");
                else
                    Debug.LogError($"[InstallUGSPackages] Failed to install {package}: {request.Error?.message}");
            }

            Debug.Log("[InstallUGSPackages] Done.");
        }
    }
}
