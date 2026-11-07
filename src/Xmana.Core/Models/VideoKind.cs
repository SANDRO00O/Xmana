namespace Xmana.Core.Models
{
    /// <summary>
    /// Interpretation of the server "kind" value. API.md documents 1 = movies and 2 = series for the
    /// category/listing endpoints; anything else stays <see cref="Unknown"/> and the raw value is kept on the model.
    /// </summary>
    public enum VideoKind
    {
        Unknown = 0,
        Movie = 1,
        Series = 2,
    }
}
