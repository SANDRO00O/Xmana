namespace Xmana.Core.Images
{
    /// <summary>What an image is used for; decides which of the API's image fields is the best source.</summary>
    public enum ImageRole
    {
        /// <summary>Full-size artwork (details header, hero backdrop).</summary>
        Poster,

        /// <summary>Small card image.</summary>
        Thumbnail,

        /// <summary>Default rail/grid card image.</summary>
        MediumThumbnail,

        /// <summary>Wide backdrop for the home hero.</summary>
        Banner,

        /// <summary>Actor/director picture.</summary>
        Staff,
    }
}
