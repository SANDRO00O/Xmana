using System;
using System.Collections.Generic;

namespace Xmana.Core.Caching
{
    /// <summary>
    /// A least-recently-used cache bounded by BOTH entry count and total weight (for example decoded-image bytes), so memory use has a
    /// hard ceiling regardless of image sizes. Not thread-safe: callers lock. An entry heavier than the whole budget is not stored.
    /// </summary>
    public sealed class LruCache<TKey, TValue>
    {
        private readonly int _maxEntries;
        private readonly long _maxWeight;
        private readonly Dictionary<TKey, LinkedListNode<Entry>> _map;
        private readonly LinkedList<Entry> _order = new LinkedList<Entry>();

        public LruCache(int maxEntries, long maxWeight, IEqualityComparer<TKey>? comparer = null)
        {
            if (maxEntries < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxEntries));
            }

            if (maxWeight < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxWeight));
            }

            _maxEntries = maxEntries;
            _maxWeight = maxWeight;
            _map = new Dictionary<TKey, LinkedListNode<Entry>>(comparer ?? EqualityComparer<TKey>.Default);
        }

        public int Count => _map.Count;

        public long TotalWeight { get; private set; }

        public bool TryGet(TKey key, out TValue value)
        {
            LinkedListNode<Entry> node;
            if (_map.TryGetValue(key, out node))
            {
                _order.Remove(node);
                _order.AddFirst(node);
                value = node.Value.Value;
                return true;
            }

            value = default(TValue)!;
            return false;
        }

        /// <summary>Stores a value, evicting the least recently used entries until both limits hold. Returns false when it is too heavy to keep.</summary>
        public bool Set(TKey key, TValue value, long weight)
        {
            Remove(key);
            var safeWeight = Math.Max(0, weight);
            if (safeWeight > _maxWeight)
            {
                return false;
            }

            _map[key] = _order.AddFirst(new Entry(key, value, safeWeight));
            TotalWeight += safeWeight;

            while ((_map.Count > _maxEntries || TotalWeight > _maxWeight) && _order.Last != null && !_map.Comparer.Equals(_order.Last.Value.Key, key))
            {
                RemoveNode(_order.Last);
            }

            return true;
        }

        public bool Remove(TKey key)
        {
            LinkedListNode<Entry> node;
            if (!_map.TryGetValue(key, out node))
            {
                return false;
            }

            RemoveNode(node);
            return true;
        }

        public void Clear()
        {
            _map.Clear();
            _order.Clear();
            TotalWeight = 0;
        }

        private void RemoveNode(LinkedListNode<Entry> node)
        {
            _order.Remove(node);
            _map.Remove(node.Value.Key);
            TotalWeight -= node.Value.Weight;
        }

        private sealed class Entry
        {
            public Entry(TKey key, TValue value, long weight)
            {
                Key = key;
                Value = value;
                Weight = weight;
            }

            public TKey Key { get; }

            public TValue Value { get; }

            public long Weight { get; }
        }
    }
}
