using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.Localization;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;
public static class PlatformExitBuild
{
 static string Root=>Environment.GetEnvironmentVariable("GANSUU_WORKSPACE_ROOT") ?? Directory.GetParent(Application.dataPath).Parent.Parent.Parent.FullName;
 static void Tables(){
  var collection=LocalizationEditorSettings.GetStringTableCollection("PrototypeUI");
  foreach(string locale in new[]{"en","ko-KR"}){
   var table=(StringTable)collection.GetTable(new LocaleIdentifier(locale));
   var doc=JsonUtility.FromJson<GansuuHead.PrototypeTextCatalog.Document>(File.ReadAllText("Assets/Prototype/Localization/UI."+locale+".json"));
   foreach(var entry in doc.entries)table.AddEntry(entry.key,entry.text);
   EditorUtility.SetDirty(table);
  }
  EditorUtility.SetDirty(collection.SharedData);AssetDatabase.SaveAssets();
 }
 public static void Windows(){Tables();Build(BuildTarget.StandaloneWindows64,"Builds/Windows/GansuuHead_Windows_0.1.1-rc1/GansuuHead.exe");}
 public static void Web(){Tables();Build(BuildTarget.WebGL,"Builds/Web/GansuuHead_Web_0.1.1-rc1_Candidate");}
 static void Build(BuildTarget target,string output){
  if (Directory.Exists(Path.GetDirectoryName(Path.Combine(Root,output)))) throw new IOException("Preserve existing candidate; choose a separate GANSUU_WORKSPACE_ROOT for a rebuild.");
  Directory.CreateDirectory(Path.Combine(Root,"Docs/Reports/Builds"));
  PlayerSettings.bundleVersion=target==BuildTarget.WebGL?"0.1.1-web-rc1":"0.1.1-rc1";
  EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/MainPrototype.unity",true)};AssetDatabase.SaveAssets();
  var r=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/MainPrototype.unity"},locationPathName=Path.Combine(Root,output),target=target,options=BuildOptions.None});
  File.WriteAllText(Path.Combine(Root,"Docs/Reports/Builds/"+(target==BuildTarget.WebGL?"web":"windows")+"-build.txt"),r.summary.result+"\nErrors="+r.summary.totalErrors+"\nTime="+r.summary.totalTime+"\nBytes="+r.summary.totalSize+"\nDevelopment=false");
  if(r.summary.result!=BuildResult.Succeeded)throw new Exception("Exit menu build failed");
 }
}
