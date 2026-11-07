using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Xmana.App.Focus;
using Xmana.App.Services;
using Xmana.Core.Api;
using Xmana.Core.Discovery;
using Xmana.Core.Focus;
using Xmana.Core.Http;

namespace Xmana.App.Pages
{
    /// <summary>
    /// Advanced search (PROMPT.md section 26): title, staff, year range, minimum rating, category, type and sort. Only the
    /// filters the user filled in are sent. The screen does not promise exact staff matching or any particular sort
    /// order, because API.md documents both as unreliable. The applied filters and results are kept per navigation entry.
    /// </summary>
    public sealed partial class AdvancedSearchPage : Page
    {
        private static readonly int[] RatingValues = { 5, 6, 7, 8, 9 };

        private AdvancedSession _session;
        private RenderScheduler _renderer;
        private bool _editing;
        private readonly List<string> _categoryIds = new List<string>();

        public AdvancedSearchPage()
        {
            InitializeComponent();
            XmanaFocus.SetKey(TitleBox, FocusKeys.ContentPrimary);
            Results.VideoInvoked += (s, video) => Navigator.OpenDetails(video);
            Results.LoadMoreRequested += async (s, e) => await Safe(() => _session.Controller.LoadMoreAsync(CancellationToken.None));
            Results.RetryRequested += async (s, e) => await Safe(() => _session.Controller.RetryAsync(CancellationToken.None));
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            var entry = (e.Parameter as PageArgs)?.Entry;
            if (entry == null)
            {
                return;
            }

            var localization = AppServices.Localization;
            TitleText.Text = localization.Get("Adv_Title");
            EditButton.Label = localization.Get("Adv_Edit");
            EditButton.Glyph = "\uE70F";
            ApplyButton.Label = localization.Get("Adv_Apply");
            ApplyButton.Glyph = "\uE721";
            ResetButton.Label = localization.Get("Adv_Reset");
            StaffNote.Text = localization.Get("Adv_StaffNote");
            Results.EmptyTitle = localization.Get("Search_EmptyTitle");
            Results.EmptyBody = localization.Get("Search_EmptyBody");

            TitleBox.Header = localization.Get("Adv_VideoTitle");
            StaffBox.Header = localization.Get("Adv_Staff");
            YearFromBox.Header = localization.Get("Adv_YearFrom");
            YearToBox.Header = localization.Get("Adv_YearTo");
            RatingBox.Header = localization.Get("Adv_Rating");
            TypeBox.Header = localization.Get("Adv_Type");
            CategoryBox.Header = localization.Get("Adv_Category");
            SortBox.Header = localization.Get("Adv_Sort");

            FillFixedChoices();

            _session = PageSessions.TryGet<AdvancedSession>(entry.Id)
                ?? PageSessions.GetOrCreate(entry.Id, () => new AdvancedSession(new AdvancedSearchController(AppServices.Discovery, AppServices.Logger)));
            _renderer = new RenderScheduler(Dispatcher, Render);
            _session.Controller.Changed += OnChanged;

            await FillCategoriesAsync();
            ShowFilters(_session.Filters);
            Render();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            _renderer?.Stop();
            if (_session != null)
            {
                _session.Controller.Changed -= OnChanged;
            }
        }

        // ---- form -------------------------------------------------------------------------------

        private void FillFixedChoices()
        {
            var localization = AppServices.Localization;

            RatingBox.Items.Clear();
            RatingBox.Items.Add(localization.Get("Adv_RatingAny"));
            foreach (var value in RatingValues)
            {
                RatingBox.Items.Add(value.ToString(CultureInfo.InvariantCulture) + "+");
            }

            TypeBox.Items.Clear();
            TypeBox.Items.Add(localization.Get("Adv_TypeAny"));
            TypeBox.Items.Add(localization.Get("Browse_KindMovies"));
            TypeBox.Items.Add(localization.Get("Browse_KindSeries"));

            SortBox.Items.Clear();
            foreach (var choice in SortChoices.Advanced)
            {
                SortBox.Items.Add(localization.Get(choice.ResourceKey));
            }
        }

        private async System.Threading.Tasks.Task FillCategoriesAsync()
        {
            var localization = AppServices.Localization;
            CategoryBox.Items.Clear();
            _categoryIds.Clear();
            CategoryBox.Items.Add(localization.Get("Adv_CategoryAny"));
            _categoryIds.Add(null);

            try
            {
                var catalog = await AppServices.Categories.GetAsync(CancellationToken.None);
                var preferArabic = localization.CurrentLanguage.IsRightToLeft;
                foreach (var category in catalog.All)
                {
                    CategoryBox.Items.Add(category.GetTitle(preferArabic));
                    _categoryIds.Add(category.Id);
                }
            }
            catch (ApiException)
            {
                // without the catalog the category filter is simply unavailable
            }

            CategoryBox.Visibility = _categoryIds.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
            CategoryBox.SelectedIndex = 0;
        }

