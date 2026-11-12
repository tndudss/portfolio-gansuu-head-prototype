using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using GansuuHead;
public static class CurvedEyelidVerification
{
    [Serializable] public class Awakening {public bool inputSimulated=true;public float before,after,drowsiness;}
    [Serializable] public class Report {public bool success;public List<string> passed=new List<string>();public string error;public float beforeShake,afterShake;}
    static void Check(bool value,string text,Report r){if(!value)throw new Exception(text);r.passed.Add(text);}
    [MenuItem("Tools/Gansuu Head/Verify Curved Eyelids")]
    public static void Verify()
    {
        var r=new Report();GameObject temp=null;
        try
        {
            var scene=UnityEngine.Object.FindAnyObjectByType<MainPrototype>();var mask=scene.eyelidMask;
            Check(mask!=null && mask.controller==scene && mask.materialTemplate!=null,"Mask references connected",r);
            Check(mask.materialTemplate.shader.isSupported && !ShaderUtil.ShaderHasError(mask.materialTemplate.shader),"URP eyelid shader supported without errors",r);
            bool open=true,closed=true;
            for(int x=0;x<=10;x++)for(int y=0;y<=10;y++){var uv=new Vector2(x/10f,y/10f);open &= mask.MaskAt(uv,0)==0;closed &= mask.MaskAt(uv,1)==1;}
            Check(open,"Zero closure has no black border anywhere",r);
            Check(closed,"Failure completely covers the view",r);
            mask.Boundaries(0,.5f,out float top,out float bottom);
            Check(Mathf.Abs((1-top+mask.lateApertureLift*.125f)/(bottom-mask.lateApertureLift*.125f)-65f/35)<.01f,"Upper/lower center movement ratio 65/35",r);
            mask.Boundaries(1,.5f,out float sideTop,out float sideBottom);
            Check(sideTop<top && sideBottom>bottom,"Curved sides close before the center",r);
            Check(mask.MaskAt(new Vector2(.5f,.55f),.75f)==0 && mask.MaskAt(new Vector2(.01f,.55f),.75f)==1,"Deep closure leaves a long central eye opening",r);
            float edge=mask.MaskAt(new Vector2(.5f,top),.5f);
            Check(edge>.45f && edge<.55f,"Boundary feather has partial alpha",r);
            Check(mask.MaskAt(new Vector2(.5f,top-mask.edgeSoftness*2),.5f)==0 && mask.MaskAt(new Vector2(.5f,top+mask.edgeSoftness*2),.5f)==1,"Feather is local, not a screen fade",r);
            temp=new GameObject("Temporary eyelid test");var c=temp.AddComponent<MainPrototype>();c.head=temp.transform;c.StartShift(5);c.Session.Tick(1,0,.9f,0);c.RefreshPresentation(0,true);
            c.suddenAwakeningProbability=0;r.beforeShake=c.EyeClosure;for(int i=0;i<15;i++)c.AdvanceGameplay(new Vector2(5,0),1f/60);r.afterShake=c.EyeClosure;
            Check(r.afterShake<r.beforeShake*.95f && c.Session.Drowsiness>0,"Mouse movement quickly reopens eye while residue remains",r);
            c.Session.Tick(10,0,.2f,0);c.RefreshPresentation(0);
            Check(c.Session.State==PrototypeSession.Phase.Failure && c.EyeClosure==1,"Failure rule still forces full closure",r);
            r.success=true;
        }
        catch(Exception ex){r.error=ex.ToString();Debug.LogException(ex);}
        finally{if(temp!=null)UnityEngine.Object.DestroyImmediate(temp);}
        File.WriteAllText("AgentScripts/curved-eyelid-verification.json",JsonUtility.ToJson(r,true));
    }
    [MenuItem("Tools/Gansuu Head/Eyelid Preview/0 Percent")] public static void Zero(){Preview(0);}
    [MenuItem("Tools/Gansuu Head/Eyelid Preview/35 Percent")] public static void A(){Preview(.35f);}
    [MenuItem("Tools/Gansuu Head/Eyelid Preview/65 Percent")] public static void B(){Preview(.65f);}
    [MenuItem("Tools/Gansuu Head/Eyelid Preview/90 Percent")] public static void C(){Preview(.9f);}
    [MenuItem("Tools/Gansuu Head/Eyelid Preview/100 Percent")] public static void D(){Preview(1);}
    [MenuItem("Tools/Gansuu Head/Eyelid Preview/Shake Awake")] public static void Shake()
    {
        var c=UnityEngine.Object.FindAnyObjectByType<MainPrototype>();
        Preview(.9f);
        float before=c.EyeClosure;
        c.AdvanceGameplay(new Vector2(5,0),1f/60);
        File.WriteAllText("AgentScripts/eyelid-awakening-preview.json",
            JsonUtility.ToJson(new Awakening{before=before,after=c.EyeClosure,drowsiness=c.Session.Drowsiness},true));
    }
    static void Preview(float value)
    {
        if(EditorApplication.isPlaying)EditorApplication.isPaused=true;
        var c=UnityEngine.Object.FindAnyObjectByType<MainPrototype>();c.enabled=true;c.StartShift(5);
        if(value>0)c.Session.Tick(1,0,value,0);c.RefreshPresentation(0,true);
        c.prisonerExpression.SetDrowsiness(value,0,true);
        if(EditorApplication.isPlaying)EditorApplication.delayCall+=EditorApplication.Step;else EditorApplication.QueuePlayerLoopUpdate();
    }
}







