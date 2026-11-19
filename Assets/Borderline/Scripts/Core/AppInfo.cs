using UnityEngine;

namespace Borderline
{
    /// <summary>App-wide constants shown in the UI and store listings.</summary>
    public static class AppInfo
    {
        public const string Creator = "OG Software";
        public const string PrivacyPolicyUrl = "https://sites.google.com/view/og-software/privacy-policy";
        public const string SupportUrl = "https://sites.google.com/view/og-software/support";

        /// <summary>Comes from Project Settings > Player > Version (set it to 0.0.1).</summary>
        public static string Version => string.IsNullOrEmpty(Application.version) ? "0.0.1" : Application.version;

        public static string CreditLine => $"Creator {Creator}   v{Version}";
    }
}
