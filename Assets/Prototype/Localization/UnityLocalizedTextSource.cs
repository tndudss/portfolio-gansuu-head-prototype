using System.Globalization;
using UnityEngine;
using UnityEngine.Localization.Tables;
namespace GansuuHead
{
    [CreateAssetMenu(menuName="Gansuu Head/Unity Localized Text Source")]
    public sealed class UnityLocalizedTextSource : PrototypeTextSource
    {
        public StringTable english,korean;
        public StringTable[] additionalTables;
        string currentCode="en";
        public string CurrentCode=>currentCode;
        StringTable Table(string code){if(code=="en")return english;if(code=="ko-KR")return korean;if(additionalTables!=null)foreach(var table in additionalTables)if(table!=null && table.LocaleIdentifier.Code==code)return table;return null;}
        public void SetLocale(string code){code=Table(code)==null?"en":code;if(code==currentCode)return;currentCode=code;NotifyChanged();}
        public override string Get(string key)
        {
            var entry=Table(currentCode)?.GetEntry(key);
            if(entry==null || string.IsNullOrEmpty(entry.Value))entry=english?.GetEntry(key);
            return entry==null || string.IsNullOrEmpty(entry.Value)?key:entry.Value;
        }
        public override string Format(string key,params object[] arguments)
        {
            var value=Get(key);
            try{return string.Format(CultureInfo.GetCultureInfo(currentCode),value,arguments);}
            catch(System.FormatException){
                var fallback=english?.GetEntry(key)?.Value;
                if(!string.IsNullOrEmpty(fallback))try{return string.Format(CultureInfo.GetCultureInfo("en"),fallback,arguments);}catch(System.FormatException){}
                return key;
            }
        }
    }
}

