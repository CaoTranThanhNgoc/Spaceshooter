using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.CloudSave;
using SpaceHawk.Core;
using PlayerData = Unity.Services.CloudSave.Models.Data.Player;

namespace SpaceHawk.Online
{
    /// <summary>Syncs SaveManager's whole local save file to Unity Cloud Save under a linked
    /// account, so "create an account to keep your progress" (PlayerProfilePanel's own hint text)
    /// is actually true - previously only the display name and leaderboard score were tied to the
    /// account; crystals, energy, unlocked levels, ships and achievements stayed device-only.
    ///
    /// The whole SaveData is stored as ONE JSON string under a single key (matching the same
    /// JsonUtility round-trip SaveManager already uses locally) rather than field-by-field, since
    /// Cloud Save's own GetAs&lt;T&gt;() deserializes via Newtonsoft against public PROPERTIES,
    /// while SaveData is plain public FIELDS for JsonUtility - GetAs&lt;SaveData&gt;() directly
    /// would silently come back empty.</summary>
    public static class CloudSaveManager
    {
        private const string SaveKey = "playerSaveData";

        /// <summary>Uploads the current local save - call after Register (to back up whatever
        /// guest progress already exists) and opportunistically whenever the app loses focus
        /// while signed in (see CloudSyncTrigger).</summary>
        public static async Task PushToCloud()
        {
            if (!AccountManager.IsLinked) return;

            await LeaderboardManager.EnsureInitialized();
            if (!LeaderboardManager.IsReady) return;

            try
            {
                string json = JsonUtility.ToJson(SaveManager.Data);
                var data = new Dictionary<string, object> { { SaveKey, json } };
                await CloudSaveService.Instance.Data.Player.SaveAsync(data, new PlayerData.SaveOptions());
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CloudSaveManager] Push failed: {e.Message}");
            }
        }

        /// <summary>Downloads the cloud save for whichever account is currently signed in and
        /// overwrites local progress with it - call right after SignIn, so restoring an account
        /// on a (possibly new) device actually restores gameplay progress, not just identity.
        /// Returns false if there was nothing to restore (a brand-new account) or the request
        /// failed, in which case local progress is left untouched.</summary>
        public static async Task<bool> PullFromCloud()
        {
            await LeaderboardManager.EnsureInitialized();
            if (!LeaderboardManager.IsReady) return false;

            try
            {
                Dictionary<string, Unity.Services.CloudSave.Models.Item> result =
                    await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { SaveKey }, new PlayerData.LoadOptions());

                if (result.TryGetValue(SaveKey, out Unity.Services.CloudSave.Models.Item item))
                {
                    SaveManager.ApplyCloudData(item.Value.GetAsString());
                    return true;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CloudSaveManager] Pull failed: {e.Message}");
            }

            return false;
        }
    }
}
