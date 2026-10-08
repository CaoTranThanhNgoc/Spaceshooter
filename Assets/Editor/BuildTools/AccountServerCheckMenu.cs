using UnityEditor;
using UnityEngine;
using SpaceHawk.Online;

namespace SpaceHawk.EditorTools
{
    /// <summary>Tools > Space Hawk > Check Account Server: after deploying the Cloud Code script and adding its
    /// secrets in the Unity Dashboard, shows whether everything is in place (see AccountServerCheck).</summary>
    public static class AccountServerCheckMenu
    {
        private const string MenuPath = "Tools/Space Hawk/Check Account Server";

        [MenuItem(MenuPath)]
        private static async void Check()
        {
            Debug.Log("Account server check: asking the server...");
            string report = await AccountServerCheck.Run();
            Debug.Log(report);
        }

        // Needs the Unity services running, which they are in Play Mode (as in the game itself).
        [MenuItem(MenuPath, true)]
        private static bool CanCheck() => Application.isPlaying;
    }
}