        /// <summary>Puts previously applied filters back into the form (null shows an empty form).</summary>
        private void ShowFilters(AdvancedSearchFilters filters)
        {
            TitleBox.Text = filters?.VideoTitle ?? string.Empty;
            StaffBox.Text = filters?.StaffTitle ?? string.Empty;
            YearFromBox.Text = filters?.YearFrom?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            YearToBox.Text = filters?.YearTo?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

            var rating = filters?.Star == null ? -1 : Array.IndexOf(RatingValues, filters.Star.Value);
            RatingBox.SelectedIndex = rating + 1;

            TypeBox.SelectedIndex = filters?.Type == null ? 0 : (filters.Type == SearchType.Movies ? 1 : 2);

            var categoryId = filters?.CategoryIds?.FirstOrDefault();
            var categoryIndex = categoryId == null ? 0 : Math.Max(0, _categoryIds.IndexOf(categoryId));
            CategoryBox.SelectedIndex = categoryIndex;

            var sortIndex = SortChoices.Advanced.ToList().FindIndex(c => c.Value == filters?.Sort);
            SortBox.SelectedIndex = Math.Max(0, sortIndex);
            ValidationText.Visibility = Visibility.Collapsed;
        }

        /// <summary>Reads the form. Returns null (with a message shown) when the years are incomplete or invalid.</summary>
        private AdvancedSearchFilters ReadFilters()
        {
            var localization = AppServices.Localization;
            var filters = new AdvancedSearchFilters
            {
                VideoTitle = Clean(TitleBox.Text),
                StaffTitle = Clean(StaffBox.Text),
            };

            var from = YearFromBox.Text.Trim();
            var to = YearToBox.Text.Trim();
            if (from.Length > 0 || to.Length > 0)
            {
                int yearFrom, yearTo;
                if (!int.TryParse(from, NumberStyles.None, CultureInfo.InvariantCulture, out yearFrom)
                    || !int.TryParse(to, NumberStyles.None, CultureInfo.InvariantCulture, out yearTo)
                    || yearFrom > yearTo)
                {
                    ShowMessage(localization.Get("Adv_InvalidYears"));
                    return null;
                }

                filters.YearFrom = yearFrom;
                filters.YearTo = yearTo;
            }

            if (RatingBox.SelectedIndex > 0)
            {
                filters.Star = RatingValues[RatingBox.SelectedIndex - 1];
            }

            if (TypeBox.SelectedIndex > 0)
            {
                filters.Type = TypeBox.SelectedIndex == 1 ? SearchType.Movies : SearchType.Series;
            }

            if (CategoryBox.SelectedIndex > 0 && CategoryBox.SelectedIndex < _categoryIds.Count)
            {
                filters.CategoryIds = new[] { _categoryIds[CategoryBox.SelectedIndex] };
            }

            if (SortBox.SelectedIndex >= 0)
            {
                filters.Sort = SortChoices.Advanced[SortBox.SelectedIndex].Value;
            }

            return filters;
        }

        private static string Clean(string text)
        {
            var trimmed = text?.Trim();
            return string.IsNullOrEmpty(trimmed) ? null : trimmed;
        }

        private void ShowMessage(string text)
        {
            ValidationText.Text = text;
            ValidationText.Visibility = Visibility.Visible;
        }

        private async void OnApplyClick(object sender, RoutedEventArgs e)
        {
            ValidationText.Visibility = Visibility.Collapsed;
            var filters = ReadFilters();
            if (filters == null)
            {
                return;
            }

            if (!filters.HasCriteria)
            {
                ShowMessage(AppServices.Localization.Get("Adv_NeedCriteria"));
                return;
            }

            _session.Filters = filters;
            _editing = false;
            await Safe(async () => await _session.Controller.ApplyAsync(filters, CancellationToken.None));
            Render();
        }

        private void OnResetClick(object sender, RoutedEventArgs e)
        {
            _session.Filters = null;
            _session.Controller.Reset();
            _editing = false;
            ShowFilters(null);
            Render();
        }

        private void OnEditClick(object sender, RoutedEventArgs e)
        {
            _editing = true;
            ShowFilters(_session.Filters);
            Render();
            TitleBox.Focus(FocusState.Keyboard);
        }

        // ---- rendering --------------------------------------------------------------------------

        private void OnChanged(object sender, EventArgs e) => _renderer?.Request();

        private void Render()
        {
            var snapshot = _session.Controller.Snapshot;
            var showForm = snapshot.State == ResultsState.Idle || _editing;
            FormScroller.Visibility = showForm ? Visibility.Visible : Visibility.Collapsed;
            Results.Visibility = showForm ? Visibility.Collapsed : Visibility.Visible;
            EditButton.Visibility = showForm ? Visibility.Collapsed : Visibility.Visible;
            Results.Render(snapshot, "advanced");
        }

        private static async System.Threading.Tasks.Task Safe(Func<System.Threading.Tasks.Task> action)
        {
            try
            {
                await action();
            }
            catch (Exception)
            {
                // failures are reflected in the controller state
            }
        }

        private sealed class AdvancedSession
        {
            public AdvancedSession(AdvancedSearchController controller)
            {
                Controller = controller;
            }

            public AdvancedSearchController Controller { get; }

            public AdvancedSearchFilters Filters { get; set; }
        }
    }
}
