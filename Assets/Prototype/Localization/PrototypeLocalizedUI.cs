using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace GansuuHead
{
    public sealed class PrototypeLocalizedUI : MonoBehaviour
    {
        public MainPrototype controller;
        public PrototypeLocaleController locale;
        public Canvas canvas;
        public GameObject menuRoot,hudRoot,pauseRoot;
        public TMP_Text title,instructions,choose,result,language,remaining,drowsiness,paused,developer;
        public Button[] durationButtons;
        public Button startButton,resumeButton,koreanButton,englishButton;
        public RectTransform drowsinessFill;
        int revision=-1,lastMinutes=-1;PrototypeSession.Phase lastState=(PrototypeSession.Phase)(-1);
        float nextHud;string lastCode;
        public bool Ready=>locale!=null && locale.IsReady;
        void Awake()
        {
            for(int i=0;i<durationButtons.Length;i++){
                int value=controller.SupportedMinutes[i];durationButtons[i].onClick.AddListener(()=>{controller.SelectMinutes(value);RefreshNow();});
            }
            startButton.onClick.AddListener(()=>{controller.Begin();RefreshNow();});
            resumeButton.onClick.AddListener(()=>{controller.ResumeShift();RefreshNow();});
            koreanButton.onClick.AddListener(()=>SelectLanguage("ko-KR"));englishButton.onClick.AddListener(()=>SelectLanguage("en"));
        }
        public void SelectLanguage(string code){locale.SelectLanguage(code);RefreshNow();}
        void Update(){RefreshNow();}
        string T(string key)=>controller.uiText==null?key:controller.uiText.Get(key);
        string F(string key,params object[] args)=>controller.uiText==null?key:controller.uiText.Format(key,args);
        static TMP_Text Label(Button button)=>button.GetComponentInChildren<TMP_Text>(true);
        static void Mark(Button button,bool selected)
        {
            button.GetComponent<Image>().color=selected?new Color(.18f,.42f,.39f,.96f):new Color(.12f,.17f,.19f,.93f);
            var outline=button.GetComponent<Outline>();if(outline!=null)outline.enabled=selected;
        }
        public void RefreshNow()
        {
            if(canvas==null || controller==null)return;
            canvas.enabled=Ready;if(!Ready)return;
            var state=controller.Session.State;bool menu=state!=PrototypeSession.Phase.Playing;
            menuRoot.SetActive(menu);hudRoot.SetActive(!menu);pauseRoot.SetActive(!menu && controller.IsInputSuspended);
            int currentRevision=controller.uiText==null?0:controller.uiText.Revision;
            bool changed=revision!=currentRevision || lastState!=state;
            if(changed){
                title.text=T(PrototypeTextKeys.Title);instructions.text=T(PrototypeTextKeys.Instructions);
                choose.text=T(PrototypeTextKeys.ChooseDuration);language.text=T(PrototypeTextKeys.Language);
                paused.text=T(PrototypeTextKeys.Paused);Label(resumeButton).text=T(PrototypeTextKeys.Resume);
                result.gameObject.SetActive(state!=PrototypeSession.Phase.Ready);
                result.text=state==PrototypeSession.Phase.Success?T(PrototypeTextKeys.Success):T(PrototypeTextKeys.Failure);
                Label(startButton).text=T(state==PrototypeSession.Phase.Ready?PrototypeTextKeys.Start:PrototypeTextKeys.Retry);
                Label(koreanButton).text=T(PrototypeTextKeys.Korean);Label(englishButton).text=T(PrototypeTextKeys.English);
                for(int i=0;i<durationButtons.Length;i++)Label(durationButtons[i]).text=F(PrototypeTextKeys.Duration,controller.SupportedMinutes[i]);
                revision=currentRevision;lastState=state;nextHud=0;
            }
            if(lastMinutes!=controller.SelectedMinutes || lastCode!=locale.CurrentCode || changed){
                for(int i=0;i<durationButtons.Length;i++)Mark(durationButtons[i],controller.SelectedMinutes==controller.SupportedMinutes[i]);
                bool ko=locale.CurrentCode=="ko-KR";Mark(koreanButton,ko);Mark(englishButton,!ko);
                koreanButton.interactable=!ko;englishButton.interactable=ko;
                lastMinutes=controller.SelectedMinutes;lastCode=locale.CurrentCode;
            }
            if(Time.unscaledTime>=nextHud){
                nextHud=Time.unscaledTime+.1f;int seconds=Mathf.CeilToInt(Mathf.Max(0,controller.Session.Duration-controller.Session.Elapsed));
                remaining.text=F(PrototypeTextKeys.Remaining,seconds/60,seconds%60);
                drowsiness.text=F(PrototypeTextKeys.Drowsiness,controller.DrowsinessRatio*100);
                drowsinessFill.anchorMax=new Vector2(controller.DrowsinessRatio,1);
                drowsinessFill.GetComponent<Image>().color=Color.Lerp(new Color(.33f,.52f,.53f),new Color(.62f,.32f,.28f),controller.DrowsinessRatio);
#if UNITY_EDITOR || DEBUG
                developer.gameObject.SetActive(controller.developerToolsEnabled);
                if(controller.developerToolsEnabled)developer.text=F(PrototypeTextKeys.DeveloperStatus,controller.ClockScale,controller.CurrentAccumulation);
#else
                developer.gameObject.SetActive(false);
#endif
            }
        }
    }
}
