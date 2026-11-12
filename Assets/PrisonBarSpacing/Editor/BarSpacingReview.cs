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

public static partial class BarSpacingReview
{
    const string ScenePath="Assets/Scenes/MainPrototype.unity";
    static string Output=>File.ReadAllText("AgentScripts/BarSpacing-output-path.txt").Trim();
    static string ReportDir=>Path.Combine(Output,"reports");
    static string Run=>File.ReadAllText("AgentScripts/BarSpacing-run.txt").Trim();
    static string CaptureDir=>Path.Combine(Output,"captures",Run);
    static readonly string[] Names={"Smile","WideGrin","CheekRaise","EyesNarrow","JawOpen","MouthOpen"};
    static string PathOf(Transform t)=>t.parent==null?t.name:PathOf(t.parent)+"/"+t.name;
    static string Json(UnityEngine.Object o)=>o==null?"null":EditorJsonUtility.ToJson(o);
    [Serializable] public class ObjectState{public string path;public Vector3 position,scale;public Quaternion rotation;}
    [Serializable] public class Config{public string path,type,json;}
    [Serializable] public class Snapshot{public List<ObjectState> transforms=new List<ObjectState>();public List<Config> configs=new List<Config>();}
    static Snapshot SnapshotOriginal(){
        var s=new Snapshot();var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        foreach(var root in scene.GetRootGameObjects()){
            if(root.name=="Prisoner_V2_Integrated")continue;
            foreach(var t in root.GetComponentsInChildren<Transform>(true))s.transforms.Add(new ObjectState{path=PathOf(t),position=t.localPosition,rotation=t.localRotation,scale=t.localScale});
            foreach(var c in root.GetComponentsInChildren<Component>(true))if(c!=null&&(c is MainPrototype||c is CurvedEyelidMask||c is Camera||c is Light||c is Canvas||c.GetType().Name=="PrototypeLocalizedUI"||c.GetType().Name=="PrototypeAudio"||c.GetType().Name=="UniversalAdditionalCameraData"))s.configs.Add(new Config{path=PathOf(c.transform),type=c.GetType().FullName,json=Json(c)});
        }return s;
    }
    const string AssetsRoot="Assets/PrisonerV2";
    [Serializable] public class MaterialInfo{public string name,texturePrefix;public float[] color;public float roughness,metallic;}
    [Serializable] public class BlenderInfo{public MaterialInfo[] materials;}
    [Serializable] public class InstallReport{public bool success,gameplayUiUnchanged;public string error;public float oldFov,newFov,headAimDownDegrees;public Vector3[] gripWorldPositions;public float[] barRadialErrors;public string[] shapes;public int vertices,triangles;public List<string> materialAssets=new List<string>();public string presentationCurveChange="Only serialized PrisonerExpression art curves updated; gameplay/drowsiness/difficulty code unchanged.";}
    static Material MakeMaterial(MaterialInfo info){
        string path=AssetsRoot+"/Materials/"+info.name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,path);}
        mat.SetColor("_BaseColor",new Color(info.color[0],info.color[1],info.color[2]));mat.SetFloat("_Metallic",info.metallic);mat.SetFloat("_Smoothness",1-info.roughness);
        if(!string.IsNullOrEmpty(info.texturePrefix)){
            string prefix=AssetsRoot+"/Textures/"+info.texturePrefix;
            mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(prefix+"_Base.png"));mat.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(prefix+"_Normal.png"));
            mat.SetFloat("_BumpScale",.65f);mat.EnableKeyword("_NORMALMAP");mat.SetTexture("_MetallicGlossMap",AssetDatabase.LoadAssetAtPath<Texture2D>(prefix+"_Surface.png"));mat.EnableKeyword("_METALLICSPECGLOSSMAP");mat.SetFloat("_Smoothness",1);
        }
        if(info.name=="Sclera_Ivory"||info.name=="Iris_Olive"||info.name=="Pupil")mat.SetFloat("_Cull",0);
        if(info.name=="Mouth_Interior"){mat.SetFloat("_Cull",0);mat.EnableKeyword("_EMISSION");mat.SetColor("_EmissionColor",new Color(.006f,.0035f,.003f));}
        EditorUtility.SetDirty(mat);return mat;
    }
    static AnimationCurve ArtCurve(float a,float b,float c,float d,float e){return new AnimationCurve(new Keyframe(0,a,0,0),new Keyframe(.25f,b,0,0),new Keyframe(.5f,c,0,0),new Keyframe(.75f,d,0,0),new Keyframe(1,e,0,0));}
    [Serializable] public class PoseReport{public string name;public float drowsiness,eyeClosure;public string phase;public float[] weights;public Vector3 cameraPosition;public Quaternion cameraRotation;public int width,height;public bool finite;public float maxDelta;public string gameCapture,cameraDiagnostic;}
    [Serializable] public class VisualReport{public bool success;public string error;public string captureMethod="Live GameView target texture, including overlay canvas and HUD; separate same-camera render excludes screen overlays.";public List<PoseReport> poses=new List<PoseReport>();public List<string> movementCaptures=new List<string>();public List<string> gameplayChecks=new List<string>();public bool cameraRestored;}
    static VisualReport visual;static MainPrototype c;static Camera main;static EditorWindow gameView;static int stage,ticks;static double started;
    static Vector3 originalPosition;static Quaternion originalRotation;static bool originalBackground;static Vector3[] basis;
    static readonly float[] Levels={0,.5f,.75f,.90f,1};
    static readonly Vector2[] Angles={new Vector2(0,-8),new Vector2(0,8),new Vector2(-6,0),new Vector2(6,0),new Vector2(-6,-8),new Vector2(6,8)};
    [MenuItem("Tools/Gansuu Head/Gate Spacing/2 Capture Live Game")]
    public static void StartCapture(){
        visual=new VisualReport();try{
            if(!EditorApplication.isPlaying||EditorApplication.isPaused)throw new Exception("Run in unpaused Play mode.");
            c=UnityEngine.Object.FindAnyObjectByType<MainPrototype>();main=c.head.GetComponent<Camera>();
            if(c.prisonerExpression.face.transform.root.name!="Prisoner_V2_Integrated")throw new Exception("New model is not connected.");
            originalPosition=main.transform.localPosition;originalRotation=main.transform.localRotation;originalBackground=Application.runInBackground;Application.runInBackground=true;
            c.DeveloperReplayInput=true;
            gameView=EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor"));gameView.Focus();
            stage=-1;ticks=0;started=EditorApplication.timeSinceStartup;EditorApplication.update+=CaptureStep;
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
            if(stage==-1){SaveGame("Game_Start");SetPose(0);stage=0;return;}
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
                var portraitObject=new GameObject("TEMP_V2_Portrait");var portrait=portraitObject.AddComponent<Camera>();portrait.CopyFrom(main);portrait.enabled=false;
                var portraitData=portraitObject.AddComponent<UniversalAdditionalCameraData>();portraitData.renderPostProcessing=main.GetUniversalAdditionalCameraData().renderPostProcessing;
                portrait.transform.position=main.transform.position;portrait.transform.LookAt(new Vector3(0,2.0f,3.1f));portrait.fieldOfView=18;
                try{CameraDiagnostic(portrait,"Portrait_"+label,1000,1000);}finally{UnityEngine.Object.DestroyImmediate(portraitObject);}
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
                var diagnosticObject=new GameObject("TEMP_V2_DiagnosticCamera");var camera=diagnosticObject.AddComponent<Camera>();camera.CopyFrom(main);camera.enabled=false;
                var additional=diagnosticObject.AddComponent<UniversalAdditionalCameraData>();var originalData=main.GetUniversalAdditionalCameraData();additional.renderPostProcessing=originalData.renderPostProcessing;
                try{
                    foreach(int side in new[]{-1,1}){
                        camera.transform.position=new Vector3(side*.65f,2.05f,2.1f);camera.transform.LookAt(new Vector3(0,1.99f,3.1f));camera.fieldOfView=38;
                        CameraDiagnostic(camera,"Diagnostic_Oblique_"+side,1200,1000);
                        camera.transform.position=new Vector3(side*.72f,1.80f,2.10f);camera.transform.LookAt(new Vector3(side*.48f,1.72f,2.65f));camera.fieldOfView=28;
                        CameraDiagnostic(camera,"Diagnostic_Grip_"+side,1000,1000);
                    }
                    camera.transform.position=new Vector3(0,1.20f,0);camera.transform.LookAt(new Vector3(0,1.2f,3.1f));camera.fieldOfView=48;
                    CameraDiagnostic(camera,"Diagnostic_FullBody",1000,1400);
                }finally{UnityEngine.Object.DestroyImmediate(diagnosticObject);}
                Check(c.Session.State==PrototypeSession.Phase.Playing,"Playing HUD remains active before failure");
                c.StartShift(1);c.Session.Tick(1,0,.999f,0);c.AdvanceGameplay(Vector2.zero,.1f);
                Check(c.Session.State==PrototypeSession.Phase.Failure&&c.EyeClosure==1,"Failure threshold still closes eyelids");
                c.StartShift(3);c.Session.DeveloperSeek(c.Session.Duration-.001f);c.AdvanceGameplay(Vector2.zero,.02f);
                Check(c.Session.State==PrototypeSession.Phase.Success,"Success state still reachable");
                c.StartShift(5);c.Session.Tick(1,0,.65f,0);float prior=c.DrowsinessRatio;float probability=c.suddenAwakeningProbability;c.suddenAwakeningProbability=0;
                for(int i=0;i<60;i++)c.AdvanceGameplay(new Vector2(i%2==0?4:-4,0),1f/60);
                c.suddenAwakeningProbability=probability;Check(c.DrowsinessRatio<prior,"Existing normal input recovery still lowers drowsiness");
                SetPose(0);main.transform.localRotation=Quaternion.Euler(6,0,0);stage++;return;
            }
            if(motion==Angles.Length+1){SaveGame("Game_LowerBody_Drowsy000_pitch6");CameraDiagnostic(main,"CameraOnly_LowerBody_pitch6",1100,506);main.transform.localRotation=originalRotation;stage++;return;}
            SaveGame("Game_AfterValidation_Neutral");visual.success=true;FinishCapture(null);
        }catch(Exception e){FinishCapture(e);}
    }
    static void FinishCapture(Exception error){
        EditorApplication.update-=CaptureStep;
        if(error!=null){visual.error=error.ToString();Debug.LogException(error);}
        if(main!=null){main.transform.localPosition=originalPosition;main.transform.localRotation=originalRotation;visual.cameraRestored=true;}
        if(c!=null)c.DeveloperReplayInput=false;Application.runInBackground=originalBackground;
        File.WriteAllText(Path.Combine(ReportDir,Run+"-live-visual.json"),JsonUtility.ToJson(visual,true));
        EditorApplication.delayCall+=()=>{if(EditorApplication.isPlaying)EditorApplication.isPlaying=false;};
    }
    [Serializable] public class TransitionRow{public float seconds,drowsiness;public float[] actual,target;}
    [Serializable] public class TransitionReport{public bool success,risingMonotonic,recoveryMonotonic,returnedNeutral;public float maxRiseLag;public string error;public List<TransitionRow> samples=new List<TransitionRow>();}
    [MenuItem("Tools/Gansuu Head/Gate Spacing/3 Check Live Transitions")]
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
            SaveGame("Game_Reopened");result.returnedNeutral=previous.All(w=>w<.2f);result.success=result.risingMonotonic&&result.recoveryMonotonic&&result.returnedNeutral&&result.maxRiseLag<25;
            if(!result.success)result.error="Temporal smoothing check failed; inspect recorded samples.";
        }catch(Exception e){result.error=e.ToString();Debug.LogException(e);}
        finally{if(controller!=null)controller.DeveloperReplayInput=false;Application.runInBackground=background;File.WriteAllText(Path.Combine(ReportDir,Run+"-live-transitions.json"),JsonUtility.ToJson(result,true));EditorApplication.delayCall+=()=>{if(EditorApplication.isPlaying)EditorApplication.isPlaying=false;};}
    }
    [Serializable] public class FinalAssetReport{public bool success;public string error;public int vertices,triangles,renderers;public Vector3 boundsMin,boundsMax;public float[] defaultWeights;public string[] materialShaders;}
    [MenuItem("Tools/Gansuu Head/Gate Spacing/4 Final Asset Audit")]
    public static void FinalAssetAudit(){
        var report=new FinalAssetReport();try{
            if(EditorApplication.isPlaying)throw new Exception("Stop Play first");var root=GameObject.Find("Prisoner_V2_Integrated");if(root==null)throw new Exception("V2 missing");
            var controller=UnityEngine.Object.FindAnyObjectByType<MainPrototype>();var face=controller.prisonerExpression.face;
            if(face.transform.root!=root.transform)throw new Exception("Controller is not bound to V2");
            report.defaultWeights=Enumerable.Range(0,6).Select(face.GetBlendShapeWeight).ToArray();if(report.defaultWeights.Any(w=>Mathf.Abs(w)>.001f))throw new Exception("Saved default is not neutral");
            var renderers=root.GetComponentsInChildren<Renderer>();report.renderers=renderers.Length;var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);report.boundsMin=bounds.min;report.boundsMax=bounds.max;
            foreach(var f in root.GetComponentsInChildren<MeshFilter>()){report.vertices+=f.sharedMesh.vertexCount;report.triangles+=f.sharedMesh.triangles.Length/3;}
            foreach(var s in root.GetComponentsInChildren<SkinnedMeshRenderer>()){report.vertices+=s.sharedMesh.vertexCount;report.triangles+=s.sharedMesh.triangles.Length/3;}
            var mats=renderers.SelectMany(r=>r.sharedMaterials).Distinct().ToArray();if(mats.Any(m=>m==null||ShaderUtil.ShaderHasError(m.shader)))throw new Exception("Missing material or shader error");report.materialShaders=mats.Select(m=>m.name+": "+m.shader.name).ToArray();
            Directory.CreateDirectory(Path.Combine(Output,"output"));AssetDatabase.ExportPackage(AssetsRoot,Path.Combine(Output,"output/Prisoner_V2_Unity6000_URP17.unitypackage"),ExportPackageOptions.Recurse);if(!File.Exists(Path.Combine(Output,"output/Prisoner_V2_Unity6000_URP17.unitypackage")))throw new Exception("Package export missing");report.success=true;
        }catch(Exception e){report.error=e.ToString();Debug.LogException(e);}
        File.WriteAllText(Path.Combine(ReportDir,"final-assets.json"),JsonUtility.ToJson(report,true));
    }
}









