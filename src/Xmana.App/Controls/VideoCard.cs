using System;
using System.Globalization;
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
    /// Poster card used by every rail and grid. Focus visuals come from VideoCardStyle (Theme/Controls.xaml).
    /// The poster is loaded lazily: only when the card is near the viewport (EffectiveViewportChanged), cancelled when it
    /// is unloaded, and released again when it scrolls far away, so long rails stay light on weak hardware.
    /// </summary>
    public sealed class VideoCard : Button
    {
        public const int PosterDecodeWidth = 320;

        private const double LoadDistance = 700;
        private const double ReleaseDistance = 3200;

        public static readonly DependencyProperty VideoProperty = DependencyProperty.Register(
            nameof(Video),
            typeof(VideoSummary),
            typeof(VideoCard),
            new PropertyMetadata(null, (d, e) => ((VideoCard)d).OnVideoChanged()));

        public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(
            nameof(Progress),
            typeof(double?),
            typeof(VideoCard),
            new PropertyMetadata(null, (d, e) => ((VideoCard)d).ApplyProgress()));

        private const double PosterWidth = 152;

        private FrameworkElement _progressTrack;
        private FrameworkElement _progressFill;
        private ImageBrush _brush;
        private TextBlock _title;
        private TextBlock _caption;
        private FrameworkElement _placeholderGlyph;
        private CancellationTokenSource _loading;
        private bool _imageRequested;

        public VideoCard()
        {
            if (Application.Current != null && Application.Current.Resources.TryGetValue("VideoCardStyle", out var style))
            {
                Style = style as Style;
            }

            EffectiveViewportChanged += OnEffectiveViewportChanged;
            Unloaded += (s, e) => ReleaseImage();
        }

        /// <summary>Watched fraction (0..1) shown as a thin bar on the poster; null hides it.</summary>
        public double? Progress
        {
            get => (double?)GetValue(ProgressProperty);
            set => SetValue(ProgressProperty, value);
        }

        public VideoSummary Video
        {
            get => (VideoSummary)GetValue(VideoProperty);
            set => SetValue(VideoProperty, value);
        }

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            _brush = GetTemplateChild("PosterBrush") as ImageBrush;
            _title = GetTemplateChild("TitleText") as TextBlock;
            _caption = GetTemplateChild("CaptionText") as TextBlock;
            _placeholderGlyph = GetTemplateChild("PlaceholderGlyph") as FrameworkElement;
            _progressTrack = GetTemplateChild("ProgressTrack") as FrameworkElement;
            _progressFill = GetTemplateChild("ProgressFill") as FrameworkElement;
            ApplyVideo();
            ApplyProgress();
        }

        private void ApplyProgress()
        {
            if (_progressTrack == null || _progressFill == null)
            {
                return;
            }

            var value = Progress;
            if (!value.HasValue || value.Value <= 0)
            {
                _progressTrack.Visibility = Visibility.Collapsed;
                return;
            }

            _progressFill.Width = PosterWidth * Math.Min(1, value.Value);
            _progressTrack.Visibility = Visibility.Visible;
        }

        private void OnVideoChanged()
        {
            ReleaseImage();
            ApplyVideo();
        }

        private void ApplyVideo()
        {
            var video = Video;
            if (video == null || _title == null)
            {
                return;
            }

            var preferArabic = AppServices.Localization.CurrentLanguage.IsRightToLeft;
            var title = video.GetTitle(preferArabic) ?? string.Empty;
            _title.Text = title;

            var caption = BuildCaption(video);
            _caption.Text = caption;
            _caption.Visibility = caption.Length == 0 ? Visibility.Collapsed : Visibility.Visible;

            AutomationProperties.SetName(this, caption.Length == 0 ? title : title + ", " + caption);
        }

        /// <summary>Year and rating exactly as the API provides them; nothing is shown for missing values.</summary>
        internal static string BuildCaption(VideoSummary video)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (video.Season.HasValue && video.EpisodeNumber.HasValue)
            {
                parts.Add(string.Format(CultureInfo.CurrentCulture, AppServices.Localization.Get("Card_EpisodeShort"), video.Season.Value, video.EpisodeNumber.Value));
            }

            if (!string.IsNullOrWhiteSpace(video.Year) && video.Year != "0")
            {
                parts.Add(video.Year);
            }

            if (video.Stars.HasValue && video.Stars.Value > 0)
            {
                parts.Add("\u2605 " + video.Stars.Value.ToString("0.0", CultureInfo.InvariantCulture));
            }

            return string.Join("  ", parts);
        }

        private void OnEffectiveViewportChanged(FrameworkElement sender, EffectiveViewportChangedEventArgs args)
        {
            var distance = Math.Max(args.BringIntoViewDistanceX, args.BringIntoViewDistanceY);
            if (!_imageRequested && distance <= LoadDistance)
            {
                RequestImage();
            }
            else if (_imageRequested && distance > ReleaseDistance)
            {
                ReleaseImage();
            }
        }

        private async void RequestImage()
        {
            var video = Video;
            if (video == null || _brush == null)
            {
                return;
            }

            var url = ImageSelector.For(video, ImageRole.MediumThumbnail);
            if (url == null)
            {
                return;
            }

            _imageRequested = true;
            var cts = new CancellationTokenSource();
            _loading = cts;
            try
            {
                var bitmap = await AppServices.Images.LoadAsync(url, PosterDecodeWidth, cts.Token, RequestPriority.Primary);
                if (!cts.IsCancellationRequested && bitmap != null && _brush != null)
                {
                    _brush.ImageSource = bitmap;
                    if (_placeholderGlyph != null)
                    {
                        _placeholderGlyph.Visibility = Visibility.Collapsed;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // scrolled away or unloaded
            }
            catch (Exception)
            {
                // an image problem must never crash the UI; the placeholder simply stays
            }
        }

        private void ReleaseImage()
        {
            var cts = _loading;
            _loading = null;
            _imageRequested = false;
            if (cts != null)
            {
                cts.Cancel();
            }

            if (_brush != null)
            {
                _brush.ImageSource = null;
            }

            if (_placeholderGlyph != null)
            {
                _placeholderGlyph.Visibility = Visibility.Visible;
            }
        }
    }
}
