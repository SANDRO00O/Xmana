using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;

namespace Xmana.Core.Json
{
    public static class JsonParser
    {
        /// <summary>
        /// Parses a JSON document into a token tree. Strings that look like dates stay strings
        /// (fields such as mDate/itemDate must be preserved exactly). Throws <see cref="JsonException"/>
        /// for empty input, invalid JSON, or trailing content.
        /// </summary>
        public static JToken Parse(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new JsonReaderException("Empty JSON body.");
            }

            using (var stringReader = new StringReader(json!))
            using (var reader = new JsonTextReader(stringReader)
            {
                DateParseHandling = DateParseHandling.None,
                FloatParseHandling = FloatParseHandling.Double,
            })
            {
                var token = JToken.ReadFrom(reader);
                while (reader.Read())
                {
                    if (reader.TokenType != JsonToken.Comment)
                    {
                        throw new JsonReaderException("Unexpected content after the JSON document.");
                    }
                }

                return token;
            }
        }
    }
}
