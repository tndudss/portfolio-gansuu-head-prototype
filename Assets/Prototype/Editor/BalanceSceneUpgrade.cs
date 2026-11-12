using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using GansuuHead;
public static class BalanceSceneUpgrade
{
    [MenuItem("Tools/Gansuu Head/Balance/Apply Balanced Defaults")]
    public static void Apply()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play first.");
        var scene=EditorSceneManager.GetActiveScene();
        if(scene.path!="Assets/Scenes/MainPrototype.unity")throw new Exception("Open MainPrototype.");
        var c=UnityEngine.Object.FindAnyObjectByType<MainPrototype>();
        Configure(c);EditorUtility.SetDirty(c);EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
    }
    public static void Configure(MainPrototype c)
    {
        c.oneMinute=ModeDifficulty.Create(1);c.threeMinute=ModeDifficulty.Create(3);
        c.fiveMinute=ModeDifficulty.Create(5);
        c.globalDrowsinessMultiplier=1;c.maximumAccumulationPerSecond=.4f;
        c.sustainedRecoveryRatio=1.5f;c.fullShakePixelsPerSecond=180;
        c.continuousRecoveryDecay=.35f;c.minimumContinuousRecovery=.9f;
    }
}

