using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using GansuuHead;
public static partial class GripPolishReview {
 const string Assets="Assets/PrisonerGripPolish";
 [Serializable] public class Part{public string material;public int[] triangles;}
 [Serializable] public class BodyExport{public Vector3[] vertices;public Vector2[] uv;public Part[] parts;public string meshAsset;public Vector3 bodyPosition;}
 static bool HandFace(Vector3[] verts,int a,int b,int c,string material){
  if(material!="Skin"&&material!="Fingernails")return false;
  foreach(int i in new[]{a,b,c}){var p=verts[i];if(Mathf.Abs(p.x)<.30f||p.y<1.58f||p.y>1.90f||p.z>2.91f)return false;}return true;
 }
 [MenuItem("Tools/Gansuu Head/Grip Polish/0 Export Current Hands")]
 public static void ExportCurrent(){
  if(EditorApplication.isPlaying)throw new Exception("Stop Play first");var body=GameObject.Find("Prisoner_V2_Integrated").GetComponentsInChildren<MeshFilter>().Single(x=>x.name=="Prisoner_Uniform_Arms_Hands");var mesh=body.sharedMesh;var materials=body.GetComponent<Renderer>().sharedMaterials;
  var data=new BodyExport{vertices=mesh.vertices.Select(v=>body.transform.TransformPoint(v)).ToArray(),uv=mesh.uv,meshAsset=AssetDatabase.GetAssetPath(mesh),bodyPosition=body.transform.position,parts=Enumerable.Range(0,mesh.subMeshCount).Select(i=>new Part{material=materials[i].name,triangles=mesh.GetTriangles(i)}).ToArray()};
  File.WriteAllText(Path.Combine(Output,"output/CurrentBody_World.json"),JsonUtility.ToJson(data));
 }
 [Serializable] public class ApplyResult{public bool success,faceUnchanged,protectedComponentsUnchanged,retainedBodyVerticesExact;public string error;public int removedHandTriangles,retainedVertices,newHandVertices,newHandTriangles;public float minSampledBarGap;}
 static string Protected(){
  var c=UnityEngine.Object.FindAnyObjectByType<MainPrototype>();var list=new List<string>{Json(c),Json(c.prisonerExpression),Json(c.eyelidMask),Json(c.localizedUi),Json(c.head),Json(c.head.GetComponent<Camera>())};
  foreach(var l in UnityEngine.Object.FindObjectsByType<Light>())list.Add(Json(l)+Json(l.transform));foreach(var v in UnityEngine.Object.FindObjectsByType<UnityEngine.Rendering.Volume>())list.Add(Json(v)+Json(v.transform));
  var character=GameObject.Find("Prisoner_V2_Integrated");list.Add(Json(character.transform));foreach(var t in character.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Grip_")))list.Add(Json(t));return string.Join("\n",list);
 }
 static string Hash(string path){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","");}
 [MenuItem("Tools/Gansuu Head/Grip Polish/1 Apply Hands Only")]
 public static void ApplyHands(){
  var r=new ApplyResult();try{
   if(EditorApplication.isPlaying)throw new Exception("Stop Play first");var scene=EditorSceneManager.GetActiveScene();if(scene.path!=ScenePath||scene.isDirty)throw new Exception("Saved main scene required");
   if(Hash(ScenePath)!=File.ReadAllText(Path.Combine(ReportDir,"scene-expected-hash.txt")).Trim())throw new Exception("Concurrent scene change detected; refusing overwrite");
   var c=UnityEngine.Object.FindAnyObjectByType<MainPrototype>();var face=c.prisonerExpression.face.sharedMesh;string before=Protected();File.WriteAllText(Path.Combine(ReportDir,"protected-before.txt"),before);
   var body=GameObject.Find("Prisoner_V2_Integrated").GetComponentsInChildren<MeshFilter>().Single(f=>f.name=="Prisoner_Uniform_Arms_Hands");var renderer=body.GetComponent<MeshRenderer>();var materials=renderer.sharedMaterials;
   var data=JsonUtility.FromJson<BodyExport>(File.ReadAllText(Path.Combine(Output,"output/CurrentBody_World.json")));var source=AssetDatabase.LoadAssetAtPath<Mesh>(data.meshAsset);if(source==null)throw new Exception("Original posed body missing");
   var old=source.vertices;var oldNormals=source.normals;var oldUV=source.uv;var oldTangents=source.tangents;var world=old.Select(v=>body.transform.TransformPoint(v)).ToArray();var keep=new List<int>[source.subMeshCount];var used=new HashSet<int>();
   for(int s=0;s<source.subMeshCount;s++){keep[s]=new List<int>();var tri=source.GetTriangles(s);for(int i=0;i<tri.Length;i+=3){if(HandFace(world,tri[i],tri[i+1],tri[i+2],materials[s].name)){r.removedHandTriangles++;continue;}for(int j=0;j<3;j++){keep[s].Add(tri[i+j]);used.Add(tri[i+j]);}}}
   if(r.removedHandTriangles<5000)throw new Exception("Hand selection too small");
   var retained=used.OrderBy(i=>i).ToArray();var map=retained.Select((id,i)=>(id,i)).ToDictionary(x=>x.id,x=>x.i);var verts=retained.Select(i=>old[i]).ToList();var normals=retained.Select(i=>oldNormals[i]).ToList();var uv=retained.Select(i=>oldUV[i]).ToList();var tangents=retained.Select(i=>oldTangents[i]).ToList();var triangles=keep.Select(indices=>indices.Select(i=>map[i]).ToList()).ToArray();r.retainedVertices=retained.Length;
   var importer=(ModelImporter)AssetImporter.GetAtPath(Assets+"/Prisoner_GripPolish.fbx");importer.importAnimation=false;importer.importNormals=ModelImporterNormals.Import;importer.importTangents=ModelImporterTangents.CalculateMikk;importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),"Skin"),materials.First(m=>m.name=="Skin"));importer.SaveAndReimport();
   var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Assets+"/Prisoner_GripPolish.fbx");var hands=(GameObject)PrefabUtility.InstantiatePrefab(asset);hands.name="TEMP_HandPolish_Import";
   r.minSampledBarGap=100;int skinIndex=Array.FindIndex(materials,m=>m.name=="Skin");
   try{foreach(var hf in hands.GetComponentsInChildren<MeshFilter>()){
    var hm=hf.sharedMesh;int offset=verts.Count;var matrix=body.transform.worldToLocalMatrix*hf.transform.localToWorldMatrix;var nm=matrix.inverse.transpose;var hv=hm.vertices;var hn=hm.normals;var hu=hm.uv;var ht=hm.tangents;
    for(int i=0;i<hv.Length;i++){verts.Add(matrix.MultiplyPoint3x4(hv[i]));normals.Add(nm.MultiplyVector(hn[i]).normalized);uv.Add(hu[i]);var t=matrix.MultiplyVector(new Vector3(ht[i].x,ht[i].y,ht[i].z)).normalized;tangents.Add(new Vector4(t.x,t.y,t.z,ht[i].w*Mathf.Sign(matrix.determinant)));}
    var htIndices=hm.triangles;triangles[skinIndex].AddRange(htIndices.Select(i=>i+offset));r.newHandVertices+=hv.Length;r.newHandTriangles+=htIndices.Length/3;
    for(int i=0;i<htIndices.Length;i+=3){var points=new[]{hv[htIndices[i]],hv[htIndices[i+1]],hv[htIndices[i+2]]}.Select(v=>hf.transform.TransformPoint(v)).ToArray();foreach(var bary in new[]{new Vector3(1,0,0),new Vector3(0,1,0),new Vector3(0,0,1),new Vector3(.5f,.5f,0),new Vector3(.5f,0,.5f),new Vector3(0,.5f,.5f),Vector3.one/3}){var p=points[0]*bary.x+points[1]*bary.y+points[2]*bary.z;float center=Mathf.Sign(p.x)*.42f;float gap=new Vector2(p.x-center,p.z-2.65f).magnitude-.0275f;r.minSampledBarGap=Mathf.Min(r.minSampledBarGap,gap);}}
   }}finally{UnityEngine.Object.DestroyImmediate(hands);}
   if(r.minSampledBarGap<0)throw new Exception("New hand intersects bar");
   var mesh=new Mesh{name="Prisoner_HandPolish_Body",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.SetVertices(verts);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTangents(tangents);mesh.subMeshCount=triangles.Length;for(int i=0;i<triangles.Length;i++)mesh.SetTriangles(triangles[i],i);mesh.RecalculateBounds();
   var mergedVertices=mesh.vertices;r.retainedBodyVerticesExact=retained.Select((oldId,i)=>mergedVertices[i]==old[oldId]).All(x=>x);if(!r.retainedBodyVerticesExact)throw new Exception("Non-hand body vertex modified");
   string path=Assets+"/Prisoner_HandsPolished.asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(saved==null){AssetDatabase.CreateAsset(mesh,path);saved=mesh;}else{EditorUtility.CopySerialized(mesh,saved);UnityEngine.Object.DestroyImmediate(mesh);EditorUtility.SetDirty(saved);}body.sharedMesh=saved;PrefabUtility.RecordPrefabInstancePropertyModifications(body);
   r.faceUnchanged=face==c.prisonerExpression.face.sharedMesh;r.protectedComponentsUnchanged=before==Protected();File.WriteAllText(Path.Combine(ReportDir,"protected-after.txt"),Protected());if(!r.faceUnchanged||!r.protectedComponentsUnchanged)throw new Exception("Protected state changed");
   EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("Scene save failed");AssetDatabase.SaveAssets();File.WriteAllText(Path.Combine(ReportDir,"scene-expected-hash.txt"),Hash(ScenePath));r.success=true;
  }catch(Exception ex){r.error=ex.ToString();Debug.LogException(ex);}File.WriteAllText(Path.Combine(ReportDir,"hands-apply.json"),JsonUtility.ToJson(r,true));
 }
}

