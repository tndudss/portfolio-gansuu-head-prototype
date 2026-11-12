using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using GansuuHead;
public static class BalanceAudit
{
    [Serializable] public class Row {public int minutes;public float progress,accumulation,normal180Recovery,normal220Recovery,full300Recovery,idleSeconds;}
    [Serializable] public class Report {public List<Row> rows=new List<Row>();public string note="Frozen phase rates; recovery measured through AdvanceGameplay after 30 seconds of continuous-input fatigue, without initial burst. 100% is an endpoint-rate diagnostic, not play after victory.";}
    [MenuItem("Tools/Gansuu Head/Balance/Capture Before")]
    public static void Capture(){CaptureFile("AgentScripts/rebalance-before.json");}
    [MenuItem("Tools/Gansuu Head/Balance/Capture After")]
    public static void CaptureAfter(){CaptureFile("AgentScripts/rebalance-after.json");}
    static void CaptureFile(string output)
    {
        var scene=UnityEngine.Object.FindAnyObjectByType<MainPrototype>();
        var go=new GameObject("Before-balance audit");var c=go.AddComponent<MainPrototype>();
        EditorUtility.CopySerialized(scene,c);c.head=go.transform;c.prisonerExpression=null;c.eyelidMask=null;
        var r=new Report();
        try
        {
            foreach(int m in new[]{1,3,5})foreach(float progress in new[]{0,.25f,.5f,.75f,1})
            {
                float rate=c.AccumulationAt(m,m*60*progress);
                r.rows.Add(new Row{minutes=m,progress=progress,accumulation=rate,idleSeconds=1/rate,
                    normal180Recovery=Recovery(c,m,progress,180),normal220Recovery=Recovery(c,m,progress,220),full300Recovery=Recovery(c,m,progress,300)});
            }
            File.WriteAllText(output,JsonUtility.ToJson(r,true));
        }
        finally {UnityEngine.Object.DestroyImmediate(go);}
    }
    static float Recovery(MainPrototype c,int m,float progress,float speed)
    {
        c.StartShift(m);c.initialShakeMinimumSpeed=999999;
        c.Session.Tick(1,0,.7f,0);
        c.Session.DeveloperSeek(Mathf.Min(m*60-1,m*60*progress));
        typeof(MainPrototype).GetField("continuousTime",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(c,30f);
        float rate=c.CurrentAccumulation,before=c.Session.Drowsiness;
        c.AdvanceGameplay(new Vector2(speed/60,0),1f/60);
        return rate+(before-c.Session.Drowsiness)*60;
    }
}


