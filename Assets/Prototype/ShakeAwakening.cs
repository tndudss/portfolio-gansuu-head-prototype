using UnityEngine;
namespace GansuuHead
{
    // One random trial per completed stroke, never per render frame.
    public sealed class ShakeAwakening
    {
        System.Random random;
        Vector2 direction;
        float distance, quiet, sinceEvent=100, sinceActivation=100;
        bool strokeConsumed;
        public int Events {get;private set;}
        public int Trials {get;private set;}
        public int Activations {get;private set;}
        public float Strength {get;private set;}
        public float Age {get;private set;}=100;
        public void Reset(int seed)
        {
            random=new System.Random(seed);direction=Vector2.zero;distance=quiet=0;
            sinceEvent=sinceActivation=Age=100;strokeConsumed=false;Events=Trials=Activations=0;Strength=0;
        }
        public bool Step(Vector2 delta,float dt,float strokePixels,float probability,float cooldown,float variation)
        {
            Age+=dt;sinceEvent+=dt;sinceActivation+=dt;
            if(delta==Vector2.zero)
            {
                quiet+=dt;
                if(quiet>=.2f){direction=Vector2.zero;distance=0;strokeConsumed=false;}
                return false;
            }
            quiet=0;var incoming=delta.normalized;
            if(direction==Vector2.zero || Vector2.Dot(direction,incoming)<-.3f)
            {direction=incoming;distance=0;strokeConsumed=false;}
            distance+=Mathf.Max(0,Vector2.Dot(delta,direction));
            if(strokeConsumed || distance<Mathf.Max(8,strokePixels) || sinceEvent<.12f)return false;
            strokeConsumed=true;sinceEvent=0;Events++;
            if(sinceActivation<Mathf.Max(.25f,cooldown))return false;
            Trials++;
            if(random==null)random=new System.Random(1);
            if(random.NextDouble()>=Mathf.Clamp01(probability))return false;
            Strength=1+(float)(random.NextDouble()*2-1)*Mathf.Clamp(variation,0,.15f);
            Age=dt;sinceActivation=0;Activations++;return true;
        }
    }
}


