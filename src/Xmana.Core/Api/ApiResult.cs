using System;
using Xmana.Core.Caching;

namespace Xmana.Core.Api
{
    /// <summary>A value plus where it came from, so screens can show cached content immediately and quietly update.</summary>
    public sealed class ApiResult<T>
    {
        public ApiResult(T value, DataOrigin origin, DateTime? storedUtc)
        {
            Value = value;
            Origin = origin;
            StoredUtc = storedUtc;
        }

        public T Value { get; }

        public DataOrigin Origin { get; }

        public DateTime? StoredUtc { get; }

        public bool IsFromCache => Origin != DataOrigin.Network;

        /// <summary>True when a background refresh is running or the data is a fallback after a failure.</summary>
        public bool MayBeOutdated => Origin == DataOrigin.CacheStale || Origin == DataOrigin.CacheAfterError;
    }
}
