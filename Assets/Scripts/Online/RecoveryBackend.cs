using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.CloudCode;
using SpaceHawk.Core;

namespace SpaceHawk.Online
{
    /// <summary>What the server said. `error` is one of the codes of Assets/CloudCode/AccountRecovery.js
    /// (invalid_contact, not_verified, too_soon, invalid_code, ...) or "server_unavailable" when the
    /// server could not be reached at all.</summary>
    public struct RecoveryResult
    {
        public bool ok;
        public string error;
        public int retryAfter;
        /// <summary>SetContact only: the contact used to belong to another account and now belongs to this one.</summary>
        public bool transferred;

        public static RecoveryResult Ok => new RecoveryResult { ok = true };
        public static RecoveryResult Fail(string error, int retryAfter = 0) => new RecoveryResult { ok = false, error = error, retryAfter = retryAfter };
    }

    /// <summary>The server half of contact verification, password recovery and account cleanup. The
    /// real implementation talks to the Cloud Code script; tests swap in a fake through AccountManager.Recovery.</summary>
    public interface IRecoveryBackend
    {
        /// <summary>Sends a 6-digit code to the e-mail the player wants to attach to an account.</summary>
        Task<RecoveryResult> SendVerification(string contact);
        /// <summary>Checks that code: from then on the contact is proven to be the caller's.</summary>
        Task<RecoveryResult> VerifyContact(string contact, string code);
        /// <summary>Attaches a VERIFIED contact to the signed-in account (server refuses with not_verified otherwise).</summary>
        Task<RecoveryResult> SetContact(string username, string contact);
        Task<RecoveryResult> RequestReset(string username, string contact);
        Task<RecoveryResult> ConfirmReset(string username, string contact, string code, string newPassword);
        /// <summary>Removes the signed-in player's leaderboard scores, recovery data and cloud progress.</summary>
        Task<RecoveryResult> DeleteAccountData();
    }

    /// <summary>Calls the "AccountRecovery" Cloud Code script (Assets/CloudCode/AccountRecovery.js).</summary>
    public class CloudCodeRecoveryBackend : IRecoveryBackend
    {
        private const string ScriptName = "AccountRecovery";

        private class ServerReply
        {
            public bool ok;
            public string error;
            public int retryAfter;
            public bool transferred;
        }

        public Task<RecoveryResult> SendVerification(string contact) =>
            Call(new Dictionary<string, object> { { "action", "sendVerification" }, { "contact", contact } });

        public Task<RecoveryResult> VerifyContact(string contact, string code) =>
            Call(new Dictionary<string, object> { { "action", "verifyContact" }, { "contact", contact }, { "code", code } });

        public Task<RecoveryResult> SetContact(string username, string contact) =>
            Call(new Dictionary<string, object> { { "action", "setContact" }, { "username", username }, { "contact", contact } });

        public Task<RecoveryResult> RequestReset(string username, string contact) =>
            Call(new Dictionary<string, object> { { "action", "requestReset" }, { "username", username }, { "contact", contact } });

        public Task<RecoveryResult> ConfirmReset(string username, string contact, string code, string newPassword) =>
            Call(new Dictionary<string, object>
            {
                { "action", "confirmReset" }, { "username", username }, { "contact", contact },
                { "code", code }, { "newPassword", newPassword },
            });

        public Task<RecoveryResult> DeleteAccountData() =>
            Call(new Dictionary<string, object> { { "action", "deleteData" } });

        private static async Task<RecoveryResult> Call(Dictionary<string, object> args)
        {
            await LeaderboardManager.EnsureInitialized();
            if (!LeaderboardManager.IsReady) return RecoveryResult.Fail("server_unavailable");

            try
            {
                ServerReply reply = await CloudCodeService.Instance.CallEndpointAsync<ServerReply>(ScriptName, args);
                if (reply == null) return RecoveryResult.Fail("server_unavailable");
                if (!reply.ok) return RecoveryResult.Fail(string.IsNullOrEmpty(reply.error) ? "server_unavailable" : reply.error, reply.retryAfter);
                return new RecoveryResult { ok = true, transferred = reply.transferred };
            }
            catch (Exception e)
            {
                // Not deployed yet, no connection, script error - the player only needs to know it did not work.
                Debug.LogWarning($"[CloudCodeRecoveryBackend] {args["action"]} failed: {e.Message}");
                return RecoveryResult.Fail("server_unavailable");
            }
        }
    }
}
