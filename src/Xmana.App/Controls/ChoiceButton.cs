using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;

namespace Xmana.App.Controls
{
    /// <summary>
    /// Button used for navigation-rail items and Settings choices. Look and focus behaviour come from
    /// ChoiceButtonStyle (Theme/Controls.xaml). Accessible name follows <see cref="Label"/>.
    /// </summary>
    public sealed class ChoiceButton : Button
    {
        public static readonly DependencyProperty GlyphProperty = DependencyProperty.Register(
            nameof(Glyph),
            typeof(string),
            typeof(ChoiceButton),
            new PropertyMetadata(string.Empty, (d, e) => ((ChoiceButton)d).UpdateIcon()));

        public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
            nameof(Label),
            typeof(string),
            typeof(ChoiceButton),
            new PropertyMetadata(string.Empty, (d, e) => AutomationProperties.SetName((ChoiceButton)d, e.NewValue as string ?? string.Empty)));

        public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register(
            nameof(IsActive),
            typeof(bool),
            typeof(ChoiceButton),
            new PropertyMetadata(false, (d, e) => ((ChoiceButton)d).UpdateActivation(true)));

        private FrameworkElement _icon;

        public ChoiceButton()
        {
            if (Application.Current != null && Application.Current.Resources.TryGetValue("ChoiceButtonStyle", out var style))
            {
                Style = style as Style;
            }
        }

        /// <summary>Segoe MDL2 Assets glyph; when empty the icon is collapsed.</summary>
        public string Glyph
        {
            get => (string)GetValue(GlyphProperty);
            set => SetValue(GlyphProperty, value);
        }

        public string Label
        {
            get => (string)GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        /// <summary>True for the current section / selected option.</summary>
        public bool IsActive
        {
            get => (bool)GetValue(IsActiveProperty);
            set => SetValue(IsActiveProperty, value);
        }

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            _icon = GetTemplateChild("Icon") as FrameworkElement;
            UpdateIcon();
            UpdateActivation(false);
        }

        private void UpdateIcon()
        {
            if (_icon != null)
            {
                _icon.Visibility = string.IsNullOrEmpty(Glyph) ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        private void UpdateActivation(bool useTransitions)
        {
            VisualStateManager.GoToState(this, IsActive ? "Active" : "Inactive", useTransitions);
        }
    }
}
