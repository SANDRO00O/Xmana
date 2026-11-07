using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Xmana.App.Controls;
using Xmana.App.Focus;
using Xmana.App.Services;
using Xmana.Core.Details;
using Xmana.Core.Focus;
using Xmana.Core.Images;
using Xmana.Core.Models;

namespace Xmana.App.Pages
{
    /// <summary>
    /// Details for a movie, a series or an episode (PROMPT.md sections 17, 29, 31, 37). It renders
    /// <see cref="DetailsController"/>: metadata first, with seasons/episodes, cast, recommendations and related titles filling in
    /// independently. Nothing is shown that the API did not supply. Playback, favorites, watchlist and resume arrive in later
    /// milestones and are intentionally absent rather than faked.
    /// </summary>
    public sealed partial class DetailsPage : Page
    {
        private const int PosterDecodeWidth = 380;

        private DetailsController _controller;
        private RenderScheduler _renderer;
        private bool _updating;
        private string _posterUrl;
        private CancellationTokenSource _posterLoading;
        private string _episodesKey = string.Empty;
        private string _castKey = string.Empty;
        private bool _focusPlaced;
        private string _rememberedId;

        public DetailsPage()
        {
            InitializeComponent();
            RecommendedRail.SectionId = "recommended";
            RelatedRail.SectionId = "related";
            RecommendedRail.VideoInvoked += (s, video) => Navigator.OpenDetails(video);
            RelatedRail.VideoInvoked += (s, video) => Navigator.OpenDetails(video);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            var entry = (e.Parameter as PageArgs)?.Entry;
            var id = entry?.Parameter;
            if (entry == null || string.IsNullOrWhiteSpace(id))
            {
                return;
            }

            var localization = AppServices.Localization;
            PlayButton.Label = localization.Get("Details_Play");
            PlayButton.Glyph = "\uE768";
            PreviousButton.Label = localization.Get("Details_PreviousEpisode");
            PreviousButton.Glyph = localization.CurrentLanguage.IsRightToLeft ? "\uE76C" : "\uE76B";
            NextButton.Label = localization.Get("Details_NextEpisode");
            NextButton.Glyph = localization.CurrentLanguage.IsRightToLeft ? "\uE76B" : "\uE76C";
            SeriesButton.Label = localization.Get("Details_ViewSeries");
            SeriesButton.Glyph = "\uE7F4";
            EpisodesHeader.Text = localization.Get("Details_Episodes");
            SeriesRetryButton.Label = localization.Get("Results_Retry");
            RetryButton.Label = localization.Get("Results_Retry");
            RecommendedRail.SetTitle(localization.Get("Details_Recommended"));
            RelatedRail.SetTitle(localization.Get("Details_Related"));

            var existing = PageSessions.TryGet<DetailsController>(entry.Id);
            _controller = existing ?? PageSessions.GetOrCreate(
                entry.Id,
                () => new DetailsController(AppServices.DetailsSource, id, () => AppServices.Localization.CurrentLanguage.Code, AppServices.Logger));
            _renderer = new RenderScheduler(Dispatcher, Render);
            _controller.Changed += OnChanged;
            AppServices.Library.Changed += OnLibraryChanged;

            Render();
            if (existing == null || _controller.State == DetailsState.Loading)
            {
                StartLoad();
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            _renderer?.Stop();
            CancelPoster();
            AppServices.Library.Changed -= OnLibraryChanged;
            if (_controller != null)
            {
                _controller.Changed -= OnChanged;
            }
        }

        private void OnLibraryChanged(object sender, EventArgs e) => _renderer?.Request();

        private async void StartLoad()
        {
            try
            {
                await _controller.LoadAsync(CancellationToken.None);
            }
            catch (Exception)
            {
                // the controller reports failures through its state
            }
        }

        private void OnChanged(object sender, EventArgs e) => _renderer?.Request();

        // ---- rendering --------------------------------------------------------------------------

        private void Render()
        {
            var localization = AppServices.Localization;
            var state = _controller.State;

            Skeleton.Visibility = state == DetailsState.Loading ? Visibility.Visible : Visibility.Collapsed;
            Scroller.Visibility = state == DetailsState.Content ? Visibility.Visible : Visibility.Collapsed;
            MessagePanel.Visibility = state == DetailsState.Error || state == DetailsState.NotFound ? Visibility.Visible : Visibility.Collapsed;

            if (state == DetailsState.Error || state == DetailsState.NotFound)
            {
                var notFound = state == DetailsState.NotFound;
                MessageTitle.Text = localization.Get(notFound ? "Details_NotFoundTitle" : "Details_ErrorTitle");
                MessageBody.Text = localization.Get(notFound ? "Details_NotFoundBody" : "Results_ErrorBody");
                RetryButton.Visibility = notFound ? Visibility.Collapsed : Visibility.Visible;
                XmanaFocus.SetKey(RetryButton, FocusKeys.ContentPrimary);
                PlaceFocusOnce(RetryButton);
                return;
            }

            if (state != DetailsState.Content)
            {
                return;
            }

            var details = _controller.Details;
            var preferArabic = localization.CurrentLanguage.IsRightToLeft;

            OfflineNotice.Text = localization.Get("Results_OfflineNotice");
            OfflineNotice.Visibility = _controller.IsShowingOutdatedData ? Visibility.Visible : Visibility.Collapsed;

            RenderHeader(details, preferArabic);
            RenderLibrary(details);
            RenderActions();
            RenderSeries(details, preferArabic);
            RenderCast(details);

            SetRail(RecommendedRail, _controller.Recommended);
            SetRail(RelatedRail, _controller.Related);

            AssignPrimaryFocus();
        }

        private void RenderHeader(VideoDetails details, bool preferArabic)
        {
            var localization = AppServices.Localization;
            var summary = details.Summary;

            TitleText.Text = summary.GetTitle(preferArabic) ?? string.Empty;

            // Episode line: series title, season and episode as the server numbers them.
            var line = new List<string>();
            if (_controller.IsEpisode)
            {
                var seriesTitle = _controller.GetSeriesTitle(preferArabic);
                if (!string.IsNullOrWhiteSpace(seriesTitle))
                {
                    line.Add(seriesTitle);
                }

                if (summary.Season.HasValue)
                {
                    line.Add(string.Format(CultureInfo.CurrentCulture, localization.Get("Details_SeasonFormat"), summary.Season.Value));
                }

                if (summary.EpisodeNumber.HasValue)
                {
                    line.Add(string.Format(CultureInfo.CurrentCulture, localization.Get("Details_EpisodeFormat"), summary.EpisodeNumber.Value));
                }
            }

            SetText(EpisodeLineText, string.Join("  \u2022  ", line));

            var other = details.OtherTitle;
            SetText(OtherTitleText, other != null && other != TitleText.Text ? other : null);

            var meta = new List<string>();
            if (!string.IsNullOrWhiteSpace(summary.Year) && summary.Year != "0")
            {
                meta.Add(summary.Year);
            }

            if (summary.Stars.HasValue && summary.Stars.Value > 0)
            {
                meta.Add("\u2605 " + summary.Stars.Value.ToString("0.0", CultureInfo.InvariantCulture));
            }

            string duration;
            if (DurationFormat.TryFormat(details.Duration, out duration))
            {
                meta.Add(duration);
            }

            SetText(MetaText, string.Join("  \u2022  ", meta));

            var categories = summary.Categories
                .Select(c => preferArabic ? (c.ArTitle ?? c.EnTitle) : (c.EnTitle ?? c.ArTitle))
                .Where(t => !string.IsNullOrWhiteSpace(t));
            SetText(CategoriesText, string.Join(", ", categories));

            var language = preferArabic ? (details.LanguageArTitle ?? details.LanguageEnTitle) : (details.LanguageEnTitle ?? details.LanguageArTitle);
            SetText(LanguageText, language == null ? null : string.Format(CultureInfo.CurrentCulture, localization.Get("Details_LanguageFormat"), language));

            SetText(DescriptionText, summary.GetDescription(preferArabic));

            LoadPoster(ImageSelector.For(summary, ImageRole.Poster));
        }

        private static void SetText(TextBlock block, string text)
        {
            block.Text = text ?? string.Empty;
            block.Visibility = string.IsNullOrWhiteSpace(text) ? Visibility.Collapsed : Visibility.Visible;
        }

        private async void LoadPoster(string url)
        {
            if (url == _posterUrl)
            {
                return;
            }

            CancelPoster();
            _posterUrl = url;
            PosterBrush.ImageSource = null;
            if (url == null)
            {
                return;
            }

            var cts = new CancellationTokenSource();
            _posterLoading = cts;
            try
            {
                var bitmap = await AppServices.Images.LoadAsync(url, PosterDecodeWidth, cts.Token, Xmana.Core.Http.RequestPriority.Primary);
                if (!cts.IsCancellationRequested && bitmap != null)
                {
                    PosterBrush.ImageSource = bitmap;
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception)
            {
                // the plain placeholder stays
            }
        }

        private void CancelPoster()
        {
            var cts = _posterLoading;
            _posterLoading = null;
            if (cts != null)
            {
                cts.Cancel();
            }
        }

        // ---- episode navigation -----------------------------------------------------------------

        /// <summary>Favorite / Watchlist toggles (movies and series; an episode is added through its series). Also remembers the title locally.</summary>
        private void RenderLibrary(VideoDetails details)
        {
            var library = AppServices.Library;
            var localization = AppServices.Localization;
            if (_rememberedId != details.Id)
            {
                _rememberedId = details.Id;
                library.RememberSnapshot(details.Summary);
            }

            LibraryPanel.Visibility = _controller.IsEpisode ? Visibility.Collapsed : Visibility.Visible;

            var favorite = library.IsFavorite(details.Id);
            FavoriteButton.IsActive = favorite;
            FavoriteButton.Glyph = favorite ? "\uEB52" : "\uEB51";
            FavoriteButton.Label = localization.Get(favorite ? "Details_InFavorites" : "Details_AddFavorite");
            AutomationProperties.SetName(FavoriteButton, localization.Get(favorite ? "Details_RemoveFavorite" : "Details_AddFavorite"));

            var listed = library.IsWatchlisted(details.Id);
            WatchlistButton.IsActive = listed;
            WatchlistButton.Glyph = listed ? "\uE73E" : "\uE710";
            WatchlistButton.Label = localization.Get(listed ? "Details_InWatchlist" : "Details_AddWatchlist");
            AutomationProperties.SetName(WatchlistButton, localization.Get(listed ? "Details_RemoveWatchlist" : "Details_AddWatchlist"));
        }

        private void OnFavoriteClick(object sender, RoutedEventArgs e)
        {
            var summary = _controller.Details?.Summary;
            if (summary != null)
            {
                AppServices.Library.ToggleFavorite(summary);
            }
        }

        private void OnWatchlistClick(object sender, RoutedEventArgs e)
        {
            var summary = _controller.Details?.Summary;
            if (summary != null)
            {
                AppServices.Library.ToggleWatchlist(summary);
            }
        }

        private void RenderActions()
        {
            // A movie or an episode can be played; a series itself is a container (its episodes are played).
            var details = _controller.Details;
            var playable = details != null && (details.Summary.Kind != VideoKind.Series || _controller.IsEpisode);
            PlayButton.Visibility = playable ? Visibility.Visible : Visibility.Collapsed;

            var series = _controller.Series;
            var episode = _controller.IsEpisode;

            var previous = episode ? series?.Previous : null;
            var next = episode ? series?.Next : null;
            PreviousButton.Visibility = previous != null ? Visibility.Visible : Visibility.Collapsed;
            NextButton.Visibility = next != null ? Visibility.Visible : Visibility.Collapsed;
            SeriesButton.Visibility = episode ? Visibility.Visible : Visibility.Collapsed;
            ActionsPanel.Visibility = episode ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnPlayClick(object sender, RoutedEventArgs e)
        {
            var id = _controller.Details?.Id;
            if (!string.IsNullOrWhiteSpace(id))
            {
                Navigator.OpenPlayer(id);
            }
        }

        private async void OnPreviousClick(object sender, RoutedEventArgs e)
        {
            var previous = _controller.Series?.Previous;
            if (previous != null)
            {
                await SwitchEpisodeAsync(previous.Id);
            }
        }

        private async void OnNextClick(object sender, RoutedEventArgs e)
        {
            var next = _controller.Series?.Next;
            if (next != null)
            {
                await SwitchEpisodeAsync(next.Id);
            }
        }

        private void OnSeriesClick(object sender, RoutedEventArgs e)
        {
            var seriesId = _controller.Details?.Summary.RootSeriesId;
            if (!string.IsNullOrWhiteSpace(seriesId) && seriesId != "0")
            {
                Navigator.OpenDetailsById(seriesId);
            }
        }

        // ---- seasons and episodes ---------------------------------------------------------------

        private void RenderSeries(VideoDetails details, bool preferArabic)
        {
            var localization = AppServices.Localization;
            var state = _controller.SeriesState;
            SeriesSection.Visibility = state == SeriesLoadState.None ? Visibility.Collapsed : Visibility.Visible;
            SeriesRing.Visibility = state == SeriesLoadState.Loading ? Visibility.Visible : Visibility.Collapsed;
            SeriesRing.IsActive = state == SeriesLoadState.Loading;
            SeriesRetryButton.Visibility = state == SeriesLoadState.Failed ? Visibility.Visible : Visibility.Collapsed;

            if (state == SeriesLoadState.Failed)
            {
                SetText(SeriesMessage, localization.Get("Details_EpisodesFailed"));
                SeasonBox.Visibility = Visibility.Collapsed;
                EpisodesPanel.Children.Clear();
                _episodesKey = string.Empty;
                return;
            }

            var series = _controller.Series;
            if (state != SeriesLoadState.Loaded || series == null)
            {
                SetText(SeriesMessage, null);
                SeasonBox.Visibility = Visibility.Collapsed;
                return;
            }

            // Season selector (only worth showing for more than one season).
            _updating = true;
            SeasonBox.Items.Clear();
            foreach (var group in series.Groups)
            {
                SeasonBox.Items.Add(SeasonLabel(group.Season));
            }

            var selectedIndex = series.Groups.ToList().FindIndex(g => g.Season == series.SelectedSeason);
            SeasonBox.SelectedIndex = Math.Max(0, selectedIndex);
            SeasonBox.Visibility = series.Groups.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
            _updating = false;

            var key = series.SeriesId + "|" + series.SelectedSeason + "|" + series.CurrentEpisodeId + "|" + string.Join(",", series.SelectedEpisodes.Select(e => e.Id)) + "|" + preferArabic;
            if (key != _episodesKey)
            {
                _episodesKey = key;
                EpisodesPanel.Children.Clear();
                foreach (var episode in series.SelectedEpisodes)
                {
                    var button = new ChoiceButton
                    {
                        Label = EpisodeLabel(episode, preferArabic),
                        IsActive = episode.Id == series.CurrentEpisodeId,
                        Tag = episode,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        MaxWidth = 560,
                    };
                    XmanaFocus.SetKey(button, "episode." + episode.Id);
                    button.Click += OnEpisodeClick;
                    EpisodesPanel.Children.Add(button);
                }
            }

            SetText(SeriesMessage, series.SelectedEpisodes.Count == 0 ? localization.Get("Details_NoEpisodes") : null);
        }

        private string SeasonLabel(int? season) =>
            season.HasValue
                ? string.Format(CultureInfo.CurrentCulture, AppServices.Localization.Get("Details_SeasonFormat"), season.Value)
                : AppServices.Localization.Get("Details_OtherEpisodes");

        private static string EpisodeLabel(VideoSummary episode, bool preferArabic)
        {
            var title = episode.GetTitle(preferArabic);
            if (episode.EpisodeNumber.HasValue)
            {
                return episode.EpisodeNumber.Value.ToString(CultureInfo.InvariantCulture) + ".  " + (title ?? string.Empty);
            }

            return title ?? string.Empty;
        }

        private void OnSeasonChanged(object sender, SelectionChangedEventArgs e)
        {
            var series = _controller?.Series;
            if (_updating || series == null || SeasonBox.SelectedIndex < 0 || SeasonBox.SelectedIndex >= series.Groups.Count)
            {
                return;
            }

            _controller.SelectSeason(series.Groups[SeasonBox.SelectedIndex].Season);
        }

        private async void OnEpisodeClick(object sender, RoutedEventArgs e)
        {
            var episode = (sender as FrameworkElement)?.Tag as VideoSummary;
            if (episode != null)
            {
                await SwitchEpisodeAsync(episode.Id);
            }
        }

        /// <summary>
        /// Updates this same Details screen to another episode of the same series in place (no navigation, no page reload):
        /// selecting an episode, or Previous/Next, must feel instant and never rebuild the whole screen.
        /// </summary>
        private async System.Threading.Tasks.Task SwitchEpisodeAsync(string episodeId)
        {
            if (episodeId == _controller.VideoId)
            {
                return;
            }

            try
            {
                await _controller.SwitchToAsync(episodeId, CancellationToken.None);
            }
            catch (Exception)
            {
                // the controller reports failures through its state
            }
        }

        private async void OnSeriesRetryClick(object sender, RoutedEventArgs e)
        {
            try
            {
                await _controller.RetrySeriesAsync(CancellationToken.None);
            }
            catch (Exception)
            {
            }
        }

        // ---- cast and rails ---------------------------------------------------------------------

        private void RenderCast(VideoDetails details)
        {
            var localization = AppServices.Localization;
            var key = details.Id + "|" + localization.CurrentLanguage.Code;
            if (key == _castKey)
            {
                return;
            }

            _castKey = key;
            CastHost.Children.Clear();
            AddStaffRail(localization.Get("Details_Cast"), details.Actors, "cast");
            AddStaffRail(localization.Get("Details_Directors"), details.Directors, "director");
            AddStaffRail(localization.Get("Details_Writers"), details.Writers, "writer");
        }

        private void AddStaffRail(string title, IReadOnlyList<StaffMember> members, string group)
        {
            if (members == null || members.Count == 0)
            {
                return;
            }

            CastHost.Children.Add(new TextBlock { Text = title, Style = (Style)Application.Current.Resources["XmanaTitleTextStyle"], FontSize = 22 });

            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4, 10, 4, 10) };
            foreach (var member in members)
            {
                var card = new StaffCard { Member = member };
                if (!string.IsNullOrEmpty(member.Id))
                {
                    XmanaFocus.SetKey(card, "staff." + group + "." + member.Id);
                    var staffId = member.Id;
                    card.Click += (s, e) => Navigator.OpenStaff(staffId);
                }

                row.Children.Add(card);
            }

            CastHost.Children.Add(new ScrollViewer
            {
                IsTabStop = false,
                HorizontalScrollMode = ScrollMode.Enabled,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
                VerticalScrollMode = ScrollMode.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                ZoomMode = ZoomMode.Disabled,
                Content = row,
            });
        }

        private static void SetRail(RailView rail, IReadOnlyList<VideoSummary> items)
        {
            rail.Visibility = items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            if (items.Count > 0)
            {
                rail.SetItems(items);
            }
        }

        // ---- focus ------------------------------------------------------------------------------

        /// <summary>The first sensible control becomes the page's primary focus target (used when returning or opening).</summary>
        private void AssignPrimaryFocus()
        {
            FrameworkElement primary = PlayButton.Visibility == Visibility.Visible ? PlayButton : null;
            foreach (var candidate in new FrameworkElement[] { NextButton, PreviousButton, SeriesButton })
            {
                if (primary == null && candidate.Visibility == Visibility.Visible && ActionsPanel.Visibility == Visibility.Visible)
                {
                    primary = candidate;
                    break;
                }
            }

            if (primary == null && SeasonBox.Visibility == Visibility.Visible && SeriesSection.Visibility == Visibility.Visible)
            {
                primary = SeasonBox;
            }

            if (primary == null && EpisodesPanel.Children.Count > 0 && SeriesSection.Visibility == Visibility.Visible)
            {
                primary = EpisodesPanel.Children[0] as FrameworkElement;
            }

            if (primary == null)
            {
                primary = FindFirstCard();
            }

            if (primary != null)
            {
                XmanaFocus.SetKey(primary, FocusKeys.ContentPrimary);
                PlaceFocusOnce(primary);
            }
        }

        private FrameworkElement FindFirstCard()
        {
            foreach (var host in CastHost.Children.OfType<ScrollViewer>())
            {
                var first = (host.Content as StackPanel)?.Children.OfType<StaffCard>().FirstOrDefault(c => c.IsTabStop);
                if (first != null)
                {
                    return first;
                }
            }

            return null;
        }

        /// <summary>Puts focus on the page when nothing is focused yet (first display), never stealing it later.</summary>
        private void PlaceFocusOnce(FrameworkElement target)
        {
            if (_focusPlaced)
            {
                return;
            }

            var focused = FocusManager.GetFocusedElement();
            if (focused == null && (target as Control)?.Focus(FocusState.Keyboard) == true)
            {
                _focusPlaced = true;
            }
        }

        private async void OnRetryClick(object sender, RoutedEventArgs e)
        {
            try
            {
                await _controller.LoadAsync(CancellationToken.None);
            }
            catch (Exception)
            {
            }
        }
    }
}
