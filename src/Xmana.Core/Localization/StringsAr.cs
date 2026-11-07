using System.Collections.Generic;

namespace Xmana.Core.Localization
{
    public static class StringsAr
    {
        public static readonly IReadOnlyDictionary<string, string> All = new Dictionary<string, string>
        {
            [K.AppName] = "Xmana",
            [K.AppTagline] = "Xmana — أفلام ومسلسلات وأنمي",
            [K.AppUnofficial] = "Xmana تطبيق غير رسمي، وليس تابعًا لـ Cinemana أو Shabakaty أو Microsoft أو Netflix ولا معتمدًا منها.",

            [K.NavHome] = "الرئيسية",
            [K.NavMovies] = "أفلام",
            [K.NavSeries] = "مسلسلات",
            [K.NavAnime] = "أنمي",
            [K.NavCategories] = "التصنيفات",
            [K.NavSearch] = "بحث",
            [K.NavFavorites] = "المفضلة",
            [K.NavWatchlist] = "قائمة المشاهدة",
            [K.NavHistory] = "السجل",
            [K.NavSettings] = "الإعدادات",

            [K.HintSelect] = "اختيار",
            [K.HintBack] = "رجوع",
            [K.HintSwitchSection] = "تبديل القسم",

            [K.SectionBuilding] = "هذا القسم قيد الإنشاء.",

            [K.SettingsTitle] = "الإعدادات",
            [K.SettingsInterface] = "الواجهة",
            [K.SettingsLanguage] = "اللغة",
            [K.SettingsLanguageEnglish] = "English",
            [K.SettingsLanguageArabic] = "العربية",
            [K.SettingsAbout] = "حول التطبيق",
            [K.SettingsVersion] = "الإصدار",
            [K.SettingsLegal] = "الإشعارات القانونية والإسناد",
            [K.SettingsAttribution] = "تأتي بيانات المحتوى من واجهة Cinemana/Shabakaty العامة. يحفظ Xmana المفضلة والسجل والإعدادات على هذا الجهاز فقط.",
        };
    }
}
