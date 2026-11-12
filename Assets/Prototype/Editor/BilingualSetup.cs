using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Localization;
using UnityEngine.Localization.Metadata;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.TextCore.LowLevel;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEditor.PackageManager;
using TMPro;
using GansuuHead;
public static class BilingualSetup
{
    const string Root="Assets/Prototype/Localization/";
    static TMP_FontAsset font;
    [MenuItem("Tools/Gansuu Head/Bilingual/Import TMP Essentials")]
    public static void ImportTMP()
    {
        if(AssetDatabase.FindAssets("t:TMP_Settings").Length>0)return;
        var package=UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(TMP_Text).Assembly);
        AssetDatabase.ImportPackage(package.resolvedPath+"/Package Resources/TMP Essential Resources.unitypackage",false);
    }
    [MenuItem("Tools/Gansuu Head/Bilingual/Refine Text Bounds")]
    public static void RefineBounds()
    {
        var scene=EditorSceneManager.GetActiveScene();if(EditorApplication.isPlaying || scene.path!="Assets/Scenes/MainPrototype.unity")throw new Exception("Open MainPrototype in Edit Mode");
        var ui=UnityEngine.Object.FindAnyObjectByType<MainPrototype>().localizedUi;
        ui.title.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,52);
        ui.paused.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,52);
        ui.developer.rectTransform.anchorMin=new Vector2(0,1);ui.developer.rectTransform.anchorMax=new Vector2(1,1);ui.developer.rectTransform.sizeDelta=new Vector2(-40,40);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
    }
    static Locale LocaleAsset(string code)
    {
        string path=Root+"Locales/"+code+".asset";var locale=AssetDatabase.LoadAssetAtPath<Locale>(path);
        if(locale==null){locale=Locale.CreateLocale(code);AssetDatabase.CreateAsset(locale,path);}
        LocalizationEditorSettings.AddLocale(locale);return locale;
    }
    static void Fill(StringTable table,string file)
    {
        var document=JsonUtility.FromJson<PrototypeTextCatalog.Document>(File.ReadAllText(Root+file));
        foreach(var entry in document.entries)table.AddEntry(entry.key,entry.text);
        EditorUtility.SetDirty(table);EditorUtility.SetDirty(table.SharedData);
    }
    static TMP_FontAsset FontAsset()
    {
        string path=Root+"Fonts/PrototypeKoreanSDF.asset";var result=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if(result==null){
            var source=AssetDatabase.LoadAssetAtPath<Font>(Root+"Fonts/NotoSansCJKkr-Regular.otf");
            result=TMP_FontAsset.CreateFontAsset(source,48,6,GlyphRenderMode.SDFAA,1024,1024,AtlasPopulationMode.Dynamic,true);
            if(result==null)throw new Exception("Korean font creation failed");
            result.name="Prototype Noto CJK Korean SDF";AssetDatabase.CreateAsset(result,path);
            AssetDatabase.AddObjectToAsset(result.material,result);
            foreach(var atlas in result.atlasTextures)AssetDatabase.AddObjectToAsset(atlas,result);
        }
        var chars=new StringBuilder();for(int c=32;c<127;c++)chars.Append((char)c);
        foreach(string file in new[]{"UI.en.json","UI.ko-KR.json"}){
            var doc=JsonUtility.FromJson<PrototypeTextCatalog.Document>(File.ReadAllText(Root+file));foreach(var entry in doc.entries)chars.Append(entry.text);
        }
        if(!result.TryAddCharacters(chars.ToString(),out string missing,true) && !string.IsNullOrEmpty(missing))throw new Exception("Missing font characters: "+missing);
        EditorUtility.SetDirty(result);EditorUtility.SetDirty(result.material);foreach(var atlas in result.atlasTextures){if(!AssetDatabase.Contains(atlas))AssetDatabase.AddObjectToAsset(atlas,result);EditorUtility.SetDirty(atlas);}
        return result;
    }
    static RectTransform Rect(Transform parent,string name,Vector2 anchor,Vector2 pivot,Vector2 position,Vector2 size)
    {
        var go=new GameObject(name,typeof(RectTransform));var rect=go.GetComponent<RectTransform>();rect.SetParent(parent,false);rect.anchorMin=rect.anchorMax=anchor;rect.pivot=pivot;rect.anchoredPosition=position;rect.sizeDelta=size;return rect;
    }
    static RectTransform Stretch(Transform parent,string name)
    {
        var rect=Rect(parent,name,Vector2.zero,Vector2.zero,Vector2.zero,Vector2.zero);rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;return rect;
    }
    static TMP_Text Text(RectTransform rect,float size)
    {
        var t=rect.gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.fontSize=size;t.alignment=TextAlignmentOptions.Center;t.color=new Color(.92f,.94f,.94f);t.raycastTarget=false;t.richText=false;
        t.textWrappingMode=TextWrappingModes.Normal;t.overflowMode=TextOverflowModes.Overflow;t.margin=new Vector4(5,0,5,0);t.text=string.Empty;return t;
    }
    static TMP_Text Label(Transform parent,string name,Vector2 anchor,Vector2 pivot,Vector2 position,Vector2 size,float points)
        =>Text(Rect(parent,name,anchor,pivot,position,size),points);
    static Button Button(Transform parent,string name,Vector2 anchor,Vector2 pivot,Vector2 position,Vector2 size)
    {
        var rect=Rect(parent,name,anchor,pivot,position,size);var image=rect.gameObject.AddComponent<Image>();image.color=new Color(.12f,.17f,.19f,.93f);
        var outline=rect.gameObject.AddComponent<Outline>();outline.effectColor=new Color(.42f,.76f,.68f,.9f);outline.effectDistance=new Vector2(1,-1);outline.enabled=false;
        var b=rect.gameObject.AddComponent<Button>();b.targetGraphic=image;b.navigation=new Navigation{mode=Navigation.Mode.None};
        var colors=b.colors;colors.disabledColor=Color.white;colors.highlightedColor=new Color(.87f,.96f,.94f);b.colors=colors;
        var text=Text(Stretch(rect,"Text"),17);text.enableAutoSizing=true;text.fontSizeMin=14;text.fontSizeMax=17;return b;
    }
    [MenuItem("Tools/Gansuu Head/Bilingual/Create Tables And TMP UI")]
    public static void Apply()
    {
        var scene=EditorSceneManager.GetActiveScene();if(EditorApplication.isPlaying || scene.path!="Assets/Scenes/MainPrototype.unity" || scene.isDirty)throw new Exception("Open a saved MainPrototype in Edit Mode");
        foreach(string dir in new[]{"Locales","Tables","Resources"})Directory.CreateDirectory(Root+dir);AssetDatabase.Refresh();
        var settings=AssetDatabase.LoadAssetAtPath<LocalizationSettings>(Root+"LocalizationSettings.asset");
        if(settings==null){settings=ScriptableObject.CreateInstance<LocalizationSettings>();AssetDatabase.CreateAsset(settings,Root+"LocalizationSettings.asset");}
        LocalizationEditorSettings.ActiveLocalizationSettings=settings;
        var en=LocaleAsset("en");var ko=LocaleAsset("ko-KR");
        settings.GetAvailableLocales().AddLocale(en);settings.GetAvailableLocales().AddLocale(ko);
        if(ko.Metadata.GetMetadata<FallbackLocale>()==null)ko.Metadata.AddMetadata(new FallbackLocale(en));
        LocalizationSettings.ProjectLocale=en;
        settings.GetStartupLocaleSelectors().Clear();settings.GetStartupLocaleSelectors().Add(new GuardStartupLocaleSelector());settings.GetStartupLocaleSelectors().Add(new SpecificLocaleSelector{LocaleId=new LocaleIdentifier("en")});
        LocalizationSettings.InitializeSynchronously=false;
        var tables=LocalizationEditorSettings.GetStringTableCollection("PrototypeUI")??LocalizationEditorSettings.CreateStringTableCollection("PrototypeUI",Root+"Tables",new[]{en,ko});
        var english=(StringTable)tables.GetTable(new LocaleIdentifier("en"));var korean=(StringTable)tables.GetTable(new LocaleIdentifier("ko-KR"));Fill(english,"UI.en.json");Fill(korean,"UI.ko-KR.json");tables.SetPreloadTableFlag(true);
        var source=AssetDatabase.LoadAssetAtPath<UnityLocalizedTextSource>(Root+"UnityUI.asset");if(source==null){source=ScriptableObject.CreateInstance<UnityLocalizedTextSource>();AssetDatabase.CreateAsset(source,Root+"UnityUI.asset");}source.english=english;source.korean=korean;
        font=FontAsset();
        var c=UnityEngine.Object.FindAnyObjectByType<MainPrototype>();c.uiText=source;
        var existing=GameObject.Find("Localized Prototype UI");if(existing!=null)throw new Exception("Localized UI already exists; no automatic replacement of authored objects");
        var root=new GameObject("Localized Prototype UI",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=100;
        var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(960,600);scaler.matchWidthOrHeight=1;
        var ui=root.AddComponent<PrototypeLocalizedUI>();ui.controller=c;ui.canvas=canvas;c.localizedUi=ui;
        var locale=root.AddComponent<PrototypeLocaleController>();locale.textSource=source;ui.locale=locale;
        var mask=Stretch(root.transform,"Eyelid Mask").gameObject.AddComponent<RawImage>();mask.texture=Texture2D.whiteTexture;mask.raycastTarget=false;mask.enabled=false;c.eyelidMask.uiOverlay=mask;
        var menu=Stretch(root.transform,"Menu");ui.menuRoot=menu.gameObject;
        ui.title=Label(menu,"Title",new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(0,-15),new Vector2(720,52),32);
        ui.instructions=Label(menu,"Instructions",new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(0,-62),new Vector2(720,48),17);
        var instructionRect=ui.instructions.rectTransform;instructionRect.anchorMin=new Vector2(0,1);instructionRect.anchorMax=new Vector2(1,1);instructionRect.sizeDelta=new Vector2(-60,48);
        ui.language=Label(menu,"Language",new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(-155,-120),new Vector2(100,34),17);
        ui.koreanButton=Button(menu,"Korean",new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(-35,-120),new Vector2(112,34));
        ui.englishButton=Button(menu,"English",new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(87,-120),new Vector2(112,34));
        ui.result=Label(menu,"Result",new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,190),new Vector2(720,50),32);
        ui.choose=Label(menu,"Choose Duration",new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,124),new Vector2(720,32),17);
        ui.durationButtons=new Button[c.SupportedMinutes.Count];for(int i=0;i<ui.durationButtons.Length;i++)ui.durationButtons[i]=Button(menu,"Duration "+c.SupportedMinutes[i],new Vector2(.5f,0),new Vector2(.5f,0),new Vector2((i-(ui.durationButtons.Length-1)*.5f)*118,78),new Vector2(112,40));
        ui.startButton=Button(menu,"Start Retry",new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,20),new Vector2(240,48));
        var hud=Stretch(root.transform,"HUD");ui.hudRoot=hud.gameObject;
        ui.remaining=Label(hud,"Remaining",new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(0,-18),new Vector2(500,32),17);
        ui.drowsiness=Label(hud,"Drowsiness",new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,20),new Vector2(720,28),17);
        var bar=Rect(hud,"Drowsiness Bar",new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,58),new Vector2(324,8));bar.gameObject.AddComponent<Image>().color=new Color(.04f,.07f,.08f,.85f);bar.GetComponent<Image>().raycastTarget=false;
        var fill=Stretch(bar,"Fill");fill.offsetMin=new Vector2(2,2);fill.offsetMax=new Vector2(-2,-2);fill.gameObject.AddComponent<Image>().raycastTarget=false;ui.drowsinessFill=fill;
        var pause=Rect(hud,"Pause",new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(440,120));pause.gameObject.AddComponent<Image>().color=new Color(.04f,.07f,.08f,.96f);ui.pauseRoot=pause.gameObject;
        ui.paused=Label(pause,"Paused",new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(0,-8),new Vector2(420,52),32);
        ui.resumeButton=Button(pause,"Resume",new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,8),new Vector2(180,44));
        ui.developer=Label(root.transform,"Developer",new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(0,-156),new Vector2(900,32),14);
        if(UnityEngine.Object.FindAnyObjectByType<EventSystem>()==null)new GameObject("Prototype UI EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
        Directory.CreateDirectory("Assets/StreamingAssets/ThirdParty");File.Copy(Root+"Fonts/OFL.txt","Assets/StreamingAssets/ThirdParty/NotoSansCJK-OFL.txt",true);
        EditorUtility.SetDirty(settings);EditorUtility.SetDirty(source);EditorUtility.SetDirty(ko);EditorUtility.SetDirty(c);EditorUtility.SetDirty(c.eyelidMask);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        File.WriteAllText("AgentScripts/bilingual-setup.txt","Official Locale assets, String Tables, startup selector, TMP canvas, Noto SDF and redistributed font license created.");
    }
}


