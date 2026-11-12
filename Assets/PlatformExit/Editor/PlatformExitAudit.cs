using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using GansuuHead;
public static class PlatformExitAudit {
 [Serializable] public class Result {public bool success;public string error;public List<string> checks=new List<string>();}
 static Result r;static MainPrototype c;static PrototypeLocalizedUI ui;static PlatformExitMenu menu;static int stage,ticks;static float elapsed;
 static void Check(bool ok,string label){if(!ok)throw new Exception(label);r.checks.Add(label);}
 [MenuItem("Tools/Gansuu Head/Platform Exit/Verify Play")]
 public static void Run(){if(!EditorApplication.isPlaying)throw new Exception("Play first");r=new Result();stage=0;ticks=0;EditorApplication.update+=Tick;}
 static void Tick(){try{
  if(++ticks<15)return;
  c=UnityEngine.Object.FindAnyObjectByType<MainPrototype>();if(c==null||c.localizedUi==null||!c.localizedUi.Ready)return;ui=c.localizedUi;menu=ui.GetComponent<PlatformExitMenu>();
  switch(stage){
   case 0:Check(menu!=null,"Runtime UI installed");Check(PlatformExitMenu.SupportsQuit,"Windows editor enables Quit");c.StartShift(1);ui.RefreshNow();menu.Open();elapsed=c.Session.Elapsed;InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(Key.Escape));break;
   case 1:InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());Check(c.QuitConfirmationOpen&&c.IsInputSuspended,"ESC cannot resume under modal");c.ResumeShift();Check(c.IsInputSuspended,"Resume cannot bypass modal");break;
   case 2:Check(c.Session.Elapsed==elapsed,"Clock stays frozen under confirmation");Check(Cursor.lockState==CursorLockMode.None&&Cursor.visible,"Modal cursor visible and unlocked");menu.Cancel();Check(!c.QuitConfirmationOpen&&c.IsInputSuspended,"Cancel preserves pause");c.ReturnToMainMenu();Check(c.Session.State==PrototypeSession.Phase.Ready,"Return to main menu");menu.Open();menu.Cancel();Check(c.Session.State==PrototypeSession.Phase.Ready,"Main menu cancel stays Ready");ui.SelectLanguage("en");break;
   case 3:Check(c.uiText.Get(PrototypeTextKeys.Quit)=="Quit Game","English quit label");Check(c.uiText.Get(PrototypeTextKeys.QuitQuestion)=="Quit the game?","English confirmation label");ui.SelectLanguage("ko-KR");break;
   case 4:Check(c.uiText.Get(PrototypeTextKeys.Quit)=="게임 종료","Korean quit label");Check(c.uiText.Get(PrototypeTextKeys.Cancel)=="취소","Korean cancel label");r.success=true;Finish();return;
  }stage++;ticks=0;
 }catch(Exception e){r.error=e.ToString();Finish();}}
 static void Finish(){EditorApplication.update-=Tick;if(menu!=null)menu.Cancel();if(c!=null)c.ReturnToMainMenu();File.WriteAllText(Path.Combine(File.ReadAllText("PlatformExit-output.txt").Trim(),"editor-audit.json"),JsonUtility.ToJson(r,true));}
}
