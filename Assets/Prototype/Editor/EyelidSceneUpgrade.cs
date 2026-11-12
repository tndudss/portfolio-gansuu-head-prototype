using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using GansuuHead;
public static class EyelidSceneUpgrade
{
    [MenuItem("Tools/Gansuu Head/Apply Curved Eyelids")]
    public static void Apply()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play first.");
        var scene=EditorSceneManager.GetActiveScene();
        if(scene.path!="Assets/Scenes/MainPrototype.unity")throw new Exception("Open MainPrototype first.");
        Configure(UnityEngine.Object.FindAnyObjectByType<MainPrototype>());
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
    }
    public static void Configure(MainPrototype c)
    {
        const string path="Assets/Prototype/Materials/CurvedEyelids.mat";
        var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat==null){mat=new Material(Shader.Find("GansuuHead/CurvedEyelidUI"));AssetDatabase.CreateAsset(mat,path);}
        var mask=c.GetComponent<CurvedEyelidMask>();if(mask==null)mask=c.gameObject.AddComponent<CurvedEyelidMask>();
        mask.controller=c;mask.materialTemplate=mat;c.eyelidMask=mask;
        EditorUtility.SetDirty(c);EditorUtility.SetDirty(mask);
    }
}

