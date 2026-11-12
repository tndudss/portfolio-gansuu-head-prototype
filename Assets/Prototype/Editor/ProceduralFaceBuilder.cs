using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
public static class ProceduralFaceBuilder
{
    sealed class Geometry
    {
        public List<Vector3> v=new List<Vector3>();
        public List<int>[] triangles={new List<int>(),new List<int>(),new List<int>(),new List<int>()};
        public void Surface(int rows,int columns,int material,Func<float,float,Vector3> point)
        {
            int offset=v.Count;
            for(int r=0;r<=rows;r++)for(int c=0;c<=columns;c++)v.Add(point(c/(float)columns,r/(float)rows));
            for(int r=0;r<rows;r++)for(int c=0;c<columns;c++)
            {
                int a=offset+r*(columns+1)+c,b=a+1,d=a+columns+1;
                triangles[material].AddRange(new[]{a,b,d,b,d+1,d});
            }
        }
        public Mesh Mesh()
        {
            var m=new Mesh{name="Procedural Prisoner Face"};
            m.SetVertices(v);m.subMeshCount=4;
            for(int i=0;i<4;i++)m.SetTriangles(triangles[i],i);
            m.RecalculateNormals();m.RecalculateBounds();return m;
        }
    }
    static float Gaussian(float x,float y,float cx,float cy,float wx,float wy) =>
        Mathf.Exp(-Mathf.Pow((x-cx)/wx,2)-Mathf.Pow((y-cy)/wy,2));
    static float Front(float x,float y)
    {
        float z=-.20f*Mathf.Sqrt(Mathf.Max(.01f,1-x*x/(.29f*.29f)-y*y/(.35f*.35f)));
        return z-.045f*Gaussian(x,y,0,-.005f,.048f,.065f);
    }
    static Vector3 Mouth(float u,float v,float smile,float grin,float offset)
    {
        float x=(.083f+.006f*smile+.038f*grin)*u;
        float y=-.15f+(.006f+.024f*grin)*v+(.030f*smile+.014f*grin)*u*u;
        return new Vector3(x,y,Front(x,y)-offset);
    }
    static Geometry Build(float smile,float grin,float cheek,float narrow)
    {
        var g=new Geometry();
        g.Surface(48,96,0,(u,v)=>{
            float theta=v*Mathf.PI,phi=u*Mathf.PI*2;
            var n=new Vector3(Mathf.Sin(theta)*Mathf.Cos(phi),Mathf.Cos(theta),Mathf.Sin(theta)*Mathf.Sin(phi));
            float jaw=Mathf.Lerp(.78f,1,Mathf.SmoothStep(0,1,(n.y+.95f)/.8f));
            float x=.29f*n.x*jaw,y=.35f*n.y,z=.20f*n.z;
            float front=Mathf.Pow(Mathf.Max(0,-n.z),6);
            z-=.045f*Gaussian(x,y,0,-.005f,.048f,.065f)*front;
            float cheeks=Gaussian(x,y,.145f,-.065f,.09f,.09f)+Gaussian(x,y,-.145f,-.065f,.09f,.09f);
            z-=(.006f+.012f*cheek)*cheeks*front;y+=.018f*cheek*cheeks*front;
            float corners=Gaussian(x,y,.10f,-.14f,.07f,.065f)+Gaussian(x,y,-.10f,-.14f,.07f,.065f);
            y+=(.016f*smile+.006f*grin)*corners*front;
            x+=Mathf.Sign(x)*(.004f*smile+.007f*grin)*corners*front;
            y-=.008f*grin*Gaussian(x,y,0,-.25f,.15f,.09f)*front;
            float eyes=Gaussian(x,y,.115f,.055f,.07f,.055f)+Gaussian(x,y,-.115f,.055f,.07f,.055f);
            y+=(.055f-y)*.16f*narrow*eyes*front;
            return new Vector3(x,y,z);
        });
        for(int side=-1;side<=1;side+=2)
        {
            int sign=side;float cx=sign*.115f,cy=.055f+.005f*cheek;
            float cz=Front(cx,.055f)-.013f;
            g.Surface(16,32,2,(u,v)=>{
                float t=v*Mathf.PI,p=u*Mathf.PI*2;
                return new Vector3(cx+.052f*Mathf.Sin(t)*Mathf.Cos(p),cy+.033f*(1-.55f*narrow)*Mathf.Cos(t),cz+.018f*Mathf.Sin(t)*Mathf.Sin(p));
            });
            g.Surface(12,24,1,(u,v)=>{
                float t=v*Mathf.PI,p=u*Mathf.PI*2;
                return new Vector3(cx+.018f*Mathf.Sin(t)*Mathf.Cos(p),cy+.022f*(1-.55f*narrow)*Mathf.Cos(t),cz-.018f+.008f*Mathf.Sin(t)*Mathf.Sin(p));
            });
            g.Surface(2,24,1,(u,v)=>{
                float x=cx+(u-.5f)*.125f,y=.119f+.008f*Mathf.Sin(u*Mathf.PI)+.004f*cheek+(.5f-v)*.013f;
                return new Vector3(x,y,Front(x,y)-.014f);
            });
        }
        // Mouth cavity, protruding lip ring, and teeth all deform as geometry.
        g.Surface(12,64,1,(u,v)=>{
            float p=u*Mathf.PI*2;return Mouth(Mathf.Cos(p)*v,Mathf.Sin(p)*v,smile,grin,.014f);
        });
        g.Surface(3,64,3,(u,v)=>{
            float p=u*Mathf.PI*2,r=1+v*.14f;
            return Mouth(Mathf.Cos(p)*r,Mathf.Sin(p)*r,smile,grin,.018f+.004f*Mathf.Sin(v*Mathf.PI));
        });
        g.Surface(3,40,2,(u,v)=>{
            float x=(u-.5f)*1.65f;
            var point=Mouth(x,0,smile,grin,.020f);
            point.y+=grin*(.003f+.008f*(1-v))*Mathf.Sqrt(Mathf.Max(0,1-x*x));
            point.z=Front(point.x,point.y)-.026f;
            return point;
        });
        return g;
    }
    public static Mesh CreateAsset()
    {
        const string path="Assets/Prototype/Models/PrisonerFace.asset";
        System.IO.Directory.CreateDirectory("Assets/Prototype/Models");
        var neutral=Build(0,0,0,0).Mesh();
        Vector3[] baseVertices=neutral.vertices,baseNormals=neutral.normals;
        string[] names={"Smile","WideGrin","CheekRaise","EyesNarrow"};
        for(int shape=0;shape<4;shape++)
        {
            var posed=Build(shape==0?1:0,shape==1?1:0,shape==2?1:0,shape==3?1:0).Mesh();
            var delta=new Vector3[baseVertices.Length];var normals=new Vector3[delta.Length];
            var pv=posed.vertices;var pn=posed.normals;
            for(int i=0;i<delta.Length;i++){delta[i]=pv[i]-baseVertices[i];normals[i]=pn[i]-baseNormals[i];}
            neutral.AddBlendShapeFrame(names[shape],100,delta,normals,null);
            UnityEngine.Object.DestroyImmediate(posed);
        }
        var weights=new BoneWeight[baseVertices.Length];
        for(int i=0;i<weights.Length;i++)weights[i]=new BoneWeight{boneIndex0=0,weight0=1};
        neutral.boneWeights=weights;neutral.bindposes=new[]{Matrix4x4.identity};
        var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(existing==null){AssetDatabase.CreateAsset(neutral,path);existing=neutral;}
        else {EditorUtility.CopySerialized(neutral,existing);UnityEngine.Object.DestroyImmediate(neutral);EditorUtility.SetDirty(existing);}
        return existing;
    }
}




