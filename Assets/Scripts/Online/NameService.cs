using System;
using System.Threading;
using System.Threading.Tasks;
using SpaceHawk.Core;

namespace SpaceHawk.Online
{
    /// <summary>Display names: unique among all players and changeable once a week. The server (Cloud Code,
    /// see INameBackend) is the authority - it knows every name and keeps the weekly clock; this class asks it,
    /// keeps the local profile in step and pushes the accepted name to Unity's player-name service, which is
    /// what the Leaderboard shows. Calls are serialized so the automatic registration at start-up can never
    /// overtake (and undo) a name the player changes right after.</summary>
    public static class NameService
    {
        public static INameBackend Backend = new CloudCodeNameBackend();

        private static readonly SemaphoreSlim Gate = new SemaphoreSlim(1, 1);

        /// <summary>The answer to a change the player asked for. `code` is "" when it worked, else the server's / the
        /// client's reason: invalid_name, name_taken, name_cooldown, server_unavailable.</summary>
        public struct ChangeResult
        {
            public bool ok;
            public string code;
            public int retryAfter;
            public string name;

            public static ChangeResult Success(string name) => new ChangeResult { ok = true, code = "", name = name };
            public static ChangeResult Fail(string code, int retryAfter = 0) => new ChangeResult { ok = false, code = code, retryAfter = retryAfter };
        }

        /// <summary>The player-facing text for a failed change.</summary>
        public static string ErrorText(ChangeResult result)
        {
            switch (result.code)
            {
                case "invalid_name": return Localization.Get("profile.name_invalid");
                case "name_taken": return Localization.Get("profile.name_taken");
                case "name_cooldown": return Localization.Format("profile.name_cooldown_fmt", PlayerNameRules.FormatWait(result.retryAfter));
                case "not_connected": return Localization.Get("account.error_not_connected");
                default: return Localization.Get("account.error_server_unavailable");
            }
        }

        /// <summary>The player typed a new name: checked here, then claimed on the server (nobody else may hold it, and
        /// the last change must be a week ago). Only an accepted name is kept - locally and on the Leaderboard.</summary>
        public static async Task<ChangeResult> ChangeName(string typed)
        {
            string name = PlayerNameRules.Clean(typed);
            if (name == null) return ChangeResult.Fail("invalid_name");
            if (string.Equals(name, SaveManager.GetPlayerName(), StringComparison.Ordinal)) return ChangeResult.Success(name);

            await LeaderboardManager.EnsureInitialized();
            if (!LeaderboardManager.IsReady) return ChangeResult.Fail("not_connected");

            await Gate.WaitAsync();
            try
            {
                NameResult reply = await Backend.SetName(name, false);
                if (!reply.ok)
                {
                    if (reply.error == "name_cooldown" && reply.retryAfter > 0)
                        SaveManager.SetNameChangeUnlock(DateTimeOffset.UtcNow.ToUnixTimeSeconds() + reply.retryAfter);
                    return ChangeResult.Fail(reply.error ?? "server_unavailable", reply.retryAfter);
                }

                string accepted = string.IsNullOrEmpty(reply.name) ? name : reply.name;
                SaveManager.SetPlayerName(accepted, false);
                SaveManager.MarkNameRegistered(LeaderboardManager.CurrentPlayerId, accepted, reply.nextChangeAt);
                await LeaderboardManager.PushNameToService(accepted);
                return ChangeResult.Success(accepted);
            }
            finally
            {
                Gate.Release();
            }
        }

        /// <summary>Makes sure the name this profile shows is the one the server holds for the signed-in identity:
        /// registers it if it is not yet (a guest's first "PilotXXXX", a name from before names were unique, a
        /// profile that moved to another identity). A game-picked name that is already taken is simply replaced by
        /// another; a name the player chose is left alone. Quiet and best-effort - tried again next session.</summary>
        public static async Task EnsureRegistered()
        {
            if (!LeaderboardManager.IsReady) return;
            string playerId = LeaderboardManager.CurrentPlayerId;
            if (string.IsNullOrEmpty(playerId)) return;

            await Gate.WaitAsync();
            try
            {
                string name = SaveManager.EnsureDefaultName();
                // The Leaderboard shows what Unity's player-name service holds: keep it the same as the profile, server or not.
                await LeaderboardManager.PushNameToService(name);

                if (SaveManager.IsNameRegisteredFor(playerId)) return;
                // The profile was loaded under another identity: what that one had on record (the weekly lock) is not this one's.
                if (SaveManager.IsNameRegisteredForAnother(playerId)) SaveManager.ForgetNameRegistration();

                for (int attempt = 0; attempt < 5; attempt++)
                {
                    NameResult reply = await Backend.SetName(name, true);
                    if (reply.ok)
                    {
                        string accepted = string.IsNullOrEmpty(reply.name) ? name : reply.name;
                        SaveManager.MarkNameRegistered(playerId, accepted, reply.nextChangeAt);
                        await LeaderboardManager.PushNameToService(accepted);
                        return;
                    }

                    if (reply.error == "name_taken" && SaveManager.IsPlayerNameAuto())
                    {
                        // The game's own pick is held by somebody else: another one (for an account, its name with a short tail).
                        string account = SaveManager.IsAccountLinked() ? PlayerNameRules.AccountDefault(SaveManager.GetAccountUsername()) : null;
                        name = account != null ? PlayerNameRules.VariantOf(account) : PlayerNameRules.RandomDefaultName();
                        SaveManager.SetPlayerName(name, true);
                        await LeaderboardManager.PushNameToService(name);
                        continue;
                    }
                    return;
                }
            }
            finally
            {
                Gate.Release();
            }
        }
    }
}
