using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using GansuuHead;

public static class PrototypeSceneUpgrade
{
    const string ScenePath = "Assets/Scenes/MainPrototype.unity";
    static Material skin, cloth, dark, iron, stone, white;
    [MenuItem("Tools/Gansuu Head/Upgrade First Person Scene")]
    public static void Upgrade()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop Play mode first.");
        for (int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)
            if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                throw new Exception("Save pending scene edits first.");
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        foreach(var root in scene.GetRootGameObjects()) UnityEngine.Object.DestroyImmediate(root);
        Directory.CreateDirectory("Assets/Prototype/Materials");
        skin=Mat("Skin",new Color(.6f,.6f,.6f));
        cloth=Mat("Uniform",new Color(.34f,.4f,.43f));
        dark=Mat("Dark",new Color(.025f,.03f,.035f));
        iron=Mat("Iron",new Color(.13f,.17f,.18f));
        stone=Mat("Stone",new Color(.21f,.24f,.25f));
        white=Mat("Stripe",new Color(.7f,.7f,.62f));

        var controller = new GameObject("Prototype Controller").AddComponent<MainPrototype>();
        var camera = new GameObject("Main Camera - Warden Eyes").AddComponent<Camera>();
        camera.tag="MainCamera";
        camera.transform.position=new Vector3(0,1.65f,0);
        camera.fieldOfView=60; camera.nearClipPlane=.05f; camera.farClipPlane=40;
        camera.clearFlags=CameraClearFlags.SolidColor;
        camera.backgroundColor=new Color(.035f,.045f,.05f);
        controller.head=camera.transform;
        // All serialized defaults are explicitly applied to the upgraded scene.
        controller.failureThreshold=1; controller.eyeClosureStrength=1;
        controller.globalDrowsinessMultiplier=1;
        controller.sustainedRecoveryRatio=1.5f;
        controller.inputSensitivity=.035f;
        controller.maximumYaw=8; controller.maximumPitch=6;

        var cell=new GameObject("Cell").transform;
        Part("Back wall",PrimitiveType.Cube,cell,new Vector3(0,1.5f,4.6f),new Vector3(5,3.5f,.2f),stone);
        Part("Floor",PrimitiveType.Cube,cell,new Vector3(0,-.08f,2.5f),new Vector3(5,.16f,5),stone);
        Part("Left wall",PrimitiveType.Cube,cell,new Vector3(-2.5f,1.5f,2.5f),new Vector3(.15f,3.5f,4),stone);
        Part("Right wall",PrimitiveType.Cube,cell,new Vector3(2.5f,1.5f,2.5f),new Vector3(.15f,3.5f,4),stone);

        var bars=new GameObject("Foreground Bars").transform;
        for(int i=-4;i<=4;i++)
            Part("Vertical Bar "+i,PrimitiveType.Cylinder,bars,new Vector3(i*.48f,1.5f,2.65f),new Vector3(.055f,1.5f,.055f),iron);
        Part("Upper rail",PrimitiveType.Cube,bars,new Vector3(0,2.9f,2.65f),new Vector3(4.15f,.12f,.12f),iron);
        Part("Lower rail",PrimitiveType.Cube,bars,new Vector3(0,.35f,2.65f),new Vector3(4.15f,.12f,.12f),iron);

