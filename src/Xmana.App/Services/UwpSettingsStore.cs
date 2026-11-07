using Windows.Storage;
using Xmana.Core.Diagnostics;
using Xmana.Core.Settings;

namespace Xmana.App.Services
{
    /// <summary>ISettingsStore backed by ApplicationData.LocalSettings (device-local, no sync).</summary>
    internal sealed class UwpSettingsStore : ISettingsStore
    {
        private readonly ApplicationDataContainer _container = ApplicationData.Current.LocalSettings;

        public string? GetString(string key)
        {
            try
            {
                return _container.Values.TryGetValue(key, out var value) ? value as string : null;
            }
            catch (System.Exception ex)
            {
                Log.Error("Settings", "Read failed for key " + key, ex);
                return null;
            }
        }

        public void SetString(string key, string? value)
        {
            try
            {
                if (value == null) _container.Values.Remove(key);
                else _container.Values[key] = value;
            }
            catch (System.Exception ex)
            {
                Log.Error("Settings", "Write failed for key " + key, ex);
            }
        }
    }
}
