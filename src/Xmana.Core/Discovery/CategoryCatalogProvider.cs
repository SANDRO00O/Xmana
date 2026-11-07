using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Xmana.Core.Discovery
{
    /// <summary>
    /// Shares the category catalog between screens. A loaded catalog is kept per language code; concurrent callers share one
    /// load; a failed load is not remembered, so the next call retries. (The API layer's own cache keeps repeats cheap.)
    /// </summary>
    public sealed class CategoryCatalogProvider
    {
        private readonly CategoryCatalogLoader _loader;
        private readonly Func<string> _language;
        private readonly Func<bool> _preferArabic;
        private readonly object _gate = new object();
        private readonly Dictionary<string, CategoryCatalog> _loaded = new Dictionary<string, CategoryCatalog>(StringComparer.Ordinal);
        private readonly Dictionary<string, Task<CategoryCatalog>> _loading = new Dictionary<string, Task<CategoryCatalog>>(StringComparer.Ordinal);

        public CategoryCatalogProvider(CategoryCatalogLoader loader, Func<string> languageCode, Func<bool> preferArabicOrder)
        {
            _loader = loader ?? throw new ArgumentNullException(nameof(loader));
            _language = languageCode ?? throw new ArgumentNullException(nameof(languageCode));
            _preferArabic = preferArabicOrder ?? throw new ArgumentNullException(nameof(preferArabicOrder));
        }

        /// <summary>The catalog for the current language if it is already loaded; otherwise null (never blocks).</summary>
        public CategoryCatalog? TryGetLoaded()
        {
            lock (_gate)
            {
                CategoryCatalog catalog;
                return _loaded.TryGetValue(_language(), out catalog) ? catalog : null;
            }
        }

        public Task<CategoryCatalog> GetAsync(CancellationToken cancellationToken)
        {
            var lang = _language();
            lock (_gate)
            {
                CategoryCatalog catalog;
                if (_loaded.TryGetValue(lang, out catalog))
                {
                    return Task.FromResult(catalog);
                }

                Task<CategoryCatalog> running;
                if (!_loading.TryGetValue(lang, out running))
                {
                    running = LoadAsync(lang);
                    _loading[lang] = running;
                }

                return Wait(running, cancellationToken);
            }
        }

        private async Task<CategoryCatalog> LoadAsync(string lang)
        {
            await Task.Yield();
            try
            {
                // Not tied to any one caller's token: other screens may be waiting for the same catalog.
                var catalog = await _loader.LoadAsync(lang, _preferArabic(), CancellationToken.None).ConfigureAwait(false);
                lock (_gate)
                {
                    _loaded[lang] = catalog;
                }

                return catalog;
            }
            finally
            {
                lock (_gate)
                {
                    _loading.Remove(lang);
                }
            }
        }

        private static async Task<CategoryCatalog> Wait(Task<CategoryCatalog> task, CancellationToken cancellationToken)
        {
            if (!cancellationToken.CanBeCanceled || task.IsCompleted)
            {
                return await task.ConfigureAwait(false);
            }

            var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (cancellationToken.Register(() => cancelled.TrySetResult(true)))
            {
                if (await Task.WhenAny(task, cancelled.Task).ConfigureAwait(false) != task)
                {
                    throw new OperationCanceledException(cancellationToken);
                }
            }

            return await task.ConfigureAwait(false);
        }
    }
}