        // Shallow, static primitive silhouette behind separately placed foreground bars.
        var prisoner=new GameObject("Static Prisoner").transform;
        Part("Torso",PrimitiveType.Cube,prisoner,new Vector3(0,1.12f,3.15f),new Vector3(.85f,.95f,.38f),cloth);
        for(int i=0;i<4;i++)
            Part("Uniform stripe "+i,PrimitiveType.Cube,prisoner,new Vector3(0,.77f+i*.21f,2.948f),new Vector3(.86f,.08f,.025f),dark);
        Part("Neck",PrimitiveType.Cylinder,prisoner,new Vector3(0,1.66f,3.1f),new Vector3(.22f,.12f,.22f),skin);
        Part("Face",PrimitiveType.Sphere,prisoner,new Vector3(0,1.98f,3.07f),new Vector3(.58f,.7f,.4f),skin);
        Part("Hair",PrimitiveType.Sphere,prisoner,new Vector3(0,2.22f,3.1f),new Vector3(.6f,.27f,.42f),dark);
        for(int sign=-1;sign<=1;sign+=2)
        {
            Part("Eye white "+sign,PrimitiveType.Sphere,prisoner,new Vector3(sign*.12f,2.02f,2.872f),new Vector3(.1f,.065f,.025f),white);
            Part("Pupil "+sign,PrimitiveType.Sphere,prisoner,new Vector3(sign*.12f,2.02f,2.853f),new Vector3(.038f,.045f,.02f),dark);
            Part("Brow "+sign,PrimitiveType.Cube,prisoner,new Vector3(sign*.12f,2.1f,2.864f),new Vector3(.14f,.035f,.03f),dark);
            Limb("Upper arm "+sign,prisoner,new Vector3(sign*.36f,1.45f,3.13f),new Vector3(sign*.67f,1.25f,2.99f),.21f,cloth);
            Limb("Forearm "+sign,prisoner,new Vector3(sign*.67f,1.25f,2.99f),new Vector3(sign*.48f,1.72f,2.66f),.16f,skin);
            Part("Gripping palm "+sign,PrimitiveType.Cube,prisoner,new Vector3(sign*.48f,1.72f,2.7f),new Vector3(.18f,.23f,.15f),skin);
            for(int finger=0;finger<4;finger++)
                Part("Wrapped finger "+sign+" "+finger,PrimitiveType.Cube,prisoner,new Vector3(sign*.48f,1.64f+finger*.052f,2.603f),new Vector3(.14f,.036f,.07f),skin);
            Part("Thumb "+sign,PrimitiveType.Sphere,prisoner,new Vector3(sign*.57f,1.76f,2.64f),new Vector3(.07f,.14f,.09f),skin);
            Part("Leg "+sign,PrimitiveType.Cube,prisoner,new Vector3(sign*.23f,.34f,3.15f),new Vector3(.33f,.65f,.35f),cloth);
        }
        Part("Nose",PrimitiveType.Sphere,prisoner,new Vector3(0,1.95f,2.85f),new Vector3(.09f,.14f,.12f),skin);
        Part("Mouth",PrimitiveType.Cube,prisoner,new Vector3(0,1.81f,2.89f),new Vector3(.17f,.025f,.025f),dark);
        DifficultySceneUpgrade.Configure(controller);
        EyelidSceneUpgrade.Configure(controller);
        var light=new GameObject("Cell Key Light").AddComponent<Light>();
        light.type=LightType.Directional; light.intensity=1.8f;
        light.transform.rotation=Quaternion.Euler(35,-25,0);
        RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight=new Color(.4f,.43f,.46f);
        EditorSceneManager.MarkSceneDirty(scene);
        if(!EditorSceneManager.SaveScene(scene,ScenePath)) throw new Exception("Save failed");
        AssetDatabase.SaveAssets();
        Selection.activeGameObject=controller.gameObject;
        EditorApplication.ExecuteMenuItem("Window/General/Game");
        File.WriteAllText("AgentScripts/upgrade-result.json","{\"saved\":true,\"firstPerson\":true}");
    }
    static Material Mat(string name, Color color)
    {
        string path="Assets/Prototype/Materials/"+name+".mat";
        var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null) {m=new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m,path);}
        m.color=color; EditorUtility.SetDirty(m); return m;
    }
    static void Part(string name,PrimitiveType type,Transform parent,Vector3 pos,Vector3 scale,Material mat)
    {
        var obj=GameObject.CreatePrimitive(type);obj.name=name;obj.transform.SetParent(parent,false);
        obj.transform.localPosition=pos;obj.transform.localScale=scale;
        obj.GetComponent<Renderer>().sharedMaterial=mat;
        UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());
    }
    static void Limb(string name,Transform parent,Vector3 a,Vector3 b,float width,Material mat)
    {
        var obj=GameObject.CreatePrimitive(PrimitiveType.Cylinder); obj.name=name; obj.transform.SetParent(parent,false);
        obj.transform.position=(a+b)*.5f;obj.transform.up=(b-a).normalized;
        obj.transform.localScale=new Vector3(width,Vector3.Distance(a,b)*.5f,width);
        obj.GetComponent<Renderer>().sharedMaterial=mat;UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());
    }
}






