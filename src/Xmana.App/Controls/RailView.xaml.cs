using System;
using System.Collections.Generic;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Xmana.App.Focus;
using Xmana.Core.Models;

namespace Xmana.App.Controls
{
    public sealed partial class RailView : UserControl
    {
        /// <summary>Focus when this many cards remain before the end: time to ask for the next page.</summary>
        private const int EndThreshold = 4;

        public RailView()
        {
            InitializeComponent();
        }

        public string SectionId { get; set; }

        /// <summary>Watched fractions by video id (Continue Watching); null for rails without progress.</summary>
        public System.Collections.Generic.IReadOnlyDictionary<string, double> Progress { get; set; }

        public event EventHandler<VideoSummary> VideoInvoked;

        public event EventHandler EndReached;

        public void SetTitle(string title)
        {
            TitleText.Text = title ?? string.Empty;
        }

        /// <summary>
        /// Shows <paramref name="items"/>. When the cards already shown are a prefix of the new list (load-more) only the
        /// new cards are added, so focus and scroll position are untouched; otherwise the rail is rebuilt.
        /// </summary>
        public void SetItems(IReadOnlyList<VideoSummary> items)
        {
            var shown = Cards.Children.Count;
            var prefix = shown <= items.Count;
            for (var i = 0; prefix && i < shown; i++)
            {
                var card = (VideoCard)Cards.Children[i];
                if (card.Video == null || card.Video.Id != items[i].Id)
                {
                    prefix = false;
                }
            }

            if (!prefix)
            {
                Cards.Children.Clear();
                shown = 0;
            }

            for (var i = shown; i < items.Count; i++)
            {
                Cards.Children.Add(CreateCard(items[i], i));
            }

            ApplyProgress();
        }

        /// <summary>Updates the progress bars of the existing cards (progress can change while the list stays the same).</summary>
        private void ApplyProgress()
        {
            foreach (var child in Cards.Children)
            {
                var card = child as VideoCard;
                if (card?.Video == null)
                {
                    continue;
                }

                double value;
                card.Progress = Progress != null && Progress.TryGetValue(card.Video.Id, out value) ? value : (double?)null;
            }
        }

        private VideoCard CreateCard(VideoSummary video, int index)
        {
            var card = new VideoCard { Video = video };
            XmanaFocus.SetKey(card, "home." + SectionId + "." + video.Id);
            card.Click += (s, e) => VideoInvoked?.Invoke(this, video);
            card.GotFocus += (s, e) =>
            {
                if (index >= Cards.Children.Count - EndThreshold)
                {
                    EndReached?.Invoke(this, EventArgs.Empty);
                }
            };
            return card;
        }
    }
}
