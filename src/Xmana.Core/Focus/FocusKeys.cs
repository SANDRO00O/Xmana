using Xmana.Core.Navigation;

namespace Xmana.Core.Focus
{
    /// <summary>
    /// Stable string identities for focusable elements. The UI tags elements with these keys
    /// (Xmana.App XmanaFocus.Key) so focus can be saved and restored across navigation and
    /// language changes without holding element references.
    /// </summary>
    public static class FocusKeys
    {
        public const string NavItemPrefix = "nav.";

        /// <summary>The primary focus target of the page currently shown in the content area.</summary>
        public const string ContentPrimary = "content.primary";

        public static string NavItem(NavigationDestination destination) => NavItemPrefix + destination;

        public static bool IsNavItem(string? key) => key != null && key.StartsWith(NavItemPrefix, System.StringComparison.Ordinal);

        public static string LanguageOption(string languageCode) => "settings.language." + languageCode;
    }
}
