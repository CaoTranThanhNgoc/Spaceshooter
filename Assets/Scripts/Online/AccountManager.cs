using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using SpaceHawk.Core;

namespace SpaceHawk.Online
{
    /// <summary>Username/password account operations layered on top of the anonymous identity
    /// LeaderboardManager already creates - Unity's own Authentication service supports this
    /// directly, no separate Google/Apple/Facebook developer account needed.
    ///
    /// "Linked" means this identity now has real credentials attached (tracked locally in
    /// SaveManager - simpler and more robust than re-deriving it from the SDK's own identity
    /// list every time), so it can be recovered by signing in from any device instead of only
    /// existing anonymously on this one.
    ///
    /// Guest and account progress are kept apart. A guest who CREATES an account takes the progress along: it moves
    /// into the account, and the guest that comes back after logging out starts fresh. A guest who SIGNS IN to an
    /// existing account gets that account's progress; the guest's own is put aside and comes back on logging out -
    /// see SaveManager.StashGuestProfile / RestoreGuestProfile. Each identity keeps its own cached session (AuthProfiles).</summary>
    public static class AccountManager
    {
        /// <summary>Highest level number a guest (no linked account) can play - the Leaderboard
        /// and progress-syncing features only make sense with a persistent identity, so this is
        /// the point where a guest gets asked to sign in or create an account rather than losing
        /// progress silently on a fresh install/new device later.</summary>
        public const int GuestLevelCap = 4;

        public static bool IsLinked => SaveManager.IsAccountLinked();
        public static string CurrentUsername => SaveManager.GetAccountUsername();

        /// <summary>The server side of password recovery and account cleanup (Cloud Code). Tests replace it.</summary>
        public static IRecoveryBackend Recovery = new CloudCodeRecoveryBackend();

        public static bool HasRecoveryContact => SaveManager.HasVerifiedRecoveryContact();

        /// <summary>The result of creating an account. `contactSaved` is false when the account exists but the
        /// server could not attach the (verified) contact - the profile then asks to add it again.
        /// `contactMoved` is true when the contact used to belong to another account.</summary>
        public struct RegisterResult
        {
            public bool success;
            public string error;
            public bool contactSaved;
            public bool contactMoved;
        }

        /// <summary>Raised right after a successful Register or SignIn - AuthGatePanel listens for
        /// this to close itself, since either path resolves the first-run "who are you" gate the
        /// same way a chosen guest name does.</summary>
        public static event Action AccountLinked;

        /// <summary>The recovery contact of the signed-in account was added or changed.</summary>
        public static event Action RecoveryContactChanged;

        /// <summary>A forgotten password was reset (the account's username) - the sign-in screen can pick it up.</summary>
        public static event Action<string> PasswordResetDone;

        /// <summary>Attaches username/password to the CURRENT (anonymous) identity, preserving
        /// its existing PlayerId and leaderboard history - the normal path for a player who has
        /// been playing as a guest and now wants an account they can recover later.</summary>
        /// <param name="contact">The e-mail address kept for password recovery (any typed form). It must
        /// already be VERIFIED (RequestContactVerification / VerifyContact) - the server refuses to attach it otherwise.</param>
        public static async Task<RegisterResult> Register(string username, string password, string contact)
        {
            string canonical = RecoveryContact.Normalize(contact);
            if (canonical == null) return new RegisterResult { error = Localization.Get("account.error_invalid_contact") };

            await LeaderboardManager.EnsureInitialized();
            if (!LeaderboardManager.IsReady) return new RegisterResult { error = Localization.Get("account.error_not_connected") };

            try
            {
                await AuthenticationService.Instance.AddUsernamePasswordAsync(username, password);
                // The guest identity has just BECOME the account (same player, same Leaderboard history): it keeps
                // living in the slot the guest was in.
                AuthProfiles.AccountSlot = AuthenticationService.Instance.Profile;
                // The guest progress MOVES into the account - it is not copied back to the guest, so after logging
                // out the guest starts fresh instead of showing what now belongs to the account.
                SaveManager.SetAccountLinked(true, username);
                RememberUsername(username);
                // The account name is now the display name too (SaveManager did that when it linked the account,
                // unless the player had chosen a name) - registered with the server and shown on the Leaderboard.
                await NameService.EnsureRegistered();
                // Back up whatever guest progress already exists under this brand-new account,
                // so "create an account to keep your progress" is true from the moment it's made.
                RecoveryResult saved = await Recovery.SetContact(username, canonical);
                if (saved.ok)
                {
                    SaveManager.SetRecoveryContact(canonical);
                    RecoveryContactChanged?.Invoke();
                }
                await CloudSaveManager.PushToCloud();
                AccountLinked?.Invoke();
                return new RegisterResult { success = true, contactSaved = saved.ok, contactMoved = saved.ok && saved.transferred };
            }
            catch (Exception e)
            {
                return new RegisterResult { error = FriendlyError(e) };
            }
        }

