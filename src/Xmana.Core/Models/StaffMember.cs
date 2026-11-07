using Newtonsoft.Json.Linq;

namespace Xmana.Core.Models
{
    /// <summary>Actor/director/writer entry (API.md: nb, name, role, staff_img, staff_img_thumb, staff_img_medium_thumb).</summary>
    public sealed class StaffMember
    {
        public StaffMember(string? id, string? name, string? role, string? imageUrl, string? thumbUrl, string? mediumThumbUrl, JObject raw)
        {
            Id = id;
            Name = name;
            Role = role;
            ImageUrl = imageUrl;
            ThumbUrl = thumbUrl;
            MediumThumbUrl = mediumThumbUrl;
            Raw = raw;
        }

        public string? Id { get; }

        public string? Name { get; }

        public string? Role { get; }

        public string? ImageUrl { get; }

        public string? ThumbUrl { get; }

        public string? MediumThumbUrl { get; }

        /// <summary>The server object exactly as received (preserved for debugging and forward compatibility).</summary>
        public JObject Raw { get; }
    }
}
