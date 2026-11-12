using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class QualityStaticBatching
{
    static void Combine(Transform root,string assetName)
    {
        // Idempotent: original transforms remain for grip layout; only the renderers are replaced.
        if(root.GetComponent<MeshFilter>()!=null)return;
        var groups=new Dictionary<Material,List<CombineInstance>>();var renderers=new List<MeshRenderer>();
        foreach(var r in root.GetComponentsInChildren<MeshRenderer>()){
            var f=r.GetComponent<MeshFilter>();if(!r.enabled || f==null || f.sharedMesh==null)continue;
            var materials=r.sharedMaterials;
            for(int s=0;s<f.sharedMesh.subMeshCount;s++){
                var mat=materials[Mathf.Min(s,materials.Length-1)];if(!groups.ContainsKey(mat))groups[mat]=new List<CombineInstance>();
                groups[mat].Add(new CombineInstance{mesh=f.sharedMesh,subMeshIndex=s,transform=root.worldToLocalMatrix*f.transform.localToWorldMatrix});
            }
            renderers.Add(r);
        }
        var parts=new List<CombineInstance>();var mats=new List<Material>();var temporary=new List<Mesh>();
        foreach(var pair in groups){var mesh=new Mesh();mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;mesh.CombineMeshes(pair.Value.ToArray(),true,true);temporary.Add(mesh);parts.Add(new CombineInstance{mesh=mesh,transform=Matrix4x4.identity});mats.Add(pair.Key);}
        var combined=new Mesh{name=assetName,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};combined.CombineMeshes(parts.ToArray(),false,false);combined.RecalculateBounds();
        string path="Assets/Prototype/Models/"+assetName+".asset";AssetDatabase.CreateAsset(combined,path);
        root.gameObject.AddComponent<MeshFilter>().sharedMesh=combined;root.gameObject.AddComponent<MeshRenderer>().sharedMaterials=mats.ToArray();
        foreach(var r in renderers)r.enabled=false;foreach(var mesh in temporary)UnityEngine.Object.DestroyImmediate(mesh);
    }
    [MenuItem("Tools/Gansuu Head/Quality/Combine Static Renderers")]
    public static void Apply()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play first");var scene=EditorSceneManager.GetActiveScene();if(scene.path!="Assets/Scenes/MainPrototype.unity")throw new Exception("Open MainPrototype");
        if(GameObject.Find("Ceiling")==null){
            var roof=GameObject.CreatePrimitive(PrimitiveType.Cube);roof.name="Ceiling";roof.transform.SetParent(GameObject.Find("Cell").transform,false);
            roof.transform.localPosition=new Vector3(0,3.3f,2.5f);roof.transform.localScale=new Vector3(5,.15f,5);
            roof.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Prototype/Materials/Stone.mat");UnityEngine.Object.DestroyImmediate(roof.GetComponent<Collider>());
        }
        Combine(GameObject.Find("Foreground Bars").transform,"PrisonBars");Combine(GameObject.Find("Static Prisoner").transform,"PrisonerStaticBody");
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
    }
}

