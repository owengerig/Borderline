using UnityEngine;

namespace Borderline
{
    /// <summary>App-wide constants shown in the UI and store listings.</summary>
    public static class AppInfo
    {
        public const string Creator = "OG Software";
        public const string PrivacyPolicyUrl = "https://sites.google.com/view/og-software/privacy-policy";
        public const string SupportUrl = "https://sites.google.com/view/og-software/support";

        /// <summary>Comes from Project Settings > Player > Version (set it to 0.1.0, like every OG Software app).</summary>
        public static string Version => string.IsNullOrEmpty(Application.version) ? "0.1.0" : Application.version;

        /// <summary>Bottom-left footer, the same line in every OG Software app: "Created by OG Software v0.1.0".</summary>
        public static string CreditLine => $"Created by {Creator} v{Version}";
    }
}
