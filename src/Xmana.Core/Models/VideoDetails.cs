using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Xmana.Core.Json;

namespace Xmana.Core.Models
{
    /// <summary>The primary metadata model: <c>allVideoInfo</c> (PROMPT.md section 9).</summary>
    public sealed class VideoDetails
    {
        public VideoDetails(
            VideoSummary summary,
            string? otherTitle,
            string? arContent,
            string? enContent,
            string? mDate,
            string? itemDate,
            string? duration,
            string? imdbUrl,
            string? filmRating,
            string? seriesRating,
            string? rate,
            string? ratingImage,
            bool? isSpecial,
            bool? showComments,
            string? episodeFlag,
            string? audioStreamNumber,
            string? parentSkipping,
            string? collectionId,
            bool? isDeleted,
            bool? useParentImage,
            bool? hasIntroSkipping,
            bool? castable,
            long? likes,
            long? dislikes,
            int? commentsCount,
            long? viewsCount,
            JToken? videoLanguages,
            IReadOnlyList<StaffMember> directors,
            IReadOnlyList<StaffMember> actors,
            IReadOnlyList<StaffMember> writers)
        {
            Summary = summary;
            OtherTitle = otherTitle;
            ArContent = arContent;
            EnContent = enContent;
            MDate = mDate;
            ItemDate = itemDate;
            Duration = duration;
            ImdbUrl = imdbUrl;
            FilmRating = filmRating;
            SeriesRating = seriesRating;
            Rate = rate;
            RatingImage = ratingImage;
            IsSpecial = isSpecial;
            ShowComments = showComments;
            EpisodeFlag = episodeFlag;
            AudioStreamNumber = audioStreamNumber;
            ParentSkipping = parentSkipping;
            CollectionId = collectionId;
            IsDeleted = isDeleted;
            UseParentImage = useParentImage;
            HasIntroSkipping = hasIntroSkipping;
            Castable = castable;
            Likes = likes;
            Dislikes = dislikes;
            CommentsCount = commentsCount;
            ViewsCount = viewsCount;
            VideoLanguages = videoLanguages;
            Directors = directors;
            Actors = actors;
            Writers = writers;
        }

        /// <summary>Id, titles, images, season/episode, categories ... shared with list items.</summary>
        public VideoSummary Summary { get; }

        public string Id => Summary.Id;

        public string? OtherTitle { get; }

        public string? ArContent { get; }

        public string? EnContent { get; }

        public string? MDate { get; }

        public string? ItemDate { get; }

        /// <summary>Kept as the server sent it; its unit/format is not documented.</summary>
        public string? Duration { get; }

        public string? ImdbUrl { get; }

        public string? FilmRating { get; }

        public string? SeriesRating { get; }

        public string? Rate { get; }

        public string? RatingImage { get; }

        public bool? IsSpecial { get; }

        public bool? ShowComments { get; }

        public string? EpisodeFlag { get; }

        public string? AudioStreamNumber { get; }

        /// <summary>Server <c>parent_skipping</c>, preserved as sent (meaning not documented).</summary>
        public string? ParentSkipping { get; }

        public string? CollectionId { get; }

        public bool? IsDeleted { get; }

        public bool? UseParentImage { get; }

        public bool? HasIntroSkipping { get; }

        public bool? Castable { get; }

        public long? Likes { get; }

        public long? Dislikes { get; }

        public int? CommentsCount { get; }

        public long? ViewsCount { get; }

        /// <summary>Server <c>videoLanguages</c>; documented only as "language metadata", so kept raw.</summary>
        public JToken? VideoLanguages { get; }

        /// <summary>English name read from the <c>videoLanguages</c> object (API.md: en_title, ar_title).</summary>
        public string? LanguageEnTitle => JsonRead.Text(JsonRead.Get(VideoLanguages, "en_title"));

        public string? LanguageArTitle => JsonRead.Text(JsonRead.Get(VideoLanguages, "ar_title"));

        public IReadOnlyList<StaffMember> Directors { get; }

        public IReadOnlyList<StaffMember> Actors { get; }

        public IReadOnlyList<StaffMember> Writers { get; }
    }
}
