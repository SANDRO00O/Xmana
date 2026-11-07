using System;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Xmana.Core.Diagnostics;

namespace Xmana.App.Views
{
    /// <summary>
    /// Short branded splash: black background, centered wordmark, subtle fade. It is not a fake
    /// loading screen; the app proceeds as soon as the fade has been seen.
    /// </summary>
    public sealed partial class SplashPage : Page
    {
        public SplashPage()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            try
            {
                FadeIn.Begin();
                await Task.Delay(650);
                if (Frame != null)
                {
                    Frame.Navigate(typeof(ShellPage), null, new SuppressNavigationTransitionInfo());
                    Frame.BackStack.Clear();
                }
            }
            catch (Exception ex)
            {
                Log.Error("Splash", "Navigation to shell failed", ex);
            }
        }
    }
}
