using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Xmana.App.Focus
{
    /// <summary>
    /// Central focus helpers. Elements that should be restorable are tagged with a stable string key
    /// (XmanaFocus.Key, see Xmana.Core.Focus.FocusKeys). Navigation saves the key of the focused
    /// element and restores it later by key - never by element reference - so it survives page
    /// re-creation (back navigation, language switch).
    /// </summary>
    public static class XmanaFocus
    {
        public static readonly DependencyProperty KeyProperty = DependencyProperty.RegisterAttached(
            "Key",
            typeof(string),
            typeof(XmanaFocus),
            new PropertyMetadata(null));

        public static string GetKey(DependencyObject element) => (string)element.GetValue(KeyProperty);

        public static void SetKey(DependencyObject element, string value) => element.SetValue(KeyProperty, value);

        /// <summary>Key of the focused element (or its nearest tagged ancestor); null if none.</summary>
        public static string CurrentKey()
        {
            var node = FocusManager.GetFocusedElement() as DependencyObject;
            while (node != null)
            {
                var key = GetKey(node);
                if (!string.IsNullOrEmpty(key))
                {
                    return key;
                }

                node = VisualTreeHelper.GetParent(node);
            }

            return null;
        }

        /// <summary>True when the focused element exists and is still inside <paramref name="root"/>.</summary>
        public static bool HasValidFocus(DependencyObject root)
        {
            var node = FocusManager.GetFocusedElement() as DependencyObject;
            while (node != null)
            {
                if (ReferenceEquals(node, root))
                {
                    return true;
                }

                node = VisualTreeHelper.GetParent(node);
            }

            return false;
        }

        /// <summary>Focuses the first enabled, visible, tab-stop control tagged with <paramref name="key"/>.</summary>
        public static bool TryFocus(DependencyObject root, string key)
        {
            if (root == null || string.IsNullOrEmpty(key))
            {
                return false;
            }

            var target = Find(root, key);
            // Keyboard focus state makes the gamepad/keyboard focus visuals appear.
            return target != null && target.Focus(FocusState.Keyboard);
        }

        private static Control Find(DependencyObject node, string key)
        {
            if (node is Control control
                && GetKey(control) == key
                && control.IsEnabled
                && control.IsTabStop
                && control.Visibility == Visibility.Visible)
            {
                return control;
            }

            var count = VisualTreeHelper.GetChildrenCount(node);
            for (var i = 0; i < count; i++)
            {
                var found = Find(VisualTreeHelper.GetChild(node, i), key);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
