using Newtonsoft.Json;
using System.Collections.Generic;

namespace Xmana.Models
{
    public sealed class RecommendationRequest
    {
        [JsonProperty("MovieId")]
        public string MovieId { get; set; }

        [JsonProperty("MovieName")]
        public string MovieName { get; set; }

        [JsonProperty("ReProcessIfExpired")]
        public bool ReProcessIfExpired { get; set; } = true;
    }

    public sealed class RatingTarget
    {
        [JsonProperty("targetId")]
        public string TargetId { get; set; }

        [JsonProperty("targetType")]
        public string TargetType { get; set; }
    }

    public sealed class RatingSummaryRequest
    {
        [JsonProperty("targets")]
        public IList<RatingTarget> Targets { get; set; }
    }
}
