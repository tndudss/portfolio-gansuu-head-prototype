using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using GansuuHead;
public static class ReleaseBuild {
 static string Root=>Environment.GetEnvironmentVariable("GANSUU_LEGACY_BUILD_OUTPUT") ?? throw new InvalidOperationException("Legacy builder requires an explicit separate GANSUU_LEGACY_BUILD_OUTPUT; use PlatformExitBuild for current candidates.");
 public static void Windows(){
  EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/MainPrototype.unity",true)};
  PlayerSettings.bundleVersion="0.1.0-rc1";
  AssetDatabase.SaveAssets();
  EditorSceneManager.OpenScene("Assets/Scenes/MainPrototype.unity");
  var c=UnityEngine.Object.FindAnyObjectByType<MainPrototype>();
  if(c==null||c.suddenAwakeningProbability!=.25f)throw new Exception("Main scene/config invalid");
  var summary=new {unity=Application.unityVersion,version=PlayerSettings.bundleVersion,scene="Assets/Scenes/MainPrototype.unity",modes=new[]{1,3,5},probability=c.suddenAwakeningProbability,mouseSensitivity=c.inputSensitivity,bodyMesh=GameObject.Find("Prisoner_V2_Integrated").GetComponentsInChildren<MeshFilter>()[0].sharedMesh.name};
  File.WriteAllText(Path.Combine(Root,"reports/pc-config.txt"),summary.ToString());
  var options=new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/MainPrototype.unity"},locationPathName=Path.Combine(Root,"Windows-0.1.0-rc1/GansuuHead.exe"),target=BuildTarget.StandaloneWindows64,options=BuildOptions.None};
  var report=BuildPipeline.BuildPlayer(options);
  File.WriteAllText(Path.Combine(Root,"reports/windows-build.txt"),report.summary.result+"\nErrors="+report.summary.totalErrors+"\nBytes="+report.summary.totalSize+"\nTime="+report.summary.totalTime+"\nDevelopment=false\nTarget=StandaloneWindows64");
  if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Windows build failed");
 }
}
