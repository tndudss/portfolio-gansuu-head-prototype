using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEditor;
using TMPro;
using GansuuHead;
public static partial class BilingualVerification
{
    sealed class ResolutionScope : IDisposable
    {
        const BindingFlags InstanceFlags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        static Type Type(string name)=>AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType(name)).First(t=>t!=null);
        readonly object group;readonly Type sizeType,enumType;readonly EditorWindow view;readonly PropertyInfo selected;
        readonly int originalIndex,originalCount;
        public ResolutionScope()
        {
            var sizesType=Type("UnityEditor.GameViewSizes");var singleton=typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var sizes=singleton.GetProperty("instance",BindingFlags.Static|BindingFlags.Public).GetValue(null);
            var groupEnum=Type("UnityEditor.GameViewSizeGroupType");var groupId=Enum.Parse(groupEnum,"Standalone");
            group=sizesType.GetMethod("GetGroup",InstanceFlags).Invoke(sizes,new[]{groupId});
            enumType=Type("UnityEditor.GameViewSizeType");sizeType=Type("UnityEditor.GameViewSize");
            view=EditorWindow.GetWindow(Type("UnityEditor.GameView"));selected=view.GetType().GetProperty("selectedSizeIndex",InstanceFlags);
            originalIndex=(int)selected.GetValue(view);originalCount=Count("GetCustomCount");
        }
        int Count(string method)=>(int)group.GetType().GetMethod(method,InstanceFlags).Invoke(group,null);
        public void Set(int width,int height)
        {
            var size=Activator.CreateInstance(sizeType,InstanceFlags,null,new object[]{Enum.Parse(enumType,"FixedResolution"),width,height,"Gansuu QA "+width+"x"+height},null);
            int index=Count("GetBuiltinCount")+Count("GetCustomCount");group.GetType().GetMethod("AddCustomSize",InstanceFlags).Invoke(group,new[]{size});selected.SetValue(view,index);view.Repaint();EditorApplication.QueuePlayerLoopUpdate();
        }
        public void Dispose()
        {
            selected.SetValue(view,originalIndex);
            for(int remaining=Count("GetCustomCount")-originalCount;remaining>0;remaining--){int before=Count("GetCustomCount");group.GetType().GetMethod("RemoveCustomSize",InstanceFlags).Invoke(group,new object[]{Count("GetBuiltinCount")+before-1});if(Count("GetCustomCount")>=before)break;}
            view.Repaint();
        }
    }
    [MenuItem("Tools/Gansuu Head/Bilingual/Verify Resolution Matrix")]
    public static async void VerifyResolutions()
    {
        var r=new Report();MainPrototype c=null;ResolutionScope resolution=null;bool background=Application.runInBackground;int rate=Application.targetFrameRate;Locale initial=null;
        try{
            if(!EditorApplication.isPlaying || EditorApplication.isPaused)throw new Exception("Run unpaused Play first");
            Application.runInBackground=true;Application.targetFrameRate=60;c=UnityEngine.Object.FindAnyObjectByType<MainPrototype>();initial=LocalizationSettings.SelectedLocale;
            resolution=new ResolutionScope();int startFrame=Time.frameCount;
            foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(1920,1080),new Vector2Int(1024,768),new Vector2Int(640,480)}){
                resolution.Set(size.x,size.y);await Frames(3);Check(Screen.width==size.x && Screen.height==size.y,"Actual Game View resolution "+size,r);
                foreach(string code in new[]{"ko-KR","en"}){
                    LocalizationSettings.SelectedLocale=LocalizationSettings.AvailableLocales.GetLocale(new LocaleIdentifier(code));((UnityLocalizedTextSource)c.uiText).SetLocale(code);
                    foreach(int minutes in new[]{1,3,5})foreach(int state in new[]{0,1,2,3,4}){
                        State(c,minutes,state);await Frames();c.localizedUi.RefreshNow();Canvas.ForceUpdateCanvases();CheckText(c.localizedUi,code,minutes,state,r);
                        foreach(var text in c.localizedUi.GetComponentsInChildren<TMP_Text>()){
                            if(!text.gameObject.activeInHierarchy || string.IsNullOrEmpty(text.text))continue;
                            var corners=new Vector3[4];text.rectTransform.GetWorldCorners(corners);
                            foreach(var corner in corners){var point=RectTransformUtility.WorldToScreenPoint(null,corner);if(point.x<-.5f || point.y<-.5f || point.x>Screen.width+.5f || point.y>Screen.height+.5f)throw new Exception("Text bounds outside screen: "+size+" / "+code+" / "+text.name);}
                        }
                        r.uiStates++;
                    }
                }
            }
            r.renderedFrames=Time.frameCount-startFrame;r.success=true;
        }catch(Exception ex){r.error=ex.ToString();Debug.LogException(ex);}finally{
            resolution?.Dispose();if(c!=null){if(initial!=null){LocalizationSettings.SelectedLocale=initial;((UnityLocalizedTextSource)c.uiText).SetLocale(initial.Identifier.Code);}State(c,1,0);c.DeveloperReplayInput=false;}
            Application.runInBackground=background;Application.targetFrameRate=rate;File.WriteAllText("AgentScripts/bilingual-resolution-matrix.json",JsonUtility.ToJson(r,true));
        }
    }
    static ResolutionScope previewResolution;
    [MenuItem("Tools/Gansuu Head/Bilingual/Preview 640x480")]
    public static void PreviewSmall(){if(previewResolution==null)previewResolution=new ResolutionScope();previewResolution.Set(640,480);}
    [MenuItem("Tools/Gansuu Head/Bilingual/Restore Preview Resolution")]
    public static void RestoreResolution(){previewResolution?.Dispose();previewResolution=null;}
}
