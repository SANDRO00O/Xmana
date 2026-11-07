using System;
using System.Threading;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Xmana.App.Services;
using Xmana.Core.Http;
using Xmana.Core.Images;
using Xmana.Core.Models;

namespace Xmana.App.Controls
{
    /// <summary>
    /// Round photo with name and role for cast/crew (PROMPT.md section 29). Like <see cref="VideoCard"/>, the photo loads only
    /// when the card is near the viewport. A card without a staff id is still shown but is not a focus target.
    /// </summary>
    public sealed class StaffCard : Button
    {
        private const int PhotoDecodeWidth = 176;
        private const double LoadDistance = 700;
        private const double ReleaseDistance = 3200;

        public static readonly DependencyProperty MemberProperty = DependencyProperty.Register(
            nameof(Member),
            typeof(StaffMember),
            typeof(StaffCard),
            new PropertyMetadata(null, (d, e) => ((StaffCard)d).OnMemberChanged()));

        private ImageBrush _brush;
        private TextBlock _name;
        private TextBlock _role;
        private FrameworkElement _glyph;
        private CancellationTokenSource _loading;
        private bool _requested;

        public StaffCard()
        {
            if (Application.Current != null && Application.Current.Resources.TryGetValue("StaffCardStyle", out var style))
            {
                Style = style as Style;
            }

            EffectiveViewportChanged += OnEffectiveViewportChanged;
            Unloaded += (s, e) => Release();
        }

        /// <summary>False for a decorative card (e.g. the person header) that must never take focus.</summary>
        public bool IsInteractive { get; set; } = true;

        public StaffMember Member
        {
            get => (StaffMember)GetValue(MemberProperty);
            set => SetValue(MemberProperty, value);
        }

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            _brush = GetTemplateChild("PhotoBrush") as ImageBrush;
            _name = GetTemplateChild("NameText") as TextBlock;
            _role = GetTemplateChild("RoleText") as TextBlock;
            _glyph = GetTemplateChild("PlaceholderGlyph") as FrameworkElement;
            Apply();
        }

        private void OnMemberChanged()
        {
            Release();
            Apply();
        }

        private void Apply()
        {
            var member = Member;
            if (member == null || _name == null)
            {
                return;
            }

            _name.Text = member.Name ?? string.Empty;
            _role.Text = member.Role ?? string.Empty;
            _role.Visibility = string.IsNullOrWhiteSpace(member.Role) ? Visibility.Collapsed : Visibility.Visible;
            IsTabStop = IsInteractive && !string.IsNullOrEmpty(member.Id);
            AutomationProperties.SetName(this, string.IsNullOrWhiteSpace(member.Role) ? _name.Text : _name.Text + ", " + member.Role);
        }

        private void OnEffectiveViewportChanged(FrameworkElement sender, EffectiveViewportChangedEventArgs args)
        {
            var distance = Math.Max(args.BringIntoViewDistanceX, args.BringIntoViewDistanceY);
            if (!_requested && distance <= LoadDistance)
            {
                Request();
            }
            else if (_requested && distance > ReleaseDistance)
            {
                Release();
            }
        }

        private async void Request()
        {
            var member = Member;
            var url = member == null ? null : ImageSelector.For(member);
            if (url == null || _brush == null)
            {
                return;
            }

            _requested = true;
            var cts = new CancellationTokenSource();
            _loading = cts;
            try
            {
                var bitmap = await AppServices.Images.LoadAsync(url, PhotoDecodeWidth, cts.Token, RequestPriority.Secondary);
                if (!cts.IsCancellationRequested && bitmap != null && _brush != null)
                {
                    _brush.ImageSource = bitmap;
                    if (_glyph != null)
                    {
                        _glyph.Visibility = Visibility.Collapsed;
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception)
            {
                // photo problems never crash the UI; the placeholder stays
            }
        }

        private void Release()
        {
            var cts = _loading;
            _loading = null;
            _requested = false;
            if (cts != null)
            {
                cts.Cancel();
            }

            if (_brush != null)
            {
                _brush.ImageSource = null;
            }

            if (_glyph != null)
            {
                _glyph.Visibility = Visibility.Visible;
            }
        }
    }
}
