using System;
using System.Threading;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Xmana.App.Services;
using Xmana.Core.Details;
using Xmana.Core.Discovery;

namespace Xmana.App.Pages
{
    /// <summary>A cast or crew member and the titles the API associates with them (PROMPT.md section 29).</summary>
    public sealed partial class StaffPage : Page
    {
        private StaffController _controller;
        private RenderScheduler _renderer;

        public StaffPage()
        {
            InitializeComponent();
            Results.VideoInvoked += (s, video) => Navigator.OpenDetails(video);
            Results.RetryRequested += async (s, e) => await LoadAsync();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            var entry = (e.Parameter as PageArgs)?.Entry;
            var id = entry?.Parameter;
            if (entry == null || string.IsNullOrWhiteSpace(id))
            {
                return;
            }

            var localization = AppServices.Localization;
            TitlesHeader.Text = localization.Get("Staff_Titles");
            Results.EmptyTitle = localization.Get("Staff_EmptyTitle");
            Results.EmptyBody = localization.Get("Staff_EmptyBody");

            var existing = PageSessions.TryGet<StaffController>(entry.Id);
            _controller = existing ?? PageSessions.GetOrCreate(
                entry.Id,
                () => new StaffController(AppServices.DetailsSource, id, () => AppServices.Localization.CurrentLanguage.Code, AppServices.Logger));
            _renderer = new RenderScheduler(Dispatcher, Render);
            _controller.Changed += OnChanged;

            Render();
            if (existing == null || _controller.State == ResultsState.Idle)
            {
                await LoadAsync();
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            _renderer?.Stop();
            if (_controller != null)
            {
                _controller.Changed -= OnChanged;
            }
        }

        private async System.Threading.Tasks.Task LoadAsync()
        {
            try
            {
                await _controller.LoadAsync(CancellationToken.None);
            }
            catch (Exception)
            {
            }
        }

        private void OnChanged(object sender, EventArgs e) => _renderer?.Request();

        private void Render()
        {
            var staff = _controller.Staff;
            PersonCard.Member = staff;
            Header.Visibility = staff != null ? Visibility.Visible : Visibility.Collapsed;

            var snapshot = _controller.Snapshot;
            TitlesHeader.Visibility = snapshot.State == ResultsState.Content ? Visibility.Visible : Visibility.Collapsed;
            Results.Render(snapshot, "staff");
        }
    }
}
