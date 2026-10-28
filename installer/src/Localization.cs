// =============================================================================
//  Isora Sentinel Suite - Internationalization & Localization Engine (I18n)
//  Copyright & Architecture: Sandra Gabriela Puerto Torres
//  Distribution: Isora Cloud Group (https://isora.com.co)
//  ISO/IEC 27001:2022 & ISO/IEC 20000-1 Enterprise Multi-Language Framework
// =============================================================================

using System;
using System.Globalization;

namespace SentinelIT
{
    public enum Language
    {
        Spanish,
        English
    }

    public static class Loc
    {
        private static Language currentLanguage = Language.Spanish;
        private static bool isInitialized = false;

        public static event Action LanguageChanged;

        static Loc()
        {
            InitializeFromSystem();
        }

        public static void InitializeFromSystem()
        {
            if (isInitialized) return;
            try
            {
                string twoLetter = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
                currentLanguage = (twoLetter == "es") ? Language.Spanish : Language.English;
            }
            catch
            {
                currentLanguage = Language.Spanish;
            }
            isInitialized = true;
        }

        public static Language Current
        {
            get { return currentLanguage; }
            set
            {
                if (currentLanguage != value)
                {
                    currentLanguage = value;
                    if (LanguageChanged != null)
                    {
                        try { LanguageChanged.Invoke(); } catch { }
                    }
                }
            }
        }

        public static bool IsSpanish
        {
            get { return currentLanguage == Language.Spanish; }
        }

        public static bool IsEnglish
        {
            get { return currentLanguage == Language.English; }
        }

        public static void SetLanguage(string code)
        {
            if (string.IsNullOrEmpty(code)) return;
            string c = code.Trim().ToLowerInvariant();
            if (c.StartsWith("en"))
                Current = Language.English;
            else if (c.StartsWith("es"))
                Current = Language.Spanish;
        }

        public static string T(string es, string en)
        {
            return (currentLanguage == Language.Spanish) ? es : en;
        }

        public static string Format(string esFormat, string enFormat, params object[] args)
        {
            string template = (currentLanguage == Language.Spanish) ? esFormat : enFormat;
            return string.Format(template, args);
        }
    }
}

