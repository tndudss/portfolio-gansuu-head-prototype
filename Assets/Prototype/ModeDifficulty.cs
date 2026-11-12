using System;
using UnityEngine;
namespace GansuuHead
{
    [Serializable]
    public sealed class ModeDifficulty
    {
        [Tooltip("X = normalized play progress (0-1); Y = direct difficulty multiplier.")]
        public AnimationCurve curve;
        [Min(.5f)] public float initialIdleFailureSeconds;
        [Tooltip("Hard ceiling for this mode's curve multiplier.")]
        [Min(1)] public float lateRateMultiplier;
        [Min(0)] public float accumulationMultiplier=1;
        [Min(0)] public float recoveryMultiplier=1;
        [Min(0)] public float relaxedUntilSeconds,highFromSeconds;
        public float EvaluateMultiplier(float progress)
        {
            float cap=Mathf.Max(1,lateRateMultiplier);
            if(curve==null || curve.length==0)return 1;
            progress=Mathf.Clamp01(progress);
            float value=curve.Evaluate(progress);
            // Bound custom tangents to adjacent key values as well as the mode ceiling.
            for(int i=1;i<curve.length;i++)
                if(progress<=curve[i].time && progress>=curve[i-1].time)
                {
                    value=Mathf.Clamp(value,Mathf.Min(curve[i-1].value,curve[i].value),Mathf.Max(curve[i-1].value,curve[i].value));
                    break;
                }
            return Mathf.Clamp(value,1,cap);
        }
        public static ModeDifficulty Create(int minutes)
        {
            float easy,high,idle,cap,q1,q2,q3;
            switch(minutes)
            {
                case 1:easy=5;high=30;idle=6.5f;cap=1.24f;q1=1.05f;q2=1.12f;q3=1.18f;break;
                case 3:easy=15;high=90;idle=7;cap=1.20f;q1=1.04f;q2=1.09f;q3=1.14f;break;
                case 5:easy=60;high=210;idle=7.5f;cap=1.18f;q1=1.03f;q2=1.07f;q3=1.12f;break;
                default:throw new ArgumentOutOfRangeException(nameof(minutes));
            }
            return new ModeDifficulty {
                initialIdleFailureSeconds=idle,lateRateMultiplier=cap,
                relaxedUntilSeconds=easy,highFromSeconds=high,
                curve=new AnimationCurve(new Keyframe(0,1,0,0),new Keyframe(.25f,q1,0,0),
                    new Keyframe(.5f,q2,0,0),new Keyframe(.75f,q3,0,0),new Keyframe(1,cap,0,0))
            };
        }
    }
}

