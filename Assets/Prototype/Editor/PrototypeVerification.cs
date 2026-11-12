using System;
using System.Collections.Generic;
using GansuuHead;
using UnityEngine;
public static class PrototypeVerification
{
    public sealed class Result {public int count;public List<string> passed;}
    static void Check(bool ok,string label,List<string> passed){if(!ok)throw new Exception(label);passed.Add(label);}
    public static object StartLive(){var c=UnityEngine.Object.FindAnyObjectByType<MainPrototype>();c.Begin();return new{state=c.Session.State.ToString(),duration=c.Session.Duration};}
    public static object LiveStatus(){var c=UnityEngine.Object.FindAnyObjectByType<MainPrototype>();return new{state=c.Session.State.ToString(),elapsed=c.Session.Elapsed,drowsiness=c.Session.Drowsiness};}
    public static Result Main()
    {
        var p=new List<string>();var s=new PrototypeSession();
        s.Tick(20,0,.04f,0);Check(s.State==PrototypeSession.Phase.Ready,"Ready does not advance",p);
        s.Start(1);s.Tick(2,0,.2f,0);Check(Mathf.Abs(s.Drowsiness-.4f)<.0001f,"Normalized accumulation",p);
        s.Tick(1,.5f,.2f,.6f);Check(Mathf.Abs(s.Drowsiness-.3f)<.0001f,"Normalized motion recovery",p);
        s.Recover(10);Check(s.Drowsiness==0,"Recovery clamps at zero",p);
        s.Tick(6,0,.2f,0);Check(s.State==PrototypeSession.Phase.Failure && s.Drowsiness==1,"Failure at normalized maximum",p);
        float elapsed=s.Elapsed;s.Tick(100,100,0,1);s.Recover(1);Check(s.Elapsed==elapsed && s.Drowsiness==1,"Failure is terminal",p);
        foreach(int m in new[]{1,3,5})
        {
            s.Start(m);Check(s.Duration==m*60 && s.Drowsiness==0 && s.Elapsed==0,"Duration reset "+m,p);
            for(int i=0;i<m*60*60+2 && s.State==PrototypeSession.Phase.Playing;i++)s.Tick(1f/60,1f/60,.15f,.6f);
            Check(s.State==PrototypeSession.Phase.Success,"Survival succeeds "+m,p);
            elapsed=s.Elapsed;s.Tick(1,0,1,0);Check(s.State==PrototypeSession.Phase.Success && elapsed==s.Elapsed,"Success is terminal "+m,p);
        }
        s.Start(1);s.Tick(60,0,1f/60,0);Check(s.State==PrototypeSession.Phase.Failure,"Failure wins exact deadline tie",p);
        s.Start(1);s.Tick(1000,0,0,0);Check(s.Elapsed==60 && s.State==PrototypeSession.Phase.Success,"Large tick stops at deadline",p);
        s.Start(1,100);Check(s.FailureThreshold==1,"Threshold cannot exceed normalized one",p);
        var a=new PrototypeSession();var b=new PrototypeSession();a.Start(1);b.Start(1);
        for(int i=0;i<30;i++)a.Tick(1f/30,0,.15f,0);
        for(int i=0;i<120;i++)b.Tick(1f/120,0,.15f,0);
        Check(Mathf.Abs(a.Drowsiness-b.Drowsiness)<.0001f,"Rules independent of FPS",p);
        Check(Camera.main!=null && UnityEngine.Object.FindAnyObjectByType<MainPrototype>()!=null,"Scene references exist",p);
        return new Result{count=p.Count,passed=p};
    }
}

