using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Shapes;
using Xmana.App.Focus;
using Xmana.App.Services;
using Xmana.Core.Focus;
using Xmana.Core.Http;
using Xmana.Core.Images;
using Xmana.Core.Models;

namespace Xmana.App.Controls
{
    public sealed partial class HeroView : UserControl
    {
        private const int BackdropDecodeWidth = 1280;

        private IReadOnlyList<VideoSummary> _items = new VideoSummary[0];
        private int _index;
        private CancellationTokenSource _loading;

        public HeroView()
        {
            InitializeComponent();
            DetailsButton.Glyph = "\uE946";
            XmanaFocus.SetKey(DetailsButton, FocusKeys.ContentPrimary);
            Loaded += (s, e) => ApplyChrome();
            Unloaded += (s, e) => CancelLoading();
        }

        public event EventHandler<VideoSummary> Activated;

        public VideoSummary Current => _index >= 0 && _index < _items.Count ? _items[_index] : null;

        /// <summary>Replaces the banner list. Nothing changes (no flicker, no focus loss) when the list is identical.</summary>
        public void SetItems(IReadOnlyList<VideoSummary> items)
        {
            items = items ?? new VideoSummary[0];
            if (items.Select(i => i.Id).SequenceEqual(_items.Select(i => i.Id)))
            {
                return;
            }

            var currentId = Current?.Id;
            _items = items;
            _index = Math.Max(0, items.ToList().FindIndex(i => i.Id == currentId));
            BuildDots();
            ApplyChrome();
            Show();
        }

        /// <summary>Re-reads language-dependent text (called after a language change).</summary>
        public void Refresh()
        {
            ApplyChrome();
            Show();
        }

        private void ApplyChrome()
        {
            var localization = AppServices.Localization;
            DetailsButton.Label = localization.Get("Home_Details");

            var rtl = localization.CurrentLanguage.IsRightToLeft;
            PreviousButton.Glyph = rtl ? "\uE76C" : "\uE76B";
            NextButton.Glyph = rtl ? "\uE76B" : "\uE76C";
            AutomationProperties.SetName(PreviousButton, localization.Get("Home_PreviousBanner"));
            AutomationProperties.SetName(NextButton, localization.Get("Home_NextBanner"));

            // Brushes do not mirror with FlowDirection, so the readability gradient is flipped by hand.
            HorizontalShadeBrush.StartPoint = new Windows.Foundation.Point(rtl ? 1 : 0, 0.5);
            HorizontalShadeBrush.EndPoint = new Windows.Foundation.Point(rtl ? 0 : 1, 0.5);

            var multiple = _items.Count > 1;
            PreviousButton.Visibility = multiple ? Visibility.Visible : Visibility.Collapsed;
            NextButton.Visibility = multiple ? Visibility.Visible : Visibility.Collapsed;
            Dots.Visibility = multiple ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Show()
        {
            var video = Current;
            if (video == null)
            {
                return;
            }

            var preferArabic = AppServices.Localization.CurrentLanguage.IsRightToLeft;
            TitleText.Text = video.GetTitle(preferArabic) ?? string.Empty;

            var meta = BuildMeta(video, preferArabic);
            MetaText.Text = meta;
            MetaText.Visibility = meta.Length == 0 ? Visibility.Collapsed : Visibility.Visible;

            var description = video.GetDescription(preferArabic);
            DescriptionText.Text = description ?? string.Empty;
            DescriptionText.Visibility = description == null ? Visibility.Collapsed : Visibility.Visible;

            AutomationProperties.SetName(DetailsButton, DetailsButton.Label + ", " + TitleText.Text);
            UpdateDots();
            LoadBackdrop(video);
        }

        /// <summary>Year, rating and up to three categories, only from values the API supplied.</summary>
        private static string BuildMeta(VideoSummary video, bool preferArabic)
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(video.Year) && video.Year != "0")
            {
                parts.Add(video.Year);
            }

            if (video.Stars.HasValue && video.Stars.Value > 0)
            {
                parts.Add("\u2605 " + video.Stars.Value.ToString("0.0", CultureInfo.InvariantCulture));
            }

            var categories = video.Categories
                .Select(c => preferArabic ? (c.ArTitle ?? c.EnTitle) : (c.EnTitle ?? c.ArTitle))
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Take(3);
            var joined = string.Join(", ", categories);
            if (joined.Length > 0)
            {
                parts.Add(joined);
            }

            return string.Join("  \u2022  ", parts);
        }

        private async void LoadBackdrop(VideoSummary video)
        {
            CancelLoading();
            var url = ImageSelector.For(video, ImageRole.Banner);
            Backdrop.Source = null;
            Backdrop.Opacity = 0;
            if (url == null)
            {
                return;
            }

            var cts = new CancellationTokenSource();
            _loading = cts;
            try
            {
                var bitmap = await AppServices.Images.LoadAsync(url, BackdropDecodeWidth, cts.Token, RequestPriority.Hero);
                if (cts.IsCancellationRequested || bitmap == null)
                {
                    return;
                }

                Backdrop.Source = bitmap;
                FadeIn();
            }
            catch (OperationCanceledException)
            {
                // moved to another banner or left the page
            }
            catch (Exception)
            {
                // artwork problems never crash the UI; the plain surface stays
            }
        }

        private void FadeIn()
        {
            var duration = AppServices.Motion.Duration(260);
            if (duration <= TimeSpan.Zero)
            {
                Backdrop.Opacity = 1;
                return;
            }

            var animation = new DoubleAnimation { From = 0, To = 1, Duration = new Duration(duration), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            Storyboard.SetTarget(animation, Backdrop);
            Storyboard.SetTargetProperty(animation, "Opacity");
            var storyboard = new Storyboard();
            storyboard.Children.Add(animation);
            storyboard.Completed += (s, e) => Backdrop.Opacity = 1;
            storyboard.Begin();
        }

        private void CancelLoading()
        {
            var cts = _loading;
            _loading = null;
            if (cts != null)
            {
                cts.Cancel();
            }
        }

        private void BuildDots()
        {
            Dots.Children.Clear();
            if (_items.Count < 2)
            {
                return;
            }

            for (var i = 0; i < _items.Count && i < 12; i++)
            {
                Dots.Children.Add(new Ellipse
                {
                    Width = 7,
                    Height = 7,
                    Fill = (Brush)Application.Current.Resources["XmanaTextPrimaryBrush"],
                    Opacity = 0.3,
                });
            }
        }

        private void UpdateDots()
        {
            for (var i = 0; i < Dots.Children.Count; i++)
            {
                Dots.Children[i].Opacity = i == _index ? 1 : 0.3;
            }
        }

        private void Move(int delta)
        {
            if (_items.Count < 2)
            {
                return;
            }

            _index = (_index + delta + _items.Count) % _items.Count;
            Show();
        }

        private void OnPreviousClick(object sender, RoutedEventArgs e) => Move(-1);

        private void OnNextClick(object sender, RoutedEventArgs e) => Move(1);

        private void OnDetailsClick(object sender, RoutedEventArgs e)
        {
            var video = Current;
            if (video != null)
            {
                Activated?.Invoke(this, video);
            }
        }
    }
}
