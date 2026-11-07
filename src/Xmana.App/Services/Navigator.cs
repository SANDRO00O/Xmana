using Xmana.App.Focus;
using Xmana.Core.Models;
using Xmana.Core.Navigation;

namespace Xmana.App.Services
{
    internal static class Navigator
    {
        /// <summary>Starts playback of a video.</summary>
        public static void OpenPlayer(string videoId)
        {
            AppServices.Navigation.SaveFocusKey(XmanaFocus.CurrentKey());
            AppServices.Navigation.Push(NavigationDestination.Player, videoId);
        }

        /// <summary>Plays another episode in place of the current player screen (Back stack does not grow).</summary>
        public static void ReplacePlayer(string videoId)
        {
            AppServices.Navigation.Replace(NavigationDestination.Player, videoId);
        }

        /// <summary>Opens a video's details, first remembering the focused card so Back lands on it again.</summary>
        public static void OpenDetails(VideoSummary video)
        {
            AppServices.Navigation.SaveFocusKey(XmanaFocus.CurrentKey());
            AppServices.Navigation.Push(NavigationDestination.Details, video.Id);
        }

        /// <summary>Opens details for an id (e.g. the parent series of an episode).</summary>
        public static void OpenDetailsById(string videoId)
        {
            AppServices.Navigation.SaveFocusKey(XmanaFocus.CurrentKey());
            AppServices.Navigation.Push(NavigationDestination.Details, videoId);
        }

        /// <summary>Moves to a sibling episode without growing the Back stack.</summary>
        public static void ReplaceWithDetails(VideoSummary video)
        {
            AppServices.Navigation.Replace(NavigationDestination.Details, video.Id);
        }

        public static void OpenStaff(string staffId)
        {
            AppServices.Navigation.SaveFocusKey(XmanaFocus.CurrentKey());
            AppServices.Navigation.Push(NavigationDestination.Staff, staffId);
        }

        /// <summary>Opens a category's results.</summary>
        public static void OpenCategory(string categoryId)
        {
            AppServices.Navigation.SaveFocusKey(XmanaFocus.CurrentKey());
            AppServices.Navigation.Push(NavigationDestination.Categories, categoryId);
        }

        public static void OpenAdvancedSearch()
        {
            AppServices.Navigation.SaveFocusKey(XmanaFocus.CurrentKey());
            AppServices.Navigation.Push(NavigationDestination.Search, "advanced");
        }
    }
}
