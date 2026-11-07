using Windows.UI.Xaml;
using Windows.UI.Xaml.Input;

namespace Xmana.App.FocusManagement
{
    /// <summary>Central helpers for controller focus so pages never end up with focus lost.</summary>
    internal static class FocusService
    {
        /// <summary>Moves focus to the first focusable element below <paramref name="root"/>.</summary>
        public static bool FocusFirst(DependencyObject root)
        {
            var element = FocusManager.FindFirstFocusableElement(root) as UIElement;
            return element != null && element.Focus(FocusState.Programmatic);
        }

        /// <summary>Enables deterministic D-pad (XY) navigation inside a container.</summary>
        public static void EnableDirectionalNavigation(UIElement container)
        {
            container.XYFocusKeyboardNavigation = XYFocusKeyboardNavigationMode.Enabled;
        }
    }
}
