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
        private readonly CinemanaApiClient api = new CinemanaApiClient();
        private readonly LocalStateStore state = new LocalStateStore();
        private PlaybackService playback;
        private bool isArabic;
        private string currentSection = "Home";

        public MainPage()
        {
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
            var featured = await SafeListAsync(() => api.GetFeaturedMoviesAsync("0"));
            var latestMovies = await SafeListAsync(() => api.GetLatestMoviesAsync("0", 0));
            var latestSeries = await SafeListAsync(() => api.GetLatestSeriesAsync("0", 0));
            foreach (var series in latestSeries) series.IsSeries = true;
            var newItems = await SafeListAsync(() => api.GetLatestVideosItemsAsync("0", 12));
            HideBusy();

            var hero = featured.FirstOrDefault() ?? latestMovies.FirstOrDefault() ?? latestSeries.FirstOrDefault();
            if (hero != null) AddHero(hero);
            AddRail(L("Featured movies", "أفلام مختارة"), featured);
            AddRail(L("New arrivals", "وصل حديثاً"), newItems);
            AddRail(L("Latest movies", "أحدث الأفلام"), latestMovies);
            AddRail(L("Latest series", "أحدث المسلسلات"), latestSeries);
            if (hero == null && latestMovies.Count == 0 && latestSeries.Count == 0)
                AddMessage(L("The catalog is unavailable right now. Check your connection and try again.",
                    "الكتالوج غير متاح حالياً. تحقق من الاتصال ثم أعد المحاولة."));
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
                AddRail(L("Animation catalog", "مكتبة الأنمي والرسوم المتحركة"), items);
        }

        private async Task ShowCategoriesAsync()
        {
            ResetContent();
            ShowBusy(L("Loading categories…", "جارٍ تحميل التصنيفات…"));
            JToken response = null;
            try { response = await api.GetCategoryLanguageFrequencyAsync(); } catch { }
            HideBusy();
            PageContent.Children.Add(SectionTitle(L("Browse by category", "تصفح حسب التصنيف")));
            var categories = CinemanaApiClient.AsList(response);
            if (!categories.Any())
            {
                AddMessage(L("Category data is unavailable right now.", "بيانات التصنيفات غير متاحة حالياً."));
                return;
            }

            var grid = new GridView
            {
                ItemsSource = null,
                IsItemClickEnabled = true,
                SelectionMode = ListViewSelectionMode.None,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                ItemClick = null
            };
            var wrap = new WrapGrid { ItemWidth = 310, ItemHeight = 92, Orientation = Orientation.Horizontal };
            grid.ItemsPanel = new ItemsPanelTemplate(() => wrap);
            foreach (var entry in categories)
            {
                var id = entry["nb"]?.ToString();
                var title = isArabic ? entry["ar_title"]?.ToString() : entry["en_title"]?.ToString();
                if (String.IsNullOrWhiteSpace(id) || String.IsNullOrWhiteSpace(title)) continue;
                var button = new Button
                {
                    Content = title,
                    Tag = new CategoryChoice { Id = id, Title = title },
                    Style = (Style)Application.Current.Resources["XmanaButton"],
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Margin = new Thickness(6),
                    Height = 78
                };
                button.Click += async (s, e) =>
                {
                    var choice = (CategoryChoice)((Button)s).Tag;
                    await ShowCategoryAsync(choice);
                };
                grid.Items.Add(button);
            }

            if (grid.Items.Count == 0)
            {
                AddMessage(L("No category entries could be read from the response.", "تعذر قراءة عناصر التصنيف من الاستجابة."));
                return;
            }
            PageContent.Children.Add(grid);
        }

        private async Task ShowCategoryAsync(CategoryChoice category)
        {
            ResetContent();
            ShowBusy(L("Loading category…", "جارٍ تحميل التصنيف…"));
            var movies = await SafeListAsync(() =>
                api.GetVideosByCategoryAsync(category.Id, "desc", "1", 0, "0"));
            var series = await SafeListAsync(() =>
                api.GetVideosByCategoryAsync(category.Id, "desc", "2", 0, "0"));
            foreach (var item in series) item.IsSeries = true;
            HideBusy();
            PageContent.Children.Add(SectionTitle(category.Title));
            AddRail(L("Movies", "الأفلام"), movies);
            AddRail(L("Series", "المسلسلات"), series);
            if (movies.Count == 0 && series.Count == 0)
                AddMessage(L("No titles were returned for this category.", "لم تُرجع هذه الفئة عناوين."));
        }

        private sealed class CategoryChoice
        {
            public string Id { get; set; }
            public string Title { get; set; }
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
            query.Focus(FocusState.Programmatic);
        }

        private async Task RunSearchAsync(string text)
        {
            if (String.IsNullOrWhiteSpace(text)) return;
            ShowBusy(L("Searching…", "جارٍ البحث…"));
            var items = await SafeListAsync(() => api.SearchTitleAsync(text.Trim(), 0, "0"));
            HideBusy();
            if (PageContent.Children.Count > 0) PageContent.Children.RemoveAt(PageContent.Children.Count - 1);
            AddRail(L("Search results", "نتائج البحث"), items);
            if (items.Count == 0) AddMessage(L("No matching titles were found.", "لم يتم العثور على عناوين مطابقة."));
        }

        private void ShowLibrary()
        {
            ResetContent();
            PageContent.Children.Add(SectionTitle(L("Your collection", "مكتبتك")));
            var ids = state.Favorites;
            if (ids.Count == 0)
            {
                AddMessage(L("Your list is empty. Add titles from their details page.",
                    "قائمتك فارغة. أضف العناوين من صفحة التفاصيل."));
                return;
            }
            ShowBusy(L("Restoring your list…", "جارٍ استعادة قائمتك…"));
            _ = LoadLibraryAsync(ids.ToList());
        }

        private async Task LoadLibraryAsync(IList<string> ids)
        {
            var items = new List<VideoItem>();
            foreach (var id in ids)
            {
                try
                {
                    var item = VideoItem.FromToken(await api.GetAllVideoInfoAsync(id));
                    if (item != null) items.Add(item);
                }
                catch { }
            }
            HideBusy();
            AddRail(L("Favorites", "المفضلة"), items);
            if (items.Count == 0)
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
            AddMessage(L("Xmana is a local-first client. Favorites and interface language stay on this console. No account login is implemented.",
                "Xmana تطبيق محلي أولاً. تُحفظ المفضلة ولغة الواجهة على هذا الجهاز. لا يتضمن التطبيق تسجيل دخول للحسابات."));
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
            if (item == null) return;
            ResetContent();
            PageContent.Children.Add(SectionTitle(item.GetTitle(isArabic)));
            var metadata = String.Join("   •   ", new[] {
                item.Year, !String.IsNullOrWhiteSpace(item.Rating) ? "★ " + item.Rating : null,
                item.Duration
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

            var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14 };
            var play = new Button
            {
                Content = L("Play", "تشغيل"),
                Style = (Style)Application.Current.Resources["XmanaButton"],
                Tag = item
            };
            play.Click += async (s, e) => await StartPlaybackAsync((VideoItem)((Button)s).Tag);
            var favorite = new Button
            {
                Content = state.IsFavorite(item.Id) ? L("Remove from My List", "إزالة من قائمتي") :
                    L("Add to My List", "إضافة إلى قائمتي"),
                Style = (Style)Application.Current.Resources["XmanaButton"],
                Tag = item
            };
            favorite.Click += (s, e) =>
            {
                state.ToggleFavorite(item.Id);
                favorite.Content = state.IsFavorite(item.Id) ? L("Remove from My List", "إزالة من قائمتي") :
                    L("Add to My List", "إضافة إلى قائمتي");
            };
            actions.Children.Add(play);
            actions.Children.Add(favorite);
            PageContent.Children.Add(actions);

            if (item.IsSeries || (!String.IsNullOrWhiteSpace(item.RootSeries) && item.RootSeries != "0"))
                await AddEpisodesAsync(item);
            var related = await SafeListAsync(() => api.GetRelatedVideosAsync(item.Id, "1", "0"));
            AddRail(L("You may also like", "قد يعجبك أيضاً"), related);
        }

        private async Task AddEpisodesAsync(VideoItem item)
        {
            var seriesId = !String.IsNullOrWhiteSpace(item.RootSeries) && item.RootSeries != "0" ?
                item.RootSeries : item.Id;
            var episodes = await SafeListAsync(() => api.GetVideoSeasonAsync(seriesId));
            JToken seasonResponse = null;
            try { seasonResponse = await api.GetVideoSeasonNumberAsync(seriesId); } catch { }
            var seasons = CinemanaApiClient.AsList(seasonResponse)
                .Select(x => x["season"] != null ? x["season"].ToString() : x.ToString())
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

        private async Task StartPlaybackAsync(VideoItem item)
        {
            ShowBusy(L("Preparing playback…", "جارٍ تجهيز التشغيل…"));
            try
            {
                var files = await api.GetTranscodedFilesAsync(item.Id);
                var uri = PlaybackService.FindStreamUrl(files);
                HideBusy();
                if (uri == null)
                {
                    AddMessage(L("The service did not expose a playable stream in the current response. No media URL was logged or saved.",
                        "لم تُرجع الخدمة رابط بث قابل للتشغيل. لم يتم تسجيل رابط الوسائط أو حفظه."));
                    return;
                }
                ShowPlayer(item, uri);
            }
            catch (Exception ex)
            {
                HideBusy();
                AddMessage(L("Could not prepare this title: ", "تعذر تجهيز هذا العنوان: ") + ex.Message);
            }
        }

        private void ShowPlayer(VideoItem item, Uri uri)
        {
            ResetContent();
            playback?.Dispose();
            playback = new PlaybackService();
            var back = new Button
            {
                Content = L("Back to details", "العودة للتفاصيل"),
                Style = (Style)Application.Current.Resources["XmanaButton"]
            };
            back.Click += async (s, e) =>
            {
                playback?.Dispose();
                playback = null;
                await ShowDetailsAsync(item);
            };
            PageContent.Children.Add(new TextBlock
            {
                Text = item.GetTitle(isArabic),
                FontSize = 28,
                Foreground = new SolidColorBrush(Colors.White),
                FontWeight = Windows.UI.Text.FontWeights.SemiBold
            });
            PageContent.Children.Add(back);
            playback.View.Height = 760;
            playback.View.HorizontalAlignment = HorizontalAlignment.Stretch;
            PageContent.Children.Add(playback.View);
            playback.Play(uri);
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

        private static bool IsWebUri(string value)
        {
            Uri uri;
            return Uri.TryCreate(value, UriKind.Absolute, out uri) &&
                (uri.Scheme == "https" || uri.Scheme == "http");
        }
    }
}
