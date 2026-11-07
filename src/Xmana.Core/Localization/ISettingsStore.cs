namespace Xmana.Core.Localization
{
    /// <summary>
    /// Minimal local key/value store. The UWP app backs this with ApplicationData.LocalSettings;
    /// tests use an in-memory fake. Values never leave the device.
    /// </summary>
    public interface ISettingsStore
    {
        string? GetString(string key);

        void SetString(string key, string value);
    }
}
