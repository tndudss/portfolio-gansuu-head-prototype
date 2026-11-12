using System.Collections;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.ResourceManagement.AsyncOperations;
namespace GansuuHead
{
    public sealed class PrototypeLocaleController : MonoBehaviour
    {
        public UnityLocalizedTextSource textSource;
        public bool IsReady {get;private set;}
        public string CurrentCode=>textSource==null?"en":textSource.CurrentCode;
        IEnumerator Start()
        {
            var initialization=LocalizationSettings.InitializationOperation;
            yield return initialization;
            if(initialization.Status!=AsyncOperationStatus.Succeeded){Debug.LogError("Localization initialization failed.");yield break;}
            LocalizationSettings.SelectedLocaleChanged+=Apply;
            Apply(LocalizationSettings.SelectedLocale);IsReady=true;
        }
        void Apply(Locale locale){if(locale!=null && textSource!=null)textSource.SetLocale(locale.Identifier.Code);}
        public void SelectLanguage(string code)
        {
            if(!IsReady || string.IsNullOrEmpty(code))return;
            var locale=LocalizationSettings.AvailableLocales.GetLocale(new LocaleIdentifier(code));
            if(locale==null)return;
            LocalizationSettings.SelectedLocale=locale;Apply(locale);
            PlayerPrefs.SetString(GuardStartupLocaleSelector.PreferenceKey,code);PlayerPrefs.Save();
        }
        void OnDestroy(){LocalizationSettings.SelectedLocaleChanged-=Apply;}
    }
}

