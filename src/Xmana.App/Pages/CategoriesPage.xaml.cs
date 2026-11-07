using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Xmana.App.Controls;
using Xmana.App.Focus;
using Xmana.App.Services;
using Xmana.Core.Discovery;
using Xmana.Core.Focus;
using Xmana.Core.Http;

namespace Xmana.App.Pages
{
    /// <summary>Category list built from the actual API metadata: main categories first, then the others (PROMPT.md section 22).</summary>
    public sealed partial class CategoriesPage : Page
    {
        private const int Columns = 3;
        private bool _active;

        public CategoriesPage()
        {
            InitializeComponent();
            XmanaFocus.SetKey(RetryButton, FocusKeys.ContentPrimary);
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _active = true;
            var localization = AppServices.Localization;
            TitleText.Text = localization.Get("Nav_Categories");
            RetryButton.Label = localization.Get("Results_Retry");
            await LoadAsync();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            _active = false;
        }

        private async Task LoadAsync()
        {
            var localization = AppServices.Localization;
            Skeleton.Visibility = Visibility.Visible;
            Scroller.Visibility = Visibility.Collapsed;
            MessagePanel.Visibility = Visibility.Collapsed;

            CategoryCatalog catalog = null;
            try
            {
                catalog = await AppServices.Categories.GetAsync(CancellationToken.None);
            }
            catch (ApiException)
            {
                // shown as the error state below
            }

            if (!_active)
            {
                return;
            }

            Skeleton.Visibility = Visibility.Collapsed;
            if (catalog == null || catalog.All.Count == 0)
            {
                MessageTitle.Text = localization.Get(catalog == null ? "Results_ErrorTitle" : "Results_EmptyTitle");
                MessageBody.Text = localization.Get(catalog == null ? "Results_ErrorBody" : "Results_EmptyBody");
                RetryButton.Visibility = catalog == null ? Visibility.Visible : Visibility.Collapsed;
                MessagePanel.Visibility = Visibility.Visible;
                return;
            }

            var preferArabic = localization.CurrentLanguage.IsRightToLeft;
            var main = catalog.All.Where(c => c.IsMain).ToList();
            var more = catalog.All.Where(c => !c.IsMain).ToList();

            MainHeader.Text = localization.Get("Categories_Main");
            MoreHeader.Text = localization.Get("Categories_More");
            MainHeader.Visibility = main.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            MoreHeader.Visibility = more.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

            Fill(MainRows, main, preferArabic, true);
            Fill(MoreRows, more, preferArabic, false);
            Scroller.Visibility = Visibility.Visible;
        }

        private void Fill(StackPanel host, IReadOnlyList<CategoryEntry> entries, bool preferArabic, bool first)
        {
            host.Children.Clear();
            StackPanel row = null;
            var primaryAssigned = !first;
            foreach (var entry in entries)
            {
                if (row == null || row.Children.Count >= Columns)
                {
                    row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                    host.Children.Add(row);
                }

                var button = new ChoiceButton { Label = entry.GetTitle(preferArabic), Width = 190, Tag = entry.Id };
                XmanaFocus.SetKey(button, "category." + entry.Id);
                if (!primaryAssigned)
                {
                    XmanaFocus.SetKey(button, FocusKeys.ContentPrimary);
                    primaryAssigned = true;
                }

                button.Click += OnCategoryClick;
                row.Children.Add(button);
            }
        }

        private void OnCategoryClick(object sender, RoutedEventArgs e)
        {
            var id = (sender as FrameworkElement)?.Tag as string;
            if (id != null)
            {
                Navigator.OpenCategory(id);
            }
        }

        private async void OnRetryClick(object sender, RoutedEventArgs e)
        {
            try
            {
                await LoadAsync();
            }
            catch (Exception)
            {
                // the page already shows its error state
            }
        }
    }
}
