using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Xmana.Core.Json
{
    /// <summary>
    /// Finds the item list inside the response styles API.md describes: a bare list, an object with an
    /// "info" list, or (last resort) an object whose first list-of-objects property is the data.
    /// </summary>
    public static class JsonItems
    {
        private static readonly IReadOnlyList<JToken> None = new JToken[0];

        public static IReadOnlyList<JToken> Extract(JToken? token)
        {
            var array = token as JArray;
            if (array != null)
            {
                return array.ToList();
            }

            var obj = token as JObject;
            if (obj == null)
            {
                return None;
            }

            var info = JsonRead.Array(JsonRead.Get(obj, "info"));
            if (info != null)
            {
                return info.ToList();
            }

            foreach (var property in obj.Properties())
            {
                var list = property.Value as JArray;
                if (list != null && list.Count > 0 && list.Any(item => item is JObject))
                {
                    return list.ToList();
                }
            }

            return None;
        }
    }
}
