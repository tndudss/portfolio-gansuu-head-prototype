using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEditor;
using TMPro;
using GansuuHead;
public static partial class BilingualVerification
{
    [Serializable] public class Report{public bool success;public string error;public List<string> passed=new List<string>();public string systemLanguage,initialLocale;public bool hadSavedPreference;public int uiStates,renderedFrames;}
    static void Check(bool value,string name,Report r){if(!value)throw new Exception(name);r.passed.Add(name);}
    [MenuItem("Tools/Gansuu Head/Bilingual/Verify Assets And Rules")]
    public static void VerifyAssets()
    {
        var r=new Report();UnityLocalizedTextSource source=null;string oldCode=null;StringTable temporary=null;UnityLocalizedTextSource probe=null;
        try{
            var c=UnityEngine.Object.FindAnyObjectByType<MainPrototype>();source=c.uiText as UnityLocalizedTextSource;oldCode=source?.CurrentCode;
            Check(source!=null && c.localizedUi!=null,"Official String Table source and TMP view connected",r);
            var locales=UnityEditor.Localization.LocalizationEditorSettings.GetLocales();Check(locales.Count==2 && locales.Any(x=>x.Identifier.Code=="ko-KR") && locales.Any(x=>x.Identifier.Code=="en"),"Exactly en and ko-KR Locale assets",r);
            Check(LocalizationSettings.StartupLocaleSelectors[0] is GuardStartupLocaleSelector,"Saved preference/OS selector configured first",r);
            foreach(var test in new[]{(saved:"",system:SystemLanguage.Korean,expected:"ko-KR"),(saved:"",system:SystemLanguage.English,expected:"en"),(saved:"",system:SystemLanguage.French,expected:"en"),(saved:"en",system:SystemLanguage.Korean,expected:"en"),(saved:"ko-KR",system:SystemLanguage.English,expected:"ko-KR"),(saved:"invalid",system:SystemLanguage.Korean,expected:"ko-KR")})
                Check(GuardStartupLocaleSelector.Resolve(test.saved,test.system)==test.expected,"Startup rule "+test.saved+" / "+test.system,r);
            foreach(string code in new[]{"en","ko-KR"}){
                source.SetLocale(code);var table=code=="en"?source.english:source.korean;
                foreach(string key in PrototypeTextKeys.All){
                    var entry=table.GetEntry(key);Check(entry!=null && !string.IsNullOrWhiteSpace(entry.Value),"Translation exists "+code+" "+key,r);
                    object[] args=key==PrototypeTextKeys.Remaining?new object[]{1,5}:key==PrototypeTextKeys.Drowsiness?new object[]{65f}:key==PrototypeTextKeys.Duration?new object[]{5}:key==PrototypeTextKeys.DeveloperStatus?new object[]{1f,.15f}:Array.Empty<object>();
                    string formatted=string.Format(System.Globalization.CultureInfo.GetCultureInfo(code),entry.Value,args);
                    Check(c.localizedUi.title.font.HasCharacters(formatted,out uint[] missing,true,false),"Prewarmed TMP glyphs "+code+" "+key,r);
                }
                Check(source.Format(PrototypeTextKeys.Remaining,1,5).Contains("01:05"),"Localized timer padding "+code,r);
            }
            temporary=UnityEngine.Object.Instantiate(source.korean);temporary.RemoveEntry(PrototypeTextKeys.Title);
            probe=ScriptableObject.CreateInstance<UnityLocalizedTextSource>();probe.english=source.english;probe.korean=temporary;probe.SetLocale("ko-KR");
            Check(probe.Get(PrototypeTextKeys.Title)==source.english.GetEntry(PrototypeTextKeys.Title).Value,"Missing Korean entry falls back to English",r);
            foreach(string code in new[]{"en","ko-KR"}){source.SetLocale(code);Check(source.Get(PrototypeTextKeys.Korean)=="한국어" && source.Get(PrototypeTextKeys.English)=="English","Language autonyms remain readable "+code,r);}
            var backup=Directory.GetDirectories("AgentBackups","bilingual-*").OrderBy(x=>x).Last();
            foreach(string path in new[]{"Assets/Prototype/ShakeAwakening.cs","Assets/Prototype/PrisonerExpression.cs","Assets/Prototype/PrototypeAudio.cs","Assets/Scenes/CLI_ConnectionTest.unity","Assets/Scenes/SampleScene.unity"})
                Check(File.ReadAllBytes(path).SequenceEqual(File.ReadAllBytes(Path.Combine(backup,path))),"Preserved bytes "+path,r);
            string before=File.ReadAllText(Path.Combine(backup,"Assets/Prototype/MainPrototype.cs")),after=File.ReadAllText("Assets/Prototype/MainPrototype.cs");
            foreach(string method in new[]{"AccumulationAt(","RecoveryForRate(","StartShift(","void Update(","FilterInput(","AdvanceGameplay(","RefreshPresentation("})
                Check(Body(before,method)==Body(after,method),"Unchanged gameplay method "+method,r);
            Check(PrototypeVerification.Main().count==23,"Existing 23 session checks",r);r.success=true;
        }catch(Exception ex){r.error=ex.ToString();Debug.LogException(ex);}finally{
            if(source!=null && oldCode!=null)source.SetLocale(oldCode);if(temporary!=null)UnityEngine.Object.DestroyImmediate(temporary);if(probe!=null)UnityEngine.Object.DestroyImmediate(probe);
        }
        File.WriteAllText("AgentScripts/bilingual-assets.json",JsonUtility.ToJson(r,true));
    }
    static string Body(string source,string method)
    {
        source=source.Replace("\r\n","\n");int start=source.IndexOf(method,StringComparison.Ordinal);
        // Skip references/calls; locate the declaration line by checking its access modifier.
        while(start>=0){int line=source.LastIndexOf('\n',start)+1;string prefix=source.Substring(line,start-line);if((prefix.Contains("public ") && !prefix.Contains("=>")) || prefix.Trim()=="float" || method=="void Update(")break;start=source.IndexOf(method,start+method.Length,StringComparison.Ordinal);}
        if(start<0)throw new Exception("Method declaration missing: "+method);start=source.IndexOf('{',start);int depth=1,end=start+1;
        for(;depth>0 && end<source.Length;end++){if(source[end]=='{')depth++;else if(source[end]=='}')depth--;}
        return source.Substring(start,end-start);
    }
    static async Task Frames(int count=2)
    {
        int start=Time.frameCount;double timeout=EditorApplication.timeSinceStartup+8;
        while(Time.frameCount-start<count){if(EditorApplication.timeSinceStartup>timeout)throw new Exception("PlayerLoop stalled during UI validation");EditorApplication.QueuePlayerLoopUpdate();await Task.Delay(16);}
    }
    static void State(MainPrototype c,int minutes,int state)
    {
        c.DeveloperReplayInput=true;c.SelectMinutes(minutes);c.StartShift(minutes);
        typeof(MainPrototype).GetField("suspended",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(c,false);
        if(state==0)typeof(MainPrototype).GetProperty("Session").SetValue(c,new PrototypeSession());
        else if(state==1 || state==2)c.Session.Tick(1,0,.65f,0);
        else if(state==3){c.Session.DeveloperSeek(minutes*60-.01f);c.Session.Tick(.02f,0,0,0);}
        else c.Session.Tick(1,0,1,0);
        if(state==2)typeof(MainPrototype).GetField("suspended",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(c,true);
        c.RefreshPresentation(0,true);c.prisonerExpression.SetDrowsiness(c.DrowsinessRatio,0,true);c.head.localRotation=Quaternion.identity;
        c.localizedUi.RefreshNow();Canvas.ForceUpdateCanvases();
    }
    static void Click(Button button)
    {
        if(!button.IsActive() || !button.interactable)throw new Exception("Button not interactable: "+button.name);
        Vector2 screen=RectTransformUtility.WorldToScreenPoint(null,button.GetComponent<RectTransform>().TransformPoint(button.GetComponent<RectTransform>().rect.center));
        var pointer=new PointerEventData(EventSystem.current){position=screen,button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
        if(!hits.Any(x=>x.gameObject==button.gameObject))throw new Exception("Button not raycastable: "+button.name);
        ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerClickHandler);
    }
    [MenuItem("Tools/Gansuu Head/Bilingual/Verify Live UI")]
    public static async void VerifyLive()
    {
        var r=new Report();MainPrototype c=null;string key=GuardStartupLocaleSelector.PreferenceKey;bool had=PlayerPrefs.HasKey(key);string saved=PlayerPrefs.GetString(key,"");bool background=Application.runInBackground;int rate=Application.targetFrameRate;string code=null;
        try{
            if(!EditorApplication.isPlaying || EditorApplication.isPaused)throw new Exception("Run unpaused Play first");
            Application.runInBackground=true;Application.targetFrameRate=60;c=UnityEngine.Object.FindAnyObjectByType<MainPrototype>();code=c.localizedUi.locale.CurrentCode;
            r.systemLanguage=Application.systemLanguage.ToString();r.initialLocale=code;r.hadSavedPreference=had;
            for(int wait=0;wait<120 && !c.localizedUi.Ready;wait++)await Frames();code=c.localizedUi.locale.CurrentCode;r.initialLocale=code;Check(c.localizedUi.Ready,"Official Localization initialized",r);
            Check(code==GuardStartupLocaleSelector.Resolve(saved,Application.systemLanguage),"Actual startup follows preference/OS rule",r);
            int startFrame=Time.frameCount;var ui=c.localizedUi;
            foreach(string languageCode in new[]{code=="ko-KR"?"en":"ko-KR",code}){
                State(c,1,0);await Frames();Click(languageCode=="ko-KR"?ui.koreanButton:ui.englishButton);
                Check(ui.locale.CurrentCode==languageCode && PlayerPrefs.GetString(key)==languageCode,"Pointer event switches and saves "+languageCode,r);
                Check(ui.title.text==c.uiText.Get(PrototypeTextKeys.Title),"Visible title updates immediately "+languageCode,r);
                foreach(int minutes in new[]{1,3,5})foreach(int state in new[]{0,1,2,3,4}){
                    State(c,minutes,state);await Frames();ui.RefreshNow();Canvas.ForceUpdateCanvases();
                    CheckText(ui,languageCode,minutes,state,r);r.uiStates++;
                }
            }
            State(c,1,0);await Frames();
            for(int i=0;i<ui.durationButtons.Length;i++){Click(ui.durationButtons[i]);Click(ui.startButton);Check(c.Session.Duration==c.SupportedMinutes[i]*60 && c.Session.State==PrototypeSession.Phase.Playing,"Real duration/start button events "+c.SupportedMinutes[i],r);State(c,1,0);await Frames();}
            r.renderedFrames=Time.frameCount-startFrame;r.success=true;
        }catch(Exception ex){r.error=ex.ToString();Debug.LogException(ex);}finally{
            if(had)PlayerPrefs.SetString(key,saved);else PlayerPrefs.DeleteKey(key);PlayerPrefs.Save();
            if(c!=null){if(code!=null)c.localizedUi.locale.SelectLanguage(code);if(had)PlayerPrefs.SetString(key,saved);else PlayerPrefs.DeleteKey(key);PlayerPrefs.Save();State(c,1,0);c.DeveloperReplayInput=false;}
            Application.runInBackground=background;Application.targetFrameRate=rate;
            File.WriteAllText("AgentScripts/bilingual-live-ui.json",JsonUtility.ToJson(r,true));
        }
    }
    static void CheckText(PrototypeLocalizedUI ui,string code,int minutes,int state,Report r)
    {
        foreach(var text in ui.GetComponentsInChildren<TMP_Text>()){
            if(!text.gameObject.activeInHierarchy || string.IsNullOrEmpty(text.text))continue;text.ForceMeshUpdate(true,true);
            if(text.isTextOverflowing || text.preferredHeight>text.rectTransform.rect.height+.5f)throw new Exception("Text overflow: "+code+" "+text.name+" / "+text.text);
        }
        if(state==0)Check(ui.startButton.GetComponentInChildren<TMP_Text>().text==ui.controller.uiText.Get(PrototypeTextKeys.Start),"Menu translated "+code+" "+minutes,r);
        else if(state==3 || state==4)Check(ui.result.text==ui.controller.uiText.Get(state==3?PrototypeTextKeys.Success:PrototypeTextKeys.Failure),"Outcome translated "+code+" "+minutes+" "+state,r);
        else if(state==2)Check(ui.paused.text==ui.controller.uiText.Get(PrototypeTextKeys.Paused),"Pause translated "+code+" "+minutes,r);
        else Check(ui.drowsiness.text.Contains("65"),"HUD parameter translated "+code+" "+minutes,r);
    }
    [MenuItem("Tools/Gansuu Head/Bilingual/Preview Korean Menu")]public static void KoMenu(){Preview("ko-KR",0);}
    [MenuItem("Tools/Gansuu Head/Bilingual/Preview English Menu")]public static void EnMenu(){Preview("en",0);}
    [MenuItem("Tools/Gansuu Head/Bilingual/Preview Korean HUD")]public static void KoHud(){Preview("ko-KR",1);}
    [MenuItem("Tools/Gansuu Head/Bilingual/Preview English HUD")]public static void EnHud(){Preview("en",1);}
    [MenuItem("Tools/Gansuu Head/Bilingual/Preview Korean Pause")]public static void KoPause(){Preview("ko-KR",2);}
    [MenuItem("Tools/Gansuu Head/Bilingual/Preview English Pause")]public static void EnPause(){Preview("en",2);}
    [MenuItem("Tools/Gansuu Head/Bilingual/Preview Korean Success")]public static void KoSuccess(){Preview("ko-KR",3);}
    [MenuItem("Tools/Gansuu Head/Bilingual/Preview English Success")]public static void EnSuccess(){Preview("en",3);}
    [MenuItem("Tools/Gansuu Head/Bilingual/Preview Korean Failure")]public static void KoFailure(){Preview("ko-KR",4);}
    [MenuItem("Tools/Gansuu Head/Bilingual/Preview English Failure")]public static void EnFailure(){Preview("en",4);}
    static void Preview(string code,int state)
    {
        var c=UnityEngine.Object.FindAnyObjectByType<MainPrototype>();if(!EditorApplication.isPlaying)throw new Exception("Enter Play first");
        // Previews do not save a language choice.
        LocalizationSettings.SelectedLocale=LocalizationSettings.AvailableLocales.GetLocale(new LocaleIdentifier(code));((UnityLocalizedTextSource)c.uiText).SetLocale(code);
        State(c,5,state);EditorApplication.QueuePlayerLoopUpdate();
    }
}


