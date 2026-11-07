using Xmana.Core.Navigation;

namespace Xmana.App.Pages
{
    /// <summary>
    /// Frame parameter wrapper. A fresh instance is created for every navigation so Frame never
    /// treats a re-render (e.g. after a language change) as "same page, same parameter" and skips it.
    /// </summary>
    public sealed class PageArgs
    {
        public PageArgs(NavigationEntry entry)
        {
            Entry = entry;
        }

        public NavigationEntry Entry { get; }
    }
}
