using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Xmana.Core.Models
{
    /// <summary>
    /// One item of the public recommendation service. The item schema is NOT documented (API.md section 17 item 7), so
    /// only the id and title are read defensively and the raw object is kept. <see cref="Video"/> is set when the item
    /// looks like a normal video object. Items may carry temporary signed URLs inside <see cref="Raw"/>: never log or persist it.
    /// </summary>
    public sealed class RecommendationItem
    {
        public RecommendationItem(string id, string? title, VideoSummary? video, JObject raw)
        {
            Id = id;
            Title = title;
            Video = video;
            Raw = raw;
        }

        public string Id { get; }

        public string? Title { get; }

        public VideoSummary? Video { get; }

        public JObject Raw { get; }

        public override string ToString() => Id;
    }

    /// <summary>Recommendation response. <see cref="ExpirationDate"/> is the documented <c>expirationDate</c> text, kept as sent.</summary>
    public sealed class RecommendationResult
    {
        public RecommendationResult(IReadOnlyList<RecommendationItem> items, string? expirationDate, JToken raw)
        {
            Items = items;
            ExpirationDate = expirationDate;
            Raw = raw;
        }

        public IReadOnlyList<RecommendationItem> Items { get; }

        public string? ExpirationDate { get; }

        public bool HasItems => Items.Count > 0;

        public JToken Raw { get; }
    }
}
