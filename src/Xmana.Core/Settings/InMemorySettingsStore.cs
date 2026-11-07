using System.Collections.Generic;

namespace Xmana.Core.Settings
{
    public sealed class InMemorySettingsStore : ISettingsStore
    {
        private readonly Dictionary<string, string> _values = new Dictionary<string, string>();

        public string? GetString(string key) => _values.TryGetValue(key, out var v) ? v : null;

        public void SetString(string key, string? value)
        {
            if (value == null) _values.Remove(key);
            else _values[key] = value;
        }
    }
}
