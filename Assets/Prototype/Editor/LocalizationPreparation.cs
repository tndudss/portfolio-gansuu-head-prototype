using System;
using System.IO;
using System.Globalization;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using GansuuHead;
public static class LocalizationPreparation
{
    const string Folder="Assets/Prototype/Localization/";
    [MenuItem("Tools/Gansuu Head/Localization/Connect Text Catalog")]
    public static void Apply()
    {
        var scene=EditorSceneManager.GetActiveScene();
        if(EditorApplication.isPlaying || scene.path!="Assets/Scenes/MainPrototype.unity" || scene.isDirty)
            throw new Exception("Open a saved MainPrototype scene in Edit Mode.");
        if(UnityEngine.Object.FindAnyObjectByType<MainPrototype>().localizedUi!=null)throw new Exception("Bilingual UI is already configured; use the Bilingual tools.");
        var catalog=AssetDatabase.LoadAssetAtPath<PrototypeTextCatalog>(Folder+"PrototypeUI.asset");
        if(catalog==null){catalog=ScriptableObject.CreateInstance<PrototypeTextCatalog>();AssetDatabase.CreateAsset(catalog,Folder+"PrototypeUI.asset");}
        catalog.source=AssetDatabase.LoadAssetAtPath<TextAsset>(Folder+"UI.en.json");catalog.Reload();EditorUtility.SetDirty(catalog);
        var controller=UnityEngine.Object.FindAnyObjectByType<MainPrototype>();controller.uiText=catalog;EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
    }
    [Serializable] public class Report{public bool success;public List<string> passed=new List<string>();public string error;}
    static void Check(bool value,string name,Report r){if(!value)throw new Exception(name);r.passed.Add(name);}
    [MenuItem("Tools/Gansuu Head/Localization/Verify Text Catalog")]
    public static void Verify()
    {
        var r=new Report();GameObject go=null;PrototypeTextCatalog probe=null;TextAsset a=null,b=null;
        try{
            var main=UnityEngine.Object.FindAnyObjectByType<MainPrototype>();if(main.uiText is UnityLocalizedTextSource){BilingualVerification.VerifyAssets();return;}var catalog=main.uiText as PrototypeTextCatalog;
            Check(catalog!=null && catalog.source!=null,"Scene references external text catalog",r);
            var document=JsonUtility.FromJson<PrototypeTextCatalog.Document>(catalog.source.text);var keys=new HashSet<string>(StringComparer.Ordinal);
            foreach(var entry in document.entries)Check(keys.Add(entry.key) && !string.IsNullOrEmpty(entry.text),"Unique nonempty key: "+entry.key,r);
            foreach(string key in PrototypeTextKeys.All){
                Check(keys.Contains(key),"Required key exists: "+key,r);
                object[] args=key==PrototypeTextKeys.Remaining?new object[]{1,5}:key==PrototypeTextKeys.Drowsiness?new object[]{37.5f}:key==PrototypeTextKeys.Duration?new object[]{3}:key==PrototypeTextKeys.DeveloperStatus?new object[]{1f,.15f}:Array.Empty<object>();
                string.Format(CultureInfo.InvariantCulture,catalog.Get(key),args);
            }
            Check(catalog.Format(PrototypeTextKeys.Remaining,1,5).Contains("01:05"),"Timer keeps numeric padding",r);
            foreach(int m in new[]{1,3,5})Check(catalog.Format(PrototypeTextKeys.Duration,m).Contains(m.ToString()),"Duration parameter "+m,r);
            probe=ScriptableObject.CreateInstance<PrototypeTextCatalog>();
            a=new TextAsset("{\"entries\":[{\"key\":\"mode.duration_minutes\",\"text\":\"{0}\"}]}");
            b=new TextAsset("{\"entries\":[{\"key\":\"mode.duration_minutes\",\"text\":\"[{0}]\"}]}");
            probe.source=a;probe.Reload();go=new GameObject("Temporary localization check");var c=go.AddComponent<MainPrototype>();c.uiText=probe;
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;var refresh=typeof(MainPrototype).GetMethod("RefreshTextCache",flags);var labels=typeof(MainPrototype).GetField("modeLabels",flags);
            refresh.Invoke(c,null);Check(((string[])labels.GetValue(c))[0]=="1","Initial provider cache",r);
            probe.source=b;probe.Reload();refresh.Invoke(c,null);Check(((string[])labels.GetValue(c))[0]=="[1]","Data revision invalidates cached UI",r);
            Check(probe.Get("missing.key")=="missing.key","Missing entries display stable key, not duplicate hardcoded copy",r);
            Check(PrototypeVerification.Main().count==23,"Existing session rules remain valid",r);
            r.success=true;
        }catch(Exception ex){r.error=ex.ToString();Debug.LogException(ex);}finally{
            if(go!=null)UnityEngine.Object.DestroyImmediate(go);if(probe!=null)UnityEngine.Object.DestroyImmediate(probe);if(a!=null)UnityEngine.Object.DestroyImmediate(a);if(b!=null)UnityEngine.Object.DestroyImmediate(b);
        }
        File.WriteAllText("AgentScripts/localization-preparation.json",JsonUtility.ToJson(r,true));
    }
}


