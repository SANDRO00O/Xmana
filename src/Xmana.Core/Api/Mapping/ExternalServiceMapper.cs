using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Xmana.Core.Json;
using Xmana.Core.Models;

namespace Xmana.Core.Api.Mapping
{
    public static class ExternalServiceMapper
    {
        /// <summary>
        /// Maps the recommendation response defensively. UNKNOWN (API.md section 17): the exact item schema. Known: the
        /// response carries results plus <c>expirationDate</c>. Items are found structurally (bare list, <c>info</c>, or the
        /// first list of objects); an item's id is read from <c>nb</c> or, failing that, <c>MovieId</c> (the name used by the
        /// request). Items without an id are dropped; duplicates are removed.
        /// </summary>
        public static RecommendationResult ToRecommendations(JToken? token)
        {
            var items = new List<RecommendationItem>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var element in JsonItems.Extract(token))
            {
                var obj = element as JObject;
                if (obj == null)
                {
                    continue;
                }

                var id = JsonRead.Text(JsonRead.Get(obj, "nb")) ?? JsonRead.Text(JsonRead.Get(obj, "MovieId"));
                if (id == null || !seen.Add(id))
                {
                    continue;
                }

                var title = JsonRead.Text(JsonRead.Get(obj, "en_title")) ?? JsonRead.Text(JsonRead.Get(obj, "MovieName"));
                items.Add(new RecommendationItem(id, title, VideoMapper.ToSummary(obj), obj));
            }

            return new RecommendationResult(items, JsonRead.Text(JsonRead.Get(token, "expirationDate")), token ?? new JObject());
        }
    }
}
