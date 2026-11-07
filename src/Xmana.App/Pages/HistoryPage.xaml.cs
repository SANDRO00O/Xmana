using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Xmana.App.Focus;
using Xmana.App.Services;
using Xmana.App.Shell;
using Xmana.Core.Discovery;
using Xmana.Core.Focus;
using Xmana.Core.Library;

namespace Xmana.App.Pages
{
    /// <summary>
    /// Watch history from the local progress records (PROMPT.md section 33), newest first, with a progress bar per title. It shows what is
    /// known immediately and then fills in titles watched before the local library existed. Clearing asks first.
    /// </summary>
    public sealed partial class HistoryPage : Page, IBackHandler
    {
        private IReadOnlyList<ProgressItem> _items = new ProgressItem[0];
        private RenderScheduler _renderer;
        private bool _active;

        public HistoryPage()
        {
            InitializeComponent();
            XmanaFocus.SetKey(ClearButton, FocusKeys.ContentPrimary);
            Results.ProgressProvider = id => _items.FirstOrDefault(i => i.Video.Id == id)?.Fraction;
            Results.VideoInvoked += (s, video) => Navigator.OpenDetails(video);
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _active = true;
            var localization = AppServices.Localization;
            TitleText.Text = localization.Get("Nav_History");
            ClearButton.Label = localization.Get("History_Clear");
            ClearButton.Glyph = "\uE74D";
            Results.EmptyTitle = localization.Get("History_EmptyTitle");
            Results.EmptyBody = localization.Get("History_EmptyBody");
            _renderer = new RenderScheduler(Dispatcher, Render);

            _items = AppServices.History.LoadLocal();
            Render();

            try
            {
                var full = await AppServices.History.LoadAsync(CancellationToken.None);
                if (_active)
                {
                    _items = full;
                    Render();
                }
            }
            catch (Exception)
            {
                // the locally known list stays on screen
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            _active = false;
            _renderer?.Stop();
        }

        private void Render()
        {
            var videos = _items.Select(i => i.Video).ToList();
            ClearButton.Visibility = videos.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            var snapshot = new ResultsSnapshot(videos.Count == 0 ? ResultsState.Empty : ResultsState.Content, videos, false, false, false, false);
            Results.Render(snapshot, "history");
        }

        private void OnClearClick(object sender, RoutedEventArgs e)
        {
            var localization = AppServices.Localization;
            Confirm.Show(
                localization.Get("History_ClearTitle"),
                localization.Get("History_ClearBody"),
                localization.Get("History_ClearConfirm"),
                () =>
                {
                    AppServices.Storage.ClearHistory();
                    _items = new ProgressItem[0];
                    Render();
                });
        }

        public bool HandleBack()
        {
            if (Confirm.IsOpen)
            {
                Confirm.Close();
                return true;
            }

            return false;
        }
    }
}
