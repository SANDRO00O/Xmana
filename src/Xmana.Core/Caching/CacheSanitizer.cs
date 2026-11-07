using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Xmana.Core.Caching
{
    /// <summary>
    /// Produces the JSON that is allowed to rest on disk. Properties whose name ends in "ObjUrl"
    /// (imgObjUrl, imgThumbObjUrl, imgMediumThumbObjUrl ...) can carry temporary signed URLs and are
    /// removed (PROMPT.md section 34: never cache signed URLs as permanent resources). The input
    /// token is not modified: callers keep the full data for the current session.
    /// </summary>
    public static class CacheSanitizer
    {
        public const string SignedUrlSuffix = "ObjUrl";

        public static string ToCacheJson(JToken token)
        {
            var clone = token.DeepClone();
            Strip(clone);
            return clone.ToString(Formatting.None);
        }

        private static void Strip(JToken token)
        {
            var obj = token as JObject;
            if (obj != null)
            {
                var doomed = obj.Properties()
                    .Where(p => p.Name.EndsWith(SignedUrlSuffix, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                foreach (var property in doomed)
                {
                    property.Remove();
                }

                foreach (var child in obj.Properties().Select(p => p.Value).ToList())
                {
                    Strip(child);
                }

                return;
            }

            var array = token as JArray;
            if (array != null)
            {
                foreach (var child in array.ToList())
                {
                    Strip(child);
                }
            }
        }
    }
}
