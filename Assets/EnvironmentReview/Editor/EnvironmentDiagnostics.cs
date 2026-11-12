using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using GansuuHead;
public static partial class EnvironmentReview {
 [Serializable] public class Item{public string path,mesh;public Vector3 position,scale,min,max;public int triangles;public bool enabled;public string[] materials;}
 [Serializable] public class SceneAudit{public string pipeline,quality,gpu;public List<Item> meshes=new List<Item>();public List<Config> configs=new List<Config>();}
 [MenuItem("Tools/Gansuu Head/Environment/0 Scene Inventory")]
 public static void Inventory(){
  var r=new SceneAudit{pipeline=GraphicsSettings.currentRenderPipeline==null?"BuiltIn":AssetDatabase.GetAssetPath(GraphicsSettings.currentRenderPipeline),quality=QualitySettings.names[QualitySettings.GetQualityLevel()],gpu=SystemInfo.graphicsDeviceName};
  foreach(var mr in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include,FindObjectsSortMode.None)){
   var f=mr.GetComponent<MeshFilter>();var s=mr as SkinnedMeshRenderer;var mesh=f!=null?f.sharedMesh:s!=null?s.sharedMesh:null;
   if(mesh!=null)r.meshes.Add(new Item{path=PathOf(mr.transform),mesh=mesh.name,position=mr.transform.position,scale=mr.transform.lossyScale,min=mr.bounds.min,max=mr.bounds.max,triangles=mesh.triangles.Length/3,enabled=mr.enabled&&mr.gameObject.activeInHierarchy,materials=mr.sharedMaterials.Select(m=>m==null?"NULL":m.name).ToArray()});
  }
  foreach(var comp in UnityEngine.Object.FindObjectsByType<Component>(FindObjectsInactive.Exclude,FindObjectsSortMode.None))if(comp is Camera||comp is Light||comp is Volume||comp is UniversalAdditionalCameraData||comp is MainPrototype||comp is PrisonerExpression||comp is CurvedEyelidMask)r.configs.Add(new Config{path=PathOf(comp.transform),type=comp.GetType().FullName,json=Json(comp)});
  File.WriteAllText(Path.Combine(ReportDir,Run+"-inventory.json"),JsonUtility.ToJson(r,true));
 }
 [Serializable] public class Performance{public bool success;public string error,gpu,pipeline,scope="Unity Editor GameView; no standalone FPS claim; fixed .5 drowsiness and camera; 60 warmup + 240 rendered samples";public int width,height,samples,triangles,vertices,drawCalls,batches,setPass,shadowCasters;public float medianMs,p95Ms,meanFps;public int vSync,targetFps;}
 [Serializable] public class Compatibility{public bool success;public string error;public int sessionChecks;public bool zeroProbability,fullProbability,cooldown,fractionalProbability;}
 [Serializable] public class EnvironmentAssets{public bool success,eyelidShaderValid;public string error;public int renderers,triangles,materials,textures;public float floorTop,footBottom,footGap;public string antialiasing;}
 [MenuItem("Tools/Gansuu Head/Environment/7 Final Environment Audit")]
 public static void AuditEnvironment(){
  var r=new EnvironmentAssets();try{
   if(EditorApplication.isPlaying)throw new Exception("Stop Play first");var root=GameObject.Find("StonePrison_Environment_Integrated");
   var renderers=root.GetComponentsInChildren<MeshRenderer>().Where(x=>x.enabled).ToArray();r.renderers=renderers.Length;r.triangles=renderers.Sum(x=>x.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3);
   var mats=renderers.SelectMany(x=>x.sharedMaterials).Distinct().ToArray();r.materials=mats.Length;if(mats.Any(x=>x==null||ShaderUtil.ShaderHasError(x.shader)))throw new Exception("Missing material or shader error");
   r.textures=Directory.GetFiles(Env+"/Textures","*.png").Length;if(r.textures!=12)throw new Exception("Texture set incomplete");
   var controller=UnityEngine.Object.FindAnyObjectByType<MainPrototype>();r.eyelidShaderValid=!ShaderUtil.ShaderHasError(controller.eyelidMask.materialTemplate.shader);if(!r.eyelidShaderValid)throw new Exception("Eyelid shader error");
   r.floorTop=root.GetComponentsInChildren<Renderer>().Single(x=>x.name=="Flagstone_Floor").bounds.max.y;r.footBottom=GameObject.Find("Prisoner_V2_Integrated").GetComponentsInChildren<Renderer>().Min(x=>x.bounds.min.y);r.footGap=r.footBottom-r.floorTop;if(Mathf.Abs(r.footGap)>.012f)throw new Exception("Foot contact mismatch");
   r.antialiasing=controller.head.GetComponent<Camera>().GetUniversalAdditionalCameraData().antialiasing.ToString();
   Directory.CreateDirectory(Path.Combine(Output,"output"));AssetDatabase.ExportPackage(Env,Path.Combine(Output,"output/StonePrisonEnvironment.unitypackage"),ExportPackageOptions.Recurse);if(!File.Exists(Path.Combine(Output,"output/StonePrisonEnvironment.unitypackage")))throw new Exception("Export missing");r.success=true;
  }catch(Exception e){r.error=e.ToString();Debug.LogException(e);}File.WriteAllText(Path.Combine(ReportDir,"environment-assets.json"),JsonUtility.ToJson(r,true));
 }
 [MenuItem("Tools/Gansuu Head/Environment/6 Verify Existing Rules")]
 public static void CompatibilityCheck(){
  var r=new Compatibility();try{
   r.sessionChecks=PrototypeVerification.Main().count;
   var a=new ShakeAwakening();a.Reset(417);r.zeroProbability=!a.Step(new Vector2(100,0),.5f,8,0,.25f,0)&&a.Activations==0;
   a.Reset(417);r.fullProbability=a.Step(new Vector2(100,0),.5f,8,1,2,0);r.cooldown=!a.Step(new Vector2(-100,0),.13f,8,1,2,0);
   a.Reset(417);for(int i=0;i<400;i++)a.Step(new Vector2(i%2==0?100:-100,0),.5f,8,.35f,.25f,.1f);r.fractionalProbability=a.Activations>80&&a.Activations<200;
   r.success=r.sessionChecks==20&&r.zeroProbability&&r.fullProbability&&r.cooldown&&r.fractionalProbability;if(!r.success)throw new Exception("Existing rules regression");
  }catch(Exception e){r.error=e.ToString();Debug.LogException(e);}File.WriteAllText(Path.Combine(ReportDir,"existing-rules.json"),JsonUtility.ToJson(r,true));
 }
 [MenuItem("Tools/Gansuu Head/Environment/5 Measure Editor Performance")]
 public static async void PerformanceTest(){
  var r=new Performance();MainPrototype controller=null;bool bg=Application.runInBackground;
  try{
   if(!EditorApplication.isPlaying)throw new Exception("Play required");
   controller=UnityEngine.Object.FindAnyObjectByType<MainPrototype>();controller.DeveloperReplayInput=true;Application.runInBackground=true;
   c=controller;main=c.head.GetComponent<Camera>();gameView=EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor"));gameView.Focus();SetPose(.5f);
   var ms=new List<float>();int last=Time.renderedFrameCount;double start=EditorApplication.timeSinceStartup;
   while(ms.Count<300){EditorApplication.QueuePlayerLoopUpdate();gameView.Repaint();await Task.Delay(8);if(!EditorApplication.isPlaying)throw new Exception("Play ended");if(EditorApplication.timeSinceStartup-start>90)throw new Exception("Performance timed out");if(Time.renderedFrameCount!=last){last=Time.renderedFrameCount;ms.Add(Time.unscaledDeltaTime*1000);}}
   var data=ms.Skip(60).OrderBy(x=>x).ToArray();r.samples=data.Length;r.medianMs=data[data.Length/2];r.p95Ms=data[(int)(data.Length*.95f)];r.meanFps=1000/data.Average();
   r.triangles=UnityStats.triangles;r.vertices=UnityStats.vertices;r.drawCalls=UnityStats.drawCalls;r.batches=-1;r.setPass=UnityStats.setPassCalls;r.shadowCasters=UnityStats.shadowCasters;
   var rt=GameTarget();r.width=rt.width;r.height=rt.height;r.gpu=SystemInfo.graphicsDeviceName;r.pipeline=AssetDatabase.GetAssetPath(GraphicsSettings.currentRenderPipeline);r.vSync=QualitySettings.vSyncCount;r.targetFps=Application.targetFrameRate;r.success=true;
  }catch(Exception ex){r.error=ex.ToString();Debug.LogException(ex);}finally{if(controller!=null)controller.DeveloperReplayInput=false;Application.runInBackground=bg;File.WriteAllText(Path.Combine(ReportDir,Run+"-performance.json"),JsonUtility.ToJson(r,true));EditorApplication.delayCall+=()=>EditorApplication.isPlaying=false;}
 }
}


