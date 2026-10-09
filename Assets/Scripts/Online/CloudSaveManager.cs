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

        public enum FetchStatus
        {
            /// <summary>The account has a saved profile in the cloud.</summary>
            Found,
            /// <summary>The cloud answered: this account has never saved anything (a brand-new account).</summary>
            NoData,
            /// <summary>No answer (no connection, service error) - nothing is known about the account's progress.</summary>
            Failed,
        }

        public struct FetchResult
        {
            public FetchStatus status;
            public string json;
        }

        /// <summary>Reads the cloud save of the account signed in right now WITHOUT touching local progress - the
        /// caller decides what to do with it. "Nothing saved yet" and "could not ask" are different answers: only
        /// the first may let local progress become the account's (see AccountManager.SignIn), otherwise a hiccup
        /// in the connection would overwrite an account's real progress with whatever is on this device.</summary>
        public static async Task<FetchResult> FetchCloudSave()
        {
            await LeaderboardManager.EnsureInitialized();
            if (!LeaderboardManager.IsReady) return new FetchResult { status = FetchStatus.Failed };

            try
            {
                Dictionary<string, Unity.Services.CloudSave.Models.Item> result =
                    await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { SaveKey }, new PlayerData.LoadOptions());

                if (result.TryGetValue(SaveKey, out Unity.Services.CloudSave.Models.Item item))
                    return new FetchResult { status = FetchStatus.Found, json = item.Value.GetAsString() };
                return new FetchResult { status = FetchStatus.NoData };
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CloudSaveManager] Fetch failed: {e.Message}");
                return new FetchResult { status = FetchStatus.Failed };
            }
        }
    }
}
