using UnityEngine;
using UnityEngine.InputSystem;

namespace GansuuHead
{
    public sealed class MainPrototype : MonoBehaviour
    {
        [Header("References")]
        public Transform head;
        public PrototypeTextSource uiText;
        public PrototypeLocalizedUI localizedUi;
        public CurvedEyelidMask eyelidMask;
        [Header("Drowsiness")]
        [Tooltip("Maximum drowsiness; applied when a new shift starts.")]
        [Range(.01f,1)] public float failureThreshold = 1;
        [Header("Eyelids")]
        [Tooltip("Scales the closing curve response; 1 uses the curve directly. 0 disables it until failure.")]
        [Range(0, 3)] public float eyeClosureStrength = 1;
        [Header("Drowsiness rates")]
        [Min(0)] public float globalDrowsinessMultiplier = 1;
        [Tooltip("Sustained gross recovery / current accumulation at normal input, after endurance settles.")]
        [Range(1.3f,1.7f)] public float sustainedRecoveryRatio = 1.5f;
        [Header("Mouse")]
        [Min(0)] public float inputSensitivity = 0.035f;
        [Range(0, 12)] public float maximumYaw = 8;
        [Range(0, 10)] public float maximumPitch = 6;
        [Min(0)] public float returnSpeed = 5;
        [Min(0)] public float minimumPixelsPerSecond = 90;
        [Min(1)] public float maximumPixelsPerSecond = 1800;
        [Min(0)] public float minimumDeltaPixels = 0.5f;
        [Min(1)] public float fullShakePixelsPerSecond = 180;
        [Header("Independent mode difficulty (X = progress 0-1)")]
        public ModeDifficulty oneMinute = ModeDifficulty.Create(1);
        public ModeDifficulty threeMinute = ModeDifficulty.Create(3);
        public ModeDifficulty fiveMinute = ModeDifficulty.Create(5);

