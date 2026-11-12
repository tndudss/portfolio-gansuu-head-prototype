using UnityEngine;
namespace GansuuHead
{
    public sealed class PrisonerExpression : MonoBehaviour
    {
        public MainPrototype controller;
        public SkinnedMeshRenderer face;
        [Min(.01f)] public float smileResponseSeconds=.18f;
        [Min(.01f)] public float recoveryResponseSeconds=.06f;
        public AnimationCurve smile=Mapping(0,25,55,85,100);
        public AnimationCurve wideGrin=Mapping(0,0,15,65,100);
        public AnimationCurve cheekRaise=Mapping(0,15,40,75,100);
        public AnimationCurve eyesNarrow=Mapping(0,5,25,60,85);
        static AnimationCurve Mapping(float a,float b,float c,float d,float e) =>
            new AnimationCurve(new Keyframe(0,0,0,0),new Keyframe(.2f,a,0,0),
                new Keyframe(.4f,b,0,0),new Keyframe(.6f,c,0,0),new Keyframe(.8f,d,0,0),
                new Keyframe(.9f,e,0,0),new Keyframe(1,e,0,0));
        public float TargetWeight(int shape,float drowsiness)
        {
            AnimationCurve curve=shape==0?smile:shape==1?wideGrin:shape==2?cheekRaise:eyesNarrow;
            return Mathf.Clamp(curve.Evaluate(Mathf.Clamp01(drowsiness)),0,100);
        }
        void LateUpdate()
        {
            SetDrowsiness(controller==null?0:controller.DrowsinessRatio,Time.unscaledDeltaTime);
        }
        public void SetDrowsiness(float value,float dt,bool immediate=false)
        {
            if(face==null || face.sharedMesh==null) return;
            for(int i=0;i<4;i++)
            {
                float target=TargetWeight(i,value),current=face.GetBlendShapeWeight(i);
                float seconds=target>current?smileResponseSeconds:recoveryResponseSeconds;
                if(target<current && controller!=null && controller.SuddenAwakeningActive)seconds*=.55f;
                float next=immediate?target:Mathf.Lerp(current,target,1-Mathf.Exp(-Mathf.Max(0,dt)/Mathf.Max(.01f,seconds)));
                face.SetBlendShapeWeight(i,next);
            }
        }
    }
}


