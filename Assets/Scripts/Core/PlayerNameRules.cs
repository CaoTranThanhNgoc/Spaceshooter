using System;
using System.Globalization;
using System.Text;

namespace SpaceHawk.Core
{
    /// <summary>What a display name may look like, and when two names count as the same - the same rules the
    /// account server applies (Assets/CloudCode/AccountRecovery.js), checked here first so a bad name gets an
    /// immediate, specific answer. The server has the last word: it also knows every other player's name.</summary>
    public static class PlayerNameRules
    {
        public const int MinLength = 3;
        public const int MaxLength = 16;
        /// <summary>A name can be changed by hand once a week.</summary>
        public const long ChangeCooldownSeconds = 7L * 24 * 60 * 60;

        /// <summary>The name the game gives a player who has not chosen one: "Pilot" + four letters/digits.</summary>
        public const string DefaultPrefix = "Pilot";

        private static readonly System.Random Rng = new System.Random();

        /// <summary>The trimmed name, or null when it is not a valid name: 3-16 characters, letters (any script),
        /// digits and . _ - only (no spaces - Unity's player-name service does not take them, and the name on the
        /// Leaderboard has to be exactly this one), with at least 3 letters or digits.</summary>
        public static string Clean(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;

            string name = raw.Trim();
            if (name.Length < MinLength || name.Length > MaxLength) return null;
            foreach (char c in name)
            {
                if (!char.IsLetterOrDigit(c) && c != '.' && c != '_' && c != '-') return null;
            }
            return Key(name).Length >= MinLength ? name : null;
        }

        /// <summary>What two names are compared by: case, accents and punctuation do not count, so "Ngọc",
        /// "ngoc" and "N.g-o.c" are one name.</summary>
        public static string Key(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";

            string text = name;
            try
            {
                text = name.Normalize(NormalizationForm.FormD);
            }
            catch (Exception)
            {
                // A platform without normalization data: compare without taking accents apart.
            }

            StringBuilder key = new StringBuilder();
            foreach (char c in text)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
                if (c == 'đ' || c == 'Đ') { key.Append('d'); continue; }   // d with a stroke
                if (char.IsLetterOrDigit(c)) key.Append(char.ToLowerInvariant(c));
            }
            return key.ToString();
        }

        public static bool SameName(string a, string b) => Key(a).Length > 0 && Key(a) == Key(b);

        /// <summary>True for a name the game made up itself ("Pilot" + four lower-case letters/digits).</summary>
        public static bool IsGameDefault(string name) =>
            !string.IsNullOrEmpty(name) && System.Text.RegularExpressions.Regex.IsMatch(name, "^" + DefaultPrefix + "[a-z0-9]{4}$");

        /// <summary>What a signed-in player is shown as until they pick another name: the account name (cut to 16
        /// characters when it is longer), or null when that is not a usable name.</summary>
        public static string AccountDefault(string username)
        {
            if (string.IsNullOrWhiteSpace(username)) return null;
            string name = username.Trim();
            if (name.Length > MaxLength) name = name.Substring(0, MaxLength);
            return Clean(name);
        }

        /// <summary>"thanhne" -> "thanhne_k3x9": the account name with a short random tail, for when somebody else
        /// already holds the account name as a display name.</summary>
        public static string VariantOf(string name)
        {
            string tail = RandomDefaultName().Substring(DefaultPrefix.Length);
            string head = name.Length > MaxLength - 5 ? name.Substring(0, MaxLength - 5) : name;
            return head + "_" + tail;
        }

        /// <summary>"Pilot" + four random lower-case letters/digits, e.g. "Pilot7k2q".</summary>
        public static string RandomDefaultName()
        {
            const string alphabet = "abcdefghjkmnpqrstuvwxyz23456789";
            char[] suffix = new char[4];
            lock (Rng)
            {
                for (int i = 0; i < suffix.Length; i++) suffix[i] = alphabet[Rng.Next(alphabet.Length)];
            }
            return DefaultPrefix + new string(suffix);
        }

        /// <summary>"6 days 4 hours" / "5 hours 12 minutes" / "9 minutes" - how long until a name can change again.</summary>
        public static string FormatWait(long seconds)
        {
            seconds = Math.Max(60, seconds);
            long days = seconds / 86400;
            long hours = (seconds % 86400) / 3600;
            long minutes = (seconds % 3600) / 60;
            if (days > 0) return Localization.Format("time.days_hours_fmt", days, hours);
            if (hours > 0) return Localization.Format("time.hours_minutes_fmt", hours, minutes);
            return Localization.Format("time.minutes_fmt", minutes);
        }
    }
}
