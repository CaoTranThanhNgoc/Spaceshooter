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
            _simulatedPlayerId = null;
        }

        private static string _simulatedPlayerId;
        private static bool _simulatedOffline;

        /// <summary>Test-only: the services never become ready (no network) - nothing is initialized or signed in.</summary>
        public static void SimulateOfflineForTests(bool offline)
        {
            _simulatedOffline = offline;
            if (offline) IsReady = false;
        }

        /// <summary>Test-only: behave as if the online services were ready and signed in as `playerId` (null undoes it),
        /// so name and account logic can be exercised without a network. Public because the tests live in other assemblies.</summary>
        public static void SimulateSignedInForTests(string playerId)
        {
            _simulatedPlayerId = playerId;
            IsReady = playerId != null;
        }

        /// <summary>The online identity signed in right now, or null when there is none (yet).</summary>
        public static string CurrentPlayerId
        {
            get
            {
                if (_simulatedPlayerId != null) return _simulatedPlayerId;
                try
                {
                    if (UnityServices.State == ServicesInitializationState.Initialized && AuthenticationService.Instance.IsSignedIn)
                        return AuthenticationService.Instance.PlayerId;
                }
                catch (Exception)
                {
                    // the services are not usable (yet)
                }
                return null;
            }
        }

        public static async Task EnsureInitialized()
        {
            if (IsReady || _simulatedOffline) return;
            if (_initializing)
            {
                while (_initializing) await Task.Yield();
                return;
            }

            _initializing = true;
            bool becameReady = false;
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                    await UnityServices.InitializeAsync();

                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    // The cached session of whoever is playing: the guest identity, or the signed-in account.
                    AuthProfiles.Switch(AuthProfiles.SlotToUse);
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                }

                await SyncPlayerName();

                IsReady = true;
                becameReady = true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LeaderboardManager] Initialize failed: {e.Message}");
            }
            finally
            {
                _initializing = false;
            }

            // Not awaited: the name registration needs the services to be ready, and must not hold anything else up.
            if (becameReady && _simulatedPlayerId == null) _ = NameService.EnsureRegistered();
        }

        /// <summary>Pushes the profile's name to this online identity - needed again if UGS's own copy is missing or
        /// differs, e.g. a reinstall/cache clear created a fresh identity while the save file survived, or a fresh
        /// guest was just given a name. The name on the Leaderboard is always the one the profile shows.</summary>
        private static async Task SyncPlayerName()
        {
            try
            {
                string local = SaveManager.EnsureDefaultName();

                string current = AuthenticationService.Instance.PlayerName;
                if (StripNameDiscriminator(current) == local) return;

                await AuthenticationService.Instance.UpdatePlayerNameAsync(local);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LeaderboardManager] Could not sync player name: {e.Message}");
            }
        }

        /// <summary>The name the server accepted becomes the name Unity's player-name service (the Leaderboard) shows.</summary>
        public static async Task PushNameToService(string name)
        {
            try
            {
                if (CurrentPlayerId == null || _simulatedPlayerId != null) return;
                if (StripNameDiscriminator(AuthenticationService.Instance.PlayerName) == name) return;
                await AuthenticationService.Instance.UpdatePlayerNameAsync(name);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LeaderboardManager] Could not update the player name: {e.Message}");
            }
        }

        /// <summary>Opposite direction from SyncPlayerName - after AccountManager.SignIn restores
        /// a different account from another device, THAT account's own remembered name is
        /// authoritative, not whatever name happens to be saved locally on this device.</summary>
        public static bool PullPlayerNameFromRemote()
        {
            string remote = AuthenticationService.Instance.PlayerName;
            if (string.IsNullOrEmpty(remote)) return false;
            SaveManager.SetPlayerName(StripNameDiscriminator(remote), false);
            return true;
        }

        /// <summary>Right after AccountManager signed in to an account: the services are initialized and signed in,
        /// so the next call must not run the guest start-up again (it would push THIS profile's name onto the account).</summary>
        public static void MarkSignedIn()
        {
            if (CurrentPlayerId != null) IsReady = true;
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

        /// <summary>The name this profile shows: always one (the game gives a profile a "PilotXXXX" until the player
        /// picks a name), and it is the one the Leaderboard shows too.</summary>
        public static string GetDisplayName() => SaveManager.EnsureDefaultName();

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
                await SyncPlayerName();   // the entry shows up under the name the profile shows
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
                    bool mine = entry.PlayerId == myId;
                    // The player's own row always carries the name of their profile - the two can never disagree.
                    string name = mine
                        ? SaveManager.EnsureDefaultName()
                        : (string.IsNullOrEmpty(entry.PlayerName) ? "Pilot" : StripNameDiscriminator(entry.PlayerName));
                    result.Add(new LeaderboardRow
                    {
                        rank = entry.Rank + 1,
                        playerName = name,
                        score = (long)entry.Score,
                        isCurrentPlayer = mine,
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
