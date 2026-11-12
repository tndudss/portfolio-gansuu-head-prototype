using UnityEngine;
using UnityEngine.UI;
using TMPro;
namespace GansuuHead
{
    // Reuses the authored buttons, fonts and panel colours; no scene replacement.
    public sealed class PlatformExitMenu : MonoBehaviour
    {
        PrototypeLocalizedUI ui;
        Button menuQuit,pauseQuit,mainMenu,confirm,cancel;
        GameObject modal;
        TMP_Text question;
        int revision=-1;
        public static bool SupportsQuit {
            get {
#if UNITY_WEBGL
                return false;
#else
                return Application.platform==RuntimePlatform.WindowsPlayer || Application.platform==RuntimePlatform.WindowsEditor;
#endif
            }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install(){var ui=FindAnyObjectByType<PrototypeLocalizedUI>();if(ui!=null)ui.gameObject.AddComponent<PlatformExitMenu>();}
        Button Clone(Button source,Transform parent,string name,Vector2 pos,Vector2 size,UnityEngine.Events.UnityAction action)
        {
            var b=Instantiate(source,parent);b.name=name;b.onClick=new Button.ButtonClickedEvent();b.onClick.AddListener(action);
            b.interactable=true;var r=(RectTransform)b.transform;r.anchorMin=r.anchorMax=new Vector2(.5f,0);r.pivot=new Vector2(.5f,0);r.anchoredPosition=pos;r.sizeDelta=size;
            b.gameObject.SetActive(true);return b;
        }
        void Awake()
        {
            ui=GetComponent<PrototypeLocalizedUI>();
            var p=(RectTransform)ui.pauseRoot.transform;
            p.sizeDelta=new Vector2(p.sizeDelta.x,SupportsQuit?230:174);
            ((RectTransform)ui.resumeButton.transform).anchoredPosition=new Vector2(0,SupportsQuit?124:70);
            mainMenu=Clone(ui.resumeButton,p,"Return To Main Menu",new Vector2(0,SupportsQuit?70:16),new Vector2(180,44),()=>{ui.controller.ReturnToMainMenu();ui.RefreshNow();});
            if(!SupportsQuit)return;
            var start=(RectTransform)ui.startButton.transform;start.anchoredPosition=new Vector2(-95,20);start.sizeDelta=new Vector2(170,48);
            menuQuit=Clone(ui.startButton,ui.menuRoot.transform,"Quit Game",new Vector2(95,20),new Vector2(170,48),Open);
            pauseQuit=Clone(ui.resumeButton,p,"Quit Game",new Vector2(0,16),new Vector2(180,44),Open);
            modal=new GameObject("Quit Confirmation",typeof(RectTransform),typeof(Image));modal.transform.SetParent(ui.canvas.transform,false);
            var full=(RectTransform)modal.transform;full.anchorMin=Vector2.zero;full.anchorMax=Vector2.one;full.offsetMin=full.offsetMax=Vector2.zero;
            modal.GetComponent<Image>().color=new Color(0,0,0,.72f);
            var card=new GameObject("Panel",typeof(RectTransform),typeof(Image));card.transform.SetParent(full,false);
            var cr=(RectTransform)card.transform;cr.anchorMin=cr.anchorMax=cr.pivot=new Vector2(.5f,.5f);cr.sizeDelta=new Vector2(440,210);card.GetComponent<Image>().color=ui.pauseRoot.GetComponent<Image>().color;
            question=Instantiate(ui.paused,cr);question.name="Quit Question";question.fontSize=24;question.enableAutoSizing=true;question.fontSizeMin=20;question.fontSizeMax=24;
            var qr=question.rectTransform;qr.anchorMin=qr.anchorMax=qr.pivot=new Vector2(.5f,1);qr.anchoredPosition=new Vector2(0,-20);qr.sizeDelta=new Vector2(400,90);
            confirm=Clone(ui.startButton,cr,"Confirm Quit",new Vector2(-90,24),new Vector2(160,44),Confirm);
            cancel=Clone(ui.startButton,cr,"Cancel Quit",new Vector2(90,24),new Vector2(160,44),Cancel);
            modal.SetActive(false);
        }
        void Update()
        {
            if(!ui.Ready||ui.controller.uiText==null)return;
            int value=ui.controller.uiText.Revision;if(value==revision)return;revision=value;
            Set(mainMenu,PrototypeTextKeys.MainMenu);
            if(!SupportsQuit)return;
            Set(menuQuit,PrototypeTextKeys.Quit);Set(pauseQuit,PrototypeTextKeys.Quit);
            question.text=ui.controller.uiText.Get(PrototypeTextKeys.QuitQuestion);
            Set(confirm,PrototypeTextKeys.Confirm);Set(cancel,PrototypeTextKeys.Cancel);
        }
        void Set(Button b,string key){b.GetComponentInChildren<TMP_Text>(true).text=ui.controller.uiText.Get(key);}
        public void Open(){if(!SupportsQuit)return;ui.controller.SetQuitConfirmation(true);ui.RefreshNow();modal.SetActive(true);modal.transform.SetAsLastSibling();}
        public void Cancel(){if(modal==null)return;modal.SetActive(false);ui.controller.SetQuitConfirmation(false);ui.RefreshNow();}
        public void Confirm()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            Application.Quit();
#elif UNITY_EDITOR
            Debug.Log("Quit confirmation accepted (Editor keeps running).");
#endif
        }
    }
}
