using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Leaderboards;
using Unity.Services.Leaderboards.Models;
using SpaceHawk.Core;

namespace SpaceHawk.Online
{
    public struct LeaderboardRow
    {
        public int rank;
        public string playerName;
        public long score;
        public bool isCurrentPlayer;
    }

    /// <summary>Thin wrapper around Unity Gaming Services (Authentication + Leaderboards) - the
    /// rest of the game only ever sees SubmitLocalScore/GetTopScores, never the SDK types
    /// directly. Every call is defensive (try/catch, empty-result fallback) since this is the
    /// one system in the whole game that depends on an actual network connection.</summary>
    public static class LeaderboardManager
    {
        public const string LeaderboardId = "Global_Score";

        public static bool IsReady { get; private set; }
        private static bool _initializing;

        /// <summary>Call after switching identity entirely (AccountManager sign-out/sign-in/
        /// delete) - forces the next EnsureInitialized to actually run again (re-checking
        /// IsSignedIn, which by then reflects whatever the switch left behind) instead of
        /// short-circuiting on a now-stale IsReady=true from the old identity.</summary>
        public static void ResetSession()
        {
            IsReady = false;
        }

        public static async Task EnsureInitialized()
        {
            if (IsReady) return;
            if (_initializing)
            {
                while (_initializing) await Task.Yield();
                return;
            }

            _initializing = true;
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                    await UnityServices.InitializeAsync();

                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();

                await SyncPlayerName();

                IsReady = true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LeaderboardManager] Initialize failed: {e.Message}");
            }
            finally
            {
                _initializing = false;
            }
        }

        /// <summary>Pushes the locally-remembered name (see SaveManager/PlayerNamePrompt) to this
        /// anonymous identity - needed again if UGS's own copy is missing, e.g. after a
        /// reinstall/cache clear created a fresh anonymous identity while the save file (and the
        /// name the player picked) survived.</summary>
        private static async Task SyncPlayerName()
        {
            try
            {
                string local = SaveManager.GetPlayerName();
                if (string.IsNullOrEmpty(local)) return;

                string current = AuthenticationService.Instance.PlayerName;
                if (current == local) return;

                await AuthenticationService.Instance.UpdatePlayerNameAsync(local);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LeaderboardManager] Could not sync player name: {e.Message}");
            }
        }

        /// <summary>Opposite direction from SyncPlayerName - after AccountManager.SignIn restores
        /// a different account from another device, THAT account's own remembered name is
        /// authoritative, not whatever name happens to be saved locally on this device.</summary>
        public static void PullPlayerNameFromRemote()
        {
            string remote = AuthenticationService.Instance.PlayerName;
            if (!string.IsNullOrEmpty(remote)) SaveManager.SetPlayerName(StripNameDiscriminator(remote));
        }

        /// <summary>Unity's Authentication service appends a "#1234"-style discriminator to player
        /// names to keep them unique across players who pick the same name (e.g. "thanh#63832") -
        /// useful for the service internally, but not something a player ever typed or wants shown
        /// back to them. Stripped for display/local-name purposes only; the full remote name
        /// (with discriminator) is left untouched on the server.</summary>
        private static string StripNameDiscriminator(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            int hashIndex = name.IndexOf('#');
            return hashIndex >= 0 ? name.Substring(0, hashIndex) : name;
        }

        /// <summary>True once the player has actually picked (or accepted a random) name via
        /// PlayerNamePrompt - before that, HasChosenName is false and the prompt should show.</summary>
        public static bool HasChosenName() => !string.IsNullOrEmpty(SaveManager.GetPlayerName());

        public static string GenerateRandomName()
        {
            // Before the services are initialized AuthenticationService.Instance throws - a screen that
            // suggests a name (the profile) can open that early, so it falls back to a random id.
            string id = null;
            try
            {
                if (UnityServices.State == ServicesInitializationState.Initialized && AuthenticationService.Instance.IsSignedIn)
                    id = AuthenticationService.Instance.PlayerId;
            }
            catch (Exception)
            {
                id = null;
            }
            id ??= Guid.NewGuid().ToString("N");
            string suffix = id.Length >= 4 ? id.Substring(0, 4) : id;
            return $"Pilot{suffix}";
        }

        public static async Task SetPlayerName(string name)
        {
            await EnsureInitialized();
            SaveManager.SetPlayerName(name); // remembered locally even if the network call below fails
            if (!IsReady) return;

            try
            {
                await AuthenticationService.Instance.UpdatePlayerNameAsync(name);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LeaderboardManager] SetPlayerName failed: {e.Message}");
            }
        }

        /// <summary>The Skill Rating: the sum of the best score on each level plus the best Endless
        /// run (see SkillScore for how a run is scored). It only ever goes up, and replaying a level
        /// helps only by beating that level's own best - so grinding easy levels doesn't climb the
        /// board, playing better does.</summary>
        public static long GetLocalScore() => SaveManager.GetSkillRating();

        public static async Task SubmitLocalScore()
        {
            await EnsureInitialized();
            if (!IsReady) return;

            try
            {
                await LeaderboardsService.Instance.AddPlayerScoreAsync(LeaderboardId, GetLocalScore());
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LeaderboardManager] SubmitLocalScore failed: {e.Message}");
            }
        }

        public static async Task<List<LeaderboardRow>> GetTopScores(int count)
        {
            await EnsureInitialized();
            List<LeaderboardRow> result = new List<LeaderboardRow>();
            if (!IsReady) return result;

            try
            {
                string myId = AuthenticationService.Instance.PlayerId;
                LeaderboardScoresPage page = await LeaderboardsService.Instance.GetScoresAsync(
                    LeaderboardId, new GetScoresOptions { Offset = 0, Limit = count });

                foreach (LeaderboardEntry entry in page.Results)
                {
                    result.Add(new LeaderboardRow
                    {
                        rank = entry.Rank + 1,
                        playerName = string.IsNullOrEmpty(entry.PlayerName) ? "Pilot" : StripNameDiscriminator(entry.PlayerName),
                        score = (long)entry.Score,
                        isCurrentPlayer = entry.PlayerId == myId,
                    });
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LeaderboardManager] GetTopScores failed: {e.Message}");
            }

            return result;
        }
    }
}