        /// <summary>Switches to a DIFFERENT, already-registered identity (e.g. restoring an account on a new
        /// device). The guest identity stays cached in its own slot and the guest progress is put aside - both
        /// come back on logging out. Nothing on this device changes until the account's progress has been
        /// read: if that fails, the sign-in is undone and the guest carries on untouched.</summary>
        public static async Task<(bool success, string error)> SignIn(string username, string password)
        {
            await LeaderboardManager.EnsureInitialized();
            if (!LeaderboardManager.IsReady) return (false, Localization.Get("account.error_not_connected"));

            try
            {
                // Keep the guest's session cached; the account gets a slot of its own.
                AuthenticationService.Instance.SignOut(false);
                LeaderboardManager.ResetSession();
                AuthProfiles.Switch(AuthProfiles.SignedInSlot);

                await AuthenticationService.Instance.SignInWithUsernamePasswordAsync(username, password);
            }
            catch (Exception e)
            {
                BackToGuestIdentity(clearAccountSession: false);
                return (false, FriendlyError(e));
            }

            try
            {
                LeaderboardManager.MarkSignedIn();
                CloudSaveManager.FetchResult cloud = await CloudSaveManager.FetchCloudSave();
                if (cloud.status == CloudSaveManager.FetchStatus.Failed)
                {
                    // Not knowing what the account holds, pushing this device's progress could wipe it. Undo.
                    BackToGuestIdentity(clearAccountSession: true);
                    return (false, Localization.Get("account.error_sync_failed"));
                }

                SaveManager.StashGuestProfile();
                SaveManager.SetAccountLinked(true, username);
                AuthProfiles.AccountSlot = AuthProfiles.SignedInSlot;
                RememberUsername(username);

                // The account's own name (from Unity's name service) replaces the guest's; an account that never
                // had one starts with a name of its own, not the guest's.
                if (!LeaderboardManager.PullPlayerNameFromRemote()) SaveManager.ClearPlayerName();

                if (cloud.status == CloudSaveManager.FetchStatus.Found)
                {
                    // The whole point of signing in on a (possibly new) device: THIS account's progress.
                    SaveManager.ApplyCloudData(cloud.json);
                }
                else
                {
                    // A brand-new account with nothing saved yet: the guest progress moves into it and is backed up.
                    SaveManager.DiscardGuestStash();
                    await CloudSaveManager.PushToCloud();
                }

                await NameService.EnsureRegistered();
                AccountLinked?.Invoke();
                return (true, null);
            }
            catch (Exception e)
            {
                BackToGuestIdentity(clearAccountSession: true);
                return (false, FriendlyError(e));
            }
        }

        /// <summary>Undoes a sign-in that did not complete: forgets the account's session (when it was already
        /// established) and points the service back at the guest slot - the guest identity was never touched.</summary>
        private static void BackToGuestIdentity(bool clearAccountSession)
        {
            try
            {
                if (AuthenticationService.Instance.IsSignedIn) AuthenticationService.Instance.SignOut(clearAccountSession);
                AuthProfiles.Switch(AuthProfiles.GuestSlot);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AccountManager] Could not return to the guest identity: {e.Message}");
            }
            LeaderboardManager.ResetSession();
        }

