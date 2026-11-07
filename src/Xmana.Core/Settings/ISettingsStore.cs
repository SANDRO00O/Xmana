namespace Xmana.Core.Settings
{
    /// <summary>Tiny key/value persistence abstraction (UWP LocalSettings in the app, memory elsewhere).</summary>
    public interface ISettingsStore
    {
        string? GetString(string key);
        void SetString(string key, string? value);
    }
}
