using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using GansuuHead;
public static partial class BarSpacingReview {
 const string NewAssets="Assets/PrisonBarSpacing";
 static string Hash(string path){using(var s=SHA256.Create())return BitConverter.ToString(s.ComputeHash(File.ReadAllBytes(path))).Replace("-","");}
 [Serializable] public class PoseBaseline{public Vector3 rootPosition;public string bodyMeshName;}
 [Serializable] public class SpacingReport{public bool success,protectedSettingsUnchanged,faceMeshUnchanged,faceMaterialsUnchanged;public string error;public float pitch=.28f,clearWidth=.225f,radius=.0275f,prisonerRetreat=.1f,handInward=.06f,handContactFrameMaxError;public int bars=14,changedBodyVertices,fullHandVertices;public Vector3 prisonerPosition;public float[] barCenters;public Vector3[] gripPositions;}
 static string ProtectedSettings(){
  var c=UnityEngine.Object.FindAnyObjectByType<MainPrototype>();var data=new List<string>{Json(c),Json(c.prisonerExpression),Json(c.eyelidMask),Json(c.localizedUi),Json(c.head),Json(c.head.GetComponent<Camera>()),Json(c.head.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>()),RenderSettings.ambientMode.ToString(),RenderSettings.ambientSkyColor.ToString(),RenderSettings.ambientEquatorColor.ToString(),RenderSettings.fog.ToString(),RenderSettings.fogStartDistance.ToString(),RenderSettings.fogEndDistance.ToString()};
  foreach(var l in UnityEngine.Object.FindObjectsByType<Light>())data.Add(PathOf(l.transform)+Json(l)+Json(l.transform));
  foreach(var v in UnityEngine.Object.FindObjectsByType<Volume>())data.Add(PathOf(v.transform)+Json(v)+Json(v.transform));return string.Join("\n",data);
 }
 static void SaveMesh(Mesh mesh,string path,out Mesh result){
  result=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(result==null){AssetDatabase.CreateAsset(mesh,path);result=mesh;}else{EditorUtility.CopySerialized(mesh,result);UnityEngine.Object.DestroyImmediate(mesh);EditorUtility.SetDirty(result);}
 }
 static void CombineVariant(GameObject environment,GameObject gate){
  var renderers=environment.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name!="Environment_Batched"&&r.name!="Gate_Bars"&&r.name!="Gate_Hardware").Concat(gate.GetComponentsInChildren<MeshRenderer>()).ToArray();
  var groups=new Dictionary<Material,List<CombineInstance>>();
  foreach(var r in renderers){var mesh=r.GetComponent<MeshFilter>().sharedMesh;for(int s=0;s<mesh.subMeshCount;s++){var m=r.sharedMaterials[s];if(!groups.ContainsKey(m))groups[m]=new List<CombineInstance>();groups[m].Add(new CombineInstance{mesh=mesh,subMeshIndex=s,transform=environment.transform.worldToLocalMatrix*r.transform.localToWorldMatrix});}}
  var temporary=new List<Mesh>();var parts=new List<CombineInstance>();var materials=new List<Material>();
  foreach(var pair in groups){var mesh=new Mesh{indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(pair.Value.ToArray(),true,true);temporary.Add(mesh);parts.Add(new CombineInstance{mesh=mesh,transform=Matrix4x4.identity});materials.Add(pair.Key);}
  var combined=new Mesh{name="Prison_UniformGate_Variant",indexFormat=IndexFormat.UInt32};combined.CombineMeshes(parts.ToArray(),false,false);combined.RecalculateBounds();Unwrapping.GenerateSecondaryUVSet(combined);
  SaveMesh(combined,NewAssets+"/Environment_UniformGate.asset",out var saved);foreach(var m in temporary)UnityEngine.Object.DestroyImmediate(m);
  var target=environment.GetComponentsInChildren<MeshFilter>().Single(x=>x.name=="Environment_Batched");target.sharedMesh=saved;target.GetComponent<MeshRenderer>().sharedMaterials=materials.ToArray();
 }
 [MenuItem("Tools/Gansuu Head/Gate Spacing/1 Apply Uniform Gate And Grip")]
 public static void ApplySpacing(){
  var report=new SpacingReport();try{
   if(EditorApplication.isPlaying)throw new Exception("Stop Play first");var scene=EditorSceneManager.GetActiveScene();if(scene.path!=ScenePath||scene.isDirty)throw new Exception("Saved MainPrototype required; refusing dirty scene");
   string expected=File.ReadAllText(Path.Combine(ReportDir,"scene-expected-hash.txt")).Trim();if(Hash(ScenePath)!=expected)throw new Exception("Concurrent scene edit detected; no scene overwrite performed");
   string protectedBefore=ProtectedSettings();var c=UnityEngine.Object.FindAnyObjectByType<MainPrototype>();var faceMesh=c.prisonerExpression.face.sharedMesh;var faceMats=c.prisonerExpression.face.sharedMaterials;
   File.WriteAllText(Path.Combine(ReportDir,"protected-settings-before.txt"),protectedBefore);
   var character=GameObject.Find("Prisoner_V2_Integrated");var body=character.GetComponentsInChildren<MeshFilter>().Single(f=>f.name=="Prisoner_Uniform_Arms_Hands");
   string baselinePath=Path.Combine(ReportDir,"pose-baseline.json");PoseBaseline baseline;
   if(File.Exists(baselinePath))baseline=JsonUtility.FromJson<PoseBaseline>(File.ReadAllText(baselinePath));else{baseline=new PoseBaseline{rootPosition=character.transform.position,bodyMeshName=body.sharedMesh.name};File.WriteAllText(baselinePath,JsonUtility.ToJson(baseline,true));}
   character.transform.position=baseline.rootPosition;
   var source=AssetDatabase.LoadAllAssetsAtPath("Assets/PrisonerV2/Prisoner_V2.fbx").OfType<Mesh>().Single(m=>m.name==baseline.bodyMeshName);var revised=UnityEngine.Object.Instantiate(source);revised.name="Prisoner_Grip_UniformGate";
   var old=source.vertices;var verts=(Vector3[])old.Clone();var weights=new float[old.Length];var matrix=body.transform.localToWorldMatrix;var inverse=matrix.inverse;
   for(int i=0;i<verts.Length;i++){
    var p=matrix.MultiplyPoint3x4(old[i]);float w=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.25f,.35f,Mathf.Abs(p.x)))*Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.23f,1.5f,p.y))*Mathf.SmoothStep(0,1,Mathf.InverseLerp(3.10f,2.84f,p.z));weights[i]=w;
    if(w<.000001f)continue;var shift=new Vector3(-Mathf.Sign(p.x)*.06f,0,-.10f)*w;verts[i]=inverse.MultiplyPoint3x4(p+shift);report.changedBodyVertices++;
    if(w>.99999f&&p.z<2.84f&&p.y>1.5f){report.fullHandVertices++;var after=p+shift+new Vector3(0,0,.1f);var oldFrame=p-new Vector3(Mathf.Sign(p.x)*.48f,0,2.65f);var newFrame=after-new Vector3(Mathf.Sign(p.x)*.42f,0,2.65f);report.handContactFrameMaxError=Mathf.Max(report.handContactFrameMaxError,(oldFrame-newFrame).magnitude);}
   }
   revised.vertices=verts;revised.RecalculateNormals();var normals=revised.normals;var originalNormals=source.normals;
   for(int i=0;i<normals.Length;i++)if(weights[i]<.000001f||weights[i]>.99999f)normals[i]=originalNormals[i];revised.normals=normals;revised.RecalculateTangents();var tangents=revised.tangents;var oldTangents=source.tangents;for(int i=0;i<tangents.Length;i++)if(weights[i]<.000001f||weights[i]>.99999f)tangents[i]=oldTangents[i];revised.tangents=tangents;revised.RecalculateBounds();
   SaveMesh(revised,NewAssets+"/Prisoner_GripAdjusted.asset",out var savedBody);body.sharedMesh=savedBody;character.transform.position=baseline.rootPosition+new Vector3(0,0,.1f);
   foreach(var grip in character.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Grip_"))){var p=grip.position;p.x=Mathf.Sign(p.x)*.42f;p.z=2.65f;grip.position=p;}
   report.gripPositions=character.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Grip_")).OrderBy(t=>t.position.x).Select(t=>t.position).ToArray();
   var imp=(ModelImporter)AssetImporter.GetAtPath(NewAssets+"/Gate_UniformSpacing.fbx");imp.importAnimation=false;imp.generateSecondaryUV=true;imp.importNormals=ModelImporterNormals.Import;imp.importTangents=ModelImporterTangents.CalculateMikk;
   foreach(string name in new[]{"Env_WroughtIron","Env_WornIron"})imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),name),AssetDatabase.LoadAssetAtPath<Material>("Assets/StonePrisonEnvironment/Materials/"+name+".mat"));imp.SaveAndReimport();
   var environment=GameObject.Find("StonePrison_Environment_Integrated");var sourceAsset=AssetDatabase.LoadAssetAtPath<GameObject>(NewAssets+"/Gate_UniformSpacing.fbx");var gate=(GameObject)PrefabUtility.InstantiatePrefab(sourceAsset);gate.name="UniformGate_Source";gate.transform.position=Vector3.zero;
   CombineVariant(environment,gate);UnityEngine.Object.DestroyImmediate(gate);
   report.barCenters=Enumerable.Range(-7,14).Select(i=>(i+.5f)*.28f).ToArray();report.prisonerPosition=character.transform.position;
   report.faceMeshUnchanged=c.prisonerExpression.face.sharedMesh==faceMesh;report.faceMaterialsUnchanged=c.prisonerExpression.face.sharedMaterials.SequenceEqual(faceMats);report.protectedSettingsUnchanged=protectedBefore==ProtectedSettings();
   File.WriteAllText(Path.Combine(ReportDir,"protected-settings-after.txt"),ProtectedSettings());
   if(!report.faceMeshUnchanged||!report.faceMaterialsUnchanged||!report.protectedSettingsUnchanged||report.handContactFrameMaxError>.0001f)throw new Exception("Protected state or hand contact changed");
   EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("Save failed");AssetDatabase.SaveAssets();File.WriteAllText(Path.Combine(ReportDir,"scene-expected-hash.txt"),Hash(ScenePath));report.success=true;
  }catch(Exception e){report.error=e.ToString();Debug.LogException(e);}File.WriteAllText(Path.Combine(ReportDir,"spacing-apply.json"),JsonUtility.ToJson(report,true));
 }
}
