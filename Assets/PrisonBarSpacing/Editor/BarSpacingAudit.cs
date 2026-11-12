using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using GansuuHead;
public static partial class BarSpacingReview {
 [Serializable] public class GateAudit{public bool success;public string error;public int rods;public float minPitch,maxPitch,minClear,maxClear,headBandWidth;public float[] centers;public string expressionMeshAsset,bodyMeshAsset,environmentMeshAsset;public Vector3 prisonerPosition;public Vector3[] gripPositions;public bool savedPoseVerified;}
 [MenuItem("Tools/Gansuu Head/Gate Spacing/7 Audit Geometry")]
 public static void AuditGeometry(){
  var r=new GateAudit();try{
   if(EditorApplication.isPlaying)throw new Exception("Stop Play first");var asset=AssetDatabase.LoadAssetAtPath<GameObject>(NewAssets+"/Gate_UniformSpacing.fbx");var f=asset.GetComponentsInChildren<MeshFilter>().Single(x=>x.name=="Gate_Revised_Bars");
   var xs=f.sharedMesh.vertices.Select(v=>f.transform.TransformPoint(v).x).OrderBy(x=>x).ToArray();var clusters=new List<List<float>>();
   foreach(float x in xs){if(clusters.Count==0||x-clusters.Last().Last()>.1f)clusters.Add(new List<float>());clusters.Last().Add(x);}
   r.rods=clusters.Count;r.centers=clusters.Select(a=>(a.First()+a.Last())*.5f).ToArray();var pitches=Enumerable.Range(1,r.rods-1).Select(i=>r.centers[i]-r.centers[i-1]).ToArray();var clear=Enumerable.Range(1,r.rods-1).Select(i=>clusters[i].First()-clusters[i-1].Last()).ToArray();r.minPitch=pitches.Min();r.maxPitch=pitches.Max();r.minClear=clear.Min();r.maxClear=clear.Max();
      var character=GameObject.Find("Prisoner_V2_Integrated");r.prisonerPosition=character.transform.position;r.bodyMeshAsset=AssetDatabase.GetAssetPath(character.GetComponentsInChildren<MeshFilter>().Single(x=>x.name=="Prisoner_Uniform_Arms_Hands").sharedMesh);r.environmentMeshAsset=AssetDatabase.GetAssetPath(GameObject.Find("StonePrison_Environment_Integrated").GetComponentsInChildren<MeshFilter>().Single(x=>x.name=="Environment_Batched").sharedMesh);r.gripPositions=character.GetComponentsInChildren<Transform>().Where(x=>x.name.StartsWith("Grip_")).OrderBy(x=>x.position.x).Select(x=>x.position).ToArray();r.savedPoseVerified=Mathf.Abs(r.prisonerPosition.z-3.2f)<.0001f&&r.bodyMeshAsset==NewAssets+"/Prisoner_GripAdjusted.asset"&&r.environmentMeshAsset==NewAssets+"/Environment_UniformGate.asset"&&r.gripPositions.All(x=>Mathf.Abs(Mathf.Abs(x.x)-.42f)<.0001f&&Mathf.Abs(x.z-2.65f)<.0001f);if(!r.savedPoseVerified)throw new Exception("Saved scene pose references are not preserved");
   var face=UnityEngine.Object.FindAnyObjectByType<MainPrototype>().prisonerExpression.face;r.expressionMeshAsset=AssetDatabase.GetAssetPath(face.sharedMesh);var baked=new Mesh();face.BakeMesh(baked,true);var band=baked.vertices.Select(v=>face.transform.TransformPoint(v)).Where(p=>p.y>2.16f&&p.y<2.22f).ToArray();r.headBandWidth=band.Max(v=>v.x)-band.Min(v=>v.x);UnityEngine.Object.DestroyImmediate(baked);
   if(r.rods!=14||Mathf.Abs(r.minPitch-.28f)>.0001f||Mathf.Abs(r.maxPitch-.28f)>.0001f||Mathf.Abs(r.minClear-.225f)>.0001f||r.maxClear>=r.headBandWidth)throw new Exception("Uniform pitch or head clearance validation failed");r.success=true;
  }catch(Exception e){r.error=e.ToString();Debug.LogException(e);}File.WriteAllText(Path.Combine(ReportDir,"geometry-audit.json"),JsonUtility.ToJson(r,true));
 }
}

