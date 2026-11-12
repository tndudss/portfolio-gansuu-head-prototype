using System;
using System.IO;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEditor;
using GansuuHead;
public static class BilingualPreferenceVerification
{
    const string Journal="AgentScripts/bilingual-preference-journal.json";
    [Serializable] public class Record{public bool hadKey,restored;public string original,expected;public int successfulRestarts;}
    static Record Read()=>File.Exists(Journal)?JsonUtility.FromJson<Record>(File.ReadAllText(Journal)):null;
    static void Save(Record record)=>File.WriteAllText(Journal,JsonUtility.ToJson(record,true));
    static void Choose(string code)
    {
        var c=UnityEngine.Object.FindAnyObjectByType<MainPrototype>();if(!EditorApplication.isPlaying || !c.localizedUi.Ready)throw new Exception("Run initialized Play first");
        var record=Read();if(record==null || record.restored)record=new Record{hadKey=PlayerPrefs.HasKey(GuardStartupLocaleSelector.PreferenceKey),original=PlayerPrefs.GetString(GuardStartupLocaleSelector.PreferenceKey,"")};
        record.expected=code;Save(record);c.localizedUi.SelectLanguage(code);
    }
    [MenuItem("Tools/Gansuu Head/Bilingual/Persistence Save English")]
    public static void English(){Choose("en");}
    [MenuItem("Tools/Gansuu Head/Bilingual/Persistence Save Korean")]
    public static void Korean(){Choose("ko-KR");}
    [MenuItem("Tools/Gansuu Head/Bilingual/Persistence Verify Restart")]
    public static void Verify()
    {
        var r=Read();var c=UnityEngine.Object.FindAnyObjectByType<MainPrototype>();
        if(r==null || !EditorApplication.isPlaying || !c.localizedUi.Ready || c.localizedUi.locale.CurrentCode!=r.expected || PlayerPrefs.GetString(GuardStartupLocaleSelector.PreferenceKey)!=r.expected)
            throw new Exception("Saved language was not restored after Play restart");
        r.successfulRestarts++;Save(r);
    }
    [MenuItem("Tools/Gansuu Head/Bilingual/Persistence Restore Original")]
    public static void Restore()
    {
        var r=Read();if(r==null || r.restored)return;
        string key=GuardStartupLocaleSelector.PreferenceKey;if(r.hadKey)PlayerPrefs.SetString(key,r.original);else PlayerPrefs.DeleteKey(key);PlayerPrefs.Save();
        if(EditorApplication.isPlaying){string code=GuardStartupLocaleSelector.Resolve(r.original,Application.systemLanguage);var locale=LocalizationSettings.AvailableLocales.GetLocale(new LocaleIdentifier(code));LocalizationSettings.SelectedLocale=locale;((UnityLocalizedTextSource)UnityEngine.Object.FindAnyObjectByType<MainPrototype>().uiText).SetLocale(code);}
        r.restored=true;Save(r);
    }
}
