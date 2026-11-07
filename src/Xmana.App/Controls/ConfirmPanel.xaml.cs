using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Xmana.App.Services;

namespace Xmana.App.Controls
{
    public sealed partial class ConfirmPanel : UserControl
    {
        private Action _onConfirm;
        private Control _returnFocus;

        public ConfirmPanel()
        {
            InitializeComponent();
        }

        public bool IsOpen => Visibility == Visibility.Visible;

        /// <summary>Shows the question. <paramref name="onConfirm"/> runs only if the viewer chooses the destructive option.</summary>
        public void Show(string title, string body, string confirmLabel, Action onConfirm)
        {
            var localization = AppServices.Localization;
            TitleText.Text = title;
            BodyText.Text = body;
            CancelButton.Label = localization.Get("Confirm_Cancel");
            ConfirmButton.Label = confirmLabel;
            _onConfirm = onConfirm;
            _returnFocus = FocusManager.GetFocusedElement() as Control;
            Visibility = Visibility.Visible;
            CancelButton.Focus(FocusState.Keyboard);

            // Modal focus trap (PROMPT.md section 52): while open, focus may never land on anything outside this panel,
            // even via D-pad/gamepad XY navigation into elements the panel's own scrim does not visually cover.
            FocusManager.GettingFocus += OnGettingFocus;
        }

        /// <summary>Closes without confirming (the B button).</summary>
        public void Close()
        {
            FocusManager.GettingFocus -= OnGettingFocus;
            Visibility = Visibility.Collapsed;
            _onConfirm = null;
            _returnFocus?.Focus(FocusState.Keyboard);
            _returnFocus = null;
        }

        private void OnGettingFocus(object sender, GettingFocusEventArgs e)
        {
            if (!IsDescendant(e.NewFocusedElement as DependencyObject))
            {
                e.TryCancel();
            }
        }

        private bool IsDescendant(DependencyObject element)
        {
            while (element != null)
            {
                if (ReferenceEquals(element, this))
                {
                    return true;
                }

                element = VisualTreeHelper.GetParent(element);
            }

            return false;
        }

        private void OnCancelClick(object sender, RoutedEventArgs e) => Close();

        private void OnConfirmClick(object sender, RoutedEventArgs e)
        {
            var action = _onConfirm;
            Close();
            action?.Invoke();
        }
    }
}
