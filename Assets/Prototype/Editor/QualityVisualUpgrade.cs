using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using GansuuHead;
public static class QualityVisualUpgrade
{
    [MenuItem("Tools/Gansuu Head/Quality/Phase 3 Apply")]
    public static void Apply()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play first");
        var scene=EditorSceneManager.GetActiveScene();if(scene.path!="Assets/Scenes/MainPrototype.unity")throw new Exception("Open MainPrototype");
        var c=UnityEngine.Object.FindAnyObjectByType<MainPrototype>();
        c.eyelidMask.lateApertureLift=.21f;
        c.prisonerExpression.face.sharedMesh=ProceduralFaceBuilder.CreateAsset();
        EditorUtility.SetDirty(c.eyelidMask);EditorUtility.SetDirty(c.prisonerExpression.face);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
    }
}
