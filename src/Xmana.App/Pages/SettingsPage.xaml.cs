using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Xmana.App.Controls;
using Xmana.App.Focus;
using Xmana.App.Services;
using Xmana.App.Shell;
using Xmana.Core.Focus;
using Xmana.Core.Library;
using Xmana.Core.Localization;
using Xmana.Core.Models;
using Xmana.Core.Navigation;
using Xmana.Core.Playback;

namespace Xmana.App.Pages
{
    /// <summary>
    /// Settings (PROMPT.md section 53): interface language and motion, playback preferences, local storage (sizes, clearing, reset) and
    /// About (version, unofficial-client notice, open-source note, FAQ). Everything is stored on the device. Destructive actions ask first.
    /// </summary>
    public sealed partial class SettingsPage : Page, IBackHandler
    {
        private static readonly int[] QualityHeights = { 240, 360, 480, 720, 1080 };

        private TextBlock _imageSize;
        private TextBlock _metadataSize;
        private bool _active;

        public SettingsPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _active = true;
            TitleText.Text = AppServices.Localization.Get("Nav_Settings");
            Build();
            var ignored = RefreshSizesAsync();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            _active = false;
        }

        public bool HandleBack()
        {
            if (Confirm.IsOpen)
            {
                Confirm.Close();
                return true;
            }

            return false;
        }

        // ---- layout -----------------------------------------------------------------------------

        private void Build()
        {
            var l = AppServices.Localization;
            var prefs = AppServices.PlayerPrefs;
            Host.Children.Clear();

            // Interface
            Header(l.Get("Settings_InterfaceHeader"));
            Caption(l.Get("Settings_Language"));
            var firstFocus = true;
            foreach (var language in LanguageCatalog.All)
            {
                var option = new ChoiceButton
                {
                    Height = 44,
                    Label = language.NativeName, // autonym: always shown in the language's own script
                    IsActive = ReferenceEquals(language, l.CurrentLanguage),
                    Tag = language,
                };
                XmanaFocus.SetKey(option, firstFocus ? FocusKeys.ContentPrimary : FocusKeys.LanguageOption(language.Code));
                firstFocus = false;
                option.Click += OnLanguageClick;
                Host.Children.Add(option);
            }

            Toggle(l.Get("Settings_ReduceMotion"), () => AppServices.Motion.ReduceMotion, v => AppServices.Motion.ReduceMotion = v);

            // Playback
            Header(l.Get("Settings_PlaybackHeader"));
            Choice(
                l.Get("Settings_Quality"),
                new[] { l.Get("Settings_QualityAuto") }.Concat(QualityHeights.Select(h => h.ToString(CultureInfo.InvariantCulture) + "p")).ToList(),
                prefs.PreferredQualityHeight.HasValue ? Math.Max(0, Array.IndexOf(QualityHeights, prefs.PreferredQualityHeight.Value) + 1) : 0,
                index =>
                {
                    if (index <= 0)
                    {
                        prefs.ClearQuality();
                    }
                    else
                    {
                        prefs.PreferredQualityHeight = QualityHeights[index - 1];
                    }
                });

            var subtitleOptions = new List<SubtitlePreference> { SubtitlePreference.Off, SubtitlePreference.Arabic, SubtitlePreference.English, SubtitlePreference.Spanish };
            Choice(
                l.Get("Settings_Subtitles"),
                subtitleOptions.Select(o => o == SubtitlePreference.Off ? l.Get("Player_SubtitlesOff") : PlaybackFormat.SubtitleLanguageName(PlayerPreferences.ToLanguage(o).Value)).ToList(),
                Math.Max(0, subtitleOptions.IndexOf(prefs.Subtitle)),
                index => prefs.Subtitle = subtitleOptions[index]);
            Caption(l.Get("Settings_SubtitlesNote"));

            Toggle(l.Get("Player_Autoplay"), () => prefs.AutoplayNext, v => prefs.AutoplayNext = v);
            Toggle(l.Get("Settings_Resume"), () => prefs.ResumeEnabled, v => prefs.ResumeEnabled = v);
            Toggle(l.Get("Settings_SkipIntro"), () => prefs.SkipIntroEnabled, v => prefs.SkipIntroEnabled = v);
            Caption(l.Get("Settings_SkipIntroNote"));

            // Storage
            Header(l.Get("Settings_StorageHeader"));
            _imageSize = Caption(string.Empty);
            _metadataSize = Caption(string.Empty);
            ActionButton(l.Get("Settings_ClearImages"), async () =>
            {
                await AppServices.Storage.ClearImageCacheAsync();
                await RefreshSizesAsync();
            });
            ActionButton(l.Get("Settings_ClearMetadata"), async () =>
            {
                await AppServices.Storage.ClearMetadataCacheAsync();
                await RefreshSizesAsync();
            });
            ActionButton(l.Get("Settings_ClearSearches"), () =>
            {
                AppServices.Storage.ClearSearchHistory();
                return Task.CompletedTask;
            });
            ActionButton(l.Get("Settings_ClearHistory"), () =>
            {
                Confirm.Show(l.Get("History_ClearTitle"), l.Get("History_ClearBody"), l.Get("History_ClearConfirm"), () => AppServices.Storage.ClearHistory());
                return Task.CompletedTask;
            });
            ActionButton(l.Get("Settings_ResetAll"), () =>
            {
                Confirm.Show(l.Get("Settings_ResetTitle"), l.Get("Settings_ResetBody"), l.Get("Settings_ResetConfirm"), async () =>
                {
                    await AppServices.Storage.ResetAllAsync();
                    Build();
                    await RefreshSizesAsync();
                });
                return Task.CompletedTask;
            });

            // About
            Header(l.Get("Settings_AboutHeader"));
            Caption(string.Format(CultureInfo.CurrentCulture, l.Get("Settings_Version"), VersionText()));
            ActionButton(l.Get("Nav_Faq"), () =>
            {
                AppServices.Navigation.SaveFocusKey(XmanaFocus.CurrentKey());
                AppServices.Navigation.Push(NavigationDestination.Faq);
                return Task.CompletedTask;
            });
            Caption(l.Get("Settings_UnofficialNotice"));
            Caption(l.Get("Settings_OpenSource"));
            Caption(l.Get("Settings_ApiNote"));
        }

