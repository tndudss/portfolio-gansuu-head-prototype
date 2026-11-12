using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using GansuuHead;
public static class FinalThreeModes
{
    [Serializable] public class Report { public bool success;public string error;public List<string> passed=new List<string>(); }
    static void Check(bool value,string label,Report r){if(!value)throw new Exception(label);r.passed.Add(label);}
    [MenuItem("Tools/Gansuu Head/Modes/Apply Three Modes")]
    public static void Apply()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play first");
        var scene=EditorSceneManager.GetActiveScene();if(scene.path!="Assets/Scenes/MainPrototype.unity")throw new Exception("Open MainPrototype first");
        var c=UnityEngine.Object.FindAnyObjectByType<MainPrototype>();var ui=c.localizedUi;
        foreach(var b in ui.durationButtons.Skip(3))if(b!=null)UnityEngine.Object.DestroyImmediate(b.gameObject);
        ui.durationButtons=ui.durationButtons.Take(3).ToArray();
        for(int i=0;i<3;i++){var rect=(RectTransform)ui.durationButtons[i].transform;rect.anchoredPosition=new Vector2((i-1)*118,rect.anchoredPosition.y);}
        EditorUtility.SetDirty(ui);EditorUtility.SetDirty(c);EditorSceneManager.MarkSceneDirty(scene);
        if(!EditorSceneManager.SaveScene(scene))throw new Exception("Scene save failed");AssetDatabase.SaveAssets();Verify();
    }
    static string ProfileBlock(string scene,string name)=>Regex.Match(scene,@"(?ms)^  "+name+@":\r?\n.*?(?=^  [A-Za-z]|\z)").Value.Replace("\r\n","\n");
    [MenuItem("Tools/Gansuu Head/Modes/Verify Three Modes")]
    public static void Verify()
    {
        var r=new Report();try{
            var c=UnityEngine.Object.FindAnyObjectByType<MainPrototype>();var ui=c.localizedUi;
            Check(c.SupportedMinutes.SequenceEqual(new[]{1,3,5}),"Exactly 1 / 3 / 5 minutes",r);
            Check(ui.durationButtons.Length==3 && !ui.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="Duration 10"),"10-minute UI object removed",r);
            Check(typeof(MainPrototype).GetField("tenMinute")==null,"10-minute Inspector profile removed",r);
            string backup=File.ReadAllText("AgentScripts/three-final-backup.txt").Trim();string before=File.ReadAllText(Path.Combine(backup,"MainPrototype.unity")),after=File.ReadAllText("Assets/Scenes/MainPrototype.unity");
            foreach(string field in new[]{"oneMinute","threeMinute","fiveMinute"})Check(!string.IsNullOrEmpty(ProfileBlock(before,field)) && ProfileBlock(before,field)==ProfileBlock(after,field),"Serialized balance unchanged: "+field,r);
            Check(!after.Contains("  tenMinute:"),"Serialized ten-minute curve removed",r);
            foreach(int m in new[]{1,3,5}){
                c.SelectMinutes(m);Check(c.SelectedMinutes==m,"Selection "+m,r);
                var session=new PrototypeSession();session.Start(m);Check(session.Duration==m*60 && session.Elapsed==0 && session.State==PrototypeSession.Phase.Playing,"Start / timer "+m,r);
                session.Tick(m*60-1,0,0,0);Check(session.State==PrototypeSession.Phase.Playing && Mathf.Approximately(session.Duration-session.Elapsed,1),"One second remains "+m,r);
                session.Tick(1,0,0,0);Check(session.State==PrototypeSession.Phase.Success && session.Elapsed==m*60,"Clear at deadline "+m,r);
                session.Tick(1,0,1,0);Check(session.State==PrototypeSession.Phase.Success,"Victory terminal "+m,r);
                session.Start(m);session.Tick(8,0,1,0);Check(session.State==PrototypeSession.Phase.Failure,"Failure still works "+m,r);
            }
            foreach(int invalid in new[]{10,30}){
                bool rejected=false;try{new PrototypeSession().Start(invalid);}catch(ArgumentOutOfRangeException){rejected=true;}Check(rejected,"Session rejects "+invalid,r);
                rejected=false;try{c.SelectMinutes(invalid);}catch(ArgumentOutOfRangeException){rejected=true;}Check(rejected,"Menu rejects "+invalid,r);
                rejected=false;try{c.Profile(invalid);}catch(ArgumentOutOfRangeException){rejected=true;}Check(rejected,"Difficulty rejects "+invalid,r);
                rejected=false;try{ModeDifficulty.Create(invalid);}catch(ArgumentOutOfRangeException){rejected=true;}Check(rejected,"Profile factory rejects "+invalid,r);
            }
            c.SelectMinutes(1);var source=(UnityLocalizedTextSource)c.uiText;
            foreach(var table in new[]{source.english,source.korean}){Check(!table.Values.Any(e=>e.Value.Contains("10 min") || e.Value.Contains("10분")),"No dedicated ten-minute translation "+table.LocaleIdentifier.Code,r);}
            Check(!source.english.GetEntry(PrototypeTextKeys.Drowsiness).Value.Contains("Esc:") && !source.korean.GetEntry(PrototypeTextKeys.Drowsiness).Value.Contains("Esc:"),"Removed Esc hint remains absent",r);
            r.success=true;
        }catch(Exception ex){r.error=ex.ToString();Debug.LogException(ex);}File.WriteAllText("AgentScripts/three-final-verification.json",JsonUtility.ToJson(r,true));
    }
}
