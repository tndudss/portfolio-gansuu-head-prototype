using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using GansuuHead;
public static class QualityLiveAudioVerification
{
    [Serializable] public class Report{public bool success,focused,ambientPlaying,successSoundPlaying,failureSoundPlaying;public int normalSounds,suddenSounds;public string error;public string limitation="Editor input replay bypasses focus pause; playback counters and AudioSource state only, no human listening evaluation.";}
    [MenuItem("Tools/Gansuu Head/Quality/Verify Live Audio")]
    public static async void Run()
    {
        var r=new Report();MainPrototype c=null;float probability=0;bool background=Application.runInBackground;
        try{
            if(!EditorApplication.isPlaying || EditorApplication.isPaused)throw new Exception("Run unpaused Play first");
            c=UnityEngine.Object.FindAnyObjectByType<MainPrototype>();var audio=c.GetComponent<PrototypeAudio>();probability=c.suddenAwakeningProbability;
            Application.runInBackground=true;c.DeveloperReplayInput=true;c.suddenAwakeningProbability=0;c.StartShift(5);c.Session.Tick(1,0,.7f,0);
            int normal=audio.NormalSounds,sudden=audio.SuddenSounds;
            for(int i=0;i<35;i++){c.AdvanceGameplay(new Vector2(3,0),1f/60);EditorApplication.QueuePlayerLoopUpdate();await Task.Delay(20);}
            r.focused=Application.isFocused;r.normalSounds=audio.NormalSounds-normal;
            if(audio.SuddenSounds!=sudden)throw new Exception("Sudden sound without activation");
            c.suddenAwakeningProbability=1;c.Awakening.Reset(11);
            for(int i=0;i<20;i++){c.AdvanceGameplay(new Vector2(-3,0),1f/60);EditorApplication.QueuePlayerLoopUpdate();await Task.Delay(20);}
            r.suddenSounds=audio.SuddenSounds-sudden;
            foreach(var source in c.GetComponents<AudioSource>())if(source.loop && source.isPlaying)r.ambientPlaying=true;
            c.Session.DeveloperSeek(c.Session.Duration-.001f);c.AdvanceGameplay(Vector2.zero,.02f);
            EditorApplication.QueuePlayerLoopUpdate();await Task.Delay(100);
            r.successSoundPlaying=c.Session.State==PrototypeSession.Phase.Success && audio.GetComponents<AudioSource>()[3].isPlaying;
            await Task.Delay(750);
            c.StartShift(1);c.Session.Tick(10,0,1f/6.5f,0);
            EditorApplication.QueuePlayerLoopUpdate();await Task.Delay(100);
            r.failureSoundPlaying=c.Session.State==PrototypeSession.Phase.Failure && audio.GetComponents<AudioSource>()[3].isPlaying;
            r.success=r.normalSounds>0 && r.suddenSounds==1 && r.ambientPlaying && r.successSoundPlaying && r.failureSoundPlaying;
            if(!r.success)r.error="Expected focused play, normal response, exactly one proc sound, looping ambience.";
        }catch(Exception ex){r.error=ex.ToString();}
        finally{if(c!=null){c.DeveloperReplayInput=false;c.suddenAwakeningProbability=probability;}Application.runInBackground=background;File.WriteAllText("AgentScripts/quality-live-audio.json",JsonUtility.ToJson(r,true));}
    }
}


