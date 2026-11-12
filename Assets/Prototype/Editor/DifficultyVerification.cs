using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using GansuuHead;
public static class DifficultyVerification
{
    [Serializable] public class Sample {public int minutes;public float phaseSeconds,rate,idleSeconds;}
    [Serializable] public class Report {public bool success;public int legacyChecks,newChecks;public List<string> passed=new List<string>();public List<Sample> samples=new List<Sample>();public string error;}
    static void Check(bool ok,string name,Report r){if(!ok)throw new Exception(name);r.passed.Add(name);}
    static void Seed(MainPrototype c,int m,float phase,float d)
    {c.StartShift(m);if(d>0)c.Session.Tick(1,0,d,0);c.Session.DeveloperSeek(phase);c.RefreshPresentation(0,true);}
    static float MaxDelta(Vector3[] a,Vector3[] b)
    {float result=0;for(int i=0;i<a.Length;i++)result=Mathf.Max(result,(a[i]-b[i]).magnitude);return result;}
    [MenuItem("Tools/Gansuu Head/Verify Difficulty System")]
    public static void Verify()
    {
        var r=new Report();GameObject temp=null;Mesh bakedA=null,bakedB=null;
        try
        {
            r.legacyChecks=PrototypeVerification.Main().count;
            var scene=UnityEngine.Object.FindAnyObjectByType<MainPrototype>();
            var expr=scene.prisonerExpression;var renderer=expr.face;var mesh=renderer.sharedMesh;
            Check(scene.head==Camera.main.transform,"Camera remains warden's viewpoint",r);
            Check(expr.controller==scene && renderer!=null,"3D expression controller wired",r);
            Check(mesh.blendShapeCount==4 && mesh.vertexCount>1000,"Face is a dense skinned mesh with four shapes",r);
            string[] names={"Smile","WideGrin","CheekRaise","EyesNarrow"};
            for(int i=0;i<4;i++)
            {
                Check(mesh.GetBlendShapeName(i)==names[i],"Blend Shape name "+names[i],r);
                var dv=new Vector3[mesh.vertexCount];mesh.GetBlendShapeFrameVertices(i,0,dv,null,null);
                float moved=0;foreach(var v in dv)moved=Mathf.Max(moved,v.magnitude);
                Check(moved>.002f,"Real vertex deltas "+names[i],r);
            }
            Check(GameObject.Find("Static Prisoner").GetComponentsInChildren<SpriteRenderer>().Length==0,"No texture-swap expression renderers in active prisoner",r);
            Check(GameObject.Find("Foreground Bars")!=null && GameObject.Find("Gripping palm -1")!=null && GameObject.Find("Gripping palm 1")!=null,"Bars and gripping hands preserved",r);
            var skin=renderer.sharedMaterials[0].color;
            Check(Mathf.Abs(skin.r-skin.g)<.001f && Mathf.Abs(skin.g-skin.b)<.001f,"Gray placeholder material preserved",r);
            float[] levels={0,.2f,.4f,.6f,.8f,.9f,1};
            float[,] table={{0,0,0,0},{0,0,0,0},{25,0,15,5},{55,15,40,25},{85,65,75,60},{100,100,100,85},{100,100,100,85}};
            for(int row=0;row<levels.Length;row++)
            {
                expr.SetDrowsiness(levels[row],0,true);
                for(int shape=0;shape<4;shape++)
                    Check(Mathf.Abs(renderer.GetBlendShapeWeight(shape)-table[row,shape])<.01f,"Weight table "+levels[row]+" "+names[shape],r);
            }
            bakedA=new Mesh();bakedB=new Mesh();
            expr.SetDrowsiness(0,0,true);renderer.BakeMesh(bakedA);
            expr.SetDrowsiness(.9f,0,true);renderer.BakeMesh(bakedB);
            Check(MaxDelta(bakedA.vertices,bakedB.vertices)>.02f,"Baked renderer geometry changes from neutral to grin",r);
            for(int i=0;i<4;i++)
                Check(Mathf.Abs(expr.TargetWeight(i,.401f)-expr.TargetWeight(i,.399f))<1,"Continuous expression interpolation "+names[i],r);
            expr.SetDrowsiness(0,0,true);expr.SetDrowsiness(.9f,.03f);
            Check(renderer.GetBlendShapeWeight(0)>0 && renderer.GetBlendShapeWeight(0)<30,"Smile spreads gradually",r);
            expr.SetDrowsiness(.9f,0,true);expr.SetDrowsiness(0,.2f);
            Check(renderer.GetBlendShapeWeight(0)<4,"Awakening returns quickly to neutral",r);
            expr.SetDrowsiness(0,0,true);
            temp=new GameObject("Three-mode verification temporary");var c=temp.AddComponent<MainPrototype>();c.head=temp.transform;
            Check(!ReferenceEquals(c.oneMinute.curve,c.threeMinute.curve) && !ReferenceEquals(c.fiveMinute.curve,c.threeMinute.curve) && !ReferenceEquals(c.oneMinute.curve,c.fiveMinute.curve),"Three independent AnimationCurves",r);
            int[][] modes={new[]{1,5,30},new[]{3,15,90},new[]{5,60,210}};
            float[] idleTargets={6.5f,7,7.5f};
            for(int mode=0;mode<modes.Length;mode++)
            {
                int m=modes[mode][0];var p=c.Profile(m);
                Check(p.relaxedUntilSeconds==modes[mode][1] && p.highFromSeconds==modes[mode][2],"Phase boundaries "+m,r);
                Check(p.initialIdleFailureSeconds==idleTargets[mode],"Inspector idle target "+m,r);
                float last=0;bool monotone=true;
                for(int j=0;j<=200;j++){float rate=c.AccumulationAt(m,m*60*j/200f);monotone &= rate>=last-.00001f;last=rate;}
                Check(monotone && last>c.AccumulationAt(m,0),"Nonlinear pressure rises smoothly "+m,r);
                foreach(float boundary in new[]{p.relaxedUntilSeconds,p.highFromSeconds})
                    Check(Mathf.Abs(c.AccumulationAt(m,boundary-.001f)-c.AccumulationAt(m,boundary+.001f))<.0001f,"Smooth phase join "+m+" "+boundary,r);
                foreach(float phase in new[]{0f,p.relaxedUntilSeconds,(p.relaxedUntilSeconds+p.highFromSeconds)/2,p.highFromSeconds,m*60-12})
                {
                    Seed(c,m,phase,0);float rate=c.CurrentAccumulation;
                    for(int i=0;i<10*120 && c.Session.State==PrototypeSession.Phase.Playing;i++)c.AdvanceGameplay(Vector2.zero,1f/120);
                    float idle=c.Session.Elapsed-phase;r.samples.Add(new Sample{minutes=m,phaseSeconds=phase,rate=rate,idleSeconds=idle});
                    Check(c.Session.State==PrototypeSession.Phase.Failure && idle>2 && idle<=idleTargets[mode]+.02f,"Idle failure phase "+m+" "+phase,r);
                    if(phase==0)Check(Mathf.Abs(idle-idleTargets[mode])<.10f,"Measured opening idle target "+m,r);
                }
            }
            Check(c.AccumulationAt(3,180)>c.AccumulationAt(3,0) && c.threeMinute.lateRateMultiplier==1.2f,"Three-minute curve rises within balanced ceiling",r);
            Check(c.AccumulationAt(1,15)>c.AccumulationAt(3,15),"One-minute challenge ramps faster",r);
            Check(c.AccumulationAt(5,0)<.16f,"Standard mode has gentler start",r);
            c.suddenAwakeningProbability=0;float sensitivity=c.inputSensitivity;
            Seed(c,1,0,.8f);float before=c.Session.Drowsiness,closed=c.EyeClosure;c.AdvanceGameplay(new Vector2(5,0),1f/60);
            Check(c.Session.Drowsiness<before && c.Session.Drowsiness>0,"Normal recovery begins immediately and leaves residue",r);
            Check(c.EyeClosure<closed && c.EyeClosure>0,"First-frame eye opening follows real recovery",r);
            before=c.Session.Drowsiness;c.AdvanceGameplay(new Vector2(-5,0),1f/60);
            Check(before-c.Session.Drowsiness<.02f,"Initial impulse is not repeated every frame",r);
            for(int i=0;i<360;i++)c.AdvanceGameplay(new Vector2(i%2==0?5:-5,0),1f/60);
            Check(c.Session.Drowsiness<.4f,"Additional movement steadily reduces residual drowsiness",r);
            Seed(c,5,0,0);for(int i=0;i<9*60 && c.Session.State==PrototypeSession.Phase.Playing;i++)c.AdvanceGameplay(new Vector2(.1f,.1f),1f/60);
            Check(c.Session.State==PrototypeSession.Phase.Failure && c.Session.Elapsed<8.02f,"Tiny mouse noise cannot sustain recovery",r);
            Seed(c,3,0,.8f);c.AdvanceGameplay(new Vector2(5,0),1f/60);float normalRecovery=c.Session.Drowsiness;
            Seed(c,3,0,.8f);c.AdvanceGameplay(new Vector2(100000,0),1f/60);
            Check(Mathf.Abs(c.Session.Drowsiness-normalRecovery)<.0001f,"Abnormal input spike cannot exceed capped recovery",r);
            Seed(c,5,0,.5f);float mid=c.EyeClosure;Seed(c,5,0,.25f);float low=c.EyeClosure;Seed(c,5,0,.75f);float high=c.EyeClosure;
            Check(low<.1f && mid<.2f && high-mid>mid-low,"Ease-in eyelids accelerate with drowsiness",r);
            Seed(c,5,0,.9f);
            Check(c.EyeClosure<.75f && expr.TargetWeight(1,.9f)==100,"Full grin target reached while eyes still show the face",r);
            expr.SetDrowsiness(.9f,0,true);float weight=renderer.GetBlendShapeWeight(0);c.Session.DeveloperSeek(200);
            expr.SetDrowsiness(c.DrowsinessRatio,0,true);Check(renderer.GetBlendShapeWeight(0)==weight,"Expression depends on drowsiness, not elapsed clock",r);
            expr.SetDrowsiness(0,0,true);
            float[] measured=new float[3];int fi=0;
            foreach(int fps in new[]{30,60,120}){Seed(c,3,0,0);for(int j=0;j<fps*7 && c.Session.State==PrototypeSession.Phase.Playing;j++)c.AdvanceGameplay(Vector2.zero,1f/fps);measured[fi++]=c.Session.Elapsed;}
            Check(Mathf.Abs(measured[0]-measured[2])<.02f,"FPS-independent failure time",r);
            foreach(int m in new[]{1,3,5})
            {
                c.StartShift(m);
                for(int i=0;i<m*60*30+2 && c.Session.State==PrototypeSession.Phase.Playing;i++)
                    c.AdvanceGameplay(i%45<36?new Vector2(i%2==0?10:-10,0):Vector2.zero,1f/30);
                Check(c.Session.State==PrototypeSession.Phase.Success,"Normal burst-and-rest strategy can clear "+m,r);
                float elapsed=c.Session.Elapsed;c.AdvanceGameplay(Vector2.zero,1);
                Check(c.Session.State==PrototypeSession.Phase.Success && c.Session.Elapsed==elapsed,"Success terminal "+m,r);
            }
            c.developerToolsEnabled=true;c.developerClockScale=60;
            foreach(int m in new[]{1,3,5})
            {
                c.StartShift(m);
                for(int i=0;i<m*30+3 && c.Session.State==PrototypeSession.Phase.Playing;i++)
                    c.AdvanceGameplay(i%45<36?new Vector2(i%2==0?10:-10,0):Vector2.zero,1f/30);
                Check(c.Session.State==PrototypeSession.Phase.Success,"DEV x60 crosses late stages "+m,r);
            }
            c.developerToolsEnabled=false;
            Check(c.inputSensitivity==sensitivity,"Difficulty never modifies mouse sensitivity",r);
            Seed(c,1,0,.99f);c.AdvanceGameplay(Vector2.zero,1);Check(c.Session.State==PrototypeSession.Phase.Failure && c.EyeClosure==1 && c.Session.Drowsiness==1,"Failure at one fully closes eyes",r);
            c.StartShift(5);Check(c.EyeClosure==0 && c.Session.Elapsed==0 && c.Session.Drowsiness==0,"Restart resets normalized state",r);
            c.AdvanceGameplay(new Vector2(10000,-10000),.1f);
            var angle=c.head.localEulerAngles;Check(Mathf.Abs(Mathf.DeltaAngle(0,angle.x))<=c.maximumPitch+.001f && Mathf.Abs(Mathf.DeltaAngle(0,angle.y))<=c.maximumYaw+.001f,"Camera rotation limits preserved",r);
            bool rejected=false;try{c.StartShift(30);}catch(ArgumentOutOfRangeException){rejected=true;}Check(rejected,"Thirty minutes remains unsupported",r);
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var durations=(int[])typeof(MainPrototype).GetField("minutes",flags).GetValue(scene);
            Check(durations.Length==3 && durations[0]==1 && durations[1]==3 && durations[2]==5,"Menu contains exactly 1 3 5",r);
            for(int i=0;i<durations.Length;i++){typeof(MainPrototype).GetField("choice",flags).SetValue(c,i);c.Begin();Check(c.Session.Duration==durations[i]*60,"Menu duration "+durations[i],r);}
            r.success=true;
        }
        catch(Exception ex){r.error=ex.ToString();Debug.LogException(ex);}
        finally
        {
            r.newChecks=r.passed.Count;
            if(temp!=null)UnityEngine.Object.DestroyImmediate(temp);
            if(bakedA!=null)UnityEngine.Object.DestroyImmediate(bakedA);if(bakedB!=null)UnityEngine.Object.DestroyImmediate(bakedB);
        }
        File.WriteAllText("AgentScripts/blendshapes-after-results.json",JsonUtility.ToJson(r,true));
        Debug.Log("Three-mode / Blend Shape checks: "+r.success+"; "+r.passed.Count);
    }
    [MenuItem("Tools/Gansuu Head/Preview Awake Face")] public static void PreviewAwake(){Preview(0);}
    [MenuItem("Tools/Gansuu Head/Preview Drowsy Smile")] public static void PreviewSmile(){Preview(.6f);}
    [MenuItem("Tools/Gansuu Head/Preview Almost Closed Grin")] public static void PreviewGrin(){Preview(.9f);}
    static void Preview(float value)
    {
        var c=UnityEngine.Object.FindAnyObjectByType<MainPrototype>();c.StartShift(5);
        if(value>0)c.Session.Tick(1,0,value,0);c.RefreshPresentation(0,true);
        c.prisonerExpression.SetDrowsiness(value,0,true);
    }
}




