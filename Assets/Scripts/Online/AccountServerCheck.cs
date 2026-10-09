using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Unity.Services.CloudCode;

namespace SpaceHawk.Online
{
    /// <summary>Answers "is the account server set up?" after deploying Assets/CloudCode/AccountRecovery.js: can the
    /// script be reached, and which secrets / sending channels does it see? It only ever learns yes/no per
    /// item - never a value. Run from Unity with Tools > Space Hawk > Check Account Server (Play Mode).</summary>
    public static class AccountServerCheck
    {
        public class Report
        {
            public bool ok;
            public bool names;            // the deployed script knows display names (an older deployment does not)
            public bool adminAuth;
            public bool adminAuthWorks;   // Unity accepted the stored credentials on a harmless read
            public int adminAuthStatus;   // the HTTP status of that read (0 = not tried)
            public bool pepper;
            public bool email;
        }

        public static async Task<string> Run()
        {
            await LeaderboardManager.EnsureInitialized();
            if (!LeaderboardManager.IsReady)
            {
                return "Account server check\n  Could not reach Unity Gaming Services. Is the project linked (Project Settings > Services) and is the computer online?";
            }

            try
            {
                Report report = await CloudCodeService.Instance.CallEndpointAsync<Report>(
                    "AccountRecovery", new Dictionary<string, object> { { "action", "selfCheck" } });
                return Describe(report);
            }
            catch (Exception e)
            {
                return "Account server check\n  The script \"AccountRecovery\" could not be called - it is probably not deployed yet" +
                       " (run Tools/CloudCodeSetup/Setup-CloudCode.ps1) or this environment is not the one it was deployed to.\n  " + e.Message;
            }
        }

        /// <summary>The readable result: one line per thing the server needs, with what to do about it.</summary>
        public static string Describe(Report report)
        {
            StringBuilder text = new StringBuilder("Account server check\n");
            if (report == null || !report.ok)
            {
                text.Append("  The script answered, but not with a result - redeploy Assets/CloudCode/AccountRecovery.js.");
                return text.ToString();
            }

            text.Append("  Script AccountRecovery:   deployed and reachable\n");
            text.Append(report.names
                ? "  Display names:            supported (unique, one change a week)\n"
                : "  Display names:            NOT in the deployed script - redeploy it (Tools/CloudCodeSetup/Deploy-CloudCode.ps1)\n");
            if (!report.adminAuth)
            {
                text.Append("  UGS_ADMIN_AUTH:           MISSING - needed to reset passwords and to delete accounts (scores)\n");
            }
            else if (!report.adminAuthWorks)
            {
                text.Append("  UGS_ADMIN_AUTH:           set, but Unity REJECTED it (HTTP " + report.adminAuthStatus + ") - the value is not base64(KEY_ID:SECRET) of a live service account key" +
                            (report.adminAuthStatus == 403 ? ", or the account lacks the Leaderboards Admin role" : "") + "; recreate the secret\n");
            }
            else
            {
                text.Append("  UGS_ADMIN_AUTH:           set, accepted by Unity\n");
            }
            text.Append(report.pepper
                ? "  RECOVERY_PEPPER:          set\n"
                : "  RECOVERY_PEPPER:          MISSING - needed for every code (verification and reset)\n");
            text.Append(report.email
                ? "  E-mail codes:             ready\n"
                : "  E-mail codes:             not ready - add MAIL_RELAY_URL + MAIL_RELAY_KEY (the Gmail relay, Tools/MailRelay/Code.gs)\n");

            bool ready = report.names && report.adminAuth && report.adminAuthWorks && report.pepper && report.email;
            text.Append(ready
                ? "  => Ready: contacts can be verified, passwords reset and accounts deleted."
                : "  => Not ready yet - fix the lines marked MISSING / not ready, then run the check again.");
            return text.ToString();
        }
    }
}
