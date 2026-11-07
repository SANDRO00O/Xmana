using System;
using System.Collections.Generic;

namespace Xmana.App.Services
{
    /// <summary>
    /// Keeps per-navigation-entry page state (a browse/search controller with the pages already loaded) so that Back from a
    /// details screen returns to the same results, scroll depth and focus target instead of starting over. Bounded, least
    /// recently used first out. Used from the UI thread only.
    /// </summary>
    internal static class PageSessions
    {
        private const int MaxSessions = 8;
        private static readonly Dictionary<Guid, object> Items = new Dictionary<Guid, object>();
        private static readonly LinkedList<Guid> Order = new LinkedList<Guid>();

        /// <summary>The stored state for an entry, or null when there is none (it never creates one).</summary>
        public static T TryGet<T>(Guid entryId)
            where T : class
        {
            object existing;
            if (Items.TryGetValue(entryId, out existing) && existing is T)
            {
                Order.Remove(entryId);
                Order.AddFirst(entryId);
                return (T)existing;
            }

            return null;
        }

        public static T GetOrCreate<T>(Guid entryId, Func<T> create)
            where T : class
        {
            object existing;
            if (Items.TryGetValue(entryId, out existing) && existing is T)
            {
                Order.Remove(entryId);
                Order.AddFirst(entryId);
                return (T)existing;
            }

            var created = create();
            Items[entryId] = created;
            Order.Remove(entryId);
            Order.AddFirst(entryId);
            while (Order.Count > MaxSessions)
            {
                var oldest = Order.Last.Value;
                Order.RemoveLast();
                Items.Remove(oldest);
            }

            return created;
        }
    }
}
