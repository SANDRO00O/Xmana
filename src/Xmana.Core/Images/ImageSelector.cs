using System;
using System.Collections.Generic;
using Xmana.Core.Http;
using Xmana.Core.Models;

namespace Xmana.Core.Images
{
    /// <summary>
    /// Picks the image URL to show for a video or staff member. Plain fields (<c>img</c>, <c>imgThumb</c>,
    /// <c>imgMediumThumb</c>) are preferred because they survive caching; the <c>*ObjUrl</c> variants (which may be
    /// temporary) are only a fallback. Only absolute HTTPS URLs are ever returned.
    /// </summary>
    public static class ImageSelector
    {
        public static string? For(VideoSummary video, ImageRole role)
        {
            if (video == null)
            {
                throw new ArgumentNullException(nameof(video));
            }

            switch (role)
            {
                case ImageRole.Thumbnail:
                    return First(video.ThumbUrl, video.MediumThumbUrl, video.ImageUrl, video.ThumbObjUrl, video.MediumThumbObjUrl, video.ImageObjUrl);
                case ImageRole.Poster:
                case ImageRole.Banner:
                    return First(video.ImageUrl, video.MediumThumbUrl, video.ThumbUrl, video.ImageObjUrl, video.MediumThumbObjUrl, video.ThumbObjUrl);
                default:
                    return First(video.MediumThumbUrl, video.ThumbUrl, video.ImageUrl, video.MediumThumbObjUrl, video.ThumbObjUrl, video.ImageObjUrl);
            }
        }

        public static string? For(StaffMember staff)
        {
            if (staff == null)
            {
                throw new ArgumentNullException(nameof(staff));
            }

            return First(staff.MediumThumbUrl, staff.ThumbUrl, staff.ImageUrl);
        }

        private static string? First(params string?[] candidates)
        {
            foreach (var candidate in candidates)
            {
                Uri uri;
                if (UrlValidator.TryGetHttpsUri(candidate, out uri))
                {
                    return uri.AbsoluteUri;
                }
            }

            return null;
        }
    }
}
