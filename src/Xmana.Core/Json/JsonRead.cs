using System;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace Xmana.Core.Json
{
    /// <summary>
    /// Tolerant readers for remote JSON (PROMPT.md section 59). The API sometimes sends numbers as
    /// strings, omits fields, or sends null; none of that may throw. Every reader returns null when
    /// the value is missing or cannot be interpreted.
    /// </summary>
    public static class JsonRead
    {
        /// <summary>Case-insensitive property lookup on an object token; null for non-objects.</summary>
        public static JToken? Get(JToken? token, string name)
        {
            var obj = token as JObject;
            if (obj == null)
            {
                return null;
            }

            JToken? value;
            return obj.TryGetValue(name, StringComparison.OrdinalIgnoreCase, out value) ? value : null;
        }

        /// <summary>Scalar as a string (numbers and booleans are rendered invariantly); null for null/objects/arrays.</summary>
        public static string? String(JToken? token)
        {
            var value = token as JValue;
            if (value == null)
            {
                return null;
            }

            switch (value.Type)
            {
                case JTokenType.String:
                    return value.Value as string;
                case JTokenType.Boolean:
                    return (bool)value.Value! ? "true" : "false";
                case JTokenType.Integer:
                case JTokenType.Float:
                    return Convert.ToString(value.Value, CultureInfo.InvariantCulture);
                default:
                    return null;
            }
        }

        /// <summary>Trimmed string, or null when missing or blank.</summary>
        public static string? Text(JToken? token)
        {
            var text = String(token);
            if (text == null)
            {
                return null;
            }

            text = text.Trim();
            return text.Length == 0 ? null : text;
        }

        public static long? Long(JToken? token)
        {
            var value = token as JValue;
            if (value == null)
            {
                return null;
            }

            switch (value.Type)
            {
                case JTokenType.Integer:
                    try
                    {
                        return Convert.ToInt64(value.Value, CultureInfo.InvariantCulture);
                    }
                    catch (OverflowException)
                    {
                        return null;
                    }
                    catch (InvalidCastException)
                    {
                        return null;
                    }

                case JTokenType.Float:
                {
                    var number = Convert.ToDouble(value.Value, CultureInfo.InvariantCulture);
                    if (double.IsNaN(number) || double.IsInfinity(number) || Math.Floor(number) != number || Math.Abs(number) > 9e18)
                    {
                        return null;
                    }

                    return (long)number;
                }

                case JTokenType.String:
                {
                    var text = (value.Value as string)?.Trim();
                    long parsed;
                    if (text != null && long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out parsed))
                    {
                        return parsed;
                    }

                    return null;
                }

                default:
                    return null;
            }
        }

        public static int? Int(JToken? token)
        {
            var number = Long(token);
            if (number == null || number.Value < int.MinValue || number.Value > int.MaxValue)
            {
                return null;
            }

            return (int)number.Value;
        }

        public static double? Double(JToken? token)
        {
            var value = token as JValue;
            if (value == null)
            {
                return null;
            }

            double result;
            switch (value.Type)
            {
                case JTokenType.Integer:
                case JTokenType.Float:
                    try
                    {
                        result = Convert.ToDouble(value.Value, CultureInfo.InvariantCulture);
                    }
                    catch (InvalidCastException)
                    {
                        return null;
                    }

                    break;
                case JTokenType.String:
                    var text = (value.Value as string)?.Trim();
                    if (text == null || !double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out result))
                    {
                        return null;
                    }

                    break;
                default:
                    return null;
            }

            return double.IsNaN(result) || double.IsInfinity(result) ? (double?)null : result;
        }

        /// <summary>true/false, 1/0, or the strings "true"/"false"/"1"/"0" (any case); null otherwise.</summary>
        public static bool? Bool(JToken? token)
        {
            var value = token as JValue;
            if (value == null)
            {
                return null;
            }

            switch (value.Type)
            {
                case JTokenType.Boolean:
                    return (bool)value.Value!;
                case JTokenType.Integer:
                {
                    var number = Long(value);
                    return number == 1 ? true : number == 0 ? false : (bool?)null;
                }

                case JTokenType.String:
                {
                    var text = (value.Value as string)?.Trim();
                    if (string.Equals(text, "true", StringComparison.OrdinalIgnoreCase) || text == "1")
                    {
                        return true;
                    }

                    if (string.Equals(text, "false", StringComparison.OrdinalIgnoreCase) || text == "0")
                    {
                        return false;
                    }

                    return null;
                }

                default:
                    return null;
            }
        }

        public static JArray? Array(JToken? token) => token as JArray;

        public static JObject? Object(JToken? token) => token as JObject;
    }
}
