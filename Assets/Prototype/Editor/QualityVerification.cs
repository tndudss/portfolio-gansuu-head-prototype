using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using GansuuHead;
public static class QualityVerification
{
    [Serializable] public class Report { public bool success; public int trials,activations; public float frequency,minStrength=2,maxStrength; public List<string> passed=new List<string>();public string error; }
    static void Check(bool value,string name,Report r){if(!value)throw new Exception(name);r.passed.Add(name);}
    [MenuItem("Tools/Gansuu Head/Quality/Phase 2 Apply")]
    public static void Apply()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play first");
        var scene=EditorSceneManager.GetActiveScene();if(scene.path!="Assets/Scenes/MainPrototype.unity")throw new Exception("Open MainPrototype");
        var c=UnityEngine.Object.FindAnyObjectByType<MainPrototype>();
        c.suddenAwakeningProbability=.25f;c.suddenAwakeningCooldown=2;c.suddenAwakeningDuration=.25f;
        c.suddenRecoveryMultiplier=2;c.suddenStrengthVariation=.15f;c.shakeEventDistance=18;
        c.suddenRecoveryCurve=new AnimationCurve(new Keyframe(0,1,-2,-2),new Keyframe(1,0,0,0));
        EditorUtility.SetDirty(c);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
    }
    [MenuItem("Tools/Gansuu Head/Quality/Verify Gameplay")]
    public static void Verify()
    {
        var r=new Report();GameObject go=null;
        try{
            var a=new ShakeAwakening();a.Reset(19026);
            // Independent eligible events, each separated beyond cooldown.
            for(int i=0;i<50000;i++){
                a.Step(Vector2.zero,2.1f,18,.25f,2,.15f);
                if(a.Step(new Vector2(20,0),1f/60,18,.25f,2,.15f)){
                    r.minStrength=Mathf.Min(r.minStrength,a.Strength);r.maxStrength=Mathf.Max(r.maxStrength,a.Strength);
                }
            }
            r.trials=a.Trials;r.activations=a.Activations;r.frequency=(float)r.activations/r.trials;
            Check(r.trials==50000 && Mathf.Abs(r.frequency-.25f)<.01f,"50000 eligible stroke probability within 1 percentage point",r);
            Check(r.minStrength>=.85f && r.maxStrength<=1.15f && r.maxStrength-r.minStrength>.29f,"Intensity drawn once in 85-115 percent range",r);
            a.Reset(1);for(int i=0;i<600;i++)a.Step(new Vector2(4,0),1f/60,18,1,2,.15f);
            Check(a.Events==1 && a.Activations==1,"Continuous same-direction movement is one event",r);
            a.Reset(1);float last=-100;int serial=0;bool cooldown=true;
            for(int i=0;i<3600;i++){
                a.Step(new Vector2((i/15)%2==0?4:-4,0),1f/60,18,1,2,.15f);
                if(a.Activations!=serial){float now=i/60f;cooldown &= now-last>=1.99f;last=now;serial=a.Activations;}
            }
            Check(cooldown && serial>20 && a.Events>100,"Reversals create strokes; activations respect two-second cooldown",r);
            var scene=UnityEngine.Object.FindAnyObjectByType<MainPrototype>();
            go=new GameObject("Quality verification temporary");var c=go.AddComponent<MainPrototype>();EditorUtility.CopySerialized(scene,c);
            c.head=null;c.eyelidMask=null;c.prisonerExpression=null;c.developerToolsEnabled=false;
            c.suddenAwakeningProbability=0;
            foreach(int m in new[]{1,3,5}){
                c.StartShift(m);c.Session.Tick(1,0,.75f,0);c.Session.DeveloperSeek(m*60*.9f);
                float start=c.Session.Drowsiness;
                for(int i=0;i<120;i++)c.AdvanceGameplay(new Vector2((i/15)%2==0?3:-3,0),1f/60);
                Check(c.Session.Drowsiness<start-.1f && c.Awakening.Activations==0,"Normal input recovers at late stage without any random bonus "+m,r);
                c.StartShift(m);c.AdvanceGameplay(new Vector2(float.NaN,float.PositiveInfinity),1f/60);
                Check(!float.IsNaN(c.Session.Drowsiness),"Nonfinite mouse input rejected "+m,r);
            }
            float reference=-1;
            foreach(int fps in new[]{30,60,120,360,720}){
                c.suddenAwakeningProbability=1;c.StartShift(5);c.Awakening.Reset(72);c.Session.Tick(1,0,.8f,0);
                for(int i=0;i<fps;i++)c.AdvanceGameplay(new Vector2(180f/fps,0),1f/fps);
                Check(c.Awakening.Activations==1 && !c.SuddenAwakeningActive,"One finite nonstacking burst at FPS "+fps,r);
                if(reference<0)reference=c.Session.Drowsiness;
                Check(Mathf.Abs(reference-c.Session.Drowsiness)<.002f,"Burst integration frame-rate tolerance at FPS "+fps,r);
            }
            r.success=true;
        }catch(Exception ex){r.error=ex.ToString();Debug.LogException(ex);}finally{if(go!=null)UnityEngine.Object.DestroyImmediate(go);}
        File.WriteAllText("AgentScripts/quality-gameplay.json",JsonUtility.ToJson(r,true));
        BalanceVerification.Verify();
    }
}


