using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using GansuuHead;

public static class FirstPersonVerification
{
    [Serializable] public class Report { public bool success; public int legacyChecks; public int newChecks; public List<string> passed = new List<string>(); public string error; }
    static void Check(bool condition,string name,Report report) { if(!condition) throw new Exception(name); report.passed.Add(name); }
    [MenuItem("Tools/Gansuu Head/Verify First Person Prototype")]
    public static void Verify()
    {
        DifficultyVerification.Verify();
    }

    [MenuItem("Tools/Gansuu Head/Apply Inspector Defaults")]
    public static void ApplyInspectorDefaults()
    {
        if(EditorApplication.isPlaying) throw new Exception("Stop Play mode first.");
        var scene=UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
        if(scene.path!="Assets/Scenes/MainPrototype.unity" || scene.isDirty)
            throw new Exception("Open a saved MainPrototype scene first.");
        var c=UnityEngine.Object.FindAnyObjectByType<MainPrototype>();
        c.failureThreshold=1; c.eyeClosureStrength=1;
        EditorUtility.SetDirty(c);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
    }

    [MenuItem("Tools/Gansuu Head/Preview Half Closed Eyes")]
    public static void PreviewHalf()
    {
        var c=UnityEngine.Object.FindAnyObjectByType<MainPrototype>();
        c.StartShift(3); c.Session.Tick(c.failureThreshold*.5f/c.CurrentAccumulation,0,c.CurrentAccumulation,0); c.RefreshPresentation(0,true);
    }
    [MenuItem("Tools/Gansuu Head/Preview Failed Eyes")]
    public static void PreviewFailed()
    {
        var c=UnityEngine.Object.FindAnyObjectByType<MainPrototype>();
        c.StartShift(3); c.Session.Tick(c.failureThreshold/c.CurrentAccumulation+.1f,0,c.CurrentAccumulation,0); c.RefreshPresentation(0,true);
    }
}




