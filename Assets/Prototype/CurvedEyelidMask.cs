using UnityEngine;
using UnityEngine.Rendering.Universal;
namespace GansuuHead
{
    [DisallowMultipleComponent]
    public sealed class CurvedEyelidMask : MonoBehaviour
    {
        public MainPrototype controller;
        public Material materialTemplate;
        public UnityEngine.UI.RawImage uiOverlay;
        [Range(0,4)] public float curvature=1.7f;
        [Range(0,1)] public float upperMovement=.65f;
        [Range(0,1)] public float lowerMovement=.35f;
        [Range(.001f,.04f)] public float edgeSoftness=.008f;
        [Range(.5f,1.5f)] public float openingWidth=1;
        [Range(0,.01f)] public float tremorStrength=.0015f;
        [Range(0,.4f)] public float peripheralBlurStrength=.18f;
        [Tooltip("Lifts the last narrow opening toward the prisoner mouth; independent of the 65/35 lid shape.")]
        [Range(0,.3f)] public float lateApertureLift=.21f;
        Material runtimeMaterial;
        UniversalAdditionalCameraData cameraData;
        CameraOverrideOption originalColorOption;
        bool colorOptionCaptured;
        public float UpperShare => upperMovement+lowerMovement>0 ? upperMovement/(upperMovement+lowerMovement) : .65f;
        void LateUpdate(){Sync();}
        void EnsureResources()
        {
            if(runtimeMaterial==null && materialTemplate!=null)
                runtimeMaterial=new Material(materialTemplate){hideFlags=HideFlags.HideAndDontSave};
            if(cameraData==null && controller!=null && controller.head!=null)
            {
                var camera=controller.head.GetComponent<Camera>();
                if(camera!=null)
                {
                    cameraData=camera.GetUniversalAdditionalCameraData();
                    originalColorOption=cameraData.requiresColorOption;colorOptionCaptured=true;
                }
            }
        }
        void Sync()
        {
            EnsureResources();
            float closure=controller==null?0:controller.EyeClosure;
            bool needsColor=closure>.00001f && closure<.999999f && peripheralBlurStrength>0;
            if(cameraData!=null && colorOptionCaptured)
                cameraData.requiresColorOption=needsColor?CameraOverrideOption.On:originalColorOption;
            if(runtimeMaterial==null)return;
            if(uiOverlay!=null){uiOverlay.material=runtimeMaterial;uiOverlay.enabled=closure>.00001f;}
            float deep=controller==null?0:Mathf.SmoothStep(0,1,Mathf.Clamp01((controller.DrowsinessRatio-.7f)/.3f));
            float tremor=(Mathf.Sin(Time.unscaledTime*43)+.35f*Mathf.Sin(Time.unscaledTime*67))*
                tremorStrength*deep*(1-closure);
            runtimeMaterial.SetFloat("_LateLift",lateApertureLift);
            runtimeMaterial.SetFloat("_Closure",closure);
            runtimeMaterial.SetFloat("_Curvature",curvature);
            runtimeMaterial.SetFloat("_UpperShare",UpperShare);
            runtimeMaterial.SetFloat("_Feather",edgeSoftness);
            runtimeMaterial.SetFloat("_OpeningWidth",openingWidth);
            runtimeMaterial.SetFloat("_Tremor",tremor);
            runtimeMaterial.SetFloat("_BlurStrength",cameraData==null?0:peripheralBlurStrength);
        }
        public void DrawGUI()
        {
            if(uiOverlay!=null)return;
            if(controller==null || controller.EyeClosure<=.00001f || Event.current.type!=EventType.Repaint)return;
            Sync();
            if(runtimeMaterial==null)return;

            // Draw before the existing IMGUI HUD/result controls, preserving their readability.
            GL.PushMatrix();
            GL.LoadPixelMatrix(0,Screen.width,Screen.height,0);
            Graphics.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture,runtimeMaterial);
            GL.PopMatrix();
        }
        public void Boundaries(float x,float closure,out float top,out float bottom)
        {
            float c=Mathf.Clamp01(closure),bow=Mathf.Max(0,curvature)*c*Mathf.Pow(x/Mathf.Max(.25f,openingWidth),2);
            float lift=lateApertureLift*c*c*c;
            top=1-UpperShare*(c+bow)+lift;bottom=(1-UpperShare)*(c+bow)+lift;
        }
        public float MaskAt(Vector2 uv,float closure)
        {
            if(closure<=.00001f)return 0;if(closure>=.999999f)return 1;
            Boundaries(uv.x*2-1,closure,out float top,out float bottom);
            float distance=Mathf.Min(top-uv.y,uv.y-bottom),f=Mathf.Max(.0005f,edgeSoftness);
            float t=Mathf.Clamp01((distance+f)/(2*f));
            return 1-t*t*(3-2*t);
        }
        void OnDisable()
        {
            if(uiOverlay!=null)uiOverlay.enabled=false;
            if(cameraData!=null && colorOptionCaptured)cameraData.requiresColorOption=originalColorOption;
        }
        void OnDestroy()
        {
            OnDisable();
            if(runtimeMaterial!=null)Destroy(runtimeMaterial);
        }
    }
}




