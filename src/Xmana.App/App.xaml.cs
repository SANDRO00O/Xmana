using System;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Xmana.App.Services;
using Xmana.App.Views;
using Xmana.Core.Diagnostics;

namespace Xmana.App
{
    sealed partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            // Xbox defaults to a mouse cursor; controller-first apps request pointer mode only when needed.
            RequiresPointerMode = ApplicationRequiresPointerMode.WhenRequested;

            Suspending += OnSuspending;
            UnhandledException += OnUnhandledException;
        }

        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            if (e.PrelaunchActivated) return;

            var root = Window.Current.Content as Frame;
            if (root == null)
            {
                AppServices.Initialize();

                // Draw edge to edge; layouts keep their own TV safe-area margins.
                ApplicationView.GetForCurrentView().SetDesiredBoundsMode(ApplicationViewBoundsMode.UseCoreWindow);

                root = new Frame();
                Window.Current.Content = root;
                AppServices.ApplyLanguageToWindow(root);
                root.Navigate(typeof(SplashPage));
            }

            Window.Current.Activate();
        }

        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            try
            {
                Log.Info("Lifecycle", "Suspending");
            }
            finally
            {
                deferral.Complete();
            }
        }

        private void OnUnhandledException(object sender, Windows.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            // Never show stack traces to the user; log the type only (URLs/tokens are redacted by Log).
            Log.Error("Unhandled", e.Message ?? "Unhandled exception", e.Exception);
            e.Handled = true;
        }
    }
}
