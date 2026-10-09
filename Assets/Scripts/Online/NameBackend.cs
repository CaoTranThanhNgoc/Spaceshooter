using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.CloudCode;

namespace SpaceHawk.Online
{
    /// <summary>What the server said about a display name. `error` is invalid_name, name_taken, name_cooldown (with
    /// `retryAfter` seconds) or "server_unavailable" when the server could not be reached at all.</summary>
    public struct NameResult
    {
        public bool ok;
        public string error;
        public int retryAfter;
        /// <summary>The name the server holds for the player.</summary>
        public string name;
        /// <summary>Unix seconds from which the name may be changed again by hand; 0 = any time.</summary>
        public long nextChangeAt;

        public static NameResult Fail(string error, int retryAfter = 0) => new NameResult { ok = false, error = error, retryAfter = retryAfter };
    }

    /// <summary>The server half of display names: they are unique among all players and can be changed once a
    /// week. The real implementation talks to the Cloud Code script; tests swap in a fake through NameService.Backend.</summary>
    public interface INameBackend
    {
        /// <summary>Claims `name` for the signed-in player. `auto` is for names the game picks itself - they never start the weekly clock.</summary>
        Task<NameResult> SetName(string name, bool auto);
        Task<NameResult> GetName();
    }

    /// <summary>Calls the "AccountRecovery" Cloud Code script (setName / getName). Does NOT initialize the online
    /// services itself - it is also called from inside that initialization - so the caller makes sure they are ready.</summary>
    public class CloudCodeNameBackend : INameBackend
    {
        private const string ScriptName = "AccountRecovery";

        private class ServerReply
        {
            public bool ok;
            public string error;
            public int retryAfter;
            public string name;
            public long nextChangeAt;
        }

        public Task<NameResult> SetName(string name, bool auto) =>
            Call(new Dictionary<string, object> { { "action", "setName" }, { "displayName", name }, { "auto", auto ? "true" : "false" } });

        public Task<NameResult> GetName() =>
            Call(new Dictionary<string, object> { { "action", "getName" } });

        private static async Task<NameResult> Call(Dictionary<string, object> args)
        {
            if (!LeaderboardManager.IsReady) return NameResult.Fail("server_unavailable");

            try
            {
                ServerReply reply = await CloudCodeService.Instance.CallEndpointAsync<ServerReply>(ScriptName, args);
                if (reply == null) return NameResult.Fail("server_unavailable");
                if (!reply.ok) return NameResult.Fail(string.IsNullOrEmpty(reply.error) ? "server_unavailable" : reply.error, reply.retryAfter);
                return new NameResult { ok = true, name = reply.name, nextChangeAt = reply.nextChangeAt };
            }
            catch (Exception e)
            {
                // Not deployed yet, no connection, script error - the player only needs to know it did not work.
                Debug.LogWarning($"[CloudCodeNameBackend] {args["action"]} failed: {e.Message}");
                return NameResult.Fail("server_unavailable");
            }
        }
    }
}
