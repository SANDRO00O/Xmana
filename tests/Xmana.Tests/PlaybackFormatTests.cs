using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xmana.Core.Models;
using Xmana.Core.Playback;

namespace Xmana.Tests
{
    [TestClass]
    public class PlaybackFormatTests
    {
        [DataTestMethod]
        [DataRow(0, "0:00")]
        [DataRow(5, "0:05")]
        [DataRow(65, "1:05")]
        [DataRow(3599, "59:59")]
        [DataRow(3600, "1:00:00")]
        [DataRow(3723, "1:02:03")]
        [DataRow(-5, "0:00")]
        public void Time_IsCompact(int seconds, string expected)
        {
            Assert.AreEqual(expected, PlaybackFormat.Time(TimeSpan.FromSeconds(seconds)));
        }

        [TestMethod]
        public void Time_ClampsAbsurdValues()
        {
            Assert.AreEqual("0:00", PlaybackFormat.Time(TimeSpan.FromDays(400)));
        }

        [TestMethod]
        public void Fraction_IsClamped_AndZeroWithoutADuration()
        {
            Assert.AreEqual(0.5, PlaybackFormat.Fraction(TimeSpan.FromSeconds(50), TimeSpan.FromSeconds(100)));
            Assert.AreEqual(0, PlaybackFormat.Fraction(TimeSpan.FromSeconds(50), TimeSpan.Zero));
            Assert.AreEqual(1, PlaybackFormat.Fraction(TimeSpan.FromSeconds(500), TimeSpan.FromSeconds(100)));
            Assert.AreEqual(0, PlaybackFormat.Fraction(TimeSpan.FromSeconds(-5), TimeSpan.FromSeconds(100)));
        }

        [TestMethod]
        public void SubtitleLanguages_AreShownInTheirOwnScript()
        {
            Assert.AreEqual("English", PlaybackFormat.SubtitleLanguageName(SubtitleLanguage.English));
            Assert.AreEqual("\u0627\u0644\u0639\u0631\u0628\u064A\u0629", PlaybackFormat.SubtitleLanguageName(SubtitleLanguage.Arabic));
            Assert.AreEqual("Espa\u00F1ol", PlaybackFormat.SubtitleLanguageName(SubtitleLanguage.Spanish));
        }
    }
}
