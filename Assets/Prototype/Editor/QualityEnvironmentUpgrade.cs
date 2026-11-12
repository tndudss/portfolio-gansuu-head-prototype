using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using GansuuHead;
public static class QualityEnvironmentUpgrade
{
    const string Root="Assets/Prototype/";
    static Material Mat(string name)=>AssetDatabase.LoadAssetAtPath<Material>(Root+"Materials/"+name+".mat");
    static Mesh RoundedBox()
    {
        var vertices=new System.Collections.Generic.List<Vector3>();var triangles=new System.Collections.Generic.List<int>();
        Vector3[] normals={Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back};
        foreach(var n in normals){var u=Mathf.Abs(n.y)>.5f?Vector3.right:Vector3.up;var v=Vector3.Cross(n,u);int offset=vertices.Count;
            for(int y=0;y<=8;y++)for(int x=0;x<=8;x++){
                var p=n*.5f+u*(x/8f-.5f)+v*(y/8f-.5f);
                var inner=new Vector3(Mathf.Clamp(p.x,-.42f,.42f),Mathf.Clamp(p.y,-.42f,.42f),Mathf.Clamp(p.z,-.42f,.42f));
                vertices.Add(inner+(p-inner).normalized*.08f);
            }
            for(int y=0;y<8;y++)for(int x=0;x<8;x++){int a=offset+y*9+x;triangles.AddRange(new[]{a,a+1,a+9,a+1,a+10,a+9});}
        }
        var mesh=new Mesh{name="Rounded prototype box"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
        string path=Root+"Models/RoundedBox.asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(old!=null){EditorUtility.CopySerialized(mesh,old);UnityEngine.Object.DestroyImmediate(mesh);return old;}
        AssetDatabase.CreateAsset(mesh,path);return mesh;
    }
    static void StoneTexture()
    {
        Directory.CreateDirectory(Root+"Textures");var texture=new Texture2D(256,256,TextureFormat.RGB24,false);var random=new System.Random(701);
        for(int y=0;y<256;y++)for(int x=0;x<256;x++){
            int row=y/64;int xx=(x+(row%2)*64)%128;bool seam=y%64<3 || xx<3;
            float value=seam?.24f:.60f+(float)random.NextDouble()*.055f+.025f*Mathf.PerlinNoise(x*.07f,y*.07f);
            texture.SetPixel(x,y,new Color(value*.94f,value*.98f,value));
        }
        texture.Apply();string path=Root+"Textures/CellStone.png";File.WriteAllBytes(path,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path);var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.maxTextureSize=256;importer.mipmapEnabled=true;importer.wrapMode=TextureWrapMode.Repeat;importer.SaveAndReimport();
        var stone=Mat("Stone");stone.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);stone.mainTextureScale=new Vector2(3,3);stone.color=new Color(.48f,.52f,.55f);stone.SetFloat("_Smoothness",.08f);EditorUtility.SetDirty(stone);
    }
    static AudioClip Sound(string name,float seconds,int kind)
    {
        const int rate=22050;int count=Mathf.RoundToInt(rate*seconds);var random=new System.Random(123+kind);float filtered=0;
        Directory.CreateDirectory(Root+"Audio");string path=Root+"Audio/"+name+".wav";
        using(var stream=File.Create(path))using(var writer=new BinaryWriter(stream)){
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+count*2);writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(rate);writer.Write(rate*2);writer.Write((short)2);writer.Write((short)16);writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(count*2);
            for(int i=0;i<count;i++){
                float t=i/(float)rate,u=t/seconds;filtered=Mathf.Lerp(filtered,(float)random.NextDouble()*2-1,.08f);
                float sample;
                if(kind==0){float seam=Mathf.Sin(Mathf.PI*u);sample=(filtered*.16f+.025f*Mathf.Sin(t*2*Mathf.PI*53))*seam*seam;}
                else {float envelope=Mathf.Sin(Mathf.PI*Mathf.Min(1,u*8))*Mathf.Pow(1-u,2);if(u>=.125f)envelope=Mathf.Pow(1-u,2);
                    envelope*=Mathf.Min(1,u*60)*Mathf.Min(1,(1-u)*60);
                    float frequency=kind==1?280:kind==2?650:kind==3?440+u*220:220-u*100;
                    sample=envelope*(filtered*(kind<=2?.25f:.035f)+Mathf.Sin(t*2*Mathf.PI*frequency)*(kind<=2?.035f:.13f));
                }
                writer.Write((short)Mathf.RoundToInt(Mathf.Clamp(sample,-.9f,.9f)*32767));
            }
        }
        AssetDatabase.ImportAsset(path);var importer=(AudioImporter)AssetImporter.GetAtPath(path);var settings=importer.defaultSampleSettings;settings.loadType=AudioClipLoadType.DecompressOnLoad;settings.compressionFormat=AudioCompressionFormat.PCM;importer.defaultSampleSettings=settings;importer.forceToMono=true;importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }
    [MenuItem("Tools/Gansuu Head/Quality/Phase 4 Apply")]
    public static void Apply()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play first");var scene=EditorSceneManager.GetActiveScene();if(scene.path!="Assets/Scenes/MainPrototype.unity")throw new Exception("Open MainPrototype");
        var c=UnityEngine.Object.FindAnyObjectByType<MainPrototype>();StoneTexture();var rounded=RoundedBox();
        var center=GameObject.Find("Vertical Bar 0");if(center!=null)center.GetComponent<Renderer>().enabled=false;
        foreach(var r in UnityEngine.Object.FindObjectsByType<MeshRenderer>()){
            if(r.transform.IsChildOf(c.prisonerExpression.transform.parent) && (r.name=="Torso" || r.name.StartsWith("Uniform stripe") || r.name.StartsWith("Gripping palm") || r.name.StartsWith("Wrapped finger") || r.name.StartsWith("Leg "))){var f=r.GetComponent<MeshFilter>();if(f!=null)f.sharedMesh=rounded;}
        }
        foreach(string name in new[]{"Skin","Lips","Uniform","Dark","Stripe","Iron"}){
            var m=Mat(name);m.SetFloat("_Smoothness",name=="Iron"?.32f:.12f);m.SetFloat("_Metallic",name=="Iron"?.65f:0);m.enableInstancing=true;EditorUtility.SetDirty(m);
        }
        var hair=GameObject.Find("Hair");if(hair!=null)hair.transform.localScale=new Vector3(.57f,.18f,.40f);
        var key=GameObject.Find("Cell Key Light").GetComponent<Light>();key.intensity=1.25f;key.color=new Color(.78f,.86f,1);key.shadows=LightShadows.Soft;key.shadowStrength=.6f;
        var fill=GameObject.Find("Quiet Face Fill");if(fill==null)fill=new GameObject("Quiet Face Fill");var lamp=fill.GetComponent<Light>();if(lamp==null)lamp=fill.AddComponent<Light>();
        lamp.type=LightType.Point;lamp.range=7;lamp.intensity=2.2f;lamp.color=new Color(.88f,.92f,1);lamp.shadows=LightShadows.None;fill.transform.position=new Vector3(-.6f,2.5f,1.2f);
        RenderSettings.ambientLight=new Color(.24f,.28f,.33f);
        if(c.head.GetComponent<AudioListener>()==null)c.head.gameObject.AddComponent<AudioListener>();
        var audio=c.GetComponent<PrototypeAudio>();if(audio==null)audio=c.gameObject.AddComponent<PrototypeAudio>();audio.controller=c;
        audio.ambienceClip=Sound("CellAmbience",12,0);audio.normalClip=Sound("NormalAwakening",.12f,1);audio.suddenClip=Sound("SuddenAwakening",.25f,2);audio.successClip=Sound("ShiftComplete",.65f,3);audio.failureClip=Sound("FellAsleep",.7f,4);
        EditorUtility.SetDirty(audio);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        File.WriteAllText("AgentScripts/quality-environment.json","Applied procedural stone, rounded boxes, face fill, five original PCM clips. Camera, grip transforms, and timing rules preserved.");
    }
}