        /// <summary>Logs out: the account's progress is saved to the cloud first (so nothing played
        /// since the last sync is lost), then this device goes back to the guest profile it had
        /// before signing in (or a fresh one when the progress moved into the account) - the account's scores,
        /// ships and Crystals no longer show up in guest play. The guest identity of an account that was
        /// signed in to comes back; one that was turned into the account is replaced by a new guest.</summary>
        public static async Task SignOut()
        {
            // Bounded, so a missing connection never leaves the player stuck on "logging out".
            try
            {
                Task push = CloudSaveManager.PushToCloud();
                await Task.WhenAny(push, Task.Delay(6000));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AccountManager] Could not back up before signing out: {e.Message}");
            }

            try
            {
                if (AuthenticationService.Instance.IsSignedIn) AuthenticationService.Instance.SignOut(true);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AccountManager] SignOut failed: {e.Message}");
            }

            AuthProfiles.ForgetAccountSlot();
            SaveManager.RestoreGuestProfile();
            LeaderboardManager.ResetSession();
        }

        /// <summary>Permanently deletes the currently signed-in account - cannot be undone - together
        /// with everything that belongs to it: its scores on the Leaderboard, its recovery contact and
        /// its progress in the cloud. The server does that cleanup FIRST; if it cannot, nothing is
        /// deleted, so no score or personal data is ever left behind by a half-finished deletion. Then
        /// the identity itself goes, and this device returns to the guest profile it had before signing in.</summary>
        public static async Task<(bool success, string error)> DeleteAccount()
        {
            await LeaderboardManager.EnsureInitialized();
            if (!LeaderboardManager.IsReady) return (false, Localization.Get("account.error_not_connected"));

            RecoveryResult cleanup = await Recovery.DeleteAccountData();
            if (!cleanup.ok) return (false, RecoveryErrorText(cleanup));

            string username = CurrentUsername;
            try
            {
                await AuthenticationService.Instance.DeleteAccountAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AccountManager] DeleteAccount failed: {e.Message}");
                return (false, Localization.Get("account.delete_failed"));
            }

            ForgetUsername(username);
            AuthProfiles.ForgetAccountSlot();
            SaveManager.RestoreGuestProfile();
            LeaderboardManager.ResetSession();
            return (true, null);
        }

        // ------------------------------------------------------------------ recovery contact + reset

        /// <summary>Step 1 of attaching a contact (when registering, or adding / changing it later): the
        /// server sends a 6-digit code to it. Wrong or unreachable contacts fail here, before any account changes.</summary>
        public static Task<RecoveryResult> RequestContactVerification(string contact)
        {
            string canonical = RecoveryContact.Normalize(contact);
            if (canonical == null) return Task.FromResult(RecoveryResult.Fail("invalid_contact"));
            return Recovery.SendVerification(canonical);
        }

        /// <summary>Step 2: the code from the message - proof that the contact is the player's.</summary>
        public static Task<RecoveryResult> VerifyContact(string contact, string code)
        {
            string canonical = RecoveryContact.Normalize(contact);
            if (canonical == null) return Task.FromResult(RecoveryResult.Fail("invalid_contact"));
            return Recovery.VerifyContact(canonical, (code ?? "").Trim());
        }

        /// <summary>Adds or changes the recovery contact of the signed-in account (any typed form). The
        /// contact must have been verified first; a contact another account held moves to this one.</summary>
        public static async Task<RecoveryResult> SetRecoveryContact(string contact)
        {
            if (!IsLinked) return RecoveryResult.Fail("server_unavailable");
            string canonical = RecoveryContact.Normalize(contact);
            if (canonical == null) return RecoveryResult.Fail("invalid_contact");

            RecoveryResult result = await Recovery.SetContact(CurrentUsername, canonical);
            if (result.ok)
            {
                SaveManager.SetRecoveryContact(canonical);
                RecoveryContactChanged?.Invoke();
            }
            return result;
        }

        /// <summary>Step 1 of "forgot password": the server sends a code to the contact registered for
        /// `username`. Wrong username/contact pairs answer the same as right ones - nobody can use this to find out who has an account.</summary>
        public static Task<RecoveryResult> RequestPasswordReset(string username, string contact)
        {
            string canonical = RecoveryContact.Normalize(contact);
            if (canonical == null || string.IsNullOrWhiteSpace(username)) return Task.FromResult(RecoveryResult.Fail("invalid_contact"));
            return Recovery.RequestReset(username.Trim(), canonical);
        }

        /// <summary>Step 2: the code from the message plus the new password. On success the password of
        /// the account is the new one and the player can simply sign in.</summary>
        public static async Task<RecoveryResult> ConfirmPasswordReset(string username, string contact, string code, string newPassword)
        {
            string canonical = RecoveryContact.Normalize(contact);
            if (canonical == null || string.IsNullOrWhiteSpace(username)) return RecoveryResult.Fail("invalid_contact");
            if (!IsPasswordValid(newPassword)) return RecoveryResult.Fail("weak_password");

            RecoveryResult result = await Recovery.ConfirmReset(username.Trim(), canonical, (code ?? "").Trim(), newPassword);
            if (result.ok)
            {
                RememberUsername(username.Trim());
                PasswordResetDone?.Invoke(username.Trim());
            }
            return result;
        }

        /// <summary>The player-facing text for a server answer.</summary>
        public static string RecoveryErrorText(RecoveryResult result)
        {
            switch (result.error)
            {
                case "invalid_contact": return Localization.Get("account.error_invalid_contact");
                case "not_verified": return Localization.Get("account.error_not_verified");
                case "rate_limited": return Localization.Get("account.error_rate_limited");
                case "too_soon": return Localization.Format("account.error_too_soon_fmt", Mathf.Max(1, result.retryAfter));
                case "send_failed": return Localization.Get("account.error_send_failed");
                case "invalid_code": return Localization.Get("account.error_invalid_code");
                case "code_expired": return Localization.Get("account.error_code_expired");
                case "too_many_attempts": return Localization.Get("account.error_too_many_attempts");
                case "weak_password": return Localization.Get("account.error_weak_password");
                case "reset_failed":
                case "purge_failed":
                case "cleanup_failed":
                case "server_not_configured":
                case "server_unavailable":
                    return Localization.Get("account.error_server_unavailable");
                default: return Localization.Get("account.error_generic");
            }
        }

        /// <summary>Changes the password of the signed-in account. The only password management the
        /// Authentication service offers: it needs the current password, and there is no e-mail
        /// reset - an account has no e-mail address.</summary>
        public static async Task<(bool success, string error)> ChangePassword(string currentPassword, string newPassword)
        {
            await LeaderboardManager.EnsureInitialized();
            if (!LeaderboardManager.IsReady) return (false, Localization.Get("account.error_not_connected"));

            try
            {
                await AuthenticationService.Instance.UpdatePasswordAsync(currentPassword, newPassword);
                return (true, null);
            }
            catch (Exception e)
            {
                return (false, FriendlyError(e));
            }
        }

        // ------------------------------------------------------------------ remembered usernames

        private const string RecentKey = "SpaceHawk.RecentAccounts";
        private const int MaxRecent = 3;

        /// <summary>The usernames last used to sign in or register on THIS device, newest first - for
        /// a player who forgot theirs. Usernames are not secrets; passwords are never stored.</summary>
        public static string[] GetRecentUsernames()
        {
            string raw = PlayerPrefs.GetString(RecentKey, "");
            if (string.IsNullOrEmpty(raw)) return new string[0];
            return raw.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
        }

        public static void RememberUsername(string username)
        {
            if (string.IsNullOrWhiteSpace(username)) return;
            List<string> names = new List<string>(GetRecentUsernames());
            names.RemoveAll(n => string.Equals(n, username, StringComparison.OrdinalIgnoreCase));
            names.Insert(0, username);
            if (names.Count > MaxRecent) names.RemoveRange(MaxRecent, names.Count - MaxRecent);
            PlayerPrefs.SetString(RecentKey, string.Join("\n", names));
            PlayerPrefs.Save();
        }

        public static void ForgetUsername(string username)
        {
            if (string.IsNullOrWhiteSpace(username)) return;
            List<string> names = new List<string>(GetRecentUsernames());
            names.RemoveAll(n => string.Equals(n, username, StringComparison.OrdinalIgnoreCase));
            PlayerPrefs.SetString(RecentKey, string.Join("\n", names));
            PlayerPrefs.Save();
        }

        // ------------------------------------------------------------------ input rules

        /// <summary>Unity's Authentication service enforces a username length rule server-side (3-20
        /// characters) with no client-side hint of its own - checked here first for an immediate,
        /// specific reason.</summary>
        public static bool IsUsernameValid(string username)
        {
            if (string.IsNullOrEmpty(username) || username.Length < 3 || username.Length > 20) return false;
            foreach (char c in username)
            {
                if (!char.IsLetterOrDigit(c) && c != '_' && c != '.' && c != '-') return false;
            }
            return true;
        }

        /// <summary>The service's password rule (8-30 characters with an upper-case letter, a
        /// lower-case letter, a digit and a symbol - Unity's Player Authentication documents all four),
        /// checked client-side so a weak password gets a specific reason instead of an opaque
        /// "something went wrong".</summary>
        public static bool IsPasswordValid(string password)
        {
            if (string.IsNullOrEmpty(password) || password.Length < 8 || password.Length > 30) return false;
            bool hasUpper = false, hasLower = false, hasDigit = false, hasSymbol = false;
            foreach (char c in password)
            {
                if (char.IsUpper(c)) hasUpper = true;
                else if (char.IsLower(c)) hasLower = true;
                else if (char.IsDigit(c)) hasDigit = true;
                else if (!char.IsWhiteSpace(c)) hasSymbol = true;
            }
            return hasUpper && hasLower && hasDigit && hasSymbol;
        }

        /// <summary>Maps a raw UGS exception to a short, localized message - the SDK's own
        /// e.Message is always English server text (e.g. "Username/password external id provider
        /// is not available. PERMISSION_DENIED"), which must never reach the Vietnamese UI as-is.</summary>
        private static string FriendlyError(Exception e)
        {
            if (e is RequestFailedException rfe)
            {
                if (rfe.ErrorCode == AuthenticationErrorCodes.InvalidParameters) return Localization.Get("account.error_invalid_params");
                if (rfe.ErrorCode == AuthenticationErrorCodes.AccountAlreadyLinked) return Localization.Get("account.error_already_linked");
                if (rfe.ErrorCode == AuthenticationErrorCodes.InvalidProvider) return Localization.Get("account.error_provider_disabled");
                if (rfe.ErrorCode == AuthenticationErrorCodes.BannedUser) return Localization.Get("account.error_banned");

                string msg = rfe.Message ?? "";
                if (msg.IndexOf("already exists", StringComparison.OrdinalIgnoreCase) >= 0)
                    return Localization.Get("account.error_username_taken");
                if (msg.IndexOf("invalid username or password", StringComparison.OrdinalIgnoreCase) >= 0)
                    return Localization.Get("account.error_wrong_credentials");
                if (msg.IndexOf("not available", StringComparison.OrdinalIgnoreCase) >= 0
                    || msg.IndexOf("PERMISSION", StringComparison.OrdinalIgnoreCase) >= 0
                    || msg.IndexOf("provider", StringComparison.OrdinalIgnoreCase) >= 0)
                    return Localization.Get("account.error_service_unavailable");
            }

            Debug.LogWarning($"[AccountManager] Unmapped error: {e}");
            return Localization.Get("account.error_generic");
        }
    }
}
