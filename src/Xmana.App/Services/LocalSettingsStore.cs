using System;
using Windows.Storage;
using Xmana.Core.Localization;

namespace Xmana.App.Services
{
    /// <summary>ISettingsStore backed by the app's LocalSettings. Stays on the device; never roamed.</summary>
    internal sealed class LocalSettingsStore : ISettingsStore
    {
        public string GetString(string key)
        {
            try
            {
                return ApplicationData.Current.LocalSettings.Values[key] as string;
            }
            catch (Exception)
            {
                return null; // unreadable setting => behave as "not set"
            }
        }

        public void SetString(string key, string value)
        {
            try
            {
                ApplicationData.Current.LocalSettings.Values[key] = value;
            }
            catch (Exception)
            {
                // Persisting a preference must never crash the app.
            }
        }
    }
}
