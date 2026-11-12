using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class ReorganizationAudit {
 [Serializable] public class Report {
  public string project,scene,target,compression,renderPipeline;
  public bool dirty; public int missingScripts,missingDependencies,missingMaterials;
  public string[] dependencies,missingPaths,buildScenes;
 }
 [MenuItem("Tools/Gansuu Head/Migration/Verify References")]
 public static void Run(){
  var scene=EditorSceneManager.GetActiveScene();
  if(scene.path!="Assets/Scenes/MainPrototype.unity")throw new Exception("Open MainPrototype first");
  var deps=AssetDatabase.GetDependencies(scene.path,true);
  var missing=deps.Where(p=>p.StartsWith("Assets/")&&AssetDatabase.LoadMainAssetAtPath(p)==null).ToArray();
  var roots=scene.GetRootGameObjects();
  var objects=roots.SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Select(t=>t.gameObject).ToArray();
  int scripts=objects.Sum(g=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(g));
  int mats=objects.Where(g=>g.activeInHierarchy).SelectMany(g=>g.GetComponents<Renderer>()).Where(renderer=>renderer.enabled).Sum(renderer=>renderer.sharedMaterials.Count(m=>m==null));
  var pipeline=QualitySettings.renderPipeline ?? UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline;
  var r=new Report{project=Directory.GetParent(Application.dataPath).FullName,scene=scene.path,dirty=scene.isDirty,target=EditorUserBuildSettings.activeBuildTarget.ToString(),compression=PlayerSettings.WebGL.compressionFormat.ToString(),renderPipeline=pipeline==null?"null":AssetDatabase.GetAssetPath(pipeline),missingScripts=scripts,missingDependencies=missing.Length,missingMaterials=mats,dependencies=deps,missingPaths=missing,buildScenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray()};
  string root=Directory.GetParent(Application.dataPath).Parent.Parent.Parent.FullName;
  string output=Path.Combine(root,"Docs/Audit/Reorganization_2026-10-20",EditorUserBuildSettings.activeBuildTarget==BuildTarget.WebGL?"web-reference-audit.json":"pc-reference-audit.json");
  File.WriteAllText(output,JsonUtility.ToJson(r,true));
  if(scripts>0||missing.Length>0||mats>0||pipeline==null)throw new Exception("Reference audit failed; inspect "+output);
  Debug.Log("Migration reference audit passed: "+output);
 }
}


