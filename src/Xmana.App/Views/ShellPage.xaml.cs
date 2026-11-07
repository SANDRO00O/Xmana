using System;
using System.Collections.Generic;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Xmana.App.FocusManagement;
using Xmana.App.Navigation;

namespace Xmana.App.Views
{
    /// <summary>
    /// App shell: top tab navigation + content frame. B goes back one step (or to Home from a root
    /// section); LB/RB switch sections. Layout direction follows the UI language automatically.
    /// </summary>
    public sealed partial class ShellPage : Page
    {
        private readonly List<RadioButton> _tabs = new List<RadioButton>();
        private NavigationService? _navigation;
        private NavItem _current = NavItem.Home;

        public ShellPage()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _navigation = new NavigationService(ContentFrame);

            FocusService.EnableDirectionalNavigation(NavPanel);
            BuildTabs();

            SystemNavigationManager.GetForCurrentView().BackRequested += OnBackRequested;
            Window.Current.CoreWindow.KeyDown += OnCoreWindowKeyDown;

            ShowSection(NavItem.Home, focusTab: true);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            SystemNavigationManager.GetForCurrentView().BackRequested -= OnBackRequested;
            Window.Current.CoreWindow.KeyDown -= OnCoreWindowKeyDown;
        }

        private void BuildTabs()
        {
            if (_tabs.Count > 0) return;

            var style = (Style)Application.Current.Resources["NavTabButtonStyle"];
            foreach (var item in NavItem.All)
            {
                var tab = new RadioButton
                {
                    Style = style,
                    GroupName = "XmanaNav",
                    Tag = item,
                };
                Loc.SetKey(tab, item.LabelKey);
                tab.Click += OnTabClick;
                _tabs.Add(tab);
                NavPanel.Children.Add(tab);
            }
        }

        private void OnTabClick(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton { Tag: NavItem item } && !ReferenceEquals(item, _current))
            {
                ShowSection(item, focusTab: false);
            }
        }

        private void ShowSection(NavItem item, bool focusTab)
        {
            _current = item;
            var index = NavItem.IndexOf(item);
            if (index >= 0 && index < _tabs.Count)
            {
                _tabs[index].IsChecked = true;
                if (focusTab) _tabs[index].Focus(FocusState.Programmatic);
            }
            _navigation?.NavigateToSection(item);
        }

        private void OnBackRequested(object? sender, BackRequestedEventArgs e)
        {
            if (_navigation != null && _navigation.CanGoBack)
            {
                _navigation.GoBack();
                e.Handled = true;
                return;
            }

            // Deterministic root behaviour: from any section go to Home first; from Home let the system exit.
            if (!ReferenceEquals(_current, NavItem.Home))
            {
                ShowSection(NavItem.Home, focusTab: true);
                e.Handled = true;
            }
        }

        private void OnCoreWindowKeyDown(CoreWindow sender, KeyEventArgs args)
        {
            int step;
            switch (args.VirtualKey)
            {
                case VirtualKey.GamepadLeftShoulder:
                    step = -1;
                    break;
                case VirtualKey.GamepadRightShoulder:
                    step = 1;
                    break;
                default:
                    return;
            }

            var index = NavItem.IndexOf(_current) + step;
            if (index < 0 || index >= NavItem.All.Count) return;

            ShowSection(NavItem.All[index], focusTab: true);
            args.Handled = true;
        }
    }
}
