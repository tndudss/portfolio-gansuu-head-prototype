using System;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEditor;
using GansuuHead;
public static class BalanceLiveVerification
{
    [Serializable] public class Row {public int minutes;public float clockScale,startDrowsiness,endDrowsiness,simulationSeconds,wallSeconds;public int inputFrames;public bool success;}
    [Serializable] public class Report {public bool success;public string input="Input System virtual Mouse replay at 180 px/s, normal clock 1x; not a human hardware feel test.";public List<Row> rows=new List<Row>();public string error;}
    [MenuItem("Tools/Gansuu Head/Balance/Verify Live 1x Input")]
    public static async void Run()
    {
        var r=new Report();GameObject go=null;Mouse device=null;var original=Mouse.current;
        try
        {
            if(!EditorApplication.isPlaying)throw new Exception("Enter Play first.");
            var scene=UnityEngine.Object.FindAnyObjectByType<MainPrototype>();
            go=new GameObject("Live balance input probe");var c=go.AddComponent<MainPrototype>();
            EditorUtility.CopySerialized(scene,c);c.head=go.transform;c.prisonerExpression=null;c.eyelidMask=null;c.enabled=false;
            c.developerToolsEnabled=false;c.suddenAwakeningProbability=0;
            device=InputSystem.AddDevice<Mouse>();
            foreach(int m in new[]{1,3,5})
            {
                c.StartShift(m);c.Session.Tick(1,0,.7f,0);c.Session.DeveloperSeek(m*60*.75f);
                typeof(MainPrototype).GetField("continuousTime",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(c,30f);
                var row=new Row{minutes=m,clockScale=c.ClockScale,startDrowsiness=c.Session.Drowsiness};
                float start=c.Session.Elapsed;
                var timer=Stopwatch.StartNew();double last=0;
                while(timer.Elapsed.TotalSeconds<2.2)
                {
                    EditorApplication.QueuePlayerLoopUpdate();await Task.Delay(16);
                    double now=timer.Elapsed.TotalSeconds;float dt=(float)(now-last);last=now;
                    float x=(row.inputFrames%2==0?180:-180)*dt;
                    InputSystem.QueueDeltaStateEvent(device.delta,new Vector2(x,0));InputSystem.Update();
                    c.AdvanceGameplay(device.delta.ReadValue(),dt);row.inputFrames++;
                }
                row.wallSeconds=(float)timer.Elapsed.TotalSeconds;row.simulationSeconds=c.Session.Elapsed-start;
                row.endDrowsiness=c.Session.Drowsiness;
                row.success=row.clockScale==1 && row.endDrowsiness<row.startDrowsiness && Mathf.Abs(row.simulationSeconds-row.wallSeconds)<.1f;
                r.rows.Add(row);
            }
            r.success=r.rows.Count==4 && r.rows.TrueForAll(x=>x.success);
        }
        catch(Exception ex){r.error=ex.ToString();}
        finally
        {
            if(device!=null)InputSystem.RemoveDevice(device);if(original!=null && original.added)original.MakeCurrent();
            if(go!=null)UnityEngine.Object.DestroyImmediate(go);
            File.WriteAllText("AgentScripts/rebalance-live-1x.json",JsonUtility.ToJson(r,true));
        }
    }
}


