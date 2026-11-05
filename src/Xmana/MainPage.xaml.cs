using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.System;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Xmana.Models;
using Xmana.Services;

namespace Xmana
{
    public sealed partial class MainPage : Page
    {
        public static MainPage Current { get; private set; }

        private readonly CinemanaApiClient api = new CinemanaApiClient();
        private readonly LocalStateStore state = new LocalStateStore();
        private PlaybackService playback;
        private DispatcherTimer playerTimer;
        private Slider playerSeekSlider;
        private TextBlock playerTimeText;
        private Button playerPlayPauseButton;
        private ComboBox playerQualitySelector;
        private JToken activePlaybackFiles;
        private bool updatingSeek;
        private VideoItem playingItem;
        private DateTime lastProgressWriteUtc = DateTime.MinValue;
        private bool isArabic;
        private string currentSection = "Home";

        public MainPage()
        {
            Current = this;
            InitializeComponent();
            isArabic = state.Language == "ar";
            ApplyDirection();
            Loaded += async (s, e) => await ShowHomeAsync();
        }

        private string L(string en, string ar) => isArabic ? ar : en;

        private void ApplyDirection()
        {
            FlowDirection = isArabic ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            foreach (var child in NavigationTabs.Children.OfType<Button>())
            {
                var key = child.Tag as string;
                child.Content = isArabic
                    ? (key == "Home" ? "الرئيسية" : key == "Movies" ? "الأفلام" :
                       key == "Series" ? "المسلسلات" : key == "Anime" ? "الأنمي" :
                       key == "Search" ? "بحث" : key == "Categories" ? "التصنيفات" : "قائمتي")
                    : key;
            }
            SettingsButton.Content = L("Settings  ⚙", "الإعدادات  ⚙");
        }

        private async void Navigation_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var destination = button?.Tag as string;
            if (String.IsNullOrEmpty(destination)) return;
            await NavigateAsync(destination);
        }

        private async Task NavigateAsync(string destination)
        {
            currentSection = destination;
            MainScroll.ChangeView(null, 0, null);
            switch (destination)
            {
                case "Home": await ShowHomeAsync(); break;
                case "Movies": await ShowListingAsync(L("Movies", "الأفلام"), () => api.GetLatestMoviesAsync("0", 0)); break;
                case "Series": await ShowListingAsync(L("Series", "المسلسلات"), () => api.GetLatestSeriesAsync("0", 0), true); break;
                case "Anime": await ShowAnimeAsync(); break;
                case "Categories": await ShowCategoriesAsync(); break;
                case "Search": ShowSearch(); break;
                case "Library": ShowLibrary(); break;
                case "Settings": ShowSettings(); break;
            }
        }

