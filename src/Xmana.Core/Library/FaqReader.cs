using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Xmana.Core.Json;
using Xmana.Core.Models;

namespace Xmana.Core.Library
{
    public sealed class FaqItem
    {
        public FaqItem(string question, string? answer)
        {
            Question = question;
            Answer = answer;
        }

        public string Question { get; }

        public string? Answer { get; }
    }

    /// <summary>
    /// Reads FAQ entries defensively. API.md (section 17 item 6) did not capture the field schema of <c>/faqQuestion</c>, so nothing is
    /// assumed: for each entry the text properties are scanned and a "question" and an "answer" are taken from properties whose names
    /// say so (preferring the interface language's variant). Entries without a readable question are skipped; if nothing is readable the
    /// FAQ is simply reported as unavailable. Update this reader (and docs/api-notes.md) once a real response is captured.
    /// </summary>
    public static class FaqReader
    {
        public static IReadOnlyList<FaqItem> Read(IEnumerable<FaqEntry> entries, bool preferArabic)
        {
            var result = new List<FaqItem>();
            foreach (var entry in entries ?? new FaqEntry[0])
            {
                var texts = entry.Raw.Properties()
                    .Select(p => new { p.Name, Value = JsonRead.Text(p.Value) })
                    .Where(p => p.Value != null)
                    .ToList();

                var question = Pick(texts.Select(t => Tuple.Create(t.Name, t.Value!)), "question", preferArabic);
                if (question == null)
                {
                    continue;
                }

                var answer = Pick(texts.Select(t => Tuple.Create(t.Name, t.Value!)), "answer", preferArabic);
                result.Add(new FaqItem(question, answer));
            }

            return result;
        }

        private static string? Pick(IEnumerable<Tuple<string, string>> texts, string word, bool preferArabic)
        {
            var best = texts
                .Where(t => t.Item1.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(t => new { t.Item2, Score = Score(t.Item1, preferArabic) })
                .OrderByDescending(t => t.Score)
                .FirstOrDefault();
            return best?.Item2;
        }

        // 2 = the interface language's variant, 1 = language-neutral, 0 = the other language's variant.
        // Recognised variant markers: "ar_x", "x_ar", "arX" (and the same for "en").
        private static int Score(string name, bool preferArabic)
        {
            var arabic = HasMarker(name, "ar");
            var english = HasMarker(name, "en");
            if (arabic == english)
            {
                return 1;
            }

            return arabic == preferArabic ? 2 : 0;
        }

        private static bool HasMarker(string name, string code)
        {
            var lower = name.ToLowerInvariant();
            return lower.StartsWith(code + "_", StringComparison.Ordinal)
                || lower.EndsWith("_" + code, StringComparison.Ordinal)
                || (name.Length > code.Length && lower.StartsWith(code, StringComparison.Ordinal) && char.IsUpper(name[code.Length]));
        }
    }
}
