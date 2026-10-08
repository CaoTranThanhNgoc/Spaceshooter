using UnityEngine;

namespace SpaceHawk.Online
{
    /// <summary>Auto-created once at game launch and kept alive across every scene load - the one
    /// place that can catch OnApplicationPause/OnApplicationQuit to push the local save to the
    /// cloud before the player backgrounds or closes the app (the moment they're most likely to
    /// switch devices next). CloudSaveManager.PushToCloud is itself a no-op for a guest, so this
    /// runs harmlessly whether or not an account is linked.</summary>
    public class CloudSyncTrigger : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (FindObjectOfType<CloudSyncTrigger>() != null) return;
            GameObject go = new GameObject("CloudSyncTrigger");
            go.AddComponent<CloudSyncTrigger>();
            Object.DontDestroyOnLoad(go);
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) _ = CloudSaveManager.PushToCloud();
        }

        private void OnApplicationQuit()
        {
            _ = CloudSaveManager.PushToCloud();
        }
    }
}
