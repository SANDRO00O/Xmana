using System;
using System.Collections.Generic;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

namespace Xmana.App.Navigation
{
    /// <summary>
    /// Wraps the content Frame. Back behaviour is deterministic (B = one step back) and the focused
    /// control of the page we left is restored when returning to it (PROMPT.md section 51/52).
    /// </summary>
    internal sealed class NavigationService
    {
        private readonly Frame _frame;
        private readonly Dictionary<int, WeakReference<Control>> _focusMemory = new Dictionary<int, WeakReference<Control>>();

        public NavigationService(Frame frame)
        {
            _frame = frame;
            _frame.Navigated += OnNavigated;
        }

        public bool CanGoBack => _frame.CanGoBack;

        /// <summary>Switches top-level section. Sections are roots, so the back stack is cleared.</summary>
        public void NavigateToSection(NavItem item)
        {
            _frame.Navigate(item.PageType, item.Id, new EntranceNavigationTransitionInfo());
            _frame.BackStack.Clear();
            _focusMemory.Clear();
        }

        /// <summary>Pushes a page (details, lists, ...) remembering where focus was.</summary>
        public bool Navigate(Type pageType, object? parameter = null)
        {
            RememberFocus();
            return _frame.Navigate(pageType, parameter, new DrillInNavigationTransitionInfo());
        }

        public bool GoBack()
        {
            if (!_frame.CanGoBack) return false;
            _frame.GoBack();
            return true;
        }

        private void RememberFocus()
        {
            if (FocusManager.GetFocusedElement() is Control focused)
            {
                _focusMemory[_frame.BackStackDepth] = new WeakReference<Control>(focused);
            }
        }

        private async void OnNavigated(object sender, NavigationEventArgs e)
        {
            if (e.NavigationMode != NavigationMode.Back) return;

            if (_focusMemory.TryGetValue(_frame.BackStackDepth, out var weak) &&
                weak.TryGetTarget(out var control))
            {
                _focusMemory.Remove(_frame.BackStackDepth);
                try
                {
                    // Wait for the restored page to be laid out before moving focus.
                    await _frame.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                    {
                        control.Focus(FocusState.Programmatic);
                    });
                }
                catch (Exception)
                {
                    // Focus restoration is best-effort.
                }
            }
        }
    }
}
