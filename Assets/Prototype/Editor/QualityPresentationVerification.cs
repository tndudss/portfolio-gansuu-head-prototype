using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using GansuuHead;
public static class QualityPresentationVerification
{
    [Serializable] public class Report{public bool success;public List<string> passed=new List<string>();public string error;public int vertices,triangles;public string[] blendShapes;}
    static void Check(bool value,string name,Report r){if(!value)throw new Exception(name);r.passed.Add(name);}
    [MenuItem("Tools/Gansuu Head/Quality/Verify Presentation")]
    public static void Verify()
    {
        var r=new Report();Mesh baked=null;
        try{
            var c=UnityEngine.Object.FindAnyObjectByType<MainPrototype>();var face=c.prisonerExpression.face;var mesh=face.sharedMesh;
            r.vertices=mesh.vertexCount;r.triangles=mesh.triangles.Length/3;r.blendShapes=new string[4];for(int i=0;i<4;i++)r.blendShapes[i]=mesh.GetBlendShapeName(i);
            Check(c.head.localPosition==new Vector3(0,1.65f,0) && Quaternion.Angle(c.head.localRotation,Quaternion.identity)<.01f,"Authored first person camera preserved",r);
            Check(!GameObject.Find("Vertical Bar 0").GetComponent<Renderer>().enabled,"Center face is unobstructed; side bars retained",r);
            foreach(int sign in new[]{-1,1})Check(Vector3.Distance(GameObject.Find("Gripping palm "+sign).transform.position,new Vector3(sign*.48f,1.72f,2.7f))<.001f,"Grip position preserved "+sign,r);
            Check(GameObject.Find("Torso").GetComponent<MeshFilter>().sharedMesh.name=="Rounded prototype box","Rounded torso geometry applied",r);
            baked=new Mesh();bool finite=true;var previous=new float[4];for(int i=0;i<4;i++)previous[i]=face.GetBlendShapeWeight(i);
            for(int step=0;step<=100;step++){
                c.prisonerExpression.SetDrowsiness(step/100f,0,true);face.BakeMesh(baked);
                foreach(var v in baked.vertices)finite &= !float.IsNaN(v.x) && !float.IsInfinity(v.y) && Mathf.Abs(v.x)<1 && Mathf.Abs(v.y)<1 && Mathf.Abs(v.z)<1;
            }
            for(int i=0;i<4;i++)face.SetBlendShapeWeight(i,previous[i]);Check(finite,"101 combined expression poses have finite bounded real geometry",r);
            var audio=c.GetComponent<PrototypeAudio>();Check(audio!=null && c.head.GetComponent<AudioListener>()!=null,"Audio controller and camera listener connected",r);
            foreach(var clip in new[]{audio.ambienceClip,audio.normalClip,audio.suddenClip,audio.successClip,audio.failureClip}){
                Check(clip!=null && clip.channels==1 && clip.frequency==22050,"Original mono PCM clip connected "+(clip==null?"missing":clip.name),r);
                var samples=new float[clip.samples];Check(clip.GetData(samples,0),"Audio PCM readable "+clip.name,r);float peak=0;foreach(float sample in samples)peak=Mathf.Max(peak,Mathf.Abs(sample));
                Check(peak>0 && peak<.9f,"No digital clipping "+clip.name,r);
            }
            c.eyelidMask.Boundaries(0,.9f*.9f*.9f,out float top,out float bottom);
            Vector3 mouth=Camera.main.WorldToViewportPoint(face.transform.TransformPoint(new Vector3(0,-.12f,-.22f)));
            Check(mouth.y>bottom+c.eyelidMask.edgeSoftness && mouth.y<top-c.eyelidMask.edgeSoftness,"At 90 percent drowsiness smiling mouth lies inside aperture",r);
            Check(!ShaderUtil.ShaderHasError(c.eyelidMask.materialTemplate.shader),"URP shader has no compile errors",r);
            r.success=true;
        }catch(Exception ex){r.error=ex.ToString();Debug.LogException(ex);}finally{if(baked!=null)UnityEngine.Object.DestroyImmediate(baked);}
        File.WriteAllText("AgentScripts/quality-presentation.json",JsonUtility.ToJson(r,true));
    }
}
