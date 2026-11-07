namespace Xmana.Core.Localization
{
    /// <summary>
    /// A first-class UI language. <see cref="NativeName"/> is shown in the language's own
    /// script (autonym convention), so it is intentionally not routed through translation.
    /// </summary>
    public sealed class AppLanguage
    {
        public AppLanguage(string code, string resourceTag, string nativeName, bool isRightToLeft)
        {
            Code = code;
            ResourceTag = resourceTag;
            NativeName = nativeName;
            IsRightToLeft = isRightToLeft;
        }

        /// <summary>Short stable code persisted in local settings (e.g. "ar", "en").</summary>
        public string Code { get; }

        /// <summary>BCP-47 tag used for resource lookup and FrameworkElement.Language (e.g. "en-US").</summary>
        public string ResourceTag { get; }

        public string NativeName { get; }

        public bool IsRightToLeft { get; }

        public override string ToString() => Code;
    }
}
