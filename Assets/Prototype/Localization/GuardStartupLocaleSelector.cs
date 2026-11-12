using System;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
namespace GansuuHead
{
    [Serializable, UnityEngine.Scripting.Preserve]
    public sealed class GuardStartupLocaleSelector : IStartupLocaleSelector
    {
        public const string PreferenceKey="GansuuHead.Locale.v1";
        public static string Resolve(string saved,SystemLanguage systemLanguage)
            => saved=="ko-KR" || saved=="en" ? saved : systemLanguage==SystemLanguage.Korean?"ko-KR":"en";
        public Locale GetStartupLocale(ILocalesProvider availableLocales)
        {
            string saved=PlayerPrefs.GetString(PreferenceKey,string.Empty);
            if(!string.IsNullOrEmpty(saved)){var stored=availableLocales.GetLocale(new LocaleIdentifier(saved));if(stored!=null)return stored;}
            return availableLocales.GetLocale(new LocaleIdentifier(Resolve(string.Empty,Application.systemLanguage)));
        }
    }
}

