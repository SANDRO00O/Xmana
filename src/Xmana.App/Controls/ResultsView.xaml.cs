using System;
using System.Collections.Generic;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Xmana.App.Focus;
using Xmana.App.Services;
using Xmana.Core.Discovery;
using Xmana.Core.Models;

namespace Xmana.App.Controls
{
    /// <summary>
    /// Shared presentation of a <see cref="ResultsSnapshot"/>: poster grid (rows computed from the available width), skeleton
    /// while loading, empty and error messages, "loading more" indicator, retry for a failed page, and an offline notice.
    /// The grid is updated in place when pages are appended, so focus and scroll position are not disturbed.
    /// </summary>
    public sealed partial class ResultsView : UserControl
    {
        private const double CardSlot = 152 + 14;

        private readonly List<VideoCard> _cards = new List<VideoCard>();
        private string _keyPrefix = "results";
        private int _columns = 3;
        private ResultsSnapshot _last = ResultsSnapshot.Idle;

        public ResultsView()
        {
            InitializeComponent();
        }

        /// <summary>Optional progress per video id (History): the fraction shown as a bar on the poster.</summary>
        public Func<string, double?> ProgressProvider { get; set; }

        public string EmptyTitle { get; set; }

        public string EmptyBody { get; set; }

        public event EventHandler<VideoSummary> VideoInvoked;

        public event EventHandler LoadMoreRequested;

        public event EventHandler RetryRequested;

        /// <summary>Focuses the first card (returns false when there is none).</summary>
        public bool FocusFirstCard() => _cards.Count > 0 && _cards[0].Focus(Windows.UI.Xaml.FocusState.Keyboard);

        public void Render(ResultsSnapshot snapshot, string keyPrefix)
        {
            _last = snapshot;
            _keyPrefix = keyPrefix ?? "results";
            var localization = AppServices.Localization;

            Skeleton.Visibility = snapshot.State == ResultsState.Loading ? Visibility.Visible : Visibility.Collapsed;
            Scroller.Visibility = snapshot.State == ResultsState.Content ? Visibility.Visible : Visibility.Collapsed;
            MessagePanel.Visibility = snapshot.State == ResultsState.Empty || snapshot.State == ResultsState.Error ? Visibility.Visible : Visibility.Collapsed;

            if (snapshot.State == ResultsState.Empty || snapshot.State == ResultsState.Error)
            {
                var error = snapshot.State == ResultsState.Error;
                MessageTitle.Text = error ? localization.Get("Results_ErrorTitle") : (EmptyTitle ?? localization.Get("Results_EmptyTitle"));
                MessageBody.Text = error ? localization.Get("Results_ErrorBody") : (EmptyBody ?? localization.Get("Results_EmptyBody"));
                RetryButton.Label = localization.Get("Results_Retry");
                RetryButton.Visibility = error ? Visibility.Visible : Visibility.Collapsed;
            }

            if (snapshot.State != ResultsState.Content)
            {
                SetItems(new VideoSummary[0]);
                return;
            }

            OfflineNotice.Text = localization.Get("Results_OfflineNotice");
            OfflineNotice.Visibility = snapshot.Outdated ? Visibility.Visible : Visibility.Collapsed;

            SetItems(snapshot.Items);

            MoreRing.IsActive = snapshot.IsLoadingMore;
            MoreRing.Visibility = snapshot.IsLoadingMore ? Visibility.Visible : Visibility.Collapsed;
            LoadMoreRetryButton.Label = localization.Get("Results_LoadMoreFailed");
            LoadMoreRetryButton.Visibility = snapshot.LoadMoreFailed ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SetItems(IReadOnlyList<VideoSummary> items)
        {
            var shown = _cards.Count;
            var prefix = shown <= items.Count;
            for (var i = 0; prefix && i < shown; i++)
            {
                if (_cards[i].Video == null || _cards[i].Video.Id != items[i].Id)
                {
                    prefix = false;
                }
            }

            var rebuild = !prefix;
            if (rebuild)
            {
                _cards.Clear();
                shown = 0;
            }

            for (var i = shown; i < items.Count; i++)
            {
                _cards.Add(CreateCard(items[i]));
            }

            Layout(rebuild ? 0 : shown);

            if (ProgressProvider != null)
            {
                foreach (var card in _cards)
                {
                    card.Progress = card.Video == null ? null : ProgressProvider(card.Video.Id);
                }
            }
        }

        private VideoCard CreateCard(VideoSummary video)
        {
            var card = new VideoCard { Video = video };
            XmanaFocus.SetKey(card, _keyPrefix + "." + video.Id);
            card.Click += (s, e) => VideoInvoked?.Invoke(this, video);
            card.GotFocus += (s, e) =>
            {
                var index = _cards.IndexOf(card);
                if (_last.CanLoadMore && !_last.IsLoadingMore && !_last.LoadMoreFailed && index >= _cards.Count - _columns * 2)
                {
                    LoadMoreRequested?.Invoke(this, EventArgs.Empty);
                }
            };
            return card;
        }

        /// <summary>
        /// Places cards in rows of <c>_columns</c>. When only new cards were appended (<paramref name="fromIndex"/> &gt; 0 and the
        /// column count is unchanged) existing rows are left alone.
        /// </summary>
        private void Layout(int fromIndex)
        {
            if (fromIndex == 0)
            {
                foreach (var child in Rows.Children)
                {
                    (child as StackPanel)?.Children.Clear();
                }

                Rows.Children.Clear();
            }

            StackPanel row = null;
            if (fromIndex > 0 && Rows.Children.Count > 0)
            {
                row = (StackPanel)Rows.Children[Rows.Children.Count - 1];
                if (row.Children.Count >= _columns)
                {
                    row = null;
                }
            }

            for (var i = fromIndex; i < _cards.Count; i++)
            {
                if (row == null)
                {
                    row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
                    Rows.Children.Add(row);
                }

                row.Children.Add(_cards[i]);
                if (row.Children.Count >= _columns)
                {
                    row = null;
                }
            }
        }

        private void OnScrollerSizeChanged(object sender, SizeChangedEventArgs e)
        {
            var columns = Math.Max(1, (int)Math.Floor((e.NewSize.Width - 8) / CardSlot));
            if (columns == _columns)
            {
                return;
            }

            _columns = columns;
            // Re-flow the same card instances (focus is lost only if the layout genuinely changed width).
            foreach (var child in Rows.Children)
            {
                (child as StackPanel)?.Children.Clear();
            }

            Rows.Children.Clear();
            Layout(0);
        }

        private void OnRetryClick(object sender, RoutedEventArgs e) => RetryRequested?.Invoke(this, EventArgs.Empty);

        private void OnLoadMoreRetryClick(object sender, RoutedEventArgs e) => LoadMoreRequested?.Invoke(this, EventArgs.Empty);
    }
}
