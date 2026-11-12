using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using GansuuHead;
public static class DifficultySceneUpgrade
{
    [MenuItem("Tools/Gansuu Head/Apply Difficulty Upgrade")]
    public static void Apply()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play mode first.");
        var scene=EditorSceneManager.GetActiveScene();
        if(scene.path!="Assets/Scenes/MainPrototype.unity")throw new Exception("Open saved MainPrototype first.");
        Configure(UnityEngine.Object.FindAnyObjectByType<MainPrototype>());
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
    }
    public static void Configure(MainPrototype c)
    {
        c.failureThreshold=1;c.globalDrowsinessMultiplier=1;c.maximumAccumulationPerSecond=.4f;
        c.sustainedRecoveryRatio=1.5f;c.initialAwakeningRecovery=.38f;
        BalanceSceneUpgrade.Configure(c);
        c.oneMinute=ModeDifficulty.Create(1);c.threeMinute=ModeDifficulty.Create(3);
        c.fiveMinute=ModeDifficulty.Create(5);
        c.eyelidClosingCurve=new AnimationCurve(new Keyframe(0,0,0,0),new Keyframe(.5f,.125f,.75f,.75f),new Keyframe(1,1,3,3));
        c.eyelidOpeningCurve=new AnimationCurve(new Keyframe(0,0,3,3),new Keyframe(1,1,0,0));c.eyeOpeningSeconds=.08f;
        var old=GameObject.Find("Prisoner Expressions");if(old!=null)UnityEngine.Object.DestroyImmediate(old);
        foreach(string name in new[]{"Face","Nose","Mouth","Eye white -1","Eye white 1","Pupil -1","Pupil 1","Brow -1","Brow 1"})
        {
            var obj=GameObject.Find(name);if(obj!=null && obj.GetComponent<Renderer>()!=null)obj.GetComponent<Renderer>().enabled=false;
        }
        var root=GameObject.Find("Prisoner Face 3D");if(root==null)root=new GameObject("Prisoner Face 3D");
        root.transform.SetParent(GameObject.Find("Static Prisoner").transform,false);
        root.transform.localPosition=new Vector3(0,1.98f,3.07f);root.transform.localRotation=Quaternion.identity;root.transform.localScale=Vector3.one;
        var face=root.GetComponent<SkinnedMeshRenderer>();if(face==null)face=root.AddComponent<SkinnedMeshRenderer>();
        face.sharedMesh=ProceduralFaceBuilder.CreateAsset();
        face.bones=new[]{root.transform};face.rootBone=root.transform;
        face.localBounds=new Bounds(Vector3.zero,new Vector3(.85f,.95f,.7f));
        face.sharedMaterials=new[]{
            AssetDatabase.LoadAssetAtPath<Material>("Assets/Prototype/Materials/Skin.mat"),
            AssetDatabase.LoadAssetAtPath<Material>("Assets/Prototype/Materials/Dark.mat"),
            AssetDatabase.LoadAssetAtPath<Material>("Assets/Prototype/Materials/Stripe.mat"),
            LipMaterial()
        };
        var expr=root.GetComponent<PrisonerExpression>();if(expr==null)expr=root.AddComponent<PrisonerExpression>();
        expr.controller=c;expr.face=face;c.prisonerExpression=expr;expr.SetDrowsiness(0,0,true);
        EditorUtility.SetDirty(c);EditorUtility.SetDirty(expr);EditorUtility.SetDirty(face);
    }
    static Material LipMaterial()
    {
        const string path="Assets/Prototype/Materials/Lips.mat";
        var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.color=new Color(.38f,.38f,.38f);AssetDatabase.CreateAsset(m,path);}
        return m;
    }
}



