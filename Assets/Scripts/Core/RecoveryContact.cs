using System.Text.RegularExpressions;

namespace SpaceHawk.Core
{
    /// <summary>The e-mail address an account keeps so a forgotten password can be reset: one place that
    /// decides what is valid, what the canonical form is (what the server stores and matches) and how it
    /// is shown without giving it away.</summary>
    public static class RecoveryContact
    {
        private static readonly Regex EmailPattern = new Regex(@"^[^\s@]+@[^\s@]+\.[^\s@]{2,}$", RegexOptions.Compiled);

        /// <summary>Returns the canonical form (trimmed, lower-case) or null when the text is not a valid e-mail address.</summary>
        public static string Normalize(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;
            string email = input.Trim().ToLowerInvariant();
            if (email.Length > 120 || !EmailPattern.IsMatch(email)) return null;
            return email;
        }

        public static bool IsValid(string input) => Normalize(input) != null;

        /// <summary>"pilot@example.com" -> "pi***@example.com". An empty or unrecognized value gives an empty string.</summary>
        public static string Mask(string canonical)
        {
            string value = Normalize(canonical);
            if (value == null) return "";

            int at = value.IndexOf('@');
            string local = value.Substring(0, at);
            string shown = local.Length <= 2 ? local.Substring(0, 1) : local.Substring(0, 2);
            return shown + "***" + value.Substring(at);
        }
    }
}
