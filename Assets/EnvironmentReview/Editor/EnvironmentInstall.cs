using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using GansuuHead;
public static partial class EnvironmentReview {
 const string Env="Assets/StonePrisonEnvironment";
 [Serializable] public class EnvMaterial{public string name,texturePrefix;public float[] color;public float metallic,smoothness;}
 [Serializable] public class Manifest{public EnvMaterial[] materials;public int triangles;}
 [Serializable] public class InstallResult{public bool success,gameplayUnchanged,expressionUnchanged,eyelidsUnchanged,uiUnchanged,cameraTransformUnchanged;public string error,pipeline;public int environmentTriangles,environmentRenderers;public Vector3 min,max;public float[] gripRadialErrors;public string lighting="One directional soft-shadow light, five nonshadow local spots/points including prior face/rim lights; SSAO for local contact. Realtime lighting, no completed lightmap bake.";}
 static void CopyAssetOnce(string src,string dst){if(!File.Exists(dst)&&!AssetDatabase.CopyAsset(src,dst))throw new Exception("Copy failed "+src);}
 static void BatchRoom(GameObject root){
  var source=root.GetComponentsInChildren<MeshRenderer>();var groups=new Dictionary<Material,List<CombineInstance>>();
  foreach(var renderer in source){var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;for(int s=0;s<mesh.subMeshCount;s++){
   var mat=renderer.sharedMaterials[s];if(!groups.ContainsKey(mat))groups[mat]=new List<CombineInstance>();groups[mat].Add(new CombineInstance{mesh=mesh,subMeshIndex=s,transform=root.transform.worldToLocalMatrix*renderer.transform.localToWorldMatrix});
  }}
  var temporary=new List<Mesh>();var parts=new List<CombineInstance>();var materials=new List<Material>();
  foreach(var pair in groups){var part=new Mesh{indexFormat=IndexFormat.UInt32};part.CombineMeshes(pair.Value.ToArray(),true,true);temporary.Add(part);parts.Add(new CombineInstance{mesh=part,transform=Matrix4x4.identity});materials.Add(pair.Key);}
  var combined=new Mesh{name="StonePrison_Batched",indexFormat=IndexFormat.UInt32};combined.CombineMeshes(parts.ToArray(),false,false);combined.RecalculateBounds();Unwrapping.GenerateSecondaryUVSet(combined);
  Directory.CreateDirectory(Env+"/Models");string path=Env+"/Models/StonePrison_Batched.asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(saved==null){AssetDatabase.CreateAsset(combined,path);saved=combined;}else{EditorUtility.CopySerialized(combined,saved);UnityEngine.Object.DestroyImmediate(combined);EditorUtility.SetDirty(saved);}
  foreach(var m in temporary)UnityEngine.Object.DestroyImmediate(m);foreach(var r in source)r.enabled=false;
  var go=new GameObject("Environment_Batched");go.transform.SetParent(root.transform,false);go.AddComponent<MeshFilter>().sharedMesh=saved;go.AddComponent<MeshRenderer>().sharedMaterials=materials.ToArray();go.isStatic=true;
 }
 static Material EnvMat(EnvMaterial info){
  string path=Env+"/Materials/"+info.name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
  if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
  m.SetColor("_BaseColor",new Color(info.color[0],info.color[1],info.color[2]));m.SetFloat("_Metallic",info.metallic);m.SetFloat("_Smoothness",info.smoothness);m.enableInstancing=true;
  if(!string.IsNullOrEmpty(info.texturePrefix)){
   string t=Env+"/Textures/"+info.texturePrefix;m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(t+"_Base.png"));m.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(t+"_Normal.png"));m.EnableKeyword("_NORMALMAP");m.SetFloat("_BumpScale",info.texturePrefix=="Iron"?.55f:.8f);
   var surface=AssetDatabase.LoadAssetAtPath<Texture2D>(t+"_Surface.png");m.SetTexture("_MetallicGlossMap",surface);m.EnableKeyword("_METALLICSPECGLOSSMAP");m.SetFloat("_Smoothness",1);m.SetTexture("_OcclusionMap",surface);m.SetFloat("_OcclusionStrength",.45f);
  }
  if(info.name=="Env_LanternGlass"){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",new Color(.40f,.22f,.065f));}
  EditorUtility.SetDirty(m);return m;
 }
 static void EnvPoint(string name,Vector3 p,Color color,float intensity,float range){
  var go=GameObject.Find(name);if(go==null)go=new GameObject(name);go.transform.position=p;var l=go.GetComponent<Light>();if(l==null)l=go.AddComponent<Light>();l.type=LightType.Point;l.color=color;l.intensity=intensity;l.range=range;l.shadows=LightShadows.None;
 }
 [MenuItem("Tools/Gansuu Head/Environment/1 Apply Stone Prison")]
 public static void InstallEnvironment(){
  var result=new InstallResult();try{
   if(EditorApplication.isPlaying)throw new Exception("Stop Play first");var scene=EditorSceneManager.GetActiveScene();if(scene.path!=ScenePath||scene.isDirty)throw new Exception("Open saved MainPrototype first");
   var controller=UnityEngine.Object.FindAnyObjectByType<MainPrototype>();var camera=controller.head.GetComponent<Camera>();
   string core=Json(controller),expr=Json(controller.prisonerExpression),mask=Json(controller.eyelidMask),ui=Json(controller.localizedUi);var camPose=controller.head.localToWorldMatrix;
   Directory.CreateDirectory(Env+"/Materials");Directory.CreateDirectory(Env+"/Rendering");AssetDatabase.Refresh();
   foreach(string path in Directory.GetFiles(Env+"/Textures","*.png")){
    var t=(TextureImporter)AssetImporter.GetAtPath(path);t.maxTextureSize=1024;t.mipmapEnabled=true;t.wrapMode=TextureWrapMode.Repeat;t.anisoLevel=4;t.textureCompression=TextureImporterCompression.Compressed;
    t.sRGBTexture=path.Contains("_Base");t.textureType=path.Contains("_Normal")?TextureImporterType.NormalMap:TextureImporterType.Default;t.alphaSource=TextureImporterAlphaSource.FromInput;t.SaveAndReimport();
   }
   var data=JsonUtility.FromJson<Manifest>(File.ReadAllText(Env+"/environment-manifest.json"));var mats=data.materials.ToDictionary(m=>m.name,EnvMat);
   var imp=(ModelImporter)AssetImporter.GetAtPath(Env+"/StonePrison_Environment.fbx");imp.globalScale=1;imp.useFileScale=true;imp.importAnimation=false;imp.addCollider=false;imp.generateSecondaryUV=true;imp.importNormals=ModelImporterNormals.Import;imp.importTangents=ModelImporterTangents.CalculateMikk;imp.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
   foreach(var pair in mats)imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),pair.Key),pair.Value);imp.SaveAndReimport();
   var old=GameObject.Find("StonePrison_Environment_Integrated");if(old!=null)UnityEngine.Object.DestroyImmediate(old);
   var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Env+"/StonePrison_Environment.fbx");var root=(GameObject)PrefabUtility.InstantiatePrefab(asset);root.name="StonePrison_Environment_Integrated";root.transform.position=Vector3.zero;
   BatchRoom(root);
   foreach(var r in root.GetComponentsInChildren<MeshRenderer>()){r.shadowCastingMode=ShadowCastingMode.On;r.receiveShadows=true;r.gameObject.isStatic=true;}
   var renderers=root.GetComponentsInChildren<Renderer>().Where(r=>r.enabled).ToArray();var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);result.min=bounds.min;result.max=bounds.max;
   if(bounds.max.z<6.8f||bounds.max.z>7.5f||bounds.max.y<3.8f||bounds.min.y<-.3f)throw new Exception("Environment axes or scale mismatch "+bounds);
   foreach(var r in GameObject.Find("Cell").GetComponentsInChildren<Renderer>())r.enabled=false;
   foreach(var r in GameObject.Find("Foreground Bars").GetComponentsInChildren<Renderer>())r.enabled=false;
   // The old named transforms stay available for the original grip verifiers.
   var grips=GameObject.Find("Prisoner_V2_Integrated").GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Grip_")).OrderBy(t=>t.position.x).ToArray();
   result.gripRadialErrors=grips.Select((t,i)=>{Vector3 d=t.position-new Vector3(i==0?-.48f:.48f,t.position.y,2.65f);return new Vector2(d.x,d.z).magnitude;}).ToArray();if(result.gripRadialErrors.Any(x=>x>.002f))throw new Exception("Grip alignment changed");
   var key=GameObject.Find("Cell Key Light").GetComponent<Light>();key.transform.rotation=Quaternion.Euler(42,-32,0);key.color=new Color(.67f,.76f,.88f);key.intensity=.70f;key.shadows=LightShadows.Soft;key.shadowStrength=.72f;key.shadowBias=.025f;key.shadowNormalBias=.025f;
   EnvPoint("Environment Cool Interior",new Vector3(.7f,2.85f,4.9f),new Color(.49f,.62f,.76f),1.25f,4.5f);
   EnvPoint("Environment Lantern",root.GetComponentsInChildren<Renderer>().Single(r=>r.name=="Lantern").bounds.center+new Vector3(0,0,-.35f),new Color(.93f,.68f,.38f),1.0f,2.4f);
   RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.15f,.19f,.25f);RenderSettings.ambientEquatorColor=new Color(.10f,.12f,.145f);RenderSettings.ambientGroundColor=new Color(.055f,.062f,.072f);RenderSettings.ambientIntensity=1;
   RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogStartDistance=6.0f;RenderSettings.fogEndDistance=15;RenderSettings.fogColor=new Color(.065f,.083f,.105f);
   string rpPath=Env+"/Rendering/Prison_PC.asset",rendererPath=Env+"/Rendering/Prison_PC_Renderer.asset";
   CopyAssetOnce("Assets/Settings/PC_RPAsset.asset",rpPath);CopyAssetOnce("Assets/Settings/PC_Renderer.asset",rendererPath);
   var rp=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(rpPath);var rd=AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(rendererPath);var so=new SerializedObject(rp);so.FindProperty("m_RendererDataList").GetArrayElementAtIndex(0).objectReferenceValue=rd;
   so.FindProperty("m_ShadowDistance").floatValue=12;so.FindProperty("m_ShadowCascadeCount").intValue=2;so.FindProperty("m_AdditionalLightsShadowmapResolution").intValue=512;so.FindProperty("m_MSAA").intValue=1;so.ApplyModifiedPropertiesWithoutUndo();
   foreach(var f in rd.rendererFeatures)if(f!=null&&f.GetType().Name=="ScreenSpaceAmbientOcclusion"){
    var fs=new SerializedObject(f);fs.FindProperty("m_Settings.Intensity").floatValue=.38f;fs.FindProperty("m_Settings.Radius").floatValue=.19f;fs.FindProperty("m_Settings.Downsample").boolValue=true;fs.FindProperty("m_Settings.DirectLightingStrength").floatValue=.18f;fs.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(f);
   }
   EditorUtility.SetDirty(rd);EditorUtility.SetDirty(rp);QualitySettings.renderPipeline=rp;result.pipeline=AssetDatabase.GetAssetPath(rp);
   var cameraData=camera.GetUniversalAdditionalCameraData();cameraData.renderPostProcessing=true;cameraData.antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;cameraData.antialiasingQuality=AntialiasingQuality.High;
   string profilePath=Env+"/Rendering/Prison_Grade.asset";var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);if(profile==null){profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,profilePath);}
   var tm=profile.TryGet<Tonemapping>(out var existingTm)?existingTm:profile.Add<Tonemapping>(true);tm.mode.Override(TonemappingMode.Neutral);
   var grade=profile.TryGet<ColorAdjustments>(out var existingGrade)?existingGrade:profile.Add<ColorAdjustments>(true);grade.postExposure.Override(.10f);grade.contrast.Override(8);grade.saturation.Override(-7);grade.colorFilter.Override(new Color(.96f,.985f,1));
   var wb=profile.TryGet<WhiteBalance>(out var existingWb)?existingWb:profile.Add<WhiteBalance>(true);wb.temperature.Override(-3);
   var bloom=profile.TryGet<Bloom>(out var existingBloom)?existingBloom:profile.Add<Bloom>(true);bloom.intensity.Override(0);
   var vignette=profile.TryGet<Vignette>(out var existingVignette)?existingVignette:profile.Add<Vignette>(true);vignette.intensity.Override(0);
   var grain=profile.TryGet<FilmGrain>(out var existingGrain)?existingGrain:profile.Add<FilmGrain>(true);grain.intensity.Override(0);
   foreach(var comp in profile.components)if(!AssetDatabase.Contains(comp))AssetDatabase.AddObjectToAsset(comp,profile);
   EditorUtility.SetDirty(profile);var volumeGo=GameObject.Find("Environment Grade");if(volumeGo==null)volumeGo=new GameObject("Environment Grade");var volume=volumeGo.GetComponent<Volume>();if(volume==null)volume=volumeGo.AddComponent<Volume>();volume.isGlobal=true;volume.priority=50;volume.sharedProfile=profile;
   result.gameplayUnchanged=core==Json(controller);result.expressionUnchanged=expr==Json(controller.prisonerExpression);result.eyelidsUnchanged=mask==Json(controller.eyelidMask);result.uiUnchanged=ui==Json(controller.localizedUi);result.cameraTransformUnchanged=camPose==controller.head.localToWorldMatrix;
   if(!result.gameplayUnchanged||!result.expressionUnchanged||!result.eyelidsUnchanged||!result.uiUnchanged||!result.cameraTransformUnchanged)throw new Exception("Protected component changed");
   result.environmentTriangles=data.triangles;result.environmentRenderers=renderers.Length;PrefabUtility.SaveAsPrefabAsset(root,Env+"/StonePrison.prefab");
   EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("Scene save failed");AssetDatabase.SaveAssets();result.success=true;
  }catch(Exception ex){result.error=ex.ToString();Debug.LogException(ex);}
  File.WriteAllText(Path.Combine(ReportDir,"environment-install.json"),JsonUtility.ToJson(result,true));
 }
}