        [Min(0)] public float maximumAccumulationPerSecond = .4f;
        [Header("Sudden awakening (normal recovery never depends on chance)")]
        [Range(0,1)] public float suddenAwakeningProbability=.25f;
        [Min(.25f)] public float suddenAwakeningCooldown=2;
        [Range(.2f,.3f)] public float suddenAwakeningDuration=.25f;
        [Range(1,3)] public float suddenRecoveryMultiplier=2;
        [Range(0,.15f)] public float suddenStrengthVariation=.15f;
        [Min(8)] public float shakeEventDistance=18;
        public AnimationCurve suddenRecoveryCurve=new AnimationCurve(new Keyframe(0,1,-2,-2),new Keyframe(1,0,0,0));
        // Retained only so earlier developer scripts can still compile; no longer used by gameplay.
        [HideInInspector] public float initialAwakeningRecovery, maximumInitialRecoveryFraction, initialShakeMinimumSpeed, burstQuietSeconds=.65f, initialRecoveryCooldown;
        public ShakeAwakening Awakening {get;}=new ShakeAwakening();
        public bool SuddenAwakeningActive => Awakening.Age<suddenAwakeningDuration;
        public int AwakeningSerial => Awakening.Activations;
        public float LastAwakeningStrength => Awakening.Strength;
        public bool NormalRecoveryActive {get;private set;}
        public bool IsInputSuspended => suspended || (!Application.isFocused
#if UNITY_EDITOR
            && !DeveloperReplayInput
#endif
        );
        float burstReferenceSpeed;
        int shiftSequence;
        [Min(0)] public float continuousRecoveryDecay = .35f;
        [Range(.9f,1)] public float minimumContinuousRecovery = .9f;
        [Header("Presentation curves")]
        public AnimationCurve eyelidClosingCurve = new AnimationCurve(
            new Keyframe(0,0,0,0),new Keyframe(.5f,.125f,.75f,.75f),new Keyframe(1,1,3,3));
        public AnimationCurve eyelidOpeningCurve = new AnimationCurve(new Keyframe(0,0,3,3),new Keyframe(1,1,0,0));
        [Min(.01f)] public float eyeOpeningSeconds = .08f;
        public PrisonerExpression prisonerExpression;
        public float DrowsinessRatio => Mathf.Clamp01(Session.Drowsiness / Session.FailureThreshold);
        public float EyeClosure => Session.State == PrototypeSession.Phase.Failure ? 1 : displayedClosure;
        public float TargetEyeClosure => Session.State == PrototypeSession.Phase.Failure ? 1 :
            (eyeClosureStrength <= 0 ? 0 : Mathf.Pow(Evaluate01(eyelidClosingCurve,DrowsinessRatio),1f/eyeClosureStrength));
        public float CurrentAccumulation => AccumulationAt(Session.Duration > 0 ? Mathf.RoundToInt(Session.Duration/60) : minutes[choice],Session.Elapsed);
        float displayedClosure, openingFrom, openingTo, openingElapsed;
        bool openingActive;
        float quietTime, continuousTime;
#if UNITY_EDITOR || DEBUG
        [Header("Development only (clock acceleration, not drowsiness/input)")]
        public bool developerToolsEnabled;
        [Range(1,300)] public float developerClockScale = 1;
        public float ClockScale => developerToolsEnabled ? Mathf.Clamp(developerClockScale,1,300) : 1;
#else
        public float ClockScale => 1;
#endif
        public ModeDifficulty Profile(int selectedMinutes)
        {
            switch(selectedMinutes) {case 1:return oneMinute;case 3:return threeMinute;case 5:return fiveMinute;

                default:throw new System.ArgumentOutOfRangeException(nameof(selectedMinutes));}
        }
        static float Evaluate01(AnimationCurve curve,float x) => Mathf.Clamp01(curve == null || curve.length==0 ? x : curve.Evaluate(x));
        public float AccumulationAt(int selectedMinutes,float seconds)
        {
            var p=Profile(selectedMinutes);
            float baseRate=1f/Mathf.Max(.5f,p.initialIdleFailureSeconds)*
                Mathf.Max(0,p.accumulationMultiplier)*Mathf.Max(0,globalDrowsinessMultiplier);
            float multiplier=p.EvaluateMultiplier(seconds/(selectedMinutes*60f));
            // The absolute cap is last, so authored scale factors cannot bypass it.
            return Mathf.Min(Mathf.Max(0,maximumAccumulationPerSecond),baseRate*multiplier);
        }
        public float SustainedRecoveryAt(int selectedMinutes,float seconds,float speed,float continuousSeconds)
        {
            return RecoveryForRate(Profile(selectedMinutes),AccumulationAt(selectedMinutes,seconds),speed,continuousSeconds);
        }
        float RecoveryForRate(ModeDifficulty p,float accumulation,float speed,float continuousSeconds)
        {
            float strength=Mathf.Clamp01((speed-minimumPixelsPerSecond)/
                Mathf.Max(1,fullShakePixelsPerSecond-minimumPixelsPerSecond));
            float floor=Mathf.Clamp(minimumContinuousRecovery,.9f,1);
            float endurance=Mathf.Lerp(floor,1,Mathf.Exp(-Mathf.Max(0,continuousRecoveryDecay)*Mathf.Max(0,continuousSeconds)));
            float ratio=Mathf.Clamp(sustainedRecoveryRatio*endurance/floor,1.3f,1.7f);
            return accumulation*ratio*strength*Mathf.Max(0,p.recoveryMultiplier);
        }
        public void StartShift(int selectedMinutes)
        {
            Session.Start(selectedMinutes,failureThreshold);nextHudRefresh=0;
            angles=Vector2.zero; displayedClosure=0; openingActive=false;
            quietTime=burstQuietSeconds; Awakening.Reset(unchecked(System.Environment.TickCount + ++shiftSequence));
            continuousTime=0; NormalRecoveryActive=false; suspended=false; skipMouseFrame=true;
            if(head!=null) head.localRotation=Quaternion.identity;
            if(prisonerExpression!=null) prisonerExpression.SetDrowsiness(0,0,true);
            SyncCursor();
        }
        public PrototypeSession Session { get; private set; } = new PrototypeSession();
        readonly int[] minutes = { 1, 3, 5 };
        public System.Collections.Generic.IReadOnlyList<int> SupportedMinutes=>minutes;
        public int SelectedMinutes=>minutes[choice];
        public void SelectMinutes(int value){int index=System.Array.IndexOf(minutes,value);if(index<0)throw new System.ArgumentOutOfRangeException(nameof(value));choice=index;}
        public void ResumeShift(){if(QuitConfirmationOpen)return;SetSuspended(false);}
        void SetSuspended(bool value){suspended=value;skipMouseFrame=true;SyncCursor();}
        int choice;
        Vector2 angles;
        bool skipMouseFrame = true;
        bool suspended;
        public bool QuitConfirmationOpen {get;private set;}
        public void SetQuitConfirmation(bool open)
        {
            QuitConfirmationOpen=open;
            if(open && Session.State==PrototypeSession.Phase.Playing)SetSuspended(true);
            SyncCursor();
        }
        public void ReturnToMainMenu()
        {
            if(QuitConfirmationOpen)return;
            Session=new PrototypeSession();suspended=false;skipMouseFrame=true;
            displayedClosure=0;openingActive=false;angles=Vector2.zero;
            if(head!=null)head.localRotation=Quaternion.identity;
            if(prisonerExpression!=null)prisonerExpression.SetDrowsiness(0,0,true);
            SyncCursor();
        }
        GUIStyle title, body, button;
        readonly string[] modeLabels=new string[3];
        PrototypeTextSource cachedTextSource;int cachedTextRevision=-1;
        string titleText,instructionsText,chooseText,pausedText,resumeText,successText,failureText,startText,retryText;
        string Text(string key)=>uiText==null?key:uiText.Get(key);
        string FormatText(string key,params object[] args)=>uiText==null?key:uiText.Format(key,args);
        void RefreshTextCache()
        {
            if(uiText==cachedTextSource && cachedTextRevision==(uiText==null?0:uiText.Revision))return;
            titleText=Text(PrototypeTextKeys.Title);instructionsText=Text(PrototypeTextKeys.Instructions);
            chooseText=Text(PrototypeTextKeys.ChooseDuration);pausedText=Text(PrototypeTextKeys.Paused);
            resumeText=Text(PrototypeTextKeys.Resume);successText=Text(PrototypeTextKeys.Success);
            failureText=Text(PrototypeTextKeys.Failure);startText=Text(PrototypeTextKeys.Start);retryText=Text(PrototypeTextKeys.Retry);
            for(int i=0;i<minutes.Length;i++)modeLabels[i]=FormatText(PrototypeTextKeys.Duration,minutes[i]);
            cachedTextSource=uiText;cachedTextRevision=uiText==null?0:uiText.Revision;nextHudRefresh=0;
        }
        float nextHudRefresh;string remainingText="",drowsyText="";
        void OnApplicationFocus(bool focus)
        {
            cursorFocused = focus;
            skipMouseFrame = true;
            SyncCursor();
        }
        bool cursorFocused;
        void OnEnable(){cursorFocused=Application.isFocused;SyncCursor();}
        void SyncCursor()
        {
            bool locked=isActiveAndEnabled && cursorFocused && Application.isFocused &&
                Session.State==PrototypeSession.Phase.Playing && !suspended && !QuitConfirmationOpen;
            var mode=locked?CursorLockMode.Locked:CursorLockMode.None;
            if(Cursor.lockState!=mode){Cursor.lockState=mode;skipMouseFrame=true;}
            Cursor.visible=!locked;
        }
        // Reconcile after ESC/UI processing, including an Editor/OS cursor unlock.
        void LateUpdate()
        {
#if UNITY_EDITOR
            if(DeveloperReplayInput)return;
#endif
            SyncCursor();
        }
        void OnDisable() { ReleaseCursor(); }
        void ReleaseCursor() { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        public void Begin()
        {
            StartShift(minutes[choice]);
            suspended = false;
            if (head != null) head.localRotation = Quaternion.identity;
            angles = Vector2.zero;
            skipMouseFrame = true;
            SyncCursor();
        }
        #if UNITY_EDITOR
        public bool DeveloperReplayInput {get;set;}
        #endif
        void Update()
        {
            #if UNITY_EDITOR
            if(DeveloperReplayInput)return;
            #endif
            if (QuitConfirmationOpen || Session.State != PrototypeSession.Phase.Playing) { SyncCursor(); return; }
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                SetSuspended(!suspended);
            }
            if (suspended || !cursorFocused || !Application.isFocused)
            {
                ReleaseCursor();
                return;
            }
            SyncCursor();
            if (skipMouseFrame) { skipMouseFrame = false; return; }
#if UNITY_EDITOR || DEBUG
            if(developerToolsEnabled && Keyboard.current!=null)
            {
                var k=Keyboard.current;
                if(k.f6Key.wasPressedThisFrame) developerClockScale=developerClockScale<10?10:developerClockScale<60?60:1;
                var p=Profile(Mathf.RoundToInt(Session.Duration/60));
                if(k.f7Key.wasPressedThisFrame) Session.DeveloperSeek(p.relaxedUntilSeconds);
                if(k.f8Key.wasPressedThisFrame) Session.DeveloperSeek(p.highFromSeconds);
                if(k.f9Key.wasPressedThisFrame) Session.DeveloperSeek(Session.Duration-10);
            }
#endif
            float dt = Time.unscaledDeltaTime;
            if (dt <= 0) return;
            Vector2 delta = Mouse.current == null ? Vector2.zero : Mouse.current.delta.ReadValue();
            AdvanceGameplay(delta, dt);
            if (Session.State != PrototypeSession.Phase.Playing) ReleaseCursor();
        }
        // This path is shared by live mouse input and deterministic editor checks.
        public Vector2 FilterInput(Vector2 delta, float dt)
        {
            if (dt <= 0 || float.IsNaN(dt) || float.IsInfinity(dt) || float.IsNaN(delta.x) || float.IsNaN(delta.y) || float.IsInfinity(delta.x) || float.IsInfinity(delta.y) || delta.magnitude < minimumDeltaPixels * dt * 60f ||
                delta.magnitude / dt < minimumPixelsPerSecond) return Vector2.zero;
            return Vector2.ClampMagnitude(delta, Mathf.Max(1, maximumPixelsPerSecond) * dt);
        }
        public void AdvanceGameplay(Vector2 rawDelta, float dt)
        {
            if (Session.State != PrototypeSession.Phase.Playing || dt <= 0 || float.IsNaN(dt) || float.IsInfinity(dt)) return;
            Vector2 delta = FilterInput(rawDelta, dt);
            angles += new Vector2(-delta.y, delta.x) * inputSensitivity;
            angles.x = Mathf.Clamp(angles.x, -maximumPitch, maximumPitch);
            angles.y = Mathf.Clamp(angles.y, -maximumYaw, maximumYaw);
            if (delta == Vector2.zero)
                angles = Vector2.Lerp(angles, Vector2.zero, 1 - Mathf.Exp(-returnSpeed * dt));
            var profile=Profile(Mathf.RoundToInt(Session.Duration/60));
            float speed=delta.magnitude/dt;
            NormalRecoveryActive=speed>minimumPixelsPerSecond;
            bool initial=Awakening.Step(delta,dt,shakeEventDistance,suddenAwakeningProbability,
                suddenAwakeningCooldown,suddenStrengthVariation);
            if(initial)burstReferenceSpeed=speed;
            if(delta==Vector2.zero){quietTime+=dt;if(quietTime>=burstQuietSeconds)continuousTime=0;}
            else quietTime=0;
            float remaining=dt;
            while(remaining>0 && Session.State==PrototypeSession.Phase.Playing)
            {
                float step=Mathf.Min(remaining,1f/60f);
                float accumulation=CurrentAccumulation;
                float relief=RecoveryForRate(profile,accumulation,speed,continuousTime);
                // Midpoint quadrature keeps the short envelope consistent across frame rates.
                float age=Mathf.Max(0,Awakening.Age-dt)+(dt-remaining)+step*.5f;
                if(age<suddenAwakeningDuration)
                {
                    float envelope=Evaluate01(suddenRecoveryCurve,age/Mathf.Max(.01f,suddenAwakeningDuration));
                    float bonus=Mathf.Max(0,suddenRecoveryMultiplier*Awakening.Strength-1);
                    relief+=RecoveryForRate(profile,accumulation,burstReferenceSpeed,continuousTime)*bonus*envelope;
                }
                Session.Tick(step,step,accumulation,relief,ClockScale);
                if(delta!=Vector2.zero) continuousTime+=step;
                remaining-=step;
            }
            RefreshPresentation(dt, false, initial);
            if (head != null)
                head.localRotation = Quaternion.Euler(angles.x, angles.y, -angles.y * 0.12f);
        }
        public void RefreshPresentation(float dt, bool immediate=false, bool initialAwakening=false)
        {
            float target=TargetEyeClosure;
            if(SuddenAwakeningActive)target*=1-.18f*(1-Mathf.Clamp01(Awakening.Age/Mathf.Max(.01f,suddenAwakeningDuration)));
            if(immediate || Session.State==PrototypeSession.Phase.Failure)
            { displayedClosure=target; openingActive=false; return; }
            if(initialAwakening && target<displayedClosure)
            {
                displayedClosure=Mathf.Lerp(displayedClosure,target,Evaluate01(eyelidOpeningCurve,.7f));
                openingActive=false;
            }
            if(target<displayedClosure)
            {
                if(!openingActive) {openingFrom=displayedClosure;openingTo=target;openingElapsed=0;openingActive=true;}
                openingTo=Mathf.Min(openingTo,target);
                openingElapsed+=Mathf.Max(0,dt);
                float t=Mathf.Clamp01(openingElapsed/Mathf.Max(.01f,eyeOpeningSeconds));
                displayedClosure=Mathf.Lerp(openingFrom,openingTo,Evaluate01(eyelidOpeningCurve,t));
                if(t>=1) openingActive=false;
            }
            else {displayedClosure=target;openingActive=false;}
        }
        void OnGUI()
        {
            if(localizedUi!=null)return;
            RefreshTextCache();
            if(eyelidMask!=null)eyelidMask.DrawGUI();
            if (title == null)
            {
                title = new GUIStyle(GUI.skin.label) { fontSize = 32, alignment = TextAnchor.MiddleCenter };
                body = new GUIStyle(GUI.skin.label) { fontSize = 17, alignment = TextAnchor.MiddleCenter, wordWrap = true };
                button = new GUIStyle(GUI.skin.button) { fontSize = 17 };
            }
            float scale = Mathf.Min(Screen.width / 960f, Screen.height / 600f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            float w = Screen.width / scale, h = Screen.height / scale;
            if(Session.State!=PrototypeSession.Phase.Playing) GUI.Label(new Rect(0, 15, w, 45), titleText, title);
            if(Session.State!=PrototypeSession.Phase.Playing) GUI.Label(new Rect(0, 58, w, 32), instructionsText, body);
#if UNITY_EDITOR || DEBUG
            if(developerToolsEnabled)
                GUI.Label(new Rect(0,140,w,32),FormatText(PrototypeTextKeys.DeveloperStatus,ClockScale,CurrentAccumulation),body);
#endif
            var state = Session.State;
            if (state == PrototypeSession.Phase.Playing)
            {
                float remaining = Mathf.Max(0, Session.Duration - Session.Elapsed);
                if(Time.unscaledTime>=nextHudRefresh){
                    nextHudRefresh=Time.unscaledTime+.1f;
                    remainingText=FormatText(PrototypeTextKeys.Remaining,Mathf.CeilToInt(remaining)/60,Mathf.CeilToInt(remaining)%60);
                    drowsyText=FormatText(PrototypeTextKeys.Drowsiness,DrowsinessRatio*100);
                }
                GUI.Label(new Rect(0,18,w,32),remainingText,body);
                GUI.Box(new Rect(w / 2 - 162, h - 66, 324, 8), string.Empty);
                Color old = GUI.color;
                GUI.color = Color.Lerp(new Color(.33f,.52f,.53f),new Color(.62f,.32f,.28f),DrowsinessRatio);
                GUI.DrawTexture(new Rect(w / 2 - 160, h - 64, 320 * DrowsinessRatio, 4), Texture2D.whiteTexture);
                GUI.color = old;
                GUI.Label(new Rect(0, h - 48, w, 28), drowsyText, body);
                if (suspended || !cursorFocused || !Application.isFocused)
                {
                    GUI.Box(new Rect(w / 2 - 220, h / 2 - 50, 440, 120), string.Empty);
                    GUI.Label(new Rect(w / 2 - 210, h / 2 - 42, 420, 45), pausedText, title);
                    if (GUI.Button(new Rect(w / 2 - 90, h / 2 + 10, 180, 44), resumeText, button)) ResumeShift();
                }
                return;
            }
            if (state != PrototypeSession.Phase.Ready)
                GUI.Label(new Rect(0, h - 210, w, 50), state == PrototypeSession.Phase.Success ? successText : failureText, title);
            GUI.Label(new Rect(0, h - 156, w, 32), chooseText, body);
            for (int i = 0; i < minutes.Length; i++)
            {
                GUI.backgroundColor = choice == i ? new Color(.3f, .85f, .7f) : Color.white;
                if (GUI.Button(new Rect(w / 2 - (minutes.Length * 118 - 6) / 2f + i * 118, h - 118, 112, 40), modeLabels[i], button)) choice = i;
            }
            GUI.backgroundColor = Color.white;
            if (GUI.Button(new Rect(w / 2 - 120, h - 68, 240, 48), state == PrototypeSession.Phase.Ready ? startText : retryText, button)) Begin();
        }
    }
}























