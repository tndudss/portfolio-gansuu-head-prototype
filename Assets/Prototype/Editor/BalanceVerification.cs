using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using GansuuHead;
public static class BalanceVerification
{
    [Serializable] public class Row
    {
        public int minutes;public float progress,multiplier,accumulation,recovery,ratio,netDrowsinessPerSecond,idleSeconds,liveIdleSeconds;
        public bool continuousInputReducesDrowsiness;
    }
    [Serializable] public class Report {public bool success;public List<string> passed=new List<string>();public List<Row> rows=new List<Row>();public string error;public string endpointNote="100% uses frozen endpoint rates in an independent session. A finished game remains successful; no input/failure after victory is implied.";}
    static void Check(bool ok,string name,Report r){if(!ok)throw new Exception(name);r.passed.Add(name);}
    static void Fatigue(MainPrototype c,float seconds){typeof(MainPrototype).GetField("continuousTime",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(c,seconds);}
    static void Seed(MainPrototype c,int m,float phase,float d)
    {c.StartShift(m);c.suddenAwakeningProbability=0;if(d>0)c.Session.Tick(1,0,d,0);c.Session.DeveloperSeek(phase);Fatigue(c,30);}
    [MenuItem("Tools/Gansuu Head/Balance/Verify All")]
    public static void Verify()
    {
        var r=new Report();GameObject go=null;
        try
        {
            var scene=UnityEngine.Object.FindAnyObjectByType<MainPrototype>();
            go=new GameObject("Balance verification temporary");var c=go.AddComponent<MainPrototype>();
            EditorUtility.CopySerialized(scene,c);c.head=go.transform;c.prisonerExpression=null;c.eyelidMask=null;c.developerToolsEnabled=false;
            int[] modes={1,3,5};float[] initial={6.5f,7,7.5f};
            float[,] targets={{1,1.05f,1.12f,1.18f,1.24f},{1,1.04f,1.09f,1.14f,1.20f},{1,1.03f,1.07f,1.12f,1.18f}};
            for(int mi=0;mi<modes.Length;mi++)
            {
                int m=modes[mi];var p=c.Profile(m);
                Check(p.initialIdleFailureSeconds==initial[mi],"Opening idle parameter "+m,r);
                bool smooth=true;float last=1;
                for(int k=0;k<=1000;k++){float v=p.EvaluateMultiplier(k/1000f);smooth &= v>=last-.000001f && v<=p.lateRateMultiplier+.000001f;last=v;}
                Check(smooth,"No curve overshoot or reversal "+m,r);
                for(int stage=0;stage<5;stage++)
                {
                    float progress=stage*.25f,phase=m*60*progress;
                    float accumulation=c.AccumulationAt(m,phase),recovery=c.SustainedRecoveryAt(m,phase,180,30);
                    Check(Mathf.Abs(p.EvaluateMultiplier(progress)-targets[mi,stage])<.00001f,"Exact multiplier "+m+" "+progress,r);
                    bool recoveryBand=true;
                    foreach(float speed in new[]{180f,220f,300f})foreach(float endurance in new[]{0f,5f,30f,120f})
                    {float ratio=c.SustainedRecoveryAt(m,phase,speed,endurance)/accumulation;recoveryBand &= ratio>=1.299f && ratio<=1.701f;}
                    Check(recoveryBand,"Sustained recovery 1.3-1.7x "+m+" "+progress,r);
                    // Freeze the rate snapshot to measure endpoint pressure without pretending a game continues after success.
                    var idle=new PrototypeSession();idle.Start(m);
                    for(int j=0;j<12*120 && idle.State==PrototypeSession.Phase.Playing;j++)idle.Tick(1f/120,0,accumulation,0);
                    Check(idle.State==PrototypeSession.Phase.Failure && idle.Elapsed>=4.98f && idle.Elapsed<=8.02f,"Frozen idle 5-8s "+m+" "+progress,r);
                    var active=new PrototypeSession();active.Start(m);active.Tick(1,0,.7f,0);
                    for(int j=0;j<120;j++)active.Tick(1f/120,1f/120,accumulation,recovery);
                    Check(active.Drowsiness<.7f && active.State==PrototypeSession.Phase.Playing,"Continuous motion reduces drowsiness "+m+" "+progress,r);
                    float liveIdle=-1;
                    if(stage<4)
                    {
                        Seed(c,m,phase,0);
                        for(int j=0;j<12*120 && c.Session.State==PrototypeSession.Phase.Playing;j++)c.AdvanceGameplay(Vector2.zero,1f/120);
                        liveIdle=c.Session.Elapsed-phase;
                        Check(c.Session.State==PrototypeSession.Phase.Failure && liveIdle>=4.98f && liveIdle<=8.02f,"Live timeline idle 5-8s "+m+" "+progress,r);
                        Seed(c,m,phase,.7f);
                        for(int j=0;j<120;j++)c.AdvanceGameplay(new Vector2(j%2==0?3:-3,0),1f/60);
                        Check(c.Session.Drowsiness<.7f && c.Session.State==PrototypeSession.Phase.Playing,"Actual input path lowers fatigue "+m+" "+progress,r);
                    }
                    r.rows.Add(new Row{minutes=m,progress=progress,multiplier=p.EvaluateMultiplier(progress),accumulation=accumulation,recovery=recovery,ratio=recovery/accumulation,netDrowsinessPerSecond=accumulation-recovery,idleSeconds=idle.Elapsed,liveIdleSeconds=liveIdle,continuousInputReducesDrowsiness=true});
                }
                // All-mode real-speed simulation: ordinary continuous input, no first-burst bonus and no rest rearming.
                Seed(c,m,0,.7f);
                for(int j=0;j<m*60*60+2 && c.Session.State==PrototypeSession.Phase.Playing;j++)c.AdvanceGameplay(new Vector2(j%2==0?3:-3,0),1f/60);
                Check(c.Session.State==PrototypeSession.Phase.Success,"Normal 1x continuous input clears entire mode "+m,r);
                Seed(c,m,m*60*.9f,.7f);
                for(int j=0;j<120;j++)c.AdvanceGameplay(new Vector2(j%2==0?3:-3,0),1f/60);
                Check(c.Session.Drowsiness<.7f,"Late 90% recovery at normal speed "+m,r);
            }
            foreach(int m in modes)
            {
                float[] d=new float[5];int fi=0;
                foreach(int fps in new[]{30,60,120,360,720})
                {
                    Seed(c,m,m*60*.75f,.7f);
                    for(int j=0;j<fps*2;j++)c.AdvanceGameplay(new Vector2(j%2==0?180f/fps:-180f/fps,0),1f/fps);
                    d[fi++]=c.Session.Drowsiness;
                }
                Check(Mathf.Abs(d[0]-d[4])<.0005f,"Recovery stable across 30/60/120/360/720 FPS "+m,r);
            }
            Seed(c,5,0,0);for(int j=0;j<10*60 && c.Session.State==PrototypeSession.Phase.Playing;j++)c.AdvanceGameplay(new Vector2(.1f,.1f),1f/60);
            Check(c.Session.State==PrototypeSession.Phase.Failure,"Noise still fails",r);
            Seed(c,3,0,.6f);float before=c.Session.Drowsiness;c.AdvanceGameplay(new Vector2(95f/60,0),1f/60);
            Check(c.Session.Drowsiness>before,"Barely valid input cannot farm recovery",r);
            Seed(c,3,0,.7f);c.AdvanceGameplay(new Vector2(5,0),1f/60);float ordinary=c.Session.Drowsiness;
            Seed(c,3,0,.7f);c.AdvanceGameplay(new Vector2(100000,0),1f/60);
            Check(Mathf.Abs(c.Session.Drowsiness-ordinary)<.00001f,"Abnormal spikes do not exceed capped effort",r);
            var profile=c.Profile(1);var saved=profile.curve;
            profile.curve=new AnimationCurve(new Keyframe(0,1,0,100),new Keyframe(.25f,1.05f,-100,100),new Keyframe(1,100,-100,0));
            bool bounded=true;for(int j=0;j<=1000;j++)bounded &= profile.EvaluateMultiplier(j/1000f)<=profile.lateRateMultiplier;
            Check(bounded,"Bad authored tangents cannot bypass per-mode ceiling",r);profile.curve=saved;
            c.globalDrowsinessMultiplier=.5f;profile.accumulationMultiplier=.8f;
            Check(Mathf.Abs(c.AccumulationAt(1,0)-(.5f*.8f/6.5f))<.00001f,"Global and mode scales each apply once",r);
            c.globalDrowsinessMultiplier=100;profile.accumulationMultiplier=100;
            Check(c.AccumulationAt(1,60)<=c.maximumAccumulationPerSecond,"Final cap survives global and mode scaling",r);
            r.success=true;
        }
        catch(Exception ex){r.error=ex.ToString();Debug.LogException(ex);}
        finally{if(go!=null)UnityEngine.Object.DestroyImmediate(go);}
        File.WriteAllText("AgentScripts/rebalance-verification.json",JsonUtility.ToJson(r,true));
    }
}



