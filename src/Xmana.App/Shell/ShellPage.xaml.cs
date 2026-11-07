using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Xmana.App.Controls;
using Xmana.App.Focus;
using Xmana.App.Pages;
using Xmana.App.Services;
using Xmana.Core.Focus;
using Xmana.Core.Navigation;

namespace Xmana.App.Shell
{
    /// <summary>
    /// Application shell: top navigation header (horizontal tabs), content frame, splash overlay, back/focus coordination.
    /// It renders whatever <see cref="NavigationController"/> says is current and owns no navigation rules.
    /// </summary>
    public sealed partial class ShellPage : Page
    {
        // Segoe MDL2 Assets code points.
        private static readonly Dictionary<NavigationDestination, string> RailGlyphs = new Dictionary<NavigationDestination, string>
        {
            { NavigationDestination.Home, "\uE80F" },
            { NavigationDestination.Movies, "\uE714" },
            { NavigationDestination.Series, "\uE7F4" },
            { NavigationDestination.Anime, "\uE76E" },
            { NavigationDestination.Categories, "\uE71D" },
            { NavigationDestination.Search, "\uE721" },
            { NavigationDestination.Favorites, "\uEB51" },
            { NavigationDestination.Watchlist, "\uE7C1" },
            { NavigationDestination.History, "\uE81C" },
            { NavigationDestination.Settings, "\uE713" },
        };

        private readonly Dictionary<NavigationDestination, ChoiceButton> _railButtons =
            new Dictionary<NavigationDestination, ChoiceButton>();

        private bool _started;

        public ShellPage()
        {
            InitializeComponent();
            BuildRail();
        }

        private void BuildRail()
        {
            foreach (var destination in NavigationCatalog.RailOrder)
            {
                var button = new ChoiceButton
                {
                    Style = (Style)Application.Current.Resources["NavTabButtonStyle"],
                    Glyph = RailGlyphs[destination],
                    Tag = destination,
                };
                XmanaFocus.SetKey(button, FocusKeys.NavItem(destination));
                button.Click += OnRailItemClick;
                _railButtons[destination] = button;
                RailItems.Children.Add(button);
            }
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_started)
            {
                return;
            }

            _started = true;

            AppServices.Navigation.Navigated += OnNavigated;
            AppServices.Localization.LanguageChanged += OnLanguageChanged;
            Windows.UI.Core.SystemNavigationManager.GetForCurrentView().BackRequested += OnBackRequested;

            ApplyLanguage();
            StartHomeLoad(); // cached data usually appears before the splash finishes fading
            AppServices.Navigation.Start(); // renders the initial entry under the splash
            ContentFrame.UpdateLayout();

            await FadeAsync(SplashOverlay, 1, 0, AppServices.Motion.Duration(240));
            SplashOverlay.Visibility = Visibility.Collapsed;

            ShellContent.IsEnabled = true;
            FocusInitial();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            AppServices.Navigation.Navigated -= OnNavigated;
            AppServices.Localization.LanguageChanged -= OnLanguageChanged;
            Windows.UI.Core.SystemNavigationManager.GetForCurrentView().BackRequested -= OnBackRequested;
            _started = false;
        }

        // ---- navigation -------------------------------------------------------------------------

