using UnityEngine;
namespace GansuuHead
{
    [DisallowMultipleComponent]
    public sealed class PrototypeAudio : MonoBehaviour
    {
        public MainPrototype controller;
        public AudioClip ambienceClip,normalClip,suddenClip,successClip,failureClip;
        [Range(0,1)] public float masterVolume=.55f;
        [Range(0,.2f)] public float ambienceVolume=.045f;
        [Min(.5f)] public float normalSoundInterval=1.4f;
        AudioSource ambience,normal,sudden,result;
        PrototypeSession.Phase lastPhase;
        int lastSerial;float nextNormal;
        public int NormalSounds {get;private set;}
        public int SuddenSounds {get;private set;}
        AudioSource Source(bool loop)
        {
            var source=gameObject.AddComponent<AudioSource>();source.playOnAwake=false;source.loop=loop;
            source.spatialBlend=0;source.dopplerLevel=0;return source;
        }
        void Awake()
        {
            ambience=Source(true);normal=Source(false);sudden=Source(false);result=Source(false);
            ambience.clip=ambienceClip;if(ambienceClip!=null)ambience.Play();
        }
        void Update()
        {
            if(controller==null)return;
            var phase=controller.Session.State;bool playing=phase==PrototypeSession.Phase.Playing && !controller.IsInputSuspended && controller.enabled;
            float d=controller.DrowsinessRatio;
            ambience.volume=masterVolume*ambienceVolume*(playing?1-.28f*d:.75f);
            ambience.pitch=playing?Mathf.Lerp(1,.94f,d):1;
            normal.volume=masterVolume*.10f;result.volume=masterVolume*.22f;
            if(phase!=lastPhase)
            {
                if(phase==PrototypeSession.Phase.Success && successClip!=null)result.PlayOneShot(successClip);
                if(phase==PrototypeSession.Phase.Failure && failureClip!=null)result.PlayOneShot(failureClip);
                if(phase==PrototypeSession.Phase.Playing){lastSerial=0;nextNormal=Time.unscaledTime+.2f;}
                lastPhase=phase;
            }
            if(!playing)return;
            if(controller.AwakeningSerial!=lastSerial)
            {
                lastSerial=controller.AwakeningSerial;
                sudden.pitch=Mathf.Lerp(.97f,1.03f,Mathf.InverseLerp(.85f,1.15f,controller.LastAwakeningStrength));
                sudden.volume=masterVolume*.20f*controller.LastAwakeningStrength;
                if(suddenClip!=null){sudden.PlayOneShot(suddenClip);SuddenSounds++;}
                nextNormal=Time.unscaledTime+normalSoundInterval;
            }
            else if(controller.NormalRecoveryActive && d>.05f && Time.unscaledTime>=nextNormal)
            {
                if(normalClip!=null){normal.PlayOneShot(normalClip);NormalSounds++;}
                nextNormal=Time.unscaledTime+Mathf.Max(.5f,normalSoundInterval);
            }
        }
        void OnDisable(){if(ambience!=null)ambience.Stop();if(normal!=null)normal.Stop();if(sudden!=null)sudden.Stop();if(result!=null)result.Stop();}
        void OnEnable(){if(ambience!=null && !ambience.isPlaying)ambience.Play();}
    }
}
