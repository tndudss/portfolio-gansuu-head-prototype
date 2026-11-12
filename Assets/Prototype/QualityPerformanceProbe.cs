#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using Unity.Profiling;
using UnityEditor;
namespace GansuuHead
{
    public sealed class QualityPerformanceProbe : MonoBehaviour
    {
        [Serializable] public class Row{public string view;public int frames;public float meanMs,p95Ms,maxMs,fps;public long editorGcBytesPerFrame;}
        [Serializable] public class Report{public string context="Unity Editor Game View, 1280x720, scripted 180 px/s at normal clock. Includes Editor overhead; not a standalone Windows performance guarantee.";public string cpu,gpu;public List<Row> rows=new List<Row>();}
        MainPrototype c;Report report=new Report();readonly List<float> samples=new List<float>(300);ProfilerRecorder gc;int stage,warmup=60;long bytes;
        [MenuItem("Tools/Gansuu Head/Quality/Measure Editor Performance")]
        public static void Begin(){if(!EditorApplication.isPlaying || EditorApplication.isPaused)throw new Exception("Run unpaused Play first");var go=new GameObject("Temporary quality performance probe");go.AddComponent<QualityPerformanceProbe>();}
        void Start(){c=UnityEngine.Object.FindAnyObjectByType<MainPrototype>();c.DeveloperReplayInput=true;c.StartShift(5);report.cpu=SystemInfo.processorType;report.gpu=SystemInfo.graphicsDeviceName;gc=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame",1);}
        void Update()
        {
            float dt=Time.unscaledDeltaTime;if(dt<=0)return;
            c.AdvanceGameplay(new Vector2((Time.frameCount/20)%2==0?180*dt:-180*dt,0),dt);
            float target=stage==0?0:.65f;float difference=target-c.Session.Drowsiness;
            if(difference>0)c.Session.Tick(.0001f,0,difference/.0001f,0);else c.Session.Recover(-difference);
            c.RefreshPresentation(dt);if(warmup-->0)return;
            samples.Add(dt*1000);if(gc.Valid)bytes+=gc.LastValue;
            if(samples.Count<300)return;
            float sum=0,max=0;foreach(float v in samples){sum+=v;max=Mathf.Max(v,max);}samples.Sort();float mean=sum/samples.Count;
            report.rows.Add(new Row{view=stage==0?"open eyelids":"partial eyelids with edge blur",frames=samples.Count,meanMs=mean,p95Ms=samples[(int)(samples.Count*.95f)],maxMs=max,fps=1000/mean,editorGcBytesPerFrame=bytes/samples.Count});
            if(stage++==0){samples.Clear();bytes=0;warmup=60;return;}
            File.WriteAllText("AgentScripts/quality-performance.json",JsonUtility.ToJson(report,true));Destroy(gameObject);
        }
        void OnDestroy(){if(c!=null)c.DeveloperReplayInput=false;gc.Dispose();}
    }
}
#endif
