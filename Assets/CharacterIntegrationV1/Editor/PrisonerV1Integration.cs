using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using GansuuHead;

public static class PrisonerV1Integration
{
    const string ScenePath="Assets/Scenes/MainPrototype.unity";
    static string Output=>File.ReadAllText("AgentScripts/PrisonerV1-output-path.txt").Trim();
    static string ReportDir=>Path.Combine(Output,"reports");
    static string CaptureDir=>Path.Combine(Output,"captures");
    static readonly string[] Names={"Smile","WideGrin","CheekRaise","EyesNarrow","JawOpen","MouthOpen"};
    static string PathOf(Transform t)=>t.parent==null?t.name:PathOf(t.parent)+"/"+t.name;
    static string Json(UnityEngine.Object o)=>o==null?"null":EditorJsonUtility.ToJson(o);
    [Serializable] public class ObjectState{public string path;public Vector3 position,scale;public Quaternion rotation;}
    [Serializable] public class Config{public string path,type,json;}
    [Serializable] public class Snapshot{public List<ObjectState> transforms=new List<ObjectState>();public List<Config> configs=new List<Config>();}
    static Snapshot SnapshotOriginal(){
        var s=new Snapshot();var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        foreach(var root in scene.GetRootGameObjects()){
            if(root.name=="Prisoner_V1_Integrated")continue;
            foreach(var t in root.GetComponentsInChildren<Transform>(true))s.transforms.Add(new ObjectState{path=PathOf(t),position=t.localPosition,rotation=t.localRotation,scale=t.localScale});
            foreach(var c in root.GetComponentsInChildren<Component>(true))if(c!=null&&(c is MainPrototype||c is CurvedEyelidMask||c is Camera||c is Light||c is Canvas||c.GetType().Name=="PrototypeLocalizedUI"||c.GetType().Name=="PrototypeAudio"||c.GetType().Name=="UniversalAdditionalCameraData"))s.configs.Add(new Config{path=PathOf(c.transform),type=c.GetType().FullName,json=Json(c)});
        }return s;
    }
    [Serializable] public class InstallReport{public bool success,originalTransformsUnchanged,originalConfigsUnchanged;public string error;public int disabledOldRenderers;public float headAimDownDegrees;public List<string> retainedLowerBody=new List<string>();public string newFace;public string[] blendShapes;public Vector3[] gripWorldPositions;public float[] barRadialErrors;public List<string> preservedDifferences=new List<string>();}
    [MenuItem("Tools/Gansuu Head/Character V1/1 Apply To Main Scene")]
    public static void Install(){
        var r=new InstallReport();try{
            if(EditorApplication.isPlaying)throw new Exception("Stop Play before integration.");
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(scene.path!=ScenePath||scene.isDirty)throw new Exception("Expected clean MainPrototype scene.");
            if(GameObject.Find("Prisoner_V1_Integrated")!=null)throw new Exception("Integration already exists; do not duplicate.");
            Directory.CreateDirectory(ReportDir);Directory.CreateDirectory(CaptureDir);
            var before=SnapshotOriginal();File.WriteAllText(Path.Combine(ReportDir,"scene-config-before.json"),JsonUtility.ToJson(before,true));
            var controller=UnityEngine.Object.FindAnyObjectByType<MainPrototype>();var expression=controller.prisonerExpression;
            if(expression==null)throw new Exception("Existing expression controller missing.");
            var oldRoot=GameObject.Find("Static Prisoner");if(oldRoot==null)throw new Exception("Original prisoner root missing.");
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PrisonerV1/Prisoner_V1.prefab");if(asset==null)throw new Exception("Delivery prefab not imported.");
            var character=(GameObject)PrefabUtility.InstantiatePrefab(asset,scene);character.name="Prisoner_V1_Integrated";
            character.transform.position=new Vector3(0,0,3.10f);
            var face=character.GetComponentsInChildren<SkinnedMeshRenderer>().Single(x=>x.sharedMesh.blendShapeCount==6);
            r.blendShapes=Enumerable.Range(0,6).Select(face.sharedMesh.GetBlendShapeName).ToArray();
            if(!r.blendShapes.SequenceEqual(Names))throw new Exception("Blend Shape order mismatch.");
            foreach(var animator in character.GetComponentsInChildren<Animator>())animator.enabled=false;
            // Aim the existing Head bone slightly down at the authored warden eye height. Hands stay fixed.
            var headBone=character.GetComponentsInChildren<Transform>().Single(t=>t.name=="Head");
            var eyeCenter=character.transform.TransformPoint(new Vector3(0,2.067f,.246f));
            r.headAimDownDegrees=Mathf.Atan2(eyeCenter.y-controller.head.position.y,Mathf.Abs(eyeCenter.z-controller.head.position.z))*Mathf.Rad2Deg;
            headBone.rotation=Quaternion.AngleAxis(-r.headAimDownDegrees,Vector3.right)*headBone.rotation;
            foreach(var renderer in character.GetComponentsInChildren<Renderer>())if(renderer.sharedMaterials.Any(m=>m==null))throw new Exception("Missing model material");
            foreach(var renderer in oldRoot.GetComponentsInChildren<Renderer>(true)){
                // V1 is an upper-body asset. Keep existing legs to avoid a floating torso in the full game frame.
                if(renderer.name.StartsWith("Leg ")){if(renderer.enabled)r.retainedLowerBody.Add(PathOf(renderer.transform));continue;}
                if(renderer.enabled)r.disabledOldRenderers++;renderer.enabled=false;EditorUtility.SetDirty(renderer);
            }
            Undo.RecordObject(expression,"Connect Blender prisoner face");expression.face=face;EditorUtility.SetDirty(expression);
            for(int i=0;i<6;i++)face.SetBlendShapeWeight(i,0);
            r.newFace=PathOf(face.transform);
            var grips=character.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Grip_")).OrderBy(t=>t.position.x).ToArray();
            r.gripWorldPositions=grips.Select(t=>t.position).ToArray();r.barRadialErrors=new float[2];
            for(int i=0;i<2;i++){var bar=GameObject.Find("Vertical Bar "+(i==0?"-1":"1"));var d=grips[i].position-bar.transform.position;r.barRadialErrors[i]=new Vector2(d.x,d.z).magnitude;if(r.barRadialErrors[i]>.002f)throw new Exception("Grip axis mismatch");}
            var after=SnapshotOriginal();File.WriteAllText(Path.Combine(ReportDir,"scene-config-after.json"),JsonUtility.ToJson(after,true));
            r.originalTransformsUnchanged=JsonUtility.ToJson(new Snapshot{transforms=before.transforms})==JsonUtility.ToJson(new Snapshot{transforms=after.transforms});
            r.originalConfigsUnchanged=JsonUtility.ToJson(new Snapshot{configs=before.configs})==JsonUtility.ToJson(new Snapshot{configs=after.configs});
            if(!r.originalTransformsUnchanged||!r.originalConfigsUnchanged)throw new Exception("An original camera/light/UI/gameplay configuration changed.");
            EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("Scene save failed.");
            AssetDatabase.SaveAssets();r.success=true;
        }catch(Exception e){r.error=e.ToString();Debug.LogException(e);}
        File.WriteAllText(Path.Combine(ReportDir,"integration.json"),JsonUtility.ToJson(r,true));
    }
    [Serializable] public class PoseReport{public string name;public float drowsiness,eyeClosure;public string phase;public float[] weights;public Vector3 cameraPosition;public Quaternion cameraRotation;public int width,height;public bool finite;public float maxDelta;public string gameCapture,cameraDiagnostic;}
    [Serializable] public class VisualReport{public bool success;public string error;public string captureMethod="Live GameView target texture, including overlay canvas and HUD; separate same-camera render excludes screen overlays.";public List<PoseReport> poses=new List<PoseReport>();public List<string> movementCaptures=new List<string>();public List<string> gameplayChecks=new List<string>();public bool cameraRestored;}
    static VisualReport visual;static MainPrototype c;static Camera main;static EditorWindow gameView;static int stage,ticks;static double started;
    static Vector3 originalPosition;static Quaternion originalRotation;static bool originalBackground;static Vector3[] basis;
    static readonly float[] Levels={0,.25f,.5f,.75f,1};
    static readonly Vector2[] Angles={new Vector2(0,-8),new Vector2(0,8),new Vector2(-6,0),new Vector2(6,0),new Vector2(-6,-8),new Vector2(6,8)};
    [MenuItem("Tools/Gansuu Head/Character V1/2 Capture Live Game")]
    public static void StartCapture(){
        visual=new VisualReport();try{
            if(!EditorApplication.isPlaying||EditorApplication.isPaused)throw new Exception("Run in unpaused Play mode.");
            c=UnityEngine.Object.FindAnyObjectByType<MainPrototype>();main=c.head.GetComponent<Camera>();
            if(c.prisonerExpression.face.transform.root.name!="Prisoner_V1_Integrated")throw new Exception("New model is not connected.");
            originalPosition=main.transform.localPosition;originalRotation=main.transform.localRotation;originalBackground=Application.runInBackground;Application.runInBackground=true;
            c.DeveloperReplayInput=true;
            gameView=EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor"));gameView.Focus();
            stage=0;ticks=0;started=EditorApplication.timeSinceStartup;SetPose(0);EditorApplication.update+=CaptureStep;
        }catch(Exception e){FinishCapture(e);}
    }
    static void SetPose(float level){
        c.StartShift(5);c.Session.Tick(1,0,level*c.Session.FailureThreshold,0);c.RefreshPresentation(0,true);
        c.prisonerExpression.SetDrowsiness(level,0,true);if(c.localizedUi!=null)c.localizedUi.RefreshNow();Canvas.ForceUpdateCanvases();
    }
    static RenderTexture GameTarget(){
        var type=Type.GetType("UnityEditor.PlayModeView,UnityEditor");var view=type.GetMethod("GetMainPlayModeView",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).Invoke(null,null);
        return type.GetField("m_TargetTexture",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).GetValue(view) as RenderTexture;
    }
    static void SaveGame(string name){
        var source=GameTarget();if(source==null||!source.IsCreated())throw new Exception("No live GameView texture. Refusing camera-only fallback.");
        var prev=RenderTexture.active;var rt=RenderTexture.GetTemporary(source.width,source.height,0,RenderTextureFormat.ARGB32);
        try{
            if(SystemInfo.graphicsUVStartsAtTop)Graphics.Blit(source,rt,new Vector2(1,-1),new Vector2(0,1));else Graphics.Blit(source,rt);
            ReadPNG(rt,Path.Combine(CaptureDir,name+".png"));
        }finally{RenderTexture.active=prev;RenderTexture.ReleaseTemporary(rt);}
    }
    static void ReadPNG(RenderTexture rt,string path){
        RenderTexture.active=rt;var t=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);try{t.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);t.Apply();File.WriteAllBytes(path,t.EncodeToPNG());}finally{UnityEngine.Object.DestroyImmediate(t);}
    }
    static void CameraDiagnostic(Camera camera,string name,int width,int height){
        var prev=RenderTexture.active;var rt=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);rt.Create();
        try{RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});ReadPNG(rt,Path.Combine(CaptureDir,name+".png"));}
        finally{RenderTexture.active=prev;rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
    }
    static void Check(bool value,string text){if(!value)throw new Exception(text);visual.gameplayChecks.Add(text);}
    static void CaptureStep(){
        try{
            if(!EditorApplication.isPlaying)throw new Exception("Play mode ended during capture.");
            if(EditorApplication.timeSinceStartup-started>180)throw new Exception("Capture timed out.");
            EditorApplication.QueuePlayerLoopUpdate();gameView.Repaint();
            if(++ticks<24||c.localizedUi!=null&&!c.localizedUi.Ready)return;
            ticks=0;
            if(stage<Levels.Length){
                float d=Levels[stage];string label="Drowsy"+Mathf.RoundToInt(d*100).ToString("000");
                var face=c.prisonerExpression.face;var baked=new Mesh();face.BakeMesh(baked,true);var verts=baked.vertices;
                if(stage==0)basis=verts;
                var source=GameTarget();
                var row=new PoseReport{name=label,drowsiness=c.DrowsinessRatio,eyeClosure=c.EyeClosure,phase=c.Session.State.ToString(),weights=Enumerable.Range(0,6).Select(face.GetBlendShapeWeight).ToArray(),cameraPosition=main.transform.position,cameraRotation=main.transform.rotation,width=source.width,height=source.height,finite=verts.All(v=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z)),maxDelta=verts.Select((v,i)=>(v-basis[i]).magnitude).Max(),gameCapture="Game_"+label+".png",cameraDiagnostic="CameraOnly_"+label+".png"};
                UnityEngine.Object.DestroyImmediate(baked);
                Check(row.finite,"Finite face geometry "+label);
                for(int i=0;i<4;i++)if(Mathf.Abs(row.weights[i]-c.prisonerExpression.TargetWeight(i,d))>.02f)throw new Exception("Expression link mismatch "+label);
                SaveGame("Game_"+label);CameraDiagnostic(main,"CameraOnly_"+label,row.width,row.height);visual.poses.Add(row);
                stage++;
                if(stage<Levels.Length)SetPose(Levels[stage]);else{SetPose(.75f);main.transform.localRotation=Quaternion.Euler(Angles[0].x,Angles[0].y,-Angles[0].y*.12f);}
                return;
            }
            int motion=stage-Levels.Length;
            if(motion<Angles.Length){
                string label="Movement_pitch"+Angles[motion].x+"_yaw"+Angles[motion].y;SaveGame(label);visual.movementCaptures.Add(label+".png");stage++;
                if(motion+1<Angles.Length)main.transform.localRotation=Quaternion.Euler(Angles[motion+1].x,Angles[motion+1].y,-Angles[motion+1].y*.12f);
                else main.transform.localRotation=originalRotation;
                return;
            }
            if(motion==Angles.Length){
                var diagnosticObject=new GameObject("TEMP_V1_DiagnosticCamera");var camera=diagnosticObject.AddComponent<Camera>();camera.CopyFrom(main);camera.enabled=false;
                var additional=diagnosticObject.AddComponent<UniversalAdditionalCameraData>();var originalData=main.GetUniversalAdditionalCameraData();additional.renderPostProcessing=originalData.renderPostProcessing;
                try{
                    foreach(int side in new[]{-1,1}){
                        camera.transform.position=new Vector3(side*.65f,2.05f,2.1f);camera.transform.LookAt(new Vector3(0,1.99f,3.1f));camera.fieldOfView=38;
                        CameraDiagnostic(camera,"Diagnostic_Oblique_"+side,1200,1000);
                        camera.transform.position=new Vector3(side*.72f,1.80f,2.10f);camera.transform.LookAt(new Vector3(side*.48f,1.72f,2.65f));camera.fieldOfView=28;
                        CameraDiagnostic(camera,"Diagnostic_Grip_"+side,1000,1000);
                    }
                }finally{UnityEngine.Object.DestroyImmediate(diagnosticObject);}
                Check(c.Session.State==PrototypeSession.Phase.Playing,"Playing HUD remains active before failure");
                c.StartShift(1);c.Session.Tick(1,0,.999f,0);c.AdvanceGameplay(Vector2.zero,.1f);
                Check(c.Session.State==PrototypeSession.Phase.Failure&&c.EyeClosure==1,"Failure threshold still closes eyelids");
                c.StartShift(3);c.Session.DeveloperSeek(c.Session.Duration-.001f);c.AdvanceGameplay(Vector2.zero,.02f);
                Check(c.Session.State==PrototypeSession.Phase.Success,"Success state still reachable");
                c.StartShift(5);c.Session.Tick(1,0,.65f,0);float prior=c.DrowsinessRatio;float probability=c.suddenAwakeningProbability;c.suddenAwakeningProbability=0;
                for(int i=0;i<60;i++)c.AdvanceGameplay(new Vector2(i%2==0?4:-4,0),1f/60);
                c.suddenAwakeningProbability=probability;Check(c.DrowsinessRatio<prior,"Existing normal input recovery still lowers drowsiness");
                SetPose(0);stage++;return;
            }
            SaveGame("Game_AfterValidation_Neutral");visual.success=true;FinishCapture(null);
        }catch(Exception e){FinishCapture(e);}
    }
    static void FinishCapture(Exception error){
        EditorApplication.update-=CaptureStep;
        if(error!=null){visual.error=error.ToString();Debug.LogException(error);}
        if(main!=null){main.transform.localPosition=originalPosition;main.transform.localRotation=originalRotation;visual.cameraRestored=true;}
        if(c!=null)c.DeveloperReplayInput=false;Application.runInBackground=originalBackground;
        File.WriteAllText(Path.Combine(ReportDir,"live-visual.json"),JsonUtility.ToJson(visual,true));
        EditorApplication.delayCall+=()=>{if(EditorApplication.isPlaying)EditorApplication.isPlaying=false;};
    }
    [Serializable] public class TransitionRow{public float seconds,drowsiness;public float[] actual,target;}
    [Serializable] public class TransitionReport{public bool success,risingMonotonic,recoveryMonotonic,returnedNeutral;public float maxRiseLag;public string error;public List<TransitionRow> samples=new List<TransitionRow>();}
    [MenuItem("Tools/Gansuu Head/Character V1/3 Check Live Transitions")]
    public static async void CheckTransitions(){
        var result=new TransitionReport{risingMonotonic=true,recoveryMonotonic=true};MainPrototype controller=null;bool background=Application.runInBackground;
        try{
            if(!EditorApplication.isPlaying||EditorApplication.isPaused)throw new Exception("Run in unpaused Play mode");
            controller=UnityEngine.Object.FindAnyObjectByType<MainPrototype>();controller.DeveloperReplayInput=true;Application.runInBackground=true;controller.StartShift(5);
            var face=controller.prisonerExpression.face;var previous=new float[4];
            for(int frame=1;frame<=210;frame++){
                if(frame<=180)controller.Session.Tick(1f/30,0,.95f/6,0);
                controller.RefreshPresentation(1f/30);EditorApplication.QueuePlayerLoopUpdate();await Task.Delay(33);
                var actual=Enumerable.Range(0,4).Select(face.GetBlendShapeWeight).ToArray();var targets=Enumerable.Range(0,4).Select(i=>controller.prisonerExpression.TargetWeight(i,controller.DrowsinessRatio)).ToArray();
                for(int i=0;i<4;i++){if(actual[i]+.001f<previous[i])result.risingMonotonic=false;result.maxRiseLag=Mathf.Max(result.maxRiseLag,targets[i]-actual[i]);}previous=actual;
                if(frame%5==0)result.samples.Add(new TransitionRow{seconds=frame/30f,drowsiness=controller.DrowsinessRatio,actual=actual,target=targets});
            }
            controller.Session.Recover(1);controller.RefreshPresentation(1f/30);
            for(int frame=1;frame<=30;frame++){
                controller.RefreshPresentation(1f/30);EditorApplication.QueuePlayerLoopUpdate();await Task.Delay(33);
                var actual=Enumerable.Range(0,4).Select(face.GetBlendShapeWeight).ToArray();for(int i=0;i<4;i++)if(actual[i]>previous[i]+.001f)result.recoveryMonotonic=false;previous=actual;
                if(frame%5==0)result.samples.Add(new TransitionRow{seconds=7+frame/30f,drowsiness=controller.DrowsinessRatio,actual=actual,target=new float[4]});
            }
            result.returnedNeutral=previous.All(w=>w<.2f);result.success=result.risingMonotonic&&result.recoveryMonotonic&&result.returnedNeutral&&result.maxRiseLag<25;
            if(!result.success)result.error="Temporal smoothing check failed; inspect recorded samples.";
        }catch(Exception e){result.error=e.ToString();Debug.LogException(e);}
        finally{if(controller!=null)controller.DeveloperReplayInput=false;Application.runInBackground=background;File.WriteAllText(Path.Combine(ReportDir,"live-transitions.json"),JsonUtility.ToJson(result,true));EditorApplication.delayCall+=()=>{if(EditorApplication.isPlaying)EditorApplication.isPlaying=false;};}
    }
}
