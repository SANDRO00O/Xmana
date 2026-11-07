using System;
using System.Collections.Generic;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Xmana.Core.Localization;

namespace Xmana.App
{
    /// <summary>
    /// XAML bridge to the LocalizationService. Usage: <c>xmana:Loc.Key="nav.home"</c>.
    /// Texts refresh automatically when the language changes (no restart, no hard-coded strings).
    /// </summary>
    public static class Loc
    {
        public static readonly DependencyProperty KeyProperty =
            DependencyProperty.RegisterAttached("Key", typeof(string), typeof(Loc), new PropertyMetadata(null, OnKeyChanged));

        private static readonly List<WeakReference<DependencyObject>> Targets = new List<WeakReference<DependencyObject>>();
        private static ILocalizationService? _service;

        public static string? GetKey(DependencyObject element) => (string?)element.GetValue(KeyProperty);

        public static void SetKey(DependencyObject element, string? value) => element.SetValue(KeyProperty, value);

        internal static void Attach(ILocalizationService service)
        {
            if (_service != null) _service.LanguageChanged -= OnLanguageChanged;
            _service = service;
            _service.LanguageChanged += OnLanguageChanged;
        }

        public static string Get(string key) => _service?.Get(key) ?? key;

        private static void OnKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            lock (Targets)
            {
                Targets.Add(new WeakReference<DependencyObject>(d));
            }
            Apply(d);
        }

        private static void OnLanguageChanged(object? sender, EventArgs e)
        {
            lock (Targets)
            {
                for (var i = Targets.Count - 1; i >= 0; i--)
                {
                    if (Targets[i].TryGetTarget(out var target)) Apply(target);
                    else Targets.RemoveAt(i);
                }
            }
        }

        private static void Apply(DependencyObject d)
        {
            var key = GetKey(d);
            if (string.IsNullOrEmpty(key) || _service == null) return;

            var text = _service.Get(key!);
            switch (d)
            {
                case TextBlock textBlock:
                    textBlock.Text = text;
                    break;
                case ContentControl contentControl:
                    contentControl.Content = text;
                    AutomationProperties.SetName(contentControl, text);
                    break;
            }
        }
    }
}
