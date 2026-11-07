using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.System;
using Windows.System.Display;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Xmana.App.Controls;
using Xmana.App.Focus;
using Xmana.App.Services;
using Xmana.App.Shell;
using Xmana.Core.Caching;
using Xmana.Core.Focus;
using Xmana.Core.Http;
using Xmana.Core.Models;
using Xmana.Core.Playback;

namespace Xmana.App.Pages
{
    /// <summary>
    /// The full-screen player (PROMPT.md sections 39-47). All rules live in <see cref="PlaybackSession"/>; this page draws its state
    /// and maps the gamepad:
    /// controls hidden - A play/pause, Left/Right seek (accelerating), Up/Down show the controls, Menu shows them;
    /// controls shown - D-pad moves between buttons, A activates, shoulders still seek;
    /// B closes the options, hides the controls, cancels a next-episode offer, and only then leaves.
    /// Stream addresses never reach this page: it only sees qualities, tracks and states.
    /// </summary>
    public sealed partial class PlayerPage : Page, IBackHandler
    {
        private static readonly TimeSpan ControlsIdle = TimeSpan.FromSeconds(4.5);
        private static readonly TimeSpan BarIdle = TimeSpan.FromSeconds(2.5);

        private MediaPlayerEngine _engine;
        private PlaybackSession _session;
        private RenderScheduler _renderer;
        private DispatcherTimer _timer;
        private DisplayRequest _display;
        private string _videoId;
        private bool _active;
        private bool _displayActive;
        private bool _controlsVisible;
        private bool _optionsOpen;
        private DateTime _lastInput = DateTime.UtcNow;
        private DateTime _barUntil = DateTime.MinValue;
        private DateTime _noticeUntil = DateTime.MinValue;
        private PlayerFailure? _staticFailure;
        private int _panelShown;
        private int? _notifiedFallback;

        public PlayerPage()
        {
            InitializeComponent();
            Loaded += (s, e) => InputSink.Focus(FocusState.Keyboard);
        }

        // ---- lifecycle --------------------------------------------------------------------------

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _videoId = (e.Parameter as PageArgs)?.Entry?.Parameter;
            _active = true;
            _renderer = new RenderScheduler(Dispatcher, Render);

            ApplyStaticText();
            _engine = new MediaPlayerEngine();
            Video.SetMediaPlayer(_engine.Player);

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _timer.Tick += OnTimerTick;
            _timer.Start();

            Render();
            if (string.IsNullOrWhiteSpace(_videoId))
            {
                ShowStaticFailure(PlayerFailure.NoSource);
                return;
            }

            InitializeAsync();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            _active = false;
            _renderer?.Stop();
            _timer?.Stop();
            ReleaseDisplay();
            if (_optionsOpen)
            {
                FocusManager.GettingFocus -= OnOptionsGettingFocus;
            }

            if (_session != null)
            {
                _session.Changed -= OnSessionChanged;
                _session.AdvanceRequested -= OnAdvanceRequested;
                _session.Stop();
                _session.Dispose();
                if (ReferenceEquals(AppServices.ActivePlayback, _session))
                {
                    AppServices.ActivePlayback = null;
                }

                _session = null;
            }

            Video.SetMediaPlayer(null);
            _engine?.Dispose();
            _engine = null;
        }

        private async void InitializeAsync()
        {
            _staticFailure = null;
            Render();
            try
            {
                var context = await AppServices.PlaybackContexts.LoadAsync(_videoId, CancellationToken.None);
                if (!_active)
                {
                    return;
                }

                if (context == null)
                {
                    ShowStaticFailure(PlayerFailure.NoSource);
                    return;
                }

                // Remember how the title looks (plain fields only) so History / Continue Watching can show it offline.
                if (context.Summary != null)
                {
                    AppServices.Library.RememberSnapshot(context.Summary);
                }

                var session = new PlaybackSession(
                    context,
                    AppServices.PlaybackData,
                    _engine,
                    AppServices.Progress,
                    AppServices.PlayerPrefs,
                    SystemClock.Instance,
                    TaskDelayer.Instance,
                    AppServices.Logger);
                session.Changed += OnSessionChanged;
                session.AdvanceRequested += OnAdvanceRequested;
                _session = session;
                AppServices.ActivePlayback = session;

                await session.PrepareAsync(CancellationToken.None);
                if (!_active || !ReferenceEquals(_session, session))
                {
                    return;
                }

                // A meaningful saved position asks first (Resume / Start over); otherwise playback simply starts.
                if (session.State == PlayerState.Ready && !session.CanResume)
                {
                    session.Start(PlaybackStartMode.StartOver);
                }
            }
            catch (ApiException)
            {
                ShowStaticFailure(PlayerFailure.Network);
            }
            catch (Exception)
            {
                ShowStaticFailure(PlayerFailure.Unknown);
            }
        }

