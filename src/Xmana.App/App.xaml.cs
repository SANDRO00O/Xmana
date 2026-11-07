using System;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.System.Profile;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Xmana.App.Services;
using Xmana.App.Shell;

namespace Xmana.App
{
    sealed partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            // Enables XY (D-pad / stick) focus navigation instead of the Xbox "mouse mode" cursor.
            // Must be set before the first window content is created.
            RequiresPointerMode = ApplicationRequiresPointerMode.WhenRequested;

            Suspending += OnSuspending;
        }

        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            if (!(Window.Current.Content is ShellPage))
            {
                AppServices.Initialize();
                ConfigureTelevisionDisplay();
                Window.Current.Content = new ShellPage();
                RunStartupHousekeeping();
            }

            Window.Current.Activate();
        }

        /// <summary>
        /// Prunes cache files that are old enough that nothing would still trust them anyway (PROMPT.md section 51 Caching). Runs once per
        /// launch, off the UI thread, and never blocks startup: a failure here is invisible to the user.
        /// </summary>
        private static async void RunStartupHousekeeping()
        {
            try
            {
                var now = DateTime.UtcNow;
                await AppServices.ApiCache.PurgeOlderThanAsync(TimeSpan.FromDays(31), now);
                await AppServices.ImageDiskCache.PurgeOlderThanAsync(TimeSpan.FromDays(31), now);
            }
            catch (Exception)
            {
                // best-effort housekeeping only
            }
        }

        private static void ConfigureTelevisionDisplay()
        {
            // On Xbox, draw to the screen edges and apply the TV safe area ourselves
            // (XmanaSafeAreaThickness = 48/27 effective px) instead of the system's automatic inset,
            // so later full-bleed artwork can extend behind the safe area.
            if (AnalyticsInfo.VersionInfo.DeviceFamily == "Windows.Xbox")
            {
                ApplicationView.GetForCurrentView().SetDesiredBoundsMode(ApplicationViewBoundsMode.UseCoreWindow);
            }
        }

        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            // Milestone 1 has no state that must survive suspension. Later milestones flush local
            // progress/cache here (PROMPT.md section 61).
            var deferral = e.SuspendingOperation.GetDeferral();
            try
            {
                // Save where the viewer was before the system may terminate the app.
                AppServices.ActivePlayback?.Flush();
            }
            finally
            {
                deferral.Complete();
            }
        }
    }
}