        private void OnRailItemClick(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is NavigationDestination destination)
            {
                SaveCurrentFocus();
                AppServices.Navigation.NavigateToRoot(destination);
            }
        }

        private void OnNavigated(object sender, NavigationChangedEventArgs e)
        {
            ShowEntry(e.To);
            RestoreFocus(e.To, e.Kind);
        }

        private void ShowEntry(NavigationEntry entry)
        {
            var pageType = PageFor(entry);

            // The player is full screen: no navigation rail and no outer padding (it applies the TV safe area to its own controls).
            var fullScreen = entry.Destination == NavigationDestination.Player;
            Header.Visibility = fullScreen ? Visibility.Collapsed : Visibility.Visible;
            ShellContent.Padding = fullScreen ? new Thickness(0) : (Thickness)Application.Current.Resources["XmanaSafeAreaThickness"];

            // Transitions are suppressed on purpose in milestone 1: controller response must stay instant.
            ContentFrame.Navigate(pageType, new PageArgs(entry), new SuppressNavigationTransitionInfo());

            var rootDestination = RootDestinationOf(entry);
            foreach (var pair in _railButtons)
            {
                pair.Value.IsActive = pair.Key == rootDestination;
            }
        }

        private static Type PageFor(NavigationEntry entry)
        {
            switch (entry.Destination)
            {
                case NavigationDestination.Home:
                    return typeof(HomePage);
                case NavigationDestination.Movies:
                case NavigationDestination.Series:
                case NavigationDestination.Anime:
                    return typeof(BrowsePage);
                case NavigationDestination.Categories:
                    // The section root lists categories; a pushed entry carries a category id and shows its titles.
                    return entry.Parameter == null ? typeof(CategoriesPage) : typeof(BrowsePage);
                case NavigationDestination.Search:
                    return entry.Parameter == "advanced" ? typeof(AdvancedSearchPage) : typeof(SearchPage);
                case NavigationDestination.Details:
                    return typeof(DetailsPage);
                case NavigationDestination.Staff:
                    return typeof(StaffPage);
                case NavigationDestination.Player:
                    return typeof(PlayerPage);
                case NavigationDestination.Favorites:
                case NavigationDestination.Watchlist:
                    return typeof(LibraryListPage);
                case NavigationDestination.History:
                    return typeof(HistoryPage);
                case NavigationDestination.Faq:
                    return typeof(FaqPage);
                case NavigationDestination.Settings:
                    return typeof(SettingsPage);
                default:
                    return typeof(PlaceholderPage);
            }
        }

        // Pushed Details and Staff screens keep the section they were opened from highlighted in the rail.
        private static NavigationDestination RootDestinationOf(NavigationEntry entry) =>
            entry.Destination == NavigationDestination.Details || entry.Destination == NavigationDestination.Staff || entry.Destination == NavigationDestination.Player || entry.Destination == NavigationDestination.Faq
                ? AppServices.Navigation.RootDestination
                : entry.Destination;

        /// <summary>B button. Unhandled result at the Home root lets the system decide (PROMPT.md section 50).</summary>
        private void OnBackRequested(object sender, BackRequestedEventArgs e)
        {
            // A page (the player) may use the press first: close a menu, cancel an offer ...
            var handler = ContentFrame.Content as IBackHandler;
            if (handler != null && handler.HandleBack())
            {
                e.Handled = true;
                return;
            }

            SaveCurrentFocus();
            e.Handled = AppServices.Navigation.GoBack() != BackOutcome.AtRoot;
        }

        // ---- language ---------------------------------------------------------------------------

        private void OnLanguageChanged(object sender, EventArgs e)
        {
            SaveCurrentFocus();
            ApplyLanguage();
            StartHomeLoad(); // group titles come from the API in the selected language
            var current = AppServices.Navigation.Current;
            ShowEntry(current);
            RestoreFocus(current, NavigationKind.Root);
        }

        private void ApplyLanguage()
        {
            var localization = AppServices.Localization;

            // One layout, mirrored by FlowDirection; Language selects correct shaping/fonts for the script.
            LayoutRoot.FlowDirection = localization.FlowDirection;
            LayoutRoot.Language = localization.CurrentLanguage.ResourceTag;

            foreach (var pair in _railButtons)
            {
                pair.Value.Label = localization.Get("Nav_" + pair.Key);
            }

            AutomationProperties.SetName(Header, localization.Get("Nav_RailAccessibleName"));
        }

        private static async void StartHomeLoad()
        {
            try
            {
                await AppServices.Home.LoadAsync(System.Threading.CancellationToken.None);
            }
            catch (Exception ex)
            {
                AppServices.Logger.Log(Xmana.Core.Logging.LogLevel.Error, "home load failed (" + ex.GetType().Name + ")");
            }
        }

        // ---- focus ------------------------------------------------------------------------------

        private static void SaveCurrentFocus()
        {
            var key = XmanaFocus.CurrentKey();
            // Rail focus is shell state, not page state: never remembered as a page's focus.
            if (key != null && !FocusKeys.IsNavItem(key))
            {
                AppServices.Navigation.SaveFocusKey(key);
            }
        }

        private void RestoreFocus(NavigationEntry entry, NavigationKind kind)
        {
            // Wait for layout so the new page's controls exist before we look for them.
            var ignored = Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                if (!ShellContent.IsEnabled)
                {
                    return; // splash still showing; FocusInitial() runs afterwards
                }

                // 1. Restore the saved element (back navigation, returning to a section, language switch).
                if (entry.SavedFocusKey != null && XmanaFocus.TryFocus(LayoutRoot, entry.SavedFocusKey))
                {
                    return;
                }

                // 2. Focus is already on live content, or on this section's own rail item (A pressed on it): keep it.
                var valid = XmanaFocus.HasValidFocus(LayoutRoot);
                var current = XmanaFocus.CurrentKey();
                if (valid && (!FocusKeys.IsNavItem(current) || current == FocusKeys.NavItem(RootDestinationOf(entry))))
                {
                    return;
                }

                // 3. Focus was lost (page replaced) or sits on another section's rail item: prefer the page's primary
                //    control, otherwise land on the active rail item.
                if (!valid && XmanaFocus.TryFocus(ContentFrame, FocusKeys.ContentPrimary))
                {
                    return;
                }

                // A screen that was just opened from content (Push/Replace) sets its own focus when its data is ready;
                // sending focus to the rail meanwhile would make the screen open with the menu selected.
                if (!valid && (kind == NavigationKind.Push || kind == NavigationKind.Replace))
                {
                    return;
                }

                FocusActiveRailItem(RootDestinationOf(entry));
            });
        }

        private void FocusInitial()
        {
            var entry = AppServices.Navigation.Current;
            if (entry.SavedFocusKey != null && XmanaFocus.TryFocus(LayoutRoot, entry.SavedFocusKey))
            {
                return;
            }

            if (!XmanaFocus.TryFocus(ContentFrame, FocusKeys.ContentPrimary))
            {
                FocusActiveRailItem(RootDestinationOf(entry));
            }
        }

        private void FocusActiveRailItem(NavigationDestination destination)
        {
            if (_railButtons.TryGetValue(destination, out var button))
            {
                button.Focus(FocusState.Keyboard);
            }
        }

        // ---- animation --------------------------------------------------------------------------

        private static Task FadeAsync(UIElement target, double from, double to, TimeSpan duration)
        {
            var completion = new TaskCompletionSource<bool>();
            if (duration <= TimeSpan.Zero)
            {
                target.Opacity = to;
                completion.SetResult(true);
                return completion.Task;
            }

            var animation = new DoubleAnimation
            {
                From = from,
                To = to,
                Duration = new Duration(duration),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            };
            Storyboard.SetTarget(animation, target);
            Storyboard.SetTargetProperty(animation, "Opacity");

            var storyboard = new Storyboard();
            storyboard.Children.Add(animation);
            storyboard.Completed += (s, e) =>
            {
                target.Opacity = to;
                completion.TrySetResult(true);
            };
            storyboard.Begin();
            return completion.Task;
        }
    }
}
