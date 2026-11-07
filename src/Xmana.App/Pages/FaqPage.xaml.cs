using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Xmana.App.Controls;
using Xmana.App.Focus;
using Xmana.App.Services;
using Xmana.Core.Focus;
using Xmana.Core.Http;
using Xmana.Core.Library;

namespace Xmana.App.Pages
{
    /// <summary>
    /// The public FAQ (PROMPT.md section 28). API.md did not capture the entry schema, so <see cref="FaqReader"/> reads it defensively; when
    /// nothing is readable the screen says the FAQ is unavailable instead of showing raw data. Each question opens its answer in place.
    /// </summary>
    public sealed partial class FaqPage : Page
    {
        private bool _active;

        public FaqPage()
        {
            InitializeComponent();
            XmanaFocus.SetKey(RetryButton, FocusKeys.ContentPrimary);
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _active = true;
            var localization = AppServices.Localization;
            TitleText.Text = localization.Get("Nav_Faq");
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
            Ring.IsActive = true;
            Ring.Visibility = Visibility.Visible;
            Scroller.Visibility = Visibility.Collapsed;
            MessagePanel.Visibility = Visibility.Collapsed;

            System.Collections.Generic.IReadOnlyList<FaqItem> items = null;
            var failed = false;
            try
            {
                var result = await AppServices.Api.GetFaqAsync(Xmana.Core.Http.RequestPriority.Low, CancellationToken.None);
                items = FaqReader.Read(result.Value, localization.CurrentLanguage.IsRightToLeft);
            }
            catch (ApiException)
            {
                failed = true;
            }

            if (!_active)
            {
                return;
            }

            Ring.IsActive = false;
            Ring.Visibility = Visibility.Collapsed;

            if (failed || items == null || items.Count == 0)
            {
                MessageTitle.Text = localization.Get(failed ? "Results_ErrorTitle" : "Faq_UnavailableTitle");
                MessageBody.Text = localization.Get(failed ? "Results_ErrorBody" : "Faq_UnavailableBody");
                RetryButton.Visibility = failed ? Visibility.Visible : Visibility.Collapsed;
                MessagePanel.Visibility = Visibility.Visible;
                return;
            }

            ItemsHost.Children.Clear();
            var first = true;
            foreach (var item in items)
            {
                AddItem(item, first);
                first = false;
            }

            Scroller.Visibility = Visibility.Visible;
        }

        private void AddItem(FaqItem item, bool primary)
        {
            var answer = new TextBlock
            {
                Text = item.Answer ?? string.Empty,
                Style = (Style)Application.Current.Resources["XmanaSecondaryTextStyle"],
                FontSize = 16,
                Margin = new Thickness(16, 2, 8, 10),
                Visibility = Visibility.Collapsed,
            };

            var question = new ChoiceButton { Label = item.Question, HorizontalAlignment = HorizontalAlignment.Stretch };
            if (primary)
            {
                XmanaFocus.SetKey(question, FocusKeys.ContentPrimary);
            }

            question.Click += (s, e) =>
            {
                if (!string.IsNullOrEmpty(item.Answer))
                {
                    answer.Visibility = answer.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
                    question.IsActive = answer.Visibility == Visibility.Visible;
                }
            };

            ItemsHost.Children.Add(question);
            ItemsHost.Children.Add(answer);
        }

        private async void OnRetryClick(object sender, RoutedEventArgs e)
        {
            try
            {
                await LoadAsync();
            }
            catch (Exception)
            {
            }
        }
    }
}