        private static string VersionText()
        {
            try
            {
                var v = Package.Current.Id.Version;
                return v.Major + "." + v.Minor + "." + v.Build + "." + v.Revision;
            }
            catch (Exception)
            {
                return "0.1.0.0";
            }
        }

        private async Task RefreshSizesAsync()
        {
            if (!_active || _imageSize == null)
            {
                return;
            }

            var l = AppServices.Localization;
            var images = await AppServices.Storage.ImageCacheBytesAsync();
            var metadata = await AppServices.Storage.MetadataCacheBytesAsync();
            if (!_active)
            {
                return;
            }

            _imageSize.Text = string.Format(CultureInfo.CurrentCulture, l.Get("Settings_ImagesSize"), StorageFormat.Bytes(images));
            _metadataSize.Text = string.Format(CultureInfo.CurrentCulture, l.Get("Settings_MetadataSize"), StorageFormat.Bytes(metadata));
        }

        // ---- building blocks --------------------------------------------------------------------

        private void Header(string text)
        {
            Host.Children.Add(new TextBlock
            {
                Text = text,
                Style = (Style)Application.Current.Resources["XmanaTitleTextStyle"],
                FontSize = 22,
                Margin = new Thickness(0, Host.Children.Count == 0 ? 0 : 18, 0, 2),
            });
        }

        private TextBlock Caption(string text)
        {
            var block = new TextBlock
            {
                Text = text,
                Style = (Style)Application.Current.Resources["XmanaCaptionTextStyle"],
                Margin = new Thickness(2, 0, 0, 0),
            };
            Host.Children.Add(block);
            return block;
        }

        private void Toggle(string label, Func<bool> get, Action<bool> set)
        {
            var l = AppServices.Localization;
            var button = new ChoiceButton { Height = 44 };
            Action refresh = () =>
            {
                var on = get();
                button.Label = label + ": " + l.Get(on ? "Player_On" : "Player_Off");
                button.IsActive = on;
            };
            refresh();
            button.Click += (s, e) =>
            {
                set(!get());
                refresh();
            };
            Host.Children.Add(button);
        }

        private void Choice(string header, IList<string> options, int selected, Action<int> onChanged)
        {
            var box = new ComboBox { Header = header, Width = 300 };
            foreach (var option in options)
            {
                box.Items.Add(option);
            }

            box.SelectedIndex = Math.Max(0, Math.Min(selected, options.Count - 1));
            box.SelectionChanged += (s, e) =>
            {
                if (box.SelectedIndex >= 0)
                {
                    onChanged(box.SelectedIndex);
                }
            };
            Host.Children.Add(box);
        }

        private void ActionButton(string label, Func<Task> onClick)
        {
            var button = new ChoiceButton { Height = 44, Label = label };
            button.Click += async (s, e) =>
            {
                try
                {
                    await onClick();
                }
                catch (Exception)
                {
                    // a failing maintenance action must never crash the app
                }
            };
            Host.Children.Add(button);
        }

        private void OnLanguageClick(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is AppLanguage language)
            {
                // The shell reacts to LanguageChanged: re-applies FlowDirection, relabels the rail and re-renders this page.
                AppServices.Localization.SetLanguage(language);
            }
        }
    }
}