        private void ShowStaticFailure(PlayerFailure failure)
        {
            _staticFailure = failure;
            Render();
        }

        private void OnSessionChanged(object sender, EventArgs e) => _renderer?.Request();

        private void OnAdvanceRequested(object sender, EventArgs e)
        {
            var next = _session?.Context.Next;
            if (next == null)
            {
                return;
            }

            var ignored = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                if (_active)
                {
                    Navigator.ReplacePlayer(next.Id);
                }
            });
        }

        // ---- rendering --------------------------------------------------------------------------

        private void ApplyStaticText()
        {
            var l = AppServices.Localization;
            ResumeTitle.Text = l.Get("Player_ResumeTitle");
            ResumeButton.Label = l.Get("Player_Resume");
            StartOverButton.Label = l.Get("Player_StartOver");
            RetryButton.Label = l.Get("Player_Retry");
            ChangeQualityButton.Label = l.Get("Player_ChangeQuality");
            ExitButton.Label = l.Get("Player_Exit");
            SkipButton.Label = l.Get("Player_Skip");
            NextHeader.Text = l.Get("Player_UpNext");
            PlayNowButton.Label = l.Get("Player_PlayNow");
            CancelNextButton.Label = l.Get("Player_Cancel");

            Icon(PreviousButton, "\uE892", l.Get("Player_PreviousEpisode"));
            Icon(BackSeekButton, "\uE100", l.Get("Player_SeekBack"));
            Icon(PlayPauseButton, "\uE768", l.Get("Player_Play"));
            Icon(ForwardSeekButton, "\uE101", l.Get("Player_SeekForward"));
            Icon(NextButton, "\uE893", l.Get("Player_NextEpisode"));
            Icon(OptionsButton, "\uE713", l.Get("Player_Options"));
            SkipButton.Glyph = "\uE893";
            PlayNowButton.Glyph = "\uE768";
        }

        private static void Icon(ChoiceButton button, string glyph, string name)
        {
            button.Glyph = glyph;
            AutomationProperties.SetName(button, name);
        }

        private void Render()
        {
            if (!_active)
            {
                return;
            }

            var l = AppServices.Localization;
            var session = _session;

            // Failure (before or after a session exists)
            var failure = _staticFailure ?? (session != null && session.State == PlayerState.Failed ? (PlayerFailure?)session.Failure : null);
            var resume = session != null && session.State == PlayerState.Ready && session.CanResume;
            FailurePanel.Visibility = failure != null ? Visibility.Visible : Visibility.Collapsed;
            ResumePanel.Visibility = failure == null && resume ? Visibility.Visible : Visibility.Collapsed;

            if (failure != null)
            {
                FailureTitle.Text = l.Get("Player_FailedTitle");
                FailureBody.Text = l.Get(FailureKey(failure.Value));
                var canRetry = !(failure.Value == PlayerFailure.NoSource && _staticFailure != null && string.IsNullOrWhiteSpace(_videoId));
                RetryButton.Visibility = canRetry ? Visibility.Visible : Visibility.Collapsed;
                // PROMPT.md section 59 (Player Error: Retry / Change Quality / Go Back): offered only when another quality exists.
                ChangeQualityButton.Visibility = session != null && session.Qualities.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
            }

            if (resume)
            {
                ResumeText.Text = string.Format(CultureInfo.CurrentCulture, l.Get("Player_ResumeBody"), PlaybackFormat.Time(session.SavedProgress.Position));
            }

            var panel = failure != null ? 1 : resume ? 2 : 0;
            if (panel != _panelShown)
            {
                _panelShown = panel;
                if (panel == 1)
                {
                    (RetryButton.Visibility == Visibility.Visible ? RetryButton : ChangeQualityButton).Focus(FocusState.Keyboard);
                }
                else if (panel == 2)
                {
                    ResumeButton.Focus(FocusState.Keyboard);
                }
                else
                {
                    InputSink.Focus(FocusState.Keyboard);
                }
            }

            if (session == null)
            {
                StatusPanel.Visibility = failure == null ? Visibility.Visible : Visibility.Collapsed;
                StatusRing.IsActive = failure == null;
                StatusText.Text = l.Get("Player_Loading");
                return;
            }

            var context = session.Context;
            var preferArabic = l.CurrentLanguage.IsRightToLeft;
            TitleText.Text = context.GetTitle(preferArabic) ?? string.Empty;
            var episodeLine = EpisodeLine(context);
            EpisodeText.Text = episodeLine;
            EpisodeText.Visibility = episodeLine.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

            // Loading / buffering / reconnecting
            var busy = session.State == PlayerState.Preparing
                || session.State == PlayerState.Opening
                || (session.State == PlayerState.Playing && session.IsBuffering);
            StatusPanel.Visibility = busy && failure == null && !resume ? Visibility.Visible : Visibility.Collapsed;
            StatusRing.IsActive = StatusPanel.Visibility == Visibility.Visible;
            StatusText.Text = l.Get(session.IsRecovering ? "Player_Reconnecting" : "Player_Loading");

            // Play / pause
            var playing = session.State == PlayerState.Playing;
            PlayPauseButton.Glyph = playing ? "\uE769" : "\uE768";
            AutomationProperties.SetName(PlayPauseButton, l.Get(playing ? "Player_Pause" : "Player_Play"));
            UpdateDisplayRequest(playing);

            PreviousButton.Visibility = context.Previous != null ? Visibility.Visible : Visibility.Collapsed;
            NextButton.Visibility = context.Next != null ? Visibility.Visible : Visibility.Collapsed;

            // Next episode (visibility itself is decided in UpdateChrome, which also knows about modals)
            var offering = session.AutoNextPhase == AutoNextPhase.Offering && context.Next != null;
            if (offering)
            {
                NextTitleText.Text = context.Next.GetTitle(preferArabic) ?? string.Empty;
                var seconds = session.AutoNextSecondsRemaining;
                var showCountdown = session.AutoNextShowsCountdown && seconds.HasValue;
                NextCountdownText.Text = showCountdown ? string.Format(CultureInfo.CurrentCulture, l.Get("Player_NextIn"), seconds.Value) : string.Empty;
                NextCountdownText.Visibility = showCountdown ? Visibility.Visible : Visibility.Collapsed;
            }

            // Quality fallback notice
            if (session.FellBackFromHeight.HasValue && session.CurrentQuality != null && _notifiedFallback != session.CurrentQuality.Height)
            {
                _notifiedFallback = session.CurrentQuality.Height;
                NoticeText.Text = string.Format(CultureInfo.CurrentCulture, l.Get("Player_FellBack"), session.CurrentQuality.Label);
                _noticeUntil = DateTime.UtcNow.AddSeconds(5);
            }

            UpdateChrome();
        }

        private static string FailureKey(PlayerFailure failure)
        {
            switch (failure)
            {
                case PlayerFailure.NoSource:
                    return "Player_FailedNoSource";
                case PlayerFailure.Network:
                    return "Player_FailedNetwork";
                case PlayerFailure.InvalidMedia:
                case PlayerFailure.Decoding:
                    return "Player_FailedMedia";
                default:
                    return "Player_FailedUnknown";
            }
        }

        private static string EpisodeLine(PlaybackContext context)
        {
            if (!context.IsEpisode)
            {
                return string.Empty;
            }

            var l = AppServices.Localization;
            var parts = new System.Collections.Generic.List<string>();
            if (context.Season.HasValue)
            {
                parts.Add(string.Format(CultureInfo.CurrentCulture, l.Get("Details_SeasonFormat"), context.Season.Value));
            }

            if (context.Episode.HasValue)
            {
                parts.Add(string.Format(CultureInfo.CurrentCulture, l.Get("Details_EpisodeFormat"), context.Episode.Value));
            }

            return string.Join("  \u2022  ", parts);
        }

        /// <summary>Shows or hides the transient chrome (title, progress bar, buttons, scrims) from the current visibility flags.</summary>
        private void UpdateChrome()
        {
            var session = _session;
            var barVisible = _controlsVisible || DateTime.UtcNow < _barUntil;
            var blocking = FailurePanel.Visibility == Visibility.Visible || ResumePanel.Visibility == Visibility.Visible;
            // Any modal (a prompt, the failure screen, or the options panel) must be the ONLY focusable surface on screen
            // (PROMPT.md section 52, modal focus trapping): collapse the transport chrome rather than merely cover it, so
            // D-pad/gamepad navigation has nowhere else to go.
            var modal = blocking || _optionsOpen;

            TopBar.Visibility = (_controlsVisible || barVisible) && !modal && session != null ? Visibility.Visible : Visibility.Collapsed;
            BottomBar.Visibility = barVisible && !modal && session != null ? Visibility.Visible : Visibility.Collapsed;
            ButtonsRow.Visibility = _controlsVisible && !modal ? Visibility.Visible : Visibility.Collapsed;
            SkipButton.Visibility = !modal && session != null && session.ActiveSkip != null ? Visibility.Visible : Visibility.Collapsed;
            NextCard.Visibility = !modal && session != null && session.AutoNextPhase == AutoNextPhase.Offering && session.Context.Next != null ? Visibility.Visible : Visibility.Collapsed;
            Scrims.Visibility = BottomBar.Visibility == Visibility.Visible || _optionsOpen ? Visibility.Visible : Visibility.Collapsed;
            InputSink.IsEnabled = !modal;

            var showNotice = DateTime.UtcNow < _noticeUntil && !blocking;
            NoticeText.Visibility = showNotice ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateDisplayRequest(bool playing)
        {
            try
            {
                if (playing && !_displayActive)
                {
                    _display = _display ?? new DisplayRequest();
                    _display.RequestActive();
                    _displayActive = true;
                }
                else if (!playing && _displayActive)
                {
                    ReleaseDisplay();
                }
            }
            catch (Exception)
            {
                // keeping the screen awake is a nicety, never a requirement
            }
        }

        private void ReleaseDisplay()
        {
            if (_displayActive && _display != null)
            {
                try
                {
                    _display.RequestRelease();
                }
                catch (Exception)
                {
                }
            }

            _displayActive = false;
        }

        // ---- timer ------------------------------------------------------------------------------

        private void OnTimerTick(object sender, object e)
        {
            var session = _session;
            if (session != null && _engine != null)
            {
                var position = _engine.Position;
                var duration = _engine.Duration;
                session.Tick(position, duration);

                var shownPosition = session.Position;
                var shownDuration = session.Duration > TimeSpan.Zero ? session.Duration : duration;
                SeekBar.Value = PlaybackFormat.Fraction(shownPosition, shownDuration) * 1000;
                PositionText.Text = PlaybackFormat.Time(shownPosition);
                RemainingText.Text = shownDuration > TimeSpan.Zero ? "-" + PlaybackFormat.Time(shownDuration - shownPosition) : string.Empty;

                // Plain ticks do not raise Changed, but the offer countdown and the skip button depend on them.
                Render();
            }

            // Hide the buttons after a quiet moment (never while a menu is open or the stream is paused on purpose).
            if (_controlsVisible && !_optionsOpen && session != null && session.State == PlayerState.Playing && DateTime.UtcNow - _lastInput > ControlsIdle)
            {
                HideControls();
            }

            UpdateChrome();
        }

        // ---- input ------------------------------------------------------------------------------

        private void OnKeyDown(object sender, KeyRoutedEventArgs e)
        {
            _lastInput = DateTime.UtcNow;
            var session = _session;
            if (session == null || _optionsOpen
                || FailurePanel.Visibility == Visibility.Visible
                || ResumePanel.Visibility == Visibility.Visible)
            {
                return; // menus and prompts use normal focus navigation
            }

            var key = e.Key;
            var left = key == VirtualKey.Left || key == VirtualKey.GamepadDPadLeft || key == VirtualKey.GamepadLeftThumbstickLeft;
            var right = key == VirtualKey.Right || key == VirtualKey.GamepadDPadRight || key == VirtualKey.GamepadLeftThumbstickRight;
            var vertical = key == VirtualKey.Up || key == VirtualKey.Down || key == VirtualKey.GamepadDPadUp || key == VirtualKey.GamepadDPadDown
                || key == VirtualKey.GamepadLeftThumbstickUp || key == VirtualKey.GamepadLeftThumbstickDown;
            var menu = key == VirtualKey.GamepadMenu || key == VirtualKey.GamepadView;

            if (key == VirtualKey.GamepadLeftShoulder || key == VirtualKey.GamepadRightShoulder)
            {
                Seek(key == VirtualKey.GamepadLeftShoulder ? -1 : 1);
                e.Handled = true;
                return;
            }

            if (_controlsVisible)
            {
                return; // buttons are shown: D-pad moves focus, A activates
            }

            if (left || right)
            {
                Seek(left ? -1 : 1);
                e.Handled = true;
            }
            else if (vertical)
            {
                if (NextCard.Visibility == Visibility.Visible)
                {
                    PlayNowButton.Focus(FocusState.Keyboard);
                }
                else if (SkipButton.Visibility == Visibility.Visible)
                {
                    SkipButton.Focus(FocusState.Keyboard);
                }
                else
                {
                    ShowControls();
                }

                e.Handled = true;
            }
            else if (menu)
            {
                ShowControls();
                e.Handled = true;
            }
        }

        private void Seek(int direction)
        {
            _session?.SeekBy(direction);
            _barUntil = DateTime.UtcNow + BarIdle; // show where we are, without taking focus
            UpdateChrome();
        }

        private void ShowControls()
        {
            _controlsVisible = true;
            _lastInput = DateTime.UtcNow;
            UpdateChrome();
            PlayPauseButton.Focus(FocusState.Keyboard);
        }

        private void HideControls()
        {
            _controlsVisible = false;
            UpdateChrome();
            InputSink.Focus(FocusState.Keyboard);
        }

        private void OnSinkClick(object sender, RoutedEventArgs e)
        {
            // A on the invisible sink: toggle, and flash the progress bar so the viewer sees the effect.
            _session?.TogglePlayPause();
            _barUntil = DateTime.UtcNow + BarIdle;
            UpdateChrome();
        }

        public bool HandleBack()
        {
            if (_optionsOpen)
            {
                CloseOptions();
                return true;
            }

            if (_controlsVisible && FailurePanel.Visibility != Visibility.Visible && ResumePanel.Visibility != Visibility.Visible)
            {
                HideControls();
                return true;
            }

            if (NextCard.Visibility == Visibility.Visible && _session != null)
            {
                _session.CancelAutoNext();
                return true;
            }

            return false; // leave the player
        }

        // ---- buttons ----------------------------------------------------------------------------

        private void OnPlayPauseClick(object sender, RoutedEventArgs e) => _session?.TogglePlayPause();

        private void OnBackSeekClick(object sender, RoutedEventArgs e) => Seek(-1);

        private void OnForwardSeekClick(object sender, RoutedEventArgs e) => Seek(1);

        private void OnPreviousClick(object sender, RoutedEventArgs e)
        {
            var previous = _session?.Context.Previous;
            if (previous != null)
            {
                Navigator.ReplacePlayer(previous.Id);
            }
        }

        private void OnNextClick(object sender, RoutedEventArgs e)
        {
            var next = _session?.Context.Next;
            if (next != null)
            {
                Navigator.ReplacePlayer(next.Id);
            }
        }

        private void OnPlayNowClick(object sender, RoutedEventArgs e) => OnNextClick(sender, e);

        private void OnCancelNextClick(object sender, RoutedEventArgs e)
        {
            _session?.CancelAutoNext();
            InputSink.Focus(FocusState.Keyboard);
        }

        private void OnSkipClick(object sender, RoutedEventArgs e)
        {
            _session?.SkipIntro();
            InputSink.Focus(FocusState.Keyboard);
        }

        private void OnResumeClick(object sender, RoutedEventArgs e) => _session?.Start(PlaybackStartMode.Resume);

        private void OnStartOverClick(object sender, RoutedEventArgs e) => _session?.Start(PlaybackStartMode.StartOver);

        private async void OnRetryClick(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_session != null && _session.State == PlayerState.Failed)
                {
                    await _session.RetryAsync(CancellationToken.None);
                }
                else
                {
                    InitializeAsync();
                }
            }
            catch (Exception)
            {
                // the session reports problems through its state
            }
        }

        private void OnExitClick(object sender, RoutedEventArgs e) => AppServices.Navigation.GoBack();

        private void OnChangeQualityClick(object sender, RoutedEventArgs e)
        {
            if (_session != null)
            {
                OpenOptions();
            }
        }

        // ---- options ----------------------------------------------------------------------------

        private void OnOptionsClick(object sender, RoutedEventArgs e) => OpenOptions();

        private void OpenOptions()
        {
            var session = _session;
            if (session == null)
            {
                return;
            }

            var l = AppServices.Localization;
            var preferArabic = l.CurrentLanguage.IsRightToLeft;
            OptionsHost.Children.Clear();
            ChoiceButton first = null;

            // Quality: exactly what the API returned for this title, best first.
            AddHeader(l.Get("Player_Quality"));
            foreach (var quality in session.Qualities.OrderByDescending(q => q.Height))
            {
                var picked = quality;
                var button = Option(quality.Label, session.CurrentQuality != null && session.CurrentQuality.Height == quality.Height, async () =>
                {
                    CloseOptions();
                    await session.SelectQualityAsync(picked, CancellationToken.None);
                });
                first = first ?? button;
            }

            // Subtitles: only when at least one track can really be loaded.
            if (session.Subtitles.Count > 0)
            {
                AddHeader(l.Get("Player_Subtitles"));
                Option(l.Get("Player_SubtitlesOff"), session.CurrentSubtitle == null, () =>
                {
                    session.SelectSubtitle(null);
                    CloseOptions();
                    return Task.CompletedTask;
                });
                foreach (var track in session.Subtitles)
                {
                    var language = track.Language;
                    Option(PlaybackFormat.SubtitleLanguageName(language), session.CurrentSubtitle == language, () =>
                    {
                        session.SelectSubtitle(language);
                        CloseOptions();
                        return Task.CompletedTask;
                    });
                }
            }

            // Autoplay (series only).
            if (session.Context.IsEpisode)
            {
                AddHeader(l.Get("Player_Autoplay"));
                var prefs = AppServices.PlayerPrefs;
                var toggle = Option(l.Get(prefs.AutoplayNext ? "Player_On" : "Player_Off"), prefs.AutoplayNext, null);
                toggle.Click += (s, e) =>
                {
                    prefs.AutoplayNext = !prefs.AutoplayNext;
                    toggle.Label = l.Get(prefs.AutoplayNext ? "Player_On" : "Player_Off");
                    toggle.IsActive = prefs.AutoplayNext;
                };
            }

            // Episodes of this season.
            var episodes = session.Context.SeasonEpisodes;
            if (episodes.Count > 1)
            {
                AddHeader(l.Get("Player_Episodes"));
                foreach (var episode in episodes)
                {
                    var target = episode;
                    var label = (target.EpisodeNumber.HasValue ? target.EpisodeNumber.Value.ToString(CultureInfo.InvariantCulture) + ".  " : string.Empty) + (target.GetTitle(preferArabic) ?? string.Empty);
                    Option(label, target.Id == session.Context.VideoId, () =>
                    {
                        if (target.Id != session.Context.VideoId)
                        {
                            Navigator.ReplacePlayer(target.Id);
                        }

                        return Task.CompletedTask;
                    });
                }
            }

            _optionsOpen = true;
            OptionsPanel.Visibility = Visibility.Visible;
            UpdateChrome();
            FocusManager.GettingFocus += OnOptionsGettingFocus;
            if (first != null)
            {
                first.Focus(FocusState.Keyboard);
            }
        }

        // Modal focus trap for the options panel (PROMPT.md section 52): matches ConfirmPanel's approach.
        private void OnOptionsGettingFocus(object sender, GettingFocusEventArgs e)
        {
            var node = e.NewFocusedElement as DependencyObject;
            while (node != null)
            {
                if (ReferenceEquals(node, OptionsPanel))
                {
                    return;
                }

                node = VisualTreeHelper.GetParent(node);
            }

            e.TryCancel();
        }

        private void CloseOptions()
        {
            FocusManager.GettingFocus -= OnOptionsGettingFocus;
            _optionsOpen = false;
            OptionsPanel.Visibility = Visibility.Collapsed;
            _controlsVisible = false;
            UpdateChrome();
            InputSink.Focus(FocusState.Keyboard);
        }

        private void AddHeader(string text)
        {
            OptionsHost.Children.Add(new TextBlock
            {
                Text = text,
                Style = (Style)Application.Current.Resources["XmanaTitleTextStyle"],
                FontSize = 20,
                Margin = new Thickness(0, OptionsHost.Children.Count == 0 ? 0 : 14, 0, 4),
            });
        }

        private ChoiceButton Option(string label, bool active, Func<Task> onClick)
        {
            var button = new ChoiceButton { Label = label, IsActive = active, HorizontalAlignment = HorizontalAlignment.Stretch };
            if (onClick != null)
            {
                button.Click += async (s, e) =>
                {
                    try
                    {
                        await onClick();
                    }
                    catch (Exception)
                    {
                    }
                };
            }

            OptionsHost.Children.Add(button);
            return button;
        }
    }
}
