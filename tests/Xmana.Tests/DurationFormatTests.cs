using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xmana.Core.Details;

namespace Xmana.Tests
{
    [TestClass]
    public class DurationFormatTests
    {
        [DataTestMethod]
        [DataRow("1:35:20", "1:35:20")]
        [DataRow(" 95:10 ", "95:10")]
        [DataRow("0:45", "0:45")]
        public void ClockStyleValues_AreShownAsSent(string raw, string expected)
        {
            Assert.IsTrue(DurationFormat.TryFormat(raw, out var text));
            Assert.AreEqual(expected, text);
        }

        [DataTestMethod]
        [DataRow("1500")]
        [DataRow("95")]
        [DataRow("")]
        [DataRow("   ")]
        [DataRow(null)]
        [DataRow("1h 35m")]
        [DataRow("1:99")]
        [DataRow("abc")]
        public void BareNumbersAndUnknownFormats_AreNeverGuessed(string? raw)
        {
            Assert.IsFalse(DurationFormat.TryFormat(raw, out var text));
            Assert.AreEqual(string.Empty, text);
        }
    }
}
