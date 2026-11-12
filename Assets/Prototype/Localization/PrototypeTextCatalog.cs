using System;
using System.Collections.Generic;
using UnityEngine;
namespace GansuuHead
{
    [CreateAssetMenu(menuName="Gansuu Head/UI Text Catalog")]
    public sealed class PrototypeTextCatalog : PrototypeTextSource
    {
        [Serializable] public class Entry{public string key;public string text;}
        [Serializable] public class Document{public Entry[] entries;}
        public TextAsset source;
        Dictionary<string,string> values;
        public override string Get(string key)
        {
            if(values==null)Reload();
            return values.TryGetValue(key,out string value)?value:key;
        }
        public void Reload()
        {
            values=new Dictionary<string,string>(StringComparer.Ordinal);
            if(source!=null){var document=JsonUtility.FromJson<Document>(source.text);
                if(document!=null && document.entries!=null)foreach(var entry in document.entries)
                    if(entry!=null && !string.IsNullOrEmpty(entry.key))values[entry.key]=entry.text??string.Empty;
            }
            NotifyChanged();
        }
        void OnEnable(){values=null;NotifyChanged();}
        void OnValidate(){values=null;NotifyChanged();}
    }
    public static class PrototypeTextKeys
    {
        public const string Title="game.title",Instructions="game.instructions",ChooseDuration="menu.choose_duration",
            Paused="pause.title",Resume="pause.resume",Success="result.success",Failure="result.failure",
            Start="menu.start",Retry="menu.retry",Duration="mode.duration_minutes",Remaining="hud.remaining",
            Drowsiness="hud.drowsiness",DeveloperStatus="dev.status",Language="menu.language",Korean="language.ko",English="language.en";
        public const string MainMenu="pause.main_menu",Quit="menu.quit",QuitQuestion="quit.question",Confirm="quit.confirm",Cancel="quit.cancel";
        public static readonly string[] All={Title,Instructions,ChooseDuration,Paused,Resume,Success,Failure,Start,Retry,Duration,Remaining,Drowsiness,DeveloperStatus,Language,Korean,English,MainMenu,Quit,QuitQuestion,Confirm,Cancel};
    }
}

