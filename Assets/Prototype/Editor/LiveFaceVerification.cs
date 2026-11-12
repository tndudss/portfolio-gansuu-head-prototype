using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using GansuuHead;
public static class LiveFaceVerification
{
    [Serializable] public class Result
    {
        public bool success;public float initial,earlySmile,settledSmile,afterRecovery;public string error;
    }
    static async Task Pump(int milliseconds)
    {
        float start=Time.unscaledTime;double deadline=EditorApplication.timeSinceStartup+10;
        while(Time.unscaledTime-start<milliseconds/1000f){
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("PlayerLoop did not advance enough for actual presentation verification");
            EditorApplication.QueuePlayerLoopUpdate();await Task.Delay(16);
        }
    }
    [MenuItem("Tools/Gansuu Head/Verify Live Face Response")]
    public static async void Run()
    {
        var r=new Result();MainPrototype c=null;bool enabled=false;bool background=Application.runInBackground;int target=Application.targetFrameRate;
        try
        {
            if(!EditorApplication.isPlaying)throw new Exception("Enter Play mode first.");
            c=UnityEngine.Object.FindAnyObjectByType<MainPrototype>();enabled=c.enabled;
            Application.runInBackground=true;Application.targetFrameRate=60;c.DeveloperReplayInput=true; // Keep the actual LateUpdate and rendered presentation running.
            c.StartShift(5);r.initial=c.prisonerExpression.face.GetBlendShapeWeight(0);
            c.Session.Tick(1,0,.9f,0);c.RefreshPresentation(0,true);
            await Pump(40);r.earlySmile=c.prisonerExpression.face.GetBlendShapeWeight(0);
            await Pump(420);r.settledSmile=c.prisonerExpression.face.GetBlendShapeWeight(0);
            c.Session.Recover(1);c.RefreshPresentation(0,true);
            await Pump(240);r.afterRecovery=c.prisonerExpression.face.GetBlendShapeWeight(0);
            r.success=r.initial==0 && r.earlySmile>0 && r.earlySmile<90 && r.settledSmile>85 && r.afterRecovery<5;
            if(!r.success)r.error="Actual LateUpdate did not reach the expected progressive/fast-return weights.";
        }
        catch(Exception ex){r.error=ex.ToString();}
        finally
        {
            if(c!=null){c.enabled=enabled;c.DeveloperReplayInput=false;}Application.runInBackground=background;Application.targetFrameRate=target;
            File.WriteAllText("AgentScripts/live-face-response.json",JsonUtility.ToJson(r,true));
            Debug.Log("Live face response: "+r.success);
        }
    }
}


