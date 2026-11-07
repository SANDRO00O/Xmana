using System;
using System.Collections.Generic;

namespace Xmana.Core.Navigation
{
    public enum NavigationKind
    {
        /// <summary>Initial entry or a top-level switch; the stack was reset.</summary>
        Root,

        /// <summary>A detail screen was pushed on top of the current entry.</summary>
        Push,

        /// <summary>The top entry was popped.</summary>
        Back,

        /// <summary>The top entry was swapped for another one at the same depth (e.g. next episode).</summary>
        Replace,
    }

    public enum BackOutcome
    {
        /// <summary>A pushed screen was popped.</summary>
        NavigatedBack,

        /// <summary>A top-level section other than Home was left for Home.</summary>
        ReturnedToHome,

        /// <summary>Already at the Home root; the platform (system) should handle B.</summary>
        AtRoot,
    }

    public sealed class NavigationChangedEventArgs : EventArgs
    {
        public NavigationChangedEventArgs(NavigationEntry? from, NavigationEntry to, NavigationKind kind)
        {
            From = from;
            To = to;
            Kind = kind;
        }

        public NavigationEntry? From { get; }

        public NavigationEntry To { get; }

        public NavigationKind Kind { get; }
    }

    /// <summary>
    /// Pure navigation state machine with deterministic back behaviour (PROMPT.md section 50):
    ///   B on a pushed screen   -> pop it (previous focus restored)
    ///   B on a non-Home root   -> Home
    ///   B on the Home root     -> not handled (system decides)
    /// It owns no UI; the shell listens to <see cref="Navigated"/> and renders the current entry.
    /// </summary>
    public sealed class NavigationController
    {
        private readonly List<NavigationEntry> _stack = new List<NavigationEntry>();
        private readonly Dictionary<NavigationDestination, string> _rootFocus = new Dictionary<NavigationDestination, string>();

        public NavigationController(NavigationDestination start = NavigationDestination.Home)
        {
            var entry = new NavigationEntry(start, null, null);
            _stack.Add(entry);
        }

        public event EventHandler<NavigationChangedEventArgs>? Navigated;

        public NavigationEntry Current => _stack[_stack.Count - 1];

        public int Depth => _stack.Count;

        /// <summary>The top-level section that owns the current entry (the bottom of the stack).</summary>
        public NavigationDestination RootDestination => _stack[0].Destination;

        public bool CanGoBack => _stack.Count > 1 || Current.Destination != NavigationCatalog.Root;

        /// <summary>Raises <see cref="Navigated"/> for the initial entry so the UI can render it.</summary>
        public void Start()
        {
            Navigated?.Invoke(this, new NavigationChangedEventArgs(null, Current, NavigationKind.Root));
        }

        /// <summary>
        /// Records which element had focus on the current entry. For a root entry the key is also
        /// remembered per destination so returning to that section restores it.
        /// </summary>
        public void SaveFocusKey(string? focusKey)
        {
            if (string.IsNullOrEmpty(focusKey))
            {
                return;
            }

            Current.SavedFocusKey = focusKey;
            if (_stack.Count == 1)
            {
                _rootFocus[Current.Destination] = focusKey!;
            }
        }

        /// <summary>
        /// Switches to a top-level section, resetting the stack. Returns false (and does nothing)
        /// when that section is already the current root.
        /// </summary>
        public bool NavigateToRoot(NavigationDestination destination)
        {
            if (_stack.Count == 1 && Current.Destination == destination)
            {
                return false;
            }

            var from = Current;
            _rootFocus.TryGetValue(destination, out var remembered);
            _stack.Clear();
            var entry = new NavigationEntry(destination, null, remembered);
            _stack.Add(entry);
            Navigated?.Invoke(this, new NavigationChangedEventArgs(from, entry, NavigationKind.Root));
            return true;
        }

        /// <summary>Pushes a detail screen above the current entry.</summary>
        public NavigationEntry Push(NavigationDestination destination, string? parameter = null)
        {
            var from = Current;
            var entry = new NavigationEntry(destination, parameter, null);
            _stack.Add(entry);
            Navigated?.Invoke(this, new NavigationChangedEventArgs(from, entry, NavigationKind.Push));
            return entry;
        }

        /// <summary>
        /// Swaps the current entry for another at the same depth, so moving between sibling screens (episode to episode)
        /// does not grow the Back stack. On a root entry it behaves like <see cref="Push"/>.
        /// </summary>
        public NavigationEntry Replace(NavigationDestination destination, string? parameter = null)
        {
            if (_stack.Count == 1)
            {
                return Push(destination, parameter);
            }

            var from = Current;
            var entry = new NavigationEntry(destination, parameter, null);
            _stack[_stack.Count - 1] = entry;
            Navigated?.Invoke(this, new NavigationChangedEventArgs(from, entry, NavigationKind.Replace));
            return entry;
        }

        public BackOutcome GoBack()
        {
            if (_stack.Count > 1)
            {
                var from = Current;
                _stack.RemoveAt(_stack.Count - 1);
                Navigated?.Invoke(this, new NavigationChangedEventArgs(from, Current, NavigationKind.Back));
                return BackOutcome.NavigatedBack;
            }

            if (Current.Destination != NavigationCatalog.Root)
            {
                NavigateToRoot(NavigationCatalog.Root);
                return BackOutcome.ReturnedToHome;
            }

            return BackOutcome.AtRoot;
        }
    }
}
