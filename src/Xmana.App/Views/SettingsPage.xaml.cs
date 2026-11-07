using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Xmana.App.Services;
using Xmana.Core;
using Xmana.Core.Localization;

namespace Xmana.App.Views
{
    public sealed partial class SettingsPage : Page
    {
        public SettingsPage()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            VersionText.Text = AppInfo.Version;
            SyncSelection();
        }

        private void SyncSelection()
        {
            var isArabic = AppServices.Localization.CurrentLanguage == LanguageCodes.Arabic;
            ArabicChoice.IsChecked = isArabic;
            EnglishChoice.IsChecked = !isArabic;
        }

        private void OnLanguageClick(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton { Tag: string code })
            {
                AppServices.ChangeLanguage(code);
                SyncSelection();
            }
        }
    }
}
