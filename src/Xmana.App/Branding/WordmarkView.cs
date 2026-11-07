using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Xmana.App.Branding
{
    public enum WordmarkVariant
    {
        /// <summary>Splash, About, large branding areas.</summary>
        Full,

        /// <summary>Navigation headers, player overlays, settings.</summary>
        Compact,

        /// <summary>Small surfaces (X monogram from the same geometry).</summary>
        Icon,
    }

    /// <summary>
    /// Renders the Xmana wordmark as vector geometry (see Branding/Wordmarks.xaml, generated from
    /// assets/logo/generate_logo.py). Scales to any size without raster enlargement.
    /// </summary>
    public sealed class WordmarkView : ContentControl
    {
        public static readonly DependencyProperty VariantProperty = DependencyProperty.Register(
            nameof(Variant),
            typeof(WordmarkVariant),
            typeof(WordmarkView),
            new PropertyMetadata(WordmarkVariant.Full, (d, e) => ((WordmarkView)d).Apply()));

        public WordmarkView()
        {
            IsTabStop = false;
            Apply();
        }

        public WordmarkVariant Variant
        {
            get => (WordmarkVariant)GetValue(VariantProperty);
            set => SetValue(VariantProperty, value);
        }

        private void Apply()
        {
            var key = Variant == WordmarkVariant.Compact ? "XmanaWordmarkCompact"
                : Variant == WordmarkVariant.Icon ? "XmanaWordmarkIcon"
                : "XmanaWordmarkFull";

            var resources = Application.Current?.Resources;
            if (resources != null && resources.TryGetValue(key, out var template) && template is DataTemplate dataTemplate)
            {
                ContentTemplate = dataTemplate;
                Content = key; // any non-null content instantiates the (binding-free) template
            }
        }
    }
}
