using System;

namespace Xmana.Core.Navigation
{
    /// <summary>One position in the navigation stack.</summary>
    public sealed class NavigationEntry
    {
        public NavigationEntry(NavigationDestination destination, string? parameter, string? savedFocusKey)
        {
            Id = Guid.NewGuid();
            Destination = destination;
            Parameter = parameter;
            SavedFocusKey = savedFocusKey;
        }

        public Guid Id { get; }

        public NavigationDestination Destination { get; }

        /// <summary>Destination-specific argument (e.g. a video id in later milestones).</summary>
        public string? Parameter { get; }

        /// <summary>
        /// Focus key (see <see cref="Xmana.Core.Focus.FocusKeys"/>) that had focus when the user
        /// left this entry. The UI restores it when the entry becomes current again.
        /// </summary>
        public string? SavedFocusKey { get; set; }
    }
}
