using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Xmana.App.Services;

namespace Xmana.App.Pages
{
    public sealed partial class PlaceholderPage : Page
    {
        public PlaceholderPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            var entry = (e.Parameter as PageArgs)?.Entry;
            var localization = AppServices.Localization;
            TitleText.Text = entry != null ? localization.Get("Nav_" + entry.Destination) : string.Empty;
            BodyText.Text = localization.Get("Placeholder_ComingSoon");
        }
    }
}
