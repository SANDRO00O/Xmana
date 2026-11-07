using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Xmana.App.Navigation;

namespace Xmana.App.Views
{
    /// <summary>Generic section host. Each top-level section replaces its content in later milestones.</summary>
    public sealed partial class SectionPage : Page
    {
        public SectionPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            var item = NavItem.FindById(e.Parameter as string) ?? NavItem.Home;
            Loc.SetKey(TitleText, item.LabelKey);
        }
    }
}