        private void Navigation_GotFocus(object sender, RoutedEventArgs e)
        {
            foreach (var tab in NavigationTabs.Children.OfType<Button>())
            {
                tab.Background = new SolidColorBrush(Color.FromArgb(255, 27, 35, 48));
                tab.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 43, 53, 68));
                tab.BorderThickness = new Thickness(1);
            }
            SettingsButton.Background = new SolidColorBrush(Color.FromArgb(255, 27, 35, 48));
            SettingsButton.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 43, 53, 68));
            SettingsButton.BorderThickness = new Thickness(1);

            var button = sender as Button;
            if (button != null)
            {
                button.Background = new SolidColorBrush(Color.FromArgb(255, 35, 65, 70));
                button.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 86, 214, 194));
                button.BorderThickness = new Thickness(2);
            }
        }

        private void ShowBusy(string message)
        {
            BusyText.Text = message;
            BusyOverlay.Visibility = Visibility.Visible;
        }

        private void HideBusy() => BusyOverlay.Visibility = Visibility.Collapsed;
        private void ResetContent()
        {
            PageContent.Children.Clear();
            MainScroll.Visibility = Visibility.Visible;
        }

        private async Task ShowHomeAsync()
        {
            ResetContent();
            ShowBusy(L("Finding something worth watching…", "جارٍ تحميل المحتوى…"));
            var bannerTask = SafeListAsync(() => api.GetBannersAsync("0"));
            var featuredTask = SafeListAsync(() => api.GetFeaturedMoviesAsync("0"));
            var moviesTask = SafeListAsync(() => api.GetLatestMoviesAsync("0", 0));
            var seriesTask = SafeListAsync(() => api.GetLatestSeriesAsync("0", 0));
            var newTask = SafeListAsync(() => api.GetNewlyVideosItemsAsync("0", 12));
            var latestTask = SafeListAsync(() => api.GetLatestVideosItemsAsync("0", 12));
            var homeData = await Task.WhenAll(bannerTask, featuredTask, moviesTask, seriesTask, newTask, latestTask);
            var banners = homeData[0];
            var featured = homeData[1];
            var latestMovies = homeData[2];
            var latestSeries = homeData[3];
            var newItems = homeData[4];
            var latestItems = homeData[5];
            foreach (var series in latestSeries) series.IsSeries = true;
            var continueItems = await LoadContinueWatchingAsync();
            HideBusy();

            var hero = banners.FirstOrDefault() ?? featured.FirstOrDefault() ??
                latestMovies.FirstOrDefault() ?? latestSeries.FirstOrDefault();
            if (hero != null) AddHero(hero);
            AddRail(L("Continue watching", "متابعة المشاهدة"), continueItems);
            AddRail(L("Featured movies", "أفلام مختارة"), featured);
            AddRail(L("Newly added", "أضيف حديثاً"), newItems);
            AddRail(L("Latest updates", "آخر الإضافات"), latestItems);
            AddRail(L("Latest movies", "أحدث الأفلام"), latestMovies);
            AddRail(L("Latest series", "أحدث المسلسلات"), latestSeries);

            await AddHomeGroupRailsAsync();
            var popular = await SafeListAsync(() => api.GetPopularFilmsAsync("M", "0", 0));
            AddRail(L("Popular this month", "الأكثر شعبية هذا الشهر"), popular);

            if (hero == null && latestMovies.Count == 0 && latestSeries.Count == 0)
                AddMessage(L("The catalog is unavailable right now. Check your connection and try again.",
                    "الكتالوج غير متاح حالياً. تحقق من الاتصال ثم أعد المحاولة."));
        }

        private async Task AddHomeGroupRailsAsync()
        {
            var response = await SafeTokenAsync(() => api.GetVideoGroupsAsync(isArabic ? "ar" : "en", "0"));
            var groups = CinemanaApiClient.AsList(response).Take(5).ToList();
            foreach (var group in groups)
            {
                var title = ReadTokenString(group, "title");
                var groupId = ReadTokenString(group, "groupsID");
                var items = CinemanaApiClient.AsList(group["content"])
                    .Select(VideoItem.FromToken)
                    .Where(item => item != null && !String.IsNullOrWhiteSpace(item.Id))
                    .ToList();

                if (items.Count == 0 && !String.IsNullOrWhiteSpace(groupId))
                    items = await SafeListAsync(() => api.GetGroupPageAsync(groupId, "0", 0));

                if (!String.IsNullOrWhiteSpace(title))
                    AddRail(title, items);
            }
        }

        private async Task<List<VideoItem>> LoadContinueWatchingAsync()
        {
            var saved = state.PlaybackItems.Where(p => p != null && !p.Completed && p.PositionSeconds > 15)
                .OrderByDescending(p => p.LastPlayedUtc).Take(8).ToList();
            var tasks = saved.Select(async progress =>
            {
                try
                {
                    var token = await api.GetAllVideoInfoAsync(progress.VideoId);
                    var item = VideoItem.FromToken(token);
                    if (item == null) return null;
                    item.ResumeSeconds = progress.PositionSeconds;
                    item.PlaybackProgressPercent = progress.DurationSeconds > 0
                        ? Math.Max(0, Math.Min(100, progress.PositionSeconds / progress.DurationSeconds * 100))
                        : 0;
                    return item;
                }
                catch { return null; }
            });
            var results = await Task.WhenAll(tasks);
            return results.Where(item => item != null).ToList();
        }
        private async Task<List<VideoItem>> SafeListAsync(Func<Task<JToken>> request)
        {
            try
            {
                return CinemanaApiClient.AsList(await request())
                    .Select(VideoItem.FromToken)
                    .Where(v => v != null && !String.IsNullOrWhiteSpace(v.Id))
                    .ToList();
            }
            catch
            {
                return new List<VideoItem>();
            }
        }

        private void AddHero(VideoItem item)
        {
            var hero = new Grid
            {
                Height = 420,
                Margin = new Thickness(0, 0, 0, 4),
                Background = new SolidColorBrush(Color.FromArgb(255, 23, 32, 44))
            };
            hero.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.3, GridUnitType.Star) });
            hero.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            if (IsWebUri(item.Backdrop ?? item.Poster))
            {
                var image = new Image
                {
                    Stretch = Stretch.UniformToFill,
                    Source = new BitmapImage(new Uri(item.Backdrop ?? item.Poster))
                };
                Grid.SetColumn(image, 0);
                Grid.SetColumnSpan(image, 2);
                hero.Children.Add(image);
            }

            var shade = new Border
            {
                Background = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(1, 0),
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop { Color = Color.FromArgb(245, 8, 12, 18), Offset = 0 },
                        new GradientStop { Color = Color.FromArgb(220, 8, 12, 18), Offset = 0.55 },
                        new GradientStop { Color = Color.FromArgb(20, 8, 12, 18), Offset = 1 }
                    }
                }
            };
            Grid.SetColumnSpan(shade, 2);
            hero.Children.Add(shade);

            var details = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(48, 24, 20, 24),
                Spacing = 16,
                MaxWidth = 640
            };
            details.Children.Add(new TextBlock
            {
                Text = L("X M A N A   P I C K", "اختيار XMANA"),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 86, 214, 194)),
                FontSize = 14,
                CharacterSpacing = 180,
                FontWeight = Windows.UI.Text.FontWeights.Bold
            });
            details.Children.Add(new TextBlock
            {
                Text = item.GetTitle(isArabic),
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 42,
                FontWeight = Windows.UI.Text.FontWeights.Bold,
                TextWrapping = TextWrapping.Wrap,
                MaxLines = 2
            });

            var meta = String.Join("   •   ", new[] {
                item.Year, !String.IsNullOrWhiteSpace(item.Rating) ? "★ " + item.Rating : null
            }.Where(x => !String.IsNullOrWhiteSpace(x)));
            if (!String.IsNullOrEmpty(meta))
                details.Children.Add(new TextBlock
                {
                    Text = meta,
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 190, 202, 215)),
                    FontSize = 18
                });

            var description = item.GetDescription(isArabic);
            if (!String.IsNullOrWhiteSpace(description))
                details.Children.Add(new TextBlock
                {
                    Text = description,
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 208, 216, 225)),
                    FontSize = 18,
                    TextWrapping = TextWrapping.Wrap,
                    MaxLines = 3
                });

            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 14,
                Margin = new Thickness(0, 12, 0, 0)
            };
            var more = new Button
            {
                Content = L("Explore", "استكشف"),
                Style = (Style)Application.Current.Resources["XmanaButton"],
                Tag = item
            };
            more.Click += async (s, e) => await ShowDetailsAsync((VideoItem)((Button)s).Tag);
            actions.Children.Add(more);
            details.Children.Add(actions);
            Grid.SetColumn(details, 0);
            Grid.SetColumnSpan(details, 2);
            hero.Children.Add(details);
            PageContent.Children.Add(hero);
        }

        private void AddRail(string title, IList<VideoItem> items)
        {
            if (items == null || items.Count == 0) return;
            var section = new StackPanel { Spacing = 14 };
            section.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 25,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Colors.White),
                Margin = new Thickness(2, 0, 0, 0)
            });
            var rail = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                HorizontalScrollMode = ScrollMode.Enabled,
                VerticalScrollMode = ScrollMode.Disabled,
                ZoomMode = ZoomMode.Disabled,
                Height = 290
            };
            var stack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 18 };
            foreach (var item in items) stack.Children.Add(CreateCard(item));
            rail.Content = stack;
            section.Children.Add(rail);
            PageContent.Children.Add(section);
        }

        private Button CreateCard(VideoItem item)
        {
            var button = new Button
            {
                Width = 174,
                Height = 276,
                Padding = new Thickness(0),
                Background = new SolidColorBrush(Color.FromArgb(255, 22, 29, 40)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 43, 53, 68)),
                BorderThickness = new Thickness(1),
                UseSystemFocusVisuals = false,
                Tag = item
            };
            var stack = new StackPanel { Spacing = 7 };
            var poster = new Grid
            {
                Height = 224,
                Background = new SolidColorBrush(Color.FromArgb(255, 32, 41, 54))
            };
            if (IsWebUri(item.Poster))
                poster.Children.Add(new Image
                {
                    Source = new BitmapImage(new Uri(item.Poster)),
                    Stretch = Stretch.UniformToFill
                });
            else
                poster.Children.Add(new TextBlock
                {
                    Text = "XM",
                    FontSize = 34,
                    FontWeight = Windows.UI.Text.FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 86, 214, 194)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                });
            if (item.PlaybackProgressPercent > 0)
            {
                var progressTrack = new Border
                {
                    Height = 5,
                    Background = new SolidColorBrush(Color.FromArgb(170, 65, 78, 91)),
                    VerticalAlignment = VerticalAlignment.Bottom
                };
                var progressFill = new Border
                {
                    Height = 5,
                    Width = 154 * item.PlaybackProgressPercent / 100.0,
                    Background = new SolidColorBrush(Color.FromArgb(255, 86, 214, 194)),
                    HorizontalAlignment = HorizontalAlignment.Left
                };
                progressTrack.Child = progressFill;
                poster.Children.Add(progressTrack);
            }
            stack.Children.Add(poster);
            stack.Children.Add(new TextBlock
            {
                Text = item.GetTitle(isArabic),
                FontSize = 15,
                Foreground = new SolidColorBrush(Colors.White),
                TextWrapping = TextWrapping.Wrap,
                MaxLines = 2,
                Margin = new Thickness(10, 1, 10, 0)
            });
            button.Content = stack;
            button.GotFocus += (s, e) =>
            {
                button.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 86, 214, 194));
                button.BorderThickness = new Thickness(3);
                button.RenderTransform = new ScaleTransform { ScaleX = 1.035, ScaleY = 1.035 };
            };
            button.LostFocus += (s, e) =>
            {
                button.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 43, 53, 68));
                button.BorderThickness = new Thickness(1);
                button.RenderTransform = null;
            };
            button.Click += async (s, e) => await ShowDetailsAsync((VideoItem)((Button)s).Tag);
            return button;
        }

        private async Task ShowListingAsync(string heading, Func<Task<JToken>> request, bool isSeries = false)
        {
            ResetContent();
            ShowBusy(L("Loading catalog…", "جارٍ تحميل القائمة…"));
            var items = await SafeListAsync(request);
            if (isSeries) foreach (var item in items) item.IsSeries = true;
            HideBusy();
            PageContent.Children.Add(SectionTitle(heading));
            if (items.Count == 0)
            {
                AddMessage(L("No items were returned by the service.", "لم تُرجع الخدمة عناصر في الوقت الحالي."));
                return;
            }
            AddRail(heading, items);
        }

        private async Task ShowAnimeAsync()
        {
            ResetContent();
            ShowBusy(L("Loading anime…", "جارٍ تحميل الأنمي…"));
            var animeMovies = await SafeListAsync(() =>
                api.GetVideosByCategoryAndLanguageAsync("57", "22", "1", "desc", "0", 0));
            var animeSeries = await SafeListAsync(() =>
                api.GetVideosByCategoryAndLanguageAsync("57", "22", "2", "desc", "0", 0));
            foreach (var series in animeSeries) series.IsSeries = true;
            var items = animeMovies.Concat(animeSeries).GroupBy(v => v.Id).Select(g => g.First()).ToList();
            HideBusy();
            PageContent.Children.Add(SectionTitle(L("Anime", "الأنمي")));
            if (items.Count == 0)
                AddMessage(L("No anime items were returned for this category.",
                    "لم تُرجع فئة الأنمي أي عناصر حالياً."));
            else
                AddRail(L("Animation & anime", "الأنمي والرسوم المتحركة"), items);
        }

        private async Task<JToken> SafeTokenAsync(Func<Task<JToken>> request)
        {
            try { return await request(); }
            catch { return null; }
        }

        private async Task ShowCategoriesAsync()
        {
            ResetContent();
            ShowBusy(L("Loading categories…", "جارٍ تحميل التصنيفات…"));
            var mainTask = SafeTokenAsync(() => api.GetMainCategoriesAsync(isArabic ? "ar" : "en"));
            var subTask = SafeTokenAsync(() => api.GetSubCategoriesAsync(isArabic ? "ar" : "en"));
            var frequencyTask = SafeTokenAsync(() => api.GetCategoryLanguageFrequencyAsync());
            var data = await Task.WhenAll(mainTask, subTask, frequencyTask);
            HideBusy();

            PageContent.Children.Add(SectionTitle(L("Browse by category", "تصفح حسب التصنيف")));
            var categories = new List<CategoryChoice>();
            AddCategoryChoices(categories, CinemanaApiClient.AsList(data[0]));
            AddCategoryChoices(categories, CinemanaApiClient.AsList(data[1]));
            categories = categories
                .Where(x => !String.IsNullOrWhiteSpace(x.Id) && !String.IsNullOrWhiteSpace(x.Title))
                .GroupBy(x => x.Id)
                .Select(group => group.First())
                .OrderBy(x => x.Title)
                .ToList();

            if (categories.Count == 0)
            {
                AddMessage(L("Category data is unavailable right now.", "بيانات التصنيفات غير متاحة حالياً."));
                return;
            }

            var tiles = new WrapGrid
            {
                ItemWidth = 310,
                ItemHeight = 92,
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            foreach (var category in categories)
            {
                var button = new Button
                {
                    Content = category.Title,
                    Tag = category,
                    Style = (Style)Application.Current.Resources["XmanaButton"],
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Margin = new Thickness(6),
                    Height = 78
                };
                button.Click += async (s, e) =>
                    await ShowCategoryAsync((CategoryChoice)((Button)s).Tag, data[2]);
                tiles.Children.Add(button);
            }
            PageContent.Children.Add(tiles);
        }

        private void AddCategoryChoices(IList<CategoryChoice> target, IEnumerable<JToken> source)
        {
            foreach (var entry in source)
            {
                var id = ReadTokenString(entry, "nb", "id");
                var title = ReadTokenString(entry, isArabic ? "ar_title" : "en_title",
                    isArabic ? "en_title" : "ar_title");
                if (!String.IsNullOrWhiteSpace(id) && !String.IsNullOrWhiteSpace(title))
                    target.Add(new CategoryChoice { Id = id, Title = title });
            }
        }

        private async Task ShowCategoryAsync(CategoryChoice category, JToken frequencyResponse)
        {
            ResetContent();
            PageContent.Children.Add(SectionTitle(category.Title));

            var frequencyEntry = CinemanaApiClient.AsList(frequencyResponse)
                .FirstOrDefault(entry => ReadTokenString(entry, "nb") == category.Id);
            var languages = frequencyEntry?["langArray"] as JArray;
            ComboBox languageSelector = null;
            if (languages != null && languages.Count > 0)
            {
                var filterPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 14,
                    Margin = new Thickness(0, 0, 0, 10)
                };
                languageSelector = new ComboBox
                {
                    MinWidth = 320,
                    FontSize = 19,
                    PlaceholderText = L("All languages", "كل اللغات")
                };
                languageSelector.Items.Add(new ComboBoxItem
                {
                    Content = L("All languages", "كل اللغات"),
                    Tag = String.Empty,
                    IsSelected = true
                });
                foreach (var language in languages)
                {
                    var languageId = ReadTokenString(language, "langNb");
                    var languageTitle = ReadTokenString(language,
                        isArabic ? "lang_ar_title" : "lang_en_title",
                        isArabic ? "lang_en_title" : "lang_ar_title");
                    if (String.IsNullOrWhiteSpace(languageId) || String.IsNullOrWhiteSpace(languageTitle)) continue;
                    languageSelector.Items.Add(new ComboBoxItem { Content = languageTitle, Tag = languageId });
                }
                var apply = new Button
                {
                    Content = L("Apply language", "تطبيق اللغة"),
                    Style = (Style)Application.Current.Resources["XmanaButton"]
                };
                apply.Click += async (s, e) =>
                {
                    var selected = languageSelector.SelectedItem as ComboBoxItem;
                    await LoadCategoryContentsAsync(category, selected?.Tag as string);
                };
                filterPanel.Children.Add(languageSelector);
                filterPanel.Children.Add(apply);
                PageContent.Children.Add(filterPanel);
            }

            await LoadCategoryContentsAsync(category, null);
        }

        private async Task LoadCategoryContentsAsync(CategoryChoice category, string languageId)
        {
            ShowBusy(L("Loading category…", "جارٍ تحميل التصنيف…"));
            var movieTask = SafeListAsync(() => String.IsNullOrWhiteSpace(languageId)
                ? api.GetVideosByCategoryAsync(category.Id, "desc", "1", 0, "0")
                : api.GetVideosByCategoryAndLanguageAsync(category.Id, languageId, "1", "desc", "0", 0));
            var seriesTask = SafeListAsync(() => String.IsNullOrWhiteSpace(languageId)
                ? api.GetVideosByCategoryAsync(category.Id, "desc", "2", 0, "0")
                : api.GetVideosByCategoryAndLanguageAsync(category.Id, languageId, "2", "desc", "0", 0));
            var data = await Task.WhenAll(movieTask, seriesTask);
            var movies = data[0];
            var series = data[1];
            foreach (var item in series) item.IsSeries = true;
            HideBusy();

            // Preserve the heading and language filter; remove only the previous result rails.
            while (PageContent.Children.Count > 2) PageContent.Children.RemoveAt(PageContent.Children.Count - 1);
            AddRail(L("Movies", "الأفلام"), movies);
            AddRail(L("Series", "المسلسلات"), series);
            if (movies.Count == 0 && series.Count == 0)
                AddMessage(L("No titles were returned for this category and language.",
                    "لم تُرجع هذه الفئة واللغة عناوين."));
        }

        private sealed class CategoryChoice
        {
            public string Id { get; set; }
            public string Title { get; set; }
        }


        private static string ReadTokenString(JToken token, params string[] names)
        {
            if (token == null) return String.Empty;
            foreach (var name in names)
            {
                var value = token[name];
                if (value == null || value.Type == JTokenType.Null ||
                    value.Type == JTokenType.Object || value.Type == JTokenType.Array) continue;
                var result = value.ToString();
                if (!String.IsNullOrWhiteSpace(result)) return result;
            }
            return String.Empty;
        }

        private void ShowSearch()
        {
            ResetContent();
            PageContent.Children.Add(SectionTitle(L("Find your next story", "ابحث عن عملك القادم")));
            var searchRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 14,
                MaxWidth = 1100
            };
            var query = new TextBox
            {
                Width = 820,
                PlaceholderText = L("Search movies, series and anime", "ابحث عن الأفلام والمسلسلات والأنمي"),
                Style = (Style)Application.Current.Resources["XmanaTextBox"]
            };
            var button = new Button
            {
                Content = L("Search", "بحث"),
                Style = (Style)Application.Current.Resources["XmanaButton"]
            };
            button.Click += async (s, e) => await RunSearchAsync(query.Text);
            query.KeyDown += async (s, e) =>
            {
                if (e.Key == VirtualKey.Enter) await RunSearchAsync(query.Text);
            };
            searchRow.Children.Add(query);
            searchRow.Children.Add(button);
            PageContent.Children.Add(searchRow);
            AddMessage(L("Search uses Cinemana's Base64URL title-search route. Enter a title to begin.",
                "يستخدم البحث مسار العناوين الموثق لدى Cinemana. أدخل عنواناً للبدء."));
            var history = state.SearchHistory;
            if (history.Count > 0)
            {
                var historyPanel = new StackPanel { Spacing = 10 };
                historyPanel.Children.Add(new TextBlock
                {
                    Text = L("Recent searches", "عمليات البحث الأخيرة"),
                    FontSize = 19,
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 175, 190, 207))
                });
                var historyRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                foreach (var term in history.Take(8))
                {
                    var recent = new Button
                    {
                        Content = term,
                        Tag = term,
                        Style = (Style)Application.Current.Resources["XmanaButton"]
                    };
                    recent.Click += async (s, e) => await RunSearchAsync((string)((Button)s).Tag);
                    historyRow.Children.Add(recent);
                }
                var clear = new Button
                {
                    Content = L("Clear", "مسح"),
                    Style = (Style)Application.Current.Resources["XmanaButton"]
                };
                clear.Click += (s, e) => { state.ClearSearchHistory(); NavigateAsync("Search"); };
                historyRow.Children.Add(clear);
                historyPanel.Children.Add(historyRow);
                PageContent.Children.Add(historyPanel);
            }
            var advancedToggle = new Button
            {
                Content = L("Advanced search  ▾", "بحث متقدم  ▾"),
                Style = (Style)Application.Current.Resources["XmanaButton"],
                HorizontalAlignment = HorizontalAlignment.Left
            };
            var advancedPanel = new StackPanel
            {
                Spacing = 12,
                MaxWidth = 1100,
                Visibility = Visibility.Collapsed,
                Margin = new Thickness(0, 4, 0, 12)
            };
            advancedPanel.Children.Add(new TextBlock
            {
                Text = L("Optional filters · Staff matching may be incomplete on the service",
                    "مرشحات اختيارية · البحث باسم الفنان قد لا يكون دقيقاً في الخدمة"),
                FontSize = 16,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 158, 176, 195))
            });

            var staff = new TextBox
            {
                PlaceholderText = L("Staff name", "اسم الفنان"),
                Style = (Style)Application.Current.Resources["XmanaTextBox"],
                Width = 520
            };
            var year = new TextBox
            {
                PlaceholderText = L("Year or range, e.g. 2000,2005", "السنة أو النطاق مثل 2000,2005"),
                Style = (Style)Application.Current.Resources["XmanaTextBox"],
                Width = 520
            };
            var rating = new TextBox
            {
                PlaceholderText = L("Rating filter", "مرشح التقييم"),
                Style = (Style)Application.Current.Resources["XmanaTextBox"],
                Width = 520
            };
            var category = new TextBox
            {
                PlaceholderText = L("Category ID(s), e.g. 57,60", "معرف التصنيف أو التصنيفات مثل 57,60"),
                Style = (Style)Application.Current.Resources["XmanaTextBox"],
                Width = 520
            };
            var type = new ComboBox { MinWidth = 250, FontSize = 19 };
            type.Items.Add(new ComboBoxItem { Content = L("All types", "كل الأنواع"), Tag = String.Empty, IsSelected = true });
            type.Items.Add(new ComboBoxItem { Content = L("Movies", "أفلام"), Tag = "movies" });
            type.Items.Add(new ComboBoxItem { Content = L("Series", "مسلسلات"), Tag = "series" });

            var filterRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
            filterRow.Children.Add(staff);
            filterRow.Children.Add(year);
            advancedPanel.Children.Add(filterRow);
            var secondFilterRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
            secondFilterRow.Children.Add(rating);
            secondFilterRow.Children.Add(category);
            advancedPanel.Children.Add(secondFilterRow);
            advancedPanel.Children.Add(type);

            var applyAdvanced = new Button
            {
                Content = L("Apply filters", "تطبيق المرشحات"),
                Style = (Style)Application.Current.Resources["XmanaButton"],
                HorizontalAlignment = HorizontalAlignment.Left
            };
            applyAdvanced.Click += async (s, e) =>
            {
                var selected = type.SelectedItem as ComboBoxItem;
                await RunAdvancedSearchAsync(query.Text, staff.Text, year.Text, rating.Text,
                    category.Text, selected?.Tag as string);
            };
            advancedPanel.Children.Add(applyAdvanced);
            advancedToggle.Click += (s, e) =>
            {
                advancedPanel.Visibility = advancedPanel.Visibility == Visibility.Visible
                    ? Visibility.Collapsed : Visibility.Visible;
            };
            PageContent.Children.Add(advancedToggle);
            PageContent.Children.Add(advancedPanel);
            query.Focus(FocusState.Programmatic);
        }

        private async Task RunAdvancedSearchAsync(string title, string staff, string year,
            string rating, string category, string type)
        {
            var filters = new Dictionary<string, string>();
            if (!String.IsNullOrWhiteSpace(title)) filters["videoTitle"] = title.Trim();
            if (!String.IsNullOrWhiteSpace(staff)) filters["staffTitle"] = staff.Trim();
            if (!String.IsNullOrWhiteSpace(year)) filters["year"] = year.Trim();
            if (!String.IsNullOrWhiteSpace(rating)) filters["star"] = rating.Trim();
            if (!String.IsNullOrWhiteSpace(category)) filters["category_id"] = category.Trim();
            if (!String.IsNullOrWhiteSpace(type)) filters["type"] = type;

            if (filters.Count == 0)
            {
                AddMessage(L("Enter at least one advanced filter.", "أدخل مرشحاً متقدماً واحداً على الأقل."));
                return;
            }

            if (!String.IsNullOrWhiteSpace(title)) state.AddSearch(title.Trim());
            ShowBusy(L("Applying filters…", "جارٍ تطبيق المرشحات…"));
            var items = await SafeListAsync(() => api.AdvancedSearchAsync(filters, "0", 0));
            HideBusy();
            while (PageContent.Children.Count > 2) PageContent.Children.RemoveAt(PageContent.Children.Count - 1);
            AddRail(L("Advanced search results", "نتائج البحث المتقدم"), items);
            if (items.Count == 0)
                AddMessage(L("No titles matched these filters.", "لا توجد عناوين تطابق هذه المرشحات."));
        }

        private async Task RunSearchAsync(string text)
        {
            if (String.IsNullOrWhiteSpace(text)) return;
            var query = text.Trim();
            state.AddSearch(query);
            ShowBusy(L("Searching…", "جارٍ البحث…"));
            var items = await SafeListAsync(() => api.SearchTitleAsync(query, 0, "0"));
            HideBusy();
            while (PageContent.Children.Count > 2) PageContent.Children.RemoveAt(PageContent.Children.Count - 1);
            AddRail(L("Search results", "نتائج البحث"), items);
            if (items.Count == 0) AddMessage(L("No matching titles were found.", "لم يتم العثور على عناوين مطابقة."));
        }

        private void ShowLibrary()
        {
            ResetContent();
            PageContent.Children.Add(SectionTitle(L("Your collection", "مكتبتك")));
            var favorites = state.Favorites.ToList();
            var watchlist = state.Watchlist.ToList();
            var recent = state.RecentlyViewed.ToList();
            var history = state.WatchHistory.Select(entry => entry.VideoId)
                .Where(id => !String.IsNullOrWhiteSpace(id)).Distinct().Take(30).ToList();
            if (favorites.Count == 0 && watchlist.Count == 0 && recent.Count == 0 && history.Count == 0)
            {
                AddMessage(L("Your collection is empty. Save a title from its details page.",
                    "مكتبتك فارغة. احفظ أي عنوان من صفحة التفاصيل."));
                return;
            }
            ShowBusy(L("Restoring your collection…", "جارٍ استعادة مكتبتك…"));
            _ = LoadLibraryAsync(favorites, watchlist, recent, history);
        }

        private async Task LoadLibraryAsync(IList<string> favorites, IList<string> watchlist, IList<string> recent, IList<string> history)
        {
            var cache = new Dictionary<string, VideoItem>(StringComparer.OrdinalIgnoreCase);
            async Task<List<VideoItem>> Resolve(IList<string> ids)
            {
                var resolved = new List<VideoItem>();
                foreach (var id in ids)
                {
                    if (cache.ContainsKey(id))
                    {
                        resolved.Add(cache[id]);
                        continue;
                    }
                    try
                    {
                        var item = VideoItem.FromToken(await api.GetAllVideoInfoAsync(id));
                        if (item != null)
                        {
                            cache[id] = item;
                            resolved.Add(item);
                        }
                    }
                    catch { }
                }
                return resolved;
            }

            var favoriteItems = await Resolve(favorites);
            var watchlistItems = await Resolve(watchlist);
            var recentItems = await Resolve(recent);
            var historyItems = await Resolve(history);
            HideBusy();
            AddRail(L("Continue watching", "متابعة المشاهدة"), await LoadContinueWatchingAsync());
            AddRail(L("Favorites", "المفضلة"), favoriteItems);
            AddRail(L("Watchlist", "قائمة المشاهدة"), watchlistItems);
            AddRail(L("Watch history", "سجل المشاهدة"), historyItems);
            AddRail(L("Recently viewed", "شوهدت مؤخراً"), recentItems);
            if (favoriteItems.Count + watchlistItems.Count + recentItems.Count + historyItems.Count == 0)
                AddMessage(L("Saved titles could not be loaded from the service.",
                    "تعذر تحميل العناوين المحفوظة من الخدمة."));
        }

        private void ShowSettings()
        {
            ResetContent();
            PageContent.Children.Add(SectionTitle(L("Preferences", "التفضيلات")));
            var language = new Button
            {
                Content = L("Language: English  ·  Switch to Arabic", "اللغة: العربية  ·  التبديل إلى الإنجليزية"),
                Style = (Style)Application.Current.Resources["XmanaButton"],
                HorizontalAlignment = HorizontalAlignment.Left
            };
            language.Click += async (s, e) =>
            {
                state.Language = isArabic ? "en" : "ar";
                isArabic = state.Language == "ar";
                ApplyDirection();
                await NavigateAsync("Settings");
            };
            PageContent.Children.Add(language);

            var qualityPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 14,
                Margin = new Thickness(0, 12, 0, 12)
            };
            qualityPanel.Children.Add(new TextBlock
            {
                Text = L("Preferred quality", "الجودة المفضلة"),
                FontSize = 19,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 190, 202, 215)),
                VerticalAlignment = VerticalAlignment.Center
            });
            var preferredQuality = new ComboBox { MinWidth = 220, FontSize = 18 };
            foreach (var value in new[] { "Auto", "1080p", "720p", "480p", "360p", "240p" })
                preferredQuality.Items.Add(new ComboBoxItem { Content = value, Tag = value });
            var currentQuality = preferredQuality.Items.OfType<ComboBoxItem>()
                .FirstOrDefault(x => String.Equals(x.Tag as string, state.PreferredQuality, StringComparison.OrdinalIgnoreCase));
            preferredQuality.SelectedItem = currentQuality ?? preferredQuality.Items[0];
            preferredQuality.SelectionChanged += (s, e) =>
            {
                var choice = preferredQuality.SelectedItem as ComboBoxItem;
                if (choice != null) state.PreferredQuality = choice.Tag as string ?? "Auto";
            };
            qualityPanel.Children.Add(preferredQuality);
            PageContent.Children.Add(qualityPanel);

            var resume = new Button
            {
                Content = state.ResumePlayback
                    ? L("Resume playback: On", "استكمال المشاهدة: مفعّل")
                    : L("Resume playback: Off", "استكمال المشاهدة: متوقف"),
                Style = (Style)Application.Current.Resources["XmanaButton"],
                HorizontalAlignment = HorizontalAlignment.Left
            };
            resume.Click += (s, e) =>
            {
                state.ResumePlayback = !state.ResumePlayback;
                resume.Content = state.ResumePlayback
                    ? L("Resume playback: On", "استكمال المشاهدة: مفعّل")
                    : L("Resume playback: Off", "استكمال المشاهدة: متوقف");
            };
            PageContent.Children.Add(resume);

            var resetProgress = new Button
            {
                Content = L("Clear continue-watching progress", "مسح تقدم المشاهدة"),
                Style = (Style)Application.Current.Resources["XmanaButton"],
                HorizontalAlignment = HorizontalAlignment.Left
            };
            resetProgress.Click += (s, e) =>
            {
                state.ClearPlaybackProgress();
                resetProgress.IsEnabled = false;
                resetProgress.Content = L("Progress cleared", "تم مسح التقدم");
            };
            PageContent.Children.Add(resetProgress);

            var resetHistory = new Button
            {
                Content = L("Clear watch history", "مسح سجل المشاهدة"),
                Style = (Style)Application.Current.Resources["XmanaButton"],
                HorizontalAlignment = HorizontalAlignment.Left
            };
            resetHistory.Click += (s, e) =>
            {
                state.ClearWatchHistory();
                resetHistory.IsEnabled = false;
                resetHistory.Content = L("Watch history cleared", "تم مسح سجل المشاهدة");
            };
            PageContent.Children.Add(resetHistory);

            AddMessage(L("Xmana is an unofficial, local-only client. Favorites, watchlist, search history, viewed titles, playback progress and preferences stay on this console. No account login or cloud sync is used.",
                "Xmana عميل مستقل وغير رسمي ويعمل محلياً. تبقى المفضلة وقائمة المشاهدة وسجل البحث والعناوين المشاهدة وتقدم التشغيل والتفضيلات على هذا الجهاز. لا يستخدم تسجيل الدخول أو المزامنة السحابية."));
            var faq = new Button
            {
                Content = L("Load FAQ", "تحميل الأسئلة الشائعة"),
                Style = (Style)Application.Current.Resources["XmanaButton"],
                HorizontalAlignment = HorizontalAlignment.Left
            };
            faq.Click += async (s, e) => await ShowFaqAsync();
            PageContent.Children.Add(faq);
        }

        private async Task ShowFaqAsync()
        {
            ShowBusy(L("Loading FAQ…", "جارٍ تحميل الأسئلة…"));
            JToken data = null;
            try { data = await api.GetFaqAsync(); } catch { }
            HideBusy();
            var text = data == null
                ? L("FAQ is currently unavailable.", "الأسئلة الشائعة غير متاحة حالياً.")
                : data.ToString(Newtonsoft.Json.Formatting.Indented);
            AddMessage(text);
        }

        private async Task ShowDetailsAsync(VideoItem item)
        {
            if (item == null || String.IsNullOrWhiteSpace(item.Id)) return;
            var wasSeries = item.IsSeries;
            var cachedProgress = state.GetProgress(item.Id);
            var detailsToken = await SafeTokenAsync(() => api.GetAllVideoInfoAsync(item.Id));
            var fullDetails = VideoItem.FromToken(detailsToken);
            if (fullDetails != null)
            {
                fullDetails.IsSeries = wasSeries;
                if (String.IsNullOrWhiteSpace(fullDetails.RootSeries)) fullDetails.RootSeries = item.RootSeries;
                if (String.IsNullOrWhiteSpace(fullDetails.Poster)) fullDetails.Poster = item.Poster;
                item = fullDetails;
            }

            ResetContent();
            state.AddRecentlyViewed(item.Id);
            if (!String.IsNullOrWhiteSpace(item.Backdrop) && IsWebUri(item.Backdrop))
            {
                var artwork = new Image
                {
                    Source = new BitmapImage(new Uri(item.Backdrop)),
                    Stretch = Stretch.UniformToFill,
                    Height = 330,
                    MaxHeight = 330,
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };
                PageContent.Children.Add(artwork);
            }

            PageContent.Children.Add(SectionTitle(item.GetTitle(isArabic)));
            var metadata = String.Join("   •   ", new[] {
                item.Year,
                !String.IsNullOrWhiteSpace(item.Rating) ? "★ " + item.Rating : null,
                item.Duration,
                !String.IsNullOrWhiteSpace(item.Season) ? L("Season ", "الموسم ") + item.Season : null,
                !String.IsNullOrWhiteSpace(item.EpisodeNumber) ? L("Episode ", "الحلقة ") + item.EpisodeNumber : null
            }.Where(x => !String.IsNullOrWhiteSpace(x)));
            if (!String.IsNullOrWhiteSpace(metadata))
                PageContent.Children.Add(new TextBlock
                {
                    Text = metadata,
                    FontSize = 18,
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 158, 176, 195))
                });
            if (!String.IsNullOrWhiteSpace(item.GetDescription(isArabic)))
                PageContent.Children.Add(new TextBlock
                {
                    Text = item.GetDescription(isArabic),
                    FontSize = 21,
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 221, 228, 236)),
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 1200
                });

            var categoryNames = item.Categories.Select(category => category.GetTitle(isArabic))
                .Where(value => !String.IsNullOrWhiteSpace(value)).ToList();
            if (categoryNames.Count > 0)
                AddMessage(L("Genres: ", "التصنيفات: ") + String.Join("  ·  ", categoryNames));
            if (item.Language != null)
            {
                var language = isArabic
                    ? (item.Language.ArabicTitle ?? item.Language.EnglishTitle)
                    : (item.Language.EnglishTitle ?? item.Language.ArabicTitle);
                if (!String.IsNullOrWhiteSpace(language))
                    AddMessage(L("Language: ", "اللغة: ") + language);
            }

            var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14 };
            var play = new Button
            {
                Content = L("Play", "تشغيل"),
                Style = (Style)Application.Current.Resources["XmanaButton"],
                Tag = item
            };
            play.Click += async (s, e) => await StartPlaybackAsync((VideoItem)((Button)s).Tag);

            var resumeProgress = state.ResumePlayback ? cachedProgress : null;
            Button resume = null;
            if (resumeProgress != null && resumeProgress.PositionSeconds > 15)
            {
                resume = new Button
                {
                    Content = L("Resume · ", "استئناف · ") + FormatTime(TimeSpan.FromSeconds(resumeProgress.PositionSeconds)),
                    Style = (Style)Application.Current.Resources["XmanaButton"],
                    Tag = new ResumeChoice { Item = item, PositionSeconds = resumeProgress.PositionSeconds }
                };
                resume.Click += async (s, e) =>
                {
                    var choice = (ResumeChoice)((Button)s).Tag;
                    await StartPlaybackAsync(choice.Item, choice.PositionSeconds);
                };
            }

            var favorite = new Button
            {
                Content = state.IsFavorite(item.Id) ? L("Remove from Favorites", "إزالة من المفضلة") :
                    L("Add to Favorites", "إضافة إلى المفضلة"),
                Style = (Style)Application.Current.Resources["XmanaButton"]
            };
            favorite.Click += (s, e) =>
            {
                state.ToggleFavorite(item.Id);
                favorite.Content = state.IsFavorite(item.Id)
                    ? L("Remove from Favorites", "إزالة من المفضلة")
                    : L("Add to Favorites", "إضافة إلى المفضلة");
            };
            var watchlist = new Button
            {
                Content = state.IsInWatchlist(item.Id) ? L("Remove from Watchlist", "إزالة من قائمة المشاهدة") :
                    L("Add to Watchlist", "إضافة إلى قائمة المشاهدة"),
                Style = (Style)Application.Current.Resources["XmanaButton"]
            };
            watchlist.Click += (s, e) =>
            {
                state.ToggleWatchlist(item.Id);
                watchlist.Content = state.IsInWatchlist(item.Id)
                    ? L("Remove from Watchlist", "إزالة من قائمة المشاهدة")
                    : L("Add to Watchlist", "إضافة إلى قائمة المشاهدة");
            };
            actions.Children.Add(play);
            if (resume != null) actions.Children.Add(resume);
            actions.Children.Add(favorite);
            actions.Children.Add(watchlist);
            PageContent.Children.Add(actions);

            AddStaffSection(L("Cast", "طاقم التمثيل"), item.Actors);
            AddStaffSection(L("Directors", "المخرجون"), item.Directors);
            AddStaffSection(L("Writers", "الكتّاب"), item.Writers);

            if (item.IsSeries || (!String.IsNullOrWhiteSpace(item.RootSeries) && item.RootSeries != "0"))
                await AddEpisodesAsync(item);

            var comments = await SafeTokenAsync(() => api.GetVideoCommentsAsync(item.Id, 10, 0));
            AddCommentsSection(comments);

            var recommendations = await SafeListAsync(() =>
                api.GetRecommendationsAsync(item.Id, item.GetTitle(isArabic)));
            if (recommendations.Count == 0)
                recommendations = await SafeListAsync(() => api.GetRelatedVideosAsync(item.Id, "1", "0"));
            AddRail(L("Recommended for you", "مقترح لك"), recommendations);
        }

        private void AddStaffSection(string heading, IList<StaffItem> staff)
        {
            if (staff == null || staff.Count == 0) return;
            var section = new StackPanel { Spacing = 12 };
            section.Children.Add(new TextBlock
            {
                Text = heading,
                FontSize = 23,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Colors.White)
            });
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            foreach (var person in staff)
            {
                var label = String.IsNullOrWhiteSpace(person.Role)
                    ? person.Name
                    : person.Name + " · " + person.Role;
                if (String.IsNullOrWhiteSpace(label)) continue;
                if (String.IsNullOrWhiteSpace(person.Id))
                {
                    row.Children.Add(new TextBlock
                    {
                        Text = label,
                        FontSize = 17,
                        Foreground = new SolidColorBrush(Color.FromArgb(255, 205, 215, 226)),
                        Margin = new Thickness(8, 12, 8, 12)
                    });
                    continue;
                }

                var button = new Button
                {
                    Content = label,
                    Tag = person,
                    Style = (Style)Application.Current.Resources["XmanaButton"]
                };
                button.Click += async (s, e) => await ShowStaffAsync((StaffItem)((Button)s).Tag);
                row.Children.Add(button);
            }
            if (row.Children.Count > 0)
            {
                section.Children.Add(row);
                PageContent.Children.Add(section);
            }
        }

        private async Task ShowStaffAsync(StaffItem person)
        {
            if (person == null || String.IsNullOrWhiteSpace(person.Id)) return;
            ResetContent();
            PageContent.Children.Add(SectionTitle(person.Name ?? L("Staff", "الفنان")));
            if (!String.IsNullOrWhiteSpace(person.Role))
                AddMessage(person.Role);
            ShowBusy(L("Loading associated works…", "جارٍ تحميل الأعمال المرتبطة…"));
            var response = await SafeTokenAsync(() =>
                api.GetStaffAsync(person.Id, isArabic ? "ar" : "en", "0"));
            HideBusy();
            var items = CinemanaApiClient.AsList(response)
                .Select(VideoItem.FromToken)
                .Where(item => item != null && !String.IsNullOrWhiteSpace(item.Id))
                .ToList();
            AddRail(L("Associated works", "الأعمال المرتبطة"), items);
            if (items.Count == 0)
                AddMessage(L("No associated titles were returned.", "لم تُرجع الخدمة عناوين مرتبطة."));
        }

        private void AddCommentsSection(JToken response)
        {
            var comments = CinemanaApiClient.AsList(response).ToList();
            var section = new StackPanel { Spacing = 10 };
            section.Children.Add(new TextBlock
            {
                Text = L("Public comments", "التعليقات العامة"),
                FontSize = 23,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Colors.White)
            });
            if (comments.Count == 0)
            {
                section.Children.Add(new TextBlock
                {
                    Text = L("No public comments are available.", "لا توجد تعليقات عامة متاحة."),
                    FontSize = 17,
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 150, 165, 181))
                });
            }
            else
            {
                foreach (var comment in comments.Take(10))
                {
                    var author = ReadTokenString(comment, "userName", "name", "user");
                    var body = ReadTokenString(comment, "comment", "text", "content", "commentText");
                    if (String.IsNullOrWhiteSpace(body)) continue;
                    section.Children.Add(new Border
                    {
                        Background = new SolidColorBrush(Color.FromArgb(255, 20, 25, 35)),
                        Padding = new Thickness(18),
                        Child = new StackPanel
                        {
                            Spacing = 6,
                            Children =
                            {
                                new TextBlock
                                {
                                    Text = author,
                                    FontSize = 15,
                                    Foreground = new SolidColorBrush(Color.FromArgb(255, 128, 214, 202))
                                },
                                new TextBlock
                                {
                                    Text = body,
                                    FontSize = 17,
                                    TextWrapping = TextWrapping.Wrap,
                                    Foreground = new SolidColorBrush(Color.FromArgb(255, 221, 228, 236))
                                }
                            }
                        }
                    });
                }
            }
            PageContent.Children.Add(section);
        }

        private async Task AddEpisodesAsync(VideoItem item)
        {
            var seriesId = !String.IsNullOrWhiteSpace(item.RootSeries) && item.RootSeries != "0" ?
                item.RootSeries : item.Id;
            var episodes = await SafeListAsync(() => api.GetVideoSeasonAsync(seriesId));
            foreach (var episode in episodes) episode.RootSeries = seriesId;
            JToken seasonResponse = null;
            try { seasonResponse = await api.GetVideoSeasonNumberAsync(seriesId); } catch { }
            var seasons = CinemanaApiClient.AsList(seasonResponse)
                .Select(x => x is JObject seasonObject && seasonObject["season"] != null ? seasonObject["season"].ToString() : x.ToString())
                .Where(x => !String.IsNullOrWhiteSpace(x))
                .Distinct()
                .OrderBy(x => { int number; return Int32.TryParse(x, out number) ? number : Int32.MaxValue; })
                .ToList();

            if (seasons.Count > 0)
            {
                foreach (var season in seasons)
                {
                    var seasonEpisodes = episodes.Where(episode => episode.Season == season).ToList();
                    if (seasonEpisodes.Count > 0)
                        AddRail(L("Season ", "الموسم ") + season, seasonEpisodes);
                }
            }
            else if (episodes.Count > 0)
            {
                AddRail(L("Episodes", "الحلقات"), episodes);
            }
        }

        private async Task StartPlaybackAsync(VideoItem item, double resumeSeconds = 0)
        {
            ShowBusy(L("Preparing playback…", "جارٍ تجهيز التشغيل…"));
            try
            {
                var files = await api.GetTranscodedFilesAsync(item.Id);
                var uri = PlaybackService.FindStreamUrl(files, state.PreferredQuality);
                HideBusy();
                if (uri == null)
                {
                    AddMessage(L("The service did not expose a playable stream in the current response. No media URL was logged or saved.",
                        "لم تُرجع الخدمة رابط بث قابل للتشغيل. لم يتم تسجيل رابط الوسائط أو حفظه."));
                    return;
                }
                ShowPlayer(item, files, uri, resumeSeconds);
            }
            catch (Exception ex)
            {
                HideBusy();
                AddMessage(L("Could not prepare this title: ", "تعذر تجهيز هذا العنوان: ") + ex.Message);
            }
        }

        private void ShowPlayer(VideoItem item, JToken files, Uri uri, double startPositionSeconds = 0)
        {
            ResetContent();
            playerTimer?.Stop();
            playback?.Dispose();
            playback = new PlaybackService();
            playingItem = item;
            activePlaybackFiles = files;
            playerQualitySelector = null;
            lastProgressWriteUtc = DateTime.MinValue;

            var stage = new Grid
            {
                Height = 720,
                Background = new SolidColorBrush(Colors.Black),
                Margin = new Thickness(0, 8, 0, 16)
            };
            playback.View.HorizontalAlignment = HorizontalAlignment.Stretch;
            playback.View.VerticalAlignment = VerticalAlignment.Stretch;
            stage.Children.Add(playback.View);

            var topOverlay = new Border
            {
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Background = new SolidColorBrush(Color.FromArgb(185, 8, 12, 18)),
                Padding = new Thickness(30, 18, 30, 18),
                Child = new TextBlock
                {
                    Text = item.GetTitle(isArabic),
                    FontSize = 25,
                    FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Colors.White)
                }
            };
            stage.Children.Add(topOverlay);

            playerSeekSlider = new Slider
            {
                Minimum = 0,
                Maximum = 1000,
                Value = 0,
                StepFrequency = 1,
                IsEnabled = false,
                Margin = new Thickness(4, 0, 4, 0)
            };
            playerSeekSlider.ValueChanged += (s, e) =>
            {
                if (updatingSeek || playback == null || playback.Duration <= TimeSpan.Zero) return;
                var seconds = playback.Duration.TotalSeconds * (playerSeekSlider.Value / 1000.0);
                playback.Seek(TimeSpan.FromSeconds(seconds));
            };

            playerTimeText = new TextBlock
            {
                Text = "00:00 / --:--",
                FontSize = 16,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 218, 226, 235)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0)
            };
            playerPlayPauseButton = new Button
            {
                Content = L("Pause", "إيقاف مؤقت"),
                Style = (Style)Application.Current.Resources["XmanaButton"]
            };
            playerPlayPauseButton.Click += (s, e) =>
            {
                playback?.TogglePlayPause();
                RefreshPlayerUi();
            };

            var backTen = new Button
            {
                Content = "−10s",
                Style = (Style)Application.Current.Resources["XmanaButton"]
            };
            backTen.Click += (s, e) => playback?.SeekBy(TimeSpan.FromSeconds(-10));

            var forwardTen = new Button
            {
                Content = "+10s",
                Style = (Style)Application.Current.Resources["XmanaButton"]
            };
            forwardTen.Click += (s, e) => playback?.SeekBy(TimeSpan.FromSeconds(10));

            var close = new Button
            {
                Content = L("Back to details", "العودة للتفاصيل"),
                Style = (Style)Application.Current.Resources["XmanaButton"]
            };
            close.Click += async (s, e) =>
            {
                SaveCurrentProgress();
                playerTimer?.Stop();
                playerTimer = null;
                playback?.Dispose();
                playback = null;
                await ShowDetailsAsync(item);
            };

            var controls = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = 12
            };
            controls.Children.Add(backTen);
            controls.Children.Add(playerPlayPauseButton);
            controls.Children.Add(forwardTen);
            controls.Children.Add(close);

            var timeline = new Grid();
            timeline.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            timeline.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(playerSeekSlider, 0);
            Grid.SetColumn(playerTimeText, 1);
            timeline.Children.Add(playerSeekSlider);
            timeline.Children.Add(playerTimeText);

            var transportPanel = new StackPanel { Spacing = 12 };
            transportPanel.Children.Add(timeline);
            transportPanel.Children.Add(controls);

            var qualities = PlaybackService.FindAvailableQualities(files);
            if (qualities.Count > 0)
            {
                playerQualitySelector = new ComboBox
                {
                    MinWidth = 220,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    FontSize = 18,
                    Header = L("Playback quality", "جودة التشغيل")
                };
                playerQualitySelector.Items.Add(new ComboBoxItem
                {
                    Content = L("Auto", "تلقائي"),
                    Tag = "Auto"
                });
                foreach (var quality in qualities)
                    playerQualitySelector.Items.Add(new ComboBoxItem { Content = quality, Tag = quality });

                var preferred = state.PreferredQuality;
                var selected = playerQualitySelector.Items
                    .OfType<ComboBoxItem>()
                    .FirstOrDefault(option => String.Equals(option.Tag as string, preferred, StringComparison.OrdinalIgnoreCase));
                playerQualitySelector.SelectedItem = selected ?? playerQualitySelector.Items[0];
                playerQualitySelector.SelectionChanged += async (s, e) =>
                {
                    var option = playerQualitySelector.SelectedItem as ComboBoxItem;
                    var quality = option?.Tag as string;
                    if (!String.IsNullOrWhiteSpace(quality))
                        await ChangePlaybackQualityAsync(quality);
                };
                transportPanel.Children.Add(playerQualitySelector);
            }
            var bottomOverlay = new Border
            {
                VerticalAlignment = VerticalAlignment.Bottom,
                Background = new SolidColorBrush(Color.FromArgb(220, 8, 12, 18)),
                Padding = new Thickness(28, 14, 28, 18),
                Child = transportPanel
            };
            stage.Children.Add(bottomOverlay);

            PageContent.Children.Add(stage);
            playback.PlaybackFailed += (s, message) =>
            {
                _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    AddMessage(L("Playback failed: ", "فشل تشغيل الفيديو: ") + message);
                });
            };
            playback.Play(uri, startPositionSeconds > 0 ? TimeSpan.FromSeconds(startPositionSeconds) : (TimeSpan?)null);

            playerTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            playerTimer.Tick += (s, e) => RefreshPlayerUi();
            playerTimer.Start();
            RefreshPlayerUi();
            close.Focus(FocusState.Programmatic);
        }

        private async Task ChangePlaybackQualityAsync(string quality)
        {
            if (playback == null || activePlaybackFiles == null) return;
            var qualities = PlaybackService.FindAvailableQualities(activePlaybackFiles);
            if (!String.Equals(quality, "Auto", StringComparison.OrdinalIgnoreCase) &&
                !qualities.Contains(quality, StringComparer.OrdinalIgnoreCase))
                return;

            var position = playback.Position;
            var uri = PlaybackService.FindStreamUrl(activePlaybackFiles, quality);
            if (uri == null)
            {
                AddMessage(L("That quality is not available for this title.", "هذه الجودة غير متاحة لهذا العنوان."));
                return;
            }

            state.PreferredQuality = quality;
            playback.Play(uri, position > TimeSpan.Zero ? position : (TimeSpan?)null);
        }

        private void RefreshPlayerUi()
        {
            if (playback == null) return;
            var duration = playback.Duration;
            var position = playback.Position;
            if (duration > TimeSpan.Zero)
            {
                playerSeekSlider.IsEnabled = playback.CanSeek;
                updatingSeek = true;
                playerSeekSlider.Value = Math.Max(0, Math.Min(1000,
                    position.TotalSeconds / duration.TotalSeconds * 1000.0));
                updatingSeek = false;
                playerTimeText.Text = FormatTime(position) + " / " + FormatTime(duration);
            }
            playerPlayPauseButton.Content = playback.IsPlaying
                ? L("Pause", "إيقاف مؤقت")
                : L("Play", "تشغيل");

            if ((DateTime.UtcNow - lastProgressWriteUtc).TotalSeconds >= 15)
                SaveCurrentProgress();
        }

        private static string FormatTime(TimeSpan value)
        {
            if (value < TimeSpan.Zero) value = TimeSpan.Zero;
            return value.TotalHours >= 1
                ? value.ToString(@"h\:mm\:ss")
                : value.ToString(@"mm\:ss");
        }

        public void HandleApplicationSuspending()
        {
            SaveCurrentProgress();
            playback?.Pause();
            playerTimer?.Stop();
        }

        private void SaveCurrentProgress()
        {
            if (playback == null || playingItem == null || playback.Duration <= TimeSpan.Zero) return;
            state.SaveProgress(new PlaybackProgress
            {
                VideoId = playingItem.Id,
                SeriesId = !String.IsNullOrWhiteSpace(playingItem.RootSeries) && playingItem.RootSeries != "0"
                    ? playingItem.RootSeries : (playingItem.IsSeries ? playingItem.Id : String.Empty),
                Season = playingItem.Season,
                Episode = playingItem.EpisodeNumber,
                PositionSeconds = playback.Position.TotalSeconds,
                DurationSeconds = playback.Duration.TotalSeconds
            });
            lastProgressWriteUtc = DateTime.UtcNow;
        }

        private TextBlock SectionTitle(string title) => new TextBlock
        {
            Text = title,
            FontSize = 34,
            Foreground = new SolidColorBrush(Colors.White),
            FontWeight = Windows.UI.Text.FontWeights.Bold,
            Margin = new Thickness(0, 4, 0, 10)
        };

        private void AddMessage(string message)
        {
            PageContent.Children.Add(new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(255, 20, 25, 35)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 42, 54, 69)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(24),
                Child = new TextBlock
                {
                    Text = message,
                    FontSize = 18,
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 192, 203, 216)),
                    TextWrapping = TextWrapping.Wrap
                }
            });
        }

        private sealed class ResumeChoice
        {
            public VideoItem Item { get; set; }
            public double PositionSeconds { get; set; }
        }

        private static bool IsWebUri(string value)
        {
            Uri uri;
            return Uri.TryCreate(value, UriKind.Absolute, out uri) &&
                (uri.Scheme == "https" || uri.Scheme == "http");
        }
    }
}
