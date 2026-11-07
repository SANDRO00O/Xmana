namespace Xmana.App.Shell
{
    /// <summary>
    /// A page that wants to consume the B button before the shell navigates back (the player closes its menus, cancels the
    /// next-episode offer, and only then exits).
    /// </summary>
    public interface IBackHandler
    {
        /// <summary>Returns true when the press was used and the shell must not navigate.</summary>
        bool HandleBack();
    }
}
