using System;
using Xmana.Core.Localization;

namespace Xmana.App.Services
{
    /// <summary>
    /// Central place for animation timing so a Reduce Motion preference (Settings UI arrives in a
    /// later milestone) can switch every animation off at once.
    /// </summary>
    internal sealed class MotionSettings
    {
        public const string ReduceMotionKey = "ui.reduceMotion";

        private readonly ISettingsStore _store;

        public MotionSettings(ISettingsStore store)
        {
            _store = store;
        }

        public bool ReduceMotion
        {
            get => string.Equals(_store.GetString(ReduceMotionKey), "true", StringComparison.OrdinalIgnoreCase);
            set => _store.SetString(ReduceMotionKey, value ? "true" : "false");
        }

        public TimeSpan Duration(int milliseconds) =>
            ReduceMotion ? TimeSpan.Zero : TimeSpan.FromMilliseconds(milliseconds);
    }
}
