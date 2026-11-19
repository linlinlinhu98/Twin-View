using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TwinView {
public sealed class ChamberController : MonoBehaviour {
    public Transform Artifact, DoorLeft, DoorRight;
    public Camera FrontCamera, SideCamera, ViewCamera;
    public Shader MaskShader;
    public Renderer FrontScreen, SideScreen;
    public Light FrontLamp, SideLamp;
    public Transform Pillar;
    public Transform Marker, MarkerRail, MarkerRing; // selection feedback, never part of any projection layer
    public Transform[] MarkerDots; // three fixed stops along the free axis; the active one is larger
    public Transform[] DepthMarker; // offset seal only: three stops along the depth rail
    public int RoomIndex;
    public bool ActiveRoom;
    public JourneyController Journey;
    public SecretAlcove Alcove;
    public bool InspectAlcove { get; private set; }
    public bool InspectRelay { get; private set; }
    public bool BridgeSelected { get; private set; }
    bool BridgeControls { get { return RoomIndex==0&&InspectRelay&&BridgeSelected; } }
    bool RemoteControls { get { return RoomIndex==1&&InspectRelay; } }
    bool ExtraControls { get { return Pillar||InspectingCourtyard; } }
    bool BridgePanel { get { return RoomIndex==0&&InspectRelay; } }
    const float M=24,LW=1100,RX=1140,RW=436;
    bool Row1 { get { return BridgePanel||RoomIndex==1; } }
    float BottomPanelHeight { get { return Row1?286:(ExtraControls?234:234); } }
    bool InspectingCourtyard { get { return RoomIndex==0&&(InspectAlcove||InspectRelay||InspectOffset||InspectGate); } }
    public bool IsMoving { get { return moving; } }
    public int ArtifactSlot { get; private set; } = 1;
    // Depth rail: ability lock, unlocked by the final seal.
    public const int DefaultDepth=1; // the middle rail, where the guardian has always stood
    public int ArtifactDepth { get; private set; } = DefaultDepth;
    public bool DepthUnlocked { get { return RoomIndex==0&&Journey&&Journey.Relay&&Journey.Relay.Solved; } }
    public OffsetSeal Offset;
    public GateSeal Gate;
    public PillarAlcove PillarAlcove;
    public bool InspectOffset { get; private set; }
    public bool InspectGate { get; private set; }
    public Vector3 ViewPosition { get; private set; }
    public Vector3 ViewTarget { get; private set; }
    public int PillarSlot { get; private set; }
    public int MoveCount { get { return moves; } }
    public ProjectionRule Front { get; private set; }
    public ProjectionRule Side { get; private set; }
    public int Pose { get; private set; }
    public bool Solved { get; private set; }
    struct Layout {
        public int pose,slot,artifact,depth,bridgePose,bridgeSlot;public bool bridgeInstalled;
        public Layout(int p,int s,int a,int d,BridgePiece bridge) {pose=p;slot=s;artifact=a;depth=d;bridgePose=bridge.Pose;bridgeSlot=bridge.Slot;bridgeInstalled=bridge.Installed;}
    }
    readonly Stack<Layout> history = new Stack<Layout>();
    bool initialized, moving;
    float viewAspect=2f;
    int moves, hint, alcoveHint, relayHint, offsetHint, gateHint;
    float angle, desiredAngle, passTime, doorOpen, orbit=35, elevation=27;
    float orbitBase=35, zoom=1f; // mouse may turn the camera only +-OrbitRange around orbitBase; the wheel scales distance
    // Asymmetric: a wall column blocks the right side.
    public const float OrbitLeft=55f, OrbitRight=30f;
    const float ZoomMin=.62f, ZoomMax=1.12f;
    Vector3 leftStart, rightStart;
    Font font;
    GUIStyle title, body, bodyBright, small, button, status, large, buttonLabel, buttonKey, currentButton, caption, chip;
    Texture2D paper, panel, accent, hover, brassBar, cyanSwatch, brownSwatch;
    AudioSource audioSource;
    AudioClip turnClip, solveClip;
    string notice;float noticeUntil;
    void BlockedPlacement() {notice="空间不足：桥块会碰到瑞兽，请先调整另一件物体。";noticeUntil=Time.unscaledTime+3;}

    void Start() {
        Initialize();
    }
    public void Initialize() {
        if(initialized) return;
        initialized=true;
        leftStart=DoorLeft.localPosition; rightStart=DoorRight.localPosition;
        Front=new ProjectionRule(); Side=new ProjectionRule();
        ConfigureCamera(FrontCamera); ConfigureCamera(SideCamera);
        if(Pillar) Pillar.localPosition=PillarPosition(2);
        Artifact.localRotation=Quaternion.identity;
        bool[] frontGoal=Front.Read(FrontCamera,MaskShader);
        bool[] sideGoal=Side.Read(SideCamera,MaskShader);
        Artifact.localRotation=Quaternion.Euler(0,180,0);
        bool[] frontReverse=Front.Read(FrontCamera,MaskShader);
        Front.SetTarget(frontGoal,RoomIndex==0?frontReverse:null); Side.SetTarget(sideGoal);
        SetStateImmediate(2,0);
        audioSource=gameObject.AddComponent<AudioSource>();
        turnClip=Tone(330,.08f); solveClip=Tone(660,.5f);
        UpdateView();
    }
    void ConfigureCamera(Camera c) {
        c.enabled=false; c.orthographic=true; c.orthographicSize=RoomIndex==0?1.05f:1.65f;
        c.aspect=1; c.clearFlags=CameraClearFlags.SolidColor; c.backgroundColor=Color.black;
        c.cullingMask=1<<(8+RoomIndex); c.allowHDR=false; c.allowMSAA=false;
        c.targetTexture=new RenderTexture(128,128,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
        c.targetTexture.antiAliasing=1; c.targetTexture.Create();
    }
    public void SetPoseImmediate(int pose) {
        SetStateImmediate(pose,PillarSlot);
    }
    public void SetStateImmediate(int pose,int slot) {
        Pose=(pose%4+4)%4; angle=desiredAngle=Pose*90; moving=false;
        PillarSlot=Mathf.Clamp(slot,0,2);
        if(Pillar) Pillar.localPosition=PillarPosition(PillarSlot);
        Artifact.localRotation=Quaternion.Euler(0,angle,0); RefreshProjection();
    }
    static Vector3 PillarPosition(int slot) { return new Vector3(-1.2f+slot*.9f,0,1.28f); }
    public static Vector3 ArtifactPosition(int slot,int depth=DefaultDepth) { return new Vector3((slot-1)*.9f,1.65f,(1-depth)*.45f); }
    // 0 back rail, 1 middle (default), 2 front rail.
    public void SetDepthImmediate(int depth) {
        ArtifactDepth=Mathf.Clamp(depth,0,2);Artifact.localPosition=ArtifactPosition(ArtifactSlot,ArtifactDepth);RefreshProjection();
    }
    public void SetArtifactSlotImmediate(int slot) {
        ArtifactSlot=Mathf.Clamp(slot,0,2);Artifact.localPosition=ArtifactPosition(ArtifactSlot,ArtifactDepth);RefreshProjection();
    }
    public void Inspect(bool inspect) {
        InspectRelay=false;InspectOffset=false;InspectGate=false;
        InspectAlcove=inspect&&Alcove;
        orbitBase=orbit=InspectAlcove?-28:35;elevation=InspectAlcove?30:27;zoom=InspectAlcove?.78f:1f;UpdateView();
    }
    public void InspectRelayStation(bool inspect) {
        InspectAlcove=false;InspectOffset=false;InspectGate=false;InspectRelay=inspect;
        orbitBase=orbit=InspectingCourtyard?-28:35;elevation=InspectingCourtyard?30:27;zoom=InspectingCourtyard?.78f:1f;UpdateView();
    }
    public void InspectOffsetSeal(bool inspect) {
        InspectAlcove=false;InspectRelay=false;InspectGate=false;InspectOffset=inspect&&Offset; // visible from the start; only the depth rail is locked
        orbitBase=orbit=InspectOffset?-28:35;elevation=InspectOffset?30:27;zoom=InspectOffset?.72f:1f;UpdateView();
    }
    public void InspectGateSeal(bool inspect) {
        InspectAlcove=false;InspectRelay=false;InspectOffset=false;InspectGate=inspect&&Gate;
        orbit=InspectGate?-34:35;elevation=InspectGate?26:27;zoom=InspectGate?.82f:1f;UpdateView();
    }
    void CycleStation() {
        if(RoomIndex==1) InspectRelayStation(!InspectRelay);
        else if(InspectGate) Inspect(false);
        else if(InspectOffset) {if(Gate) InspectGateSeal(true);else Inspect(false);}
        else if(InspectRelay) {if(Offset) InspectOffsetSeal(true);else Inspect(false);}
        else if(InspectAlcove) InspectRelayStation(true);
        else Inspect(true);
    }
    void MoveSelected(int delta) {
        if(RemoteControls) Journey.Relay.Shift(delta);
        else if(BridgeControls) ChangeBridge(0,delta);
        else if(InspectingCourtyard) SlideArtifact(delta);
        else MovePillar(delta);
    }
    void RememberLayout() {history.Push(new Layout(Pose,PillarSlot,ArtifactSlot,ArtifactDepth,Journey.Bridge));}
    public void SelectBridge(bool selected) {BridgeSelected=selected&&Journey.Bridge.Acquired;}
    public bool ToggleBridge() {
        var bridge=Journey.Bridge;
        if(RoomIndex!=0||!InspectRelay||!bridge.Acquired||moving||Journey.Travelling||Journey.Relay.Moving) return false;
        if(!bridge.Installed&&Journey.Relay.Setting!=3) return false;
        if(!bridge.Installed&&!bridge.CanPlace(bridge.Pose,bridge.Slot,Pose,ArtifactSlot,ArtifactDepth)) {BlockedPlacement();return false;}
        RememberLayout();bridge.Apply(!bridge.Installed,bridge.Pose,bridge.Slot);moves++;RefreshProjection();return true;
    }
    void ChangeBridge(int rotation,int depth) {
        var bridge=Journey.Bridge;if(moving||!bridge.Installed||Journey.Relay.Moving) return;
        int slot=Mathf.Clamp(bridge.Slot+depth,0,2);if(depth!=0&&slot==bridge.Slot)return;
        if(!bridge.CanPlace((bridge.Pose+rotation+4)%4,slot,Pose,ArtifactSlot,ArtifactDepth)) {BlockedPlacement();return;}
        RememberLayout();bridge.Apply(true,bridge.Pose+rotation,slot);moves++;RefreshProjection();
    }
    public void MoveBridgeDepth(int delta) {ChangeBridge(0,delta);}
    public void ShowHint() { if(InspectGate) gateHint=Mathf.Min(3,gateHint+1);else if(InspectOffset) offsetHint=Mathf.Min(3,offsetHint+1);else if(InspectRelay) relayHint=Mathf.Min(3,relayHint+1);else if(InspectAlcove) alcoveHint=Mathf.Min(3,alcoveHint+1);else hint=Mathf.Min(3,hint+1); }
    public void SlideArtifact(int delta) {
        if(moving||!Alcove) return;
        int next=Mathf.Clamp(ArtifactSlot+delta,0,2);if(next==ArtifactSlot) return;
        var bridge=Journey.Bridge;
        if(bridge.Installed&&!bridge.CanPlace(bridge.Pose,bridge.Slot,Pose,next,ArtifactDepth)) {BlockedPlacement();return;}
        RememberLayout();ArtifactSlot=next;
        moving=true;moves++;passTime=0;
        if(audioSource) audioSource.PlayOneShot(turnClip,.25f);
    }
    // Same clamps the mouse uses.
    public void OrbitBy(float degrees) {orbit=Mathf.Clamp(orbit+degrees,orbitBase-OrbitLeft,orbitBase+OrbitRight);UpdateView();}
    public void ZoomBy(float wheel) {zoom=Mathf.Clamp(zoom-wheel*.08f,ZoomMin,ZoomMax);UpdateView();}
    public float OrbitDegrees { get { return orbit; } }
    public float OrbitBaseDegrees { get { return orbitBase; } }
    public float Zoom { get { return zoom; } }
    public void MoveDepth(int delta) {
        if(moving||!Alcove||!DepthUnlocked||Journey.Travelling) return;
        int next=Mathf.Clamp(ArtifactDepth+delta,0,2);if(next==ArtifactDepth) return;
        var bridge=Journey.Bridge;
        if(bridge.Installed&&!bridge.CanPlace(bridge.Pose,bridge.Slot,Pose,ArtifactSlot,next)) {BlockedPlacement();return;}
        RememberLayout();ArtifactDepth=next;moving=true;moves++;passTime=0;
        if(audioSource) audioSource.PlayOneShot(turnClip,.25f);
    }
    public void RefreshProjection() {
        Front.Evaluate(Front.Read(FrontCamera,MaskShader));
        Side.Evaluate(Side.Read(SideCamera,MaskShader));
        if(FrontScreen) FrontScreen.sharedMaterial.mainTexture=Front.Display;
        if(SideScreen) SideScreen.sharedMaterial.mainTexture=Side.Display;
        if(FrontLamp) FrontLamp.color=Front.Passed?new Color(.43f,.85f,.66f):new Color(.85f,.53f,.25f);
        if(SideLamp) SideLamp.color=Side.Passed?new Color(.43f,.85f,.66f):new Color(.85f,.53f,.25f);
        if(Alcove) Alcove.Refresh();
        if(Offset) Offset.Refresh();
        if(Gate) Gate.Refresh();
        if(PillarAlcove) PillarAlcove.Refresh();
        if(Journey&&Journey.Relay) Journey.Relay.Refresh();
    }
    public void Turn(int delta) {
        if(BridgeControls) {ChangeBridge(delta,0);return;}
        if(moving||RemoteControls) return;
        var bridge=Journey.Bridge;
        if(RoomIndex==0&&bridge.Installed&&!bridge.CanPlace(bridge.Pose,bridge.Slot,(Pose+delta+4)%4,ArtifactSlot,ArtifactDepth)) {BlockedPlacement();return;}
        RememberLayout(); Pose=(Pose+delta+4)%4; desiredAngle=angle+delta*90;
        moving=true; moves++; passTime=0;
        if(audioSource) audioSource.PlayOneShot(turnClip,.25f);
    }
    public void MovePillar(int delta) {
        if(moving||!Pillar) return;
        int next=Mathf.Clamp(PillarSlot+delta,0,2);
        if(next==PillarSlot) return;
        RememberLayout();PillarSlot=next;moving=true;moves++;passTime=0;
        if(audioSource) audioSource.PlayOneShot(turnClip,.25f);
    }
    public void Undo() {
        if(RemoteControls) {Journey.Relay.Undo();return;}
        if(moving||history.Count==0) return;
        if(RoomIndex==0&&history.Peek().bridgeInstalled&&Journey.Relay.Setting!=3) return;
        Layout old=history.Pop(); desiredAngle=angle+Mathf.DeltaAngle(angle,old.pose*90);
        if(RoomIndex==0) Journey.Bridge.Apply(old.bridgeInstalled,old.bridgePose,old.bridgeSlot);
        Pose=old.pose;PillarSlot=old.slot;ArtifactSlot=old.artifact;ArtifactDepth=old.depth; moving=true; moves=Mathf.Max(0,moves-1); passTime=0;
    }
    public void ResetPuzzle() {
        history.Clear(); moves=hint=alcoveHint=relayHint=offsetHint=gateHint=0; passTime=0;
        // Reset the arrangement, never revoke an opened route.
        SetStateImmediate(2,0);
        ArtifactDepth=DefaultDepth;SetArtifactSlotImmediate(1);
        if(RoomIndex==0&&Journey&&Journey.Bridge) {Journey.Bridge.Apply(false,0,BridgePiece.StartDepth);BridgeSelected=false;}
    }
    void Update() {
        if(!initialized) return;
        AnimateDoor();
        UpdateMarker();
        if(!ActiveRoom || (Journey && (Journey.Travelling||!Journey.Started||Journey.EndingVisible))) return;
        if(Input.GetKeyDown(KeyCode.Q)||Input.GetKeyDown(KeyCode.LeftArrow)) Turn(-1);
        if(Input.GetKeyDown(KeyCode.E)||Input.GetKeyDown(KeyCode.RightArrow)) Turn(1);
        if(Input.GetKeyDown(KeyCode.Z)) Undo();
        if(Input.GetKeyDown(KeyCode.R)) ResetPuzzle();
        if(Input.GetKeyDown(KeyCode.H)) ShowHint();
        if(Input.GetKeyDown(KeyCode.A)) MoveSelected(-1);
        if(Input.GetKeyDown(KeyCode.D)) MoveSelected(1);
        if(Input.GetKeyDown(KeyCode.Tab)) CycleStation();
        if(Input.GetKeyDown(KeyCode.W)) MoveDepth(1);
        if(Input.GetKeyDown(KeyCode.S)) MoveDepth(-1);
        if(Input.GetKeyDown(KeyCode.Alpha1)&&Journey) Journey.Visit(0);
        if(Input.GetKeyDown(KeyCode.Alpha2)&&Journey) Journey.Visit(1);
        if(Input.GetKeyDown(KeyCode.T)&&RoomIndex==0&&InspectRelay) SelectBridge(!BridgeSelected);
        if(Input.GetKeyDown(KeyCode.I)) {if(RoomIndex==1) Journey.Bridge.Collect();else ToggleBridge();}
        if(Input.GetKeyDown(KeyCode.Escape)) Application.Quit();
        if(Input.GetMouseButton(1)&&ViewCamera.pixelRect.Contains(Input.mousePosition)) {
            orbit=Mathf.Clamp(orbit+Input.GetAxis("Mouse X")*3,orbitBase-OrbitLeft,orbitBase+OrbitRight);
            elevation=Mathf.Clamp(elevation-Input.GetAxis("Mouse Y")*2,15,55); UpdateView();
        }
        float wheel=Input.mouseScrollDelta.y;
        if(Mathf.Abs(wheel)>.01f&&ViewCamera.pixelRect.Contains(Input.mousePosition)) {zoom=Mathf.Clamp(zoom-wheel*.08f,ZoomMin,ZoomMax);UpdateView();}
        ViewCamera.fieldOfView=32;
        float panelTop=CanvasHeight-20-BottomPanelHeight;
        ViewCamera.rect=RemoteControls?CanvasViewport(new Rect(M,278,540,panelTop-288)):CanvasViewport(new Rect(M,278,LW,panelTop-288));
        float aspectNow=ViewCamera.rect.width*Screen.width/Mathf.Max(1f,ViewCamera.rect.height*Screen.height);
        if(Mathf.Abs(aspectNow-viewAspect)>.01f) {viewAspect=aspectNow;UpdateView();}
        if(moving) {
            angle=Mathf.MoveTowards(angle,desiredAngle,220*Time.deltaTime);
            Artifact.localRotation=Quaternion.Euler(0,angle,0);
            Artifact.localPosition=Vector3.MoveTowards(Artifact.localPosition,ArtifactPosition(ArtifactSlot,ArtifactDepth),2.8f*Time.deltaTime);
            if(Pillar) Pillar.localPosition=Vector3.MoveTowards(Pillar.localPosition,PillarPosition(PillarSlot),2.8f*Time.deltaTime);
            RefreshProjection();
            if(Mathf.Abs(angle-desiredAngle)<.01f && Vector3.Distance(Artifact.localPosition,ArtifactPosition(ArtifactSlot,ArtifactDepth))<.001f && (!Pillar || Vector3.Distance(Pillar.localPosition,PillarPosition(PillarSlot))<.001f)) { moving=false; angle=Pose*90; desiredAngle=angle; }
        }
        if(!moving&&!Solved&&Front.Passed&&Side.Passed) {
            passTime+=Time.deltaTime;
            if(passTime>.45f) { Solved=true; if(audioSource) audioSource.PlayOneShot(solveClip,.28f); }
        } else if(!Solved) passTime=0;
    }
    // Stop dots for the selected piece along its free axis.
    void UpdateMarker() {
        if(!Marker) return;
        bool show=ActiveRoom&&RoomIndex==0&&InspectingCourtyard&&!(Journey&&Journey.Travelling)&&!RemoteControls;
        if(show&&BridgeControls&&!Journey.Bridge.Installed) show=false;
        Marker.gameObject.SetActive(show);
        if(!show) return;
        MarkerRing.gameObject.SetActive(false);
        bool bridge=BridgeControls;
        float pulse=1+.12f*Mathf.Sin(Time.unscaledTime*4f);
        // Three stops; the active one is larger.
        Vector3[] stops=new Vector3[3];int active;
        if(bridge) {
            var b=Journey.Bridge;active=b.Slot;
            for(int i=0;i<3;i++) stops[i]=new Vector3(1.15f,.1f,1.23f+(i-1)*.35f);
            MarkerRail.position=new Vector3(1.15f,.09f,1.23f);MarkerRail.localScale=new Vector3(.016f,.006f,.9f);
        } else {
            active=ArtifactSlot;
            for(int i=0;i<3;i++) stops[i]=new Vector3((i-1)*.9f,.88f,-1.15f);
            MarkerRail.position=new Vector3(0,.87f,-1.15f);MarkerRail.localScale=new Vector3(1.9f,.006f,.016f);
            // Depth rail stops (offset seal only).
            if(InspectOffset&&DepthMarker!=null) {
                for(int i=0;i<3&&i<DepthMarker.Length;i++) {
                    DepthMarker[i].gameObject.SetActive(true);DepthMarker[i].position=new Vector3(-1.95f,.88f,(1-i)*.45f);
                    float d=i==ArtifactDepth?.13f*pulse:.07f;DepthMarker[i].localScale=new Vector3(d,.03f,d);
                }
            }
        }
        if(DepthMarker!=null&&!InspectOffset) foreach(var t in DepthMarker) if(t) t.gameObject.SetActive(false);
        Marker.position=Vector3.zero;MarkerRing.gameObject.SetActive(false);
        for(int i=0;i<3&&MarkerDots!=null&&i<MarkerDots.Length;i++) {
            MarkerDots[i].position=stops[i];
            float size=i==active?.13f*pulse:.07f;MarkerDots[i].localScale=new Vector3(size,.03f,size);
        }
    }
    void AnimateDoor() {
        doorOpen=Mathf.MoveTowards(doorOpen,Solved?1:0,Time.deltaTime*.65f);
        float distance=Mathf.SmoothStep(0,1.2f,doorOpen);
        DoorLeft.localPosition=leftStart+Vector3.left*distance;
        DoorRight.localPosition=rightStart+Vector3.right*distance;
    }
    void UpdateView() {
        Quaternion rotation=Quaternion.Euler(elevation,orbit,0);
        ViewTarget=transform.parent.position+new Vector3(0,1.35f,.3f);
        if(InspectingCourtyard) ViewTarget+=Vector3.right*.35f;
        if(RemoteControls) ViewTarget=Journey.Relay.Wheel.position;
        // Keep the room in frame when the view is narrow.
        float pull=Mathf.Clamp(2.45f/Mathf.Max(.5f,viewAspect),1f,1.45f);
        ViewPosition=ViewTarget+rotation*new Vector3(0,0,(RemoteControls?-3.8f:(RoomIndex==0?-6.8f:-7.8f))*pull*(RemoteControls?1f:zoom));
    }
    static AudioClip Tone(float hz,float duration) {
        int count=(int)(44100*duration); float[] data=new float[count];
        for(int i=0;i<count;i++) {
            float t=(float)i/44100; float envelope=Mathf.Sin(Mathf.PI*i/count)*Mathf.Exp(-t*5);
            data[i]=(Mathf.Sin(2*Mathf.PI*hz*t)+.25f*Mathf.Sin(4*Mathf.PI*hz*t))*envelope*.3f;
        }
        AudioClip clip=AudioClip.Create("Mechanism tone",count,1,44100,false); clip.SetData(data,0);return clip;
    }
    static Texture2D Solid(Color color) { var t=new Texture2D(1,1);t.SetPixel(0,0,color);t.Apply();return t; }
    void PrepareUI() {
        if(font) return;
        font=Resources.Load<Font>("TwinViewFont");
        if(!font) font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","SimHei","Arial"},22);
        paper=Solid(new Color(.91f,.88f,.80f)); panel=Solid(new Color(.055f,.11f,.12f,.97f));
        accent=Solid(new Color(.19f,.32f,.31f));hover=Solid(new Color(.30f,.45f,.41f));
        body=new GUIStyle(GUI.skin.label){font=font,fontSize=27,wordWrap=true,padding=new RectOffset(),margin=new RectOffset(),fixedHeight=0,fixedWidth=0,contentOffset=Vector2.zero};body.normal.textColor=new Color(.88f,.85f,.75f);
        small=new GUIStyle(body){fontSize=21};
        title=new GUIStyle(body){fontSize=52,fontStyle=FontStyle.Bold,wordWrap=false};
        large=new GUIStyle(body){fontSize=36,fontStyle=FontStyle.Bold,wordWrap=false};
        status=new GUIStyle(body){fontSize=22,alignment=TextAnchor.MiddleRight};
        button=new GUIStyle(GUI.skin.button){font=font,fontSize=24,alignment=TextAnchor.MiddleCenter,wordWrap=false,padding=new RectOffset(8,8,4,4),fixedHeight=0,contentOffset=new Vector2(0,-3)};
        brassBar=Solid(new Color(.30f,.25f,.14f));cyanSwatch=Solid(new Color(45,76,75));brownSwatch=Solid(new Color(132,116,86));
        bodyBright=new GUIStyle(body);bodyBright.normal.textColor=new Color(.97f,.94f,.86f);
        button.normal.background=accent;button.hover.background=hover;button.active.background=hover;
        button.normal.textColor=new Color(.94f,.9f,.79f);button.hover.textColor=Color.white;button.active.textColor=Color.white;
        buttonLabel=new GUIStyle(body){fontSize=26,alignment=TextAnchor.MiddleCenter,wordWrap=false,contentOffset=new Vector2(0,-2)};
        buttonLabel.normal.textColor=new Color(.94f,.9f,.79f);
        caption=new GUIStyle(body){fontSize=23,wordWrap=false};
        chip=new GUIStyle(button){fontSize=20,contentOffset=new Vector2(0,-1)};
        buttonKey=new GUIStyle(body){fontSize=20,alignment=TextAnchor.MiddleRight,wordWrap=false};
        buttonKey.normal.textColor=new Color(.62f,.66f,.60f);
        currentButton=new GUIStyle(button);
        var currentBg=Solid(new Color(.42f,.34f,.18f));currentButton.normal.background=currentBg;currentButton.hover.background=currentBg;currentButton.active.background=currentBg;
        currentButton.normal.textColor=new Color(1f,.94f,.78f);
    }
    // Button with its shortcut drawn quieter at the right.
    bool KeyButton(Rect r,string label,string key,bool current=false) {
        bool hit=GUI.Button(r,GUIContent.none,current?currentButton:button);
        var labelRect=new Rect(r.x,r.y,r.width,r.height);
        GUI.Label(labelRect,label,buttonLabel);
        if(key.Length>0) DrawText(new Rect(r.xMax-40,r.y,34,r.height),key,buttonKey);
        return hit;
    }

    void OnGUI() {
        if(!initialized||!ActiveRoom||(Journey&&!Journey.Started)) return;
        PrepareUI();
        float uiScale=CanvasScale,canvasH=CanvasHeight;
        Matrix4x4 old=GUI.matrix;GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-1600*uiScale)*.5f,(Screen.height-canvasH*uiScale)*.5f,0),Quaternion.identity,new Vector3(uiScale,uiScale,1));
        var relay=Journey.Relay;
        float panelTop=canvasH-20-BottomPanelHeight,fullW=LW;

        GUI.DrawTexture(new Rect(M,20,LW,184),panel);
        DrawNotebook(M,212,LW);
        GUI.DrawTexture(new Rect(RX,20,RW,canvasH-40),panel);
        GUI.DrawTexture(new Rect(M,panelTop,LW,BottomPanelHeight),panel);

        // Header: small label, title, current objective on its own line
        DrawText(new Rect(M+20,26,320,24),"TWIN VIEW  /  机关匣 · 第一章",small);
        // CalcSize under-reports this CJK font and clips the last glyph.
        float bh=46f,bgap=10f,w1=176f,w2=176f,w3=176f,w4=RoomIndex==1?300f:216f;
        float bx=M+LW-16-(w1+w2+w3+w4+bgap*3);
        GUI.enabled=Journey && !Journey.Travelling;
        if(GUI.Button(new Rect(bx,24,w1,bh),InspectRelay?"退出联动":"跨房传动",InspectRelay?currentButton:button)) InspectRelayStation(!InspectRelay);
        bx+=w1+bgap;
        if(Alcove&&GUI.Button(new Rect(bx,24,w2,bh),InspectAlcove||InspectOffset?"查看大门":"查看石龛",(InspectAlcove||InspectOffset)?currentButton:button)) Inspect(!(InspectAlcove||InspectOffset));
        bx+=w2+bgap;
        if(GUI.Button(new Rect(bx,24,w3,bh),RoomIndex==0?"中庭 · 当前":"返回中庭",RoomIndex==0?currentButton:button)) Journey.Visit(0);
        bx+=w3+bgap;
        GUI.enabled=Journey && Journey.Rooms[0].Solved && !Journey.Travelling;
        if(GUI.Button(new Rect(bx,24,w4,bh),RoomIndex==1?"借影室 · 当前":"前往借影室",RoomIndex==1?currentButton:button)) Journey.Visit(1);
        GUI.enabled=true;

        DrawText(new Rect(M+20,58,LW-40,58),InspectGate?(Gate.Solved?"两物一印 · 门洞已正":"中庭 · 门洞印"):InspectOffset?(Offset.Solved?"一线之差 · 偏位已正":"中庭 · 偏位印"):InspectRelay?(relay.Solved?"两室同心 · 终印已开":(RemoteControls?"借影室 · 跨房传动":"中庭 · 联动终印")):(InspectAlcove?(Alcove.Found?"留给后来者的星光":"中庭 · 无名石龛"):(RoomIndex==0?"一物两面":(Solved?"房间也是道具":"借影室"))),title);
        string description=InspectAlcove?(Alcove.Found?"石龛里藏着一枚星纹。原来边框也能成为封印的一部分。":"旧刻纹旁写着：不必独自成为完整的形状。"):(RoomIndex==0?(Solved?"双锁已合。穿过石门，前往借影室。":"转动折脊瑞兽，让两扇机关窗同时亮起。"):(Solved?"房间里还有一组铜齿轮，它通向中庭的石框。":"封印似乎缺了一块。留意房间里带刻纹的石件。"));
        if(InspectGate) description=Gate.Solved?"洞里的形状对上了。门洞就是取景框，洞外不算数。":(DepthUnlocked?"门洞石在一旁，它的孔必须保持透光。瑞兽站在哪里才能补上封印、又不挡住光？":"孔要留光，封印要填满。可瑞兽现在只能左右移动——深度轨还锁着。");
        else if(InspectOffset) description=Offset.Solved?"夹具让瑞兽能沿深度前后滑动。原来保住一面，只需沿另一条轴移动。":(DepthUnlocked?"夹具已装上，瑞兽现在能前后滑动。正窗只看左右和朝向，侧窗只看前后和朝向。":"瑞兽只能左右移动和转向，印记的侧面却总差一格。底座上的深度轨还没有夹具。");
        else if(InspectRelay) description=RemoteControls?"齿轮推动中庭石框。观察远端变化，再回中庭调整瑞兽。":"石框由借影室齿轮控制。在这里移动与旋转瑞兽，让终印同时成立。";
        string activeHint=HintText();
        if(activeHint!=null) {
            GUI.DrawTexture(new Rect(M+16,130,LW-32,42),brassBar);
            DrawText(new Rect(M+26,130,LW-52,42),activeHint,bodyBright);
        } else DrawText(new Rect(M+20,132,LW-40,40),description,bodyBright);

        // Share leftover height between the three gaps, capped.
        const float blockH=286f;
        float winGap=Mathf.Clamp((canvasH-40f-56f-blockH*2f)/3f,18f,150f);
        float stackTop=20f;
        DrawLegend(RX+16,stackTop,RW-32);
        float winTop=stackTop+56f+winGap;
        DrawWindow(RX+16,winTop,RW-32,InspectGate?"门洞印 · 正窗":InspectOffset?"偏位印 · 正窗":InspectRelay?"终印 · 正窗":(InspectAlcove?"石龛 · 正窗":"正窗"),InspectGate?Gate.Front:InspectOffset?Offset.Front:InspectRelay?relay.Front:(InspectAlcove?Alcove.Front:Front));
        DrawWindow(RX+16,winTop+blockH+winGap,RW-32,InspectGate?"门洞印 · 侧窗":InspectOffset?"偏位印 · 侧窗":InspectRelay?"终印 · 侧窗":(InspectAlcove?"石龛 · 侧窗":"侧窗"),InspectGate?Gate.Side:InspectOffset?Offset.Side:InspectRelay?relay.Side:(InspectAlcove?Alcove.Side:Side));

        if(RemoteControls) {
            GUI.DrawTexture(new Rect(580,256,544,panelTop-266),panel);
            DrawText(new Rect(596,260,510,30),"远端 · 中庭实时画面",small);
            GUI.DrawTexture(new Rect(590,294,524,panelTop-312),relay.Preview,ScaleMode.ScaleToFit);
            DrawText(new Rect(M,260,540,30),"本地 · 齿轮",small);
        }

        // Bottom bar, full width, fixed grid
        float y0=panelTop+14,y1=y0+46,y2=y1+(Row1?64:10),y3=y2+66,y4=y3+70;
        string progress=InspectAlcove?(Alcove.Found?"星纹已收录 · 可以继续试验，发现不会撤回":"旧石龛：齿轮归零可恢复原布局 · 当前档位 "+relay.Setting):(Solved?"通路已永久开启 · 复位只还原本房布局":(Front.Passed?"正窗已经满足。另一侧，还少了什么？":"观察两扇窗，再决定如何补全封印。"));
        if(InspectGate) progress=Gate.Solved?"门洞印已打开":"门洞必须透光，封印必须被填满 · 两件物体一起投影"+(DepthUnlocked?"":" · 深度轨锁定，先打开终印");
        else if(InspectOffset) progress=Offset.Solved?"偏位印已打开 · 深度 "+(ArtifactDepth+1)+" / 3":(DepthUnlocked?"深度 "+(ArtifactDepth+1)+" / 3 · W 向前 S 向后":"深度轨锁定 · 打开终印后获得夹具");
        else if(InspectRelay) progress=relay.Solved?"终印已永久打开 · 可以继续探索另一种解法":"石框档位 "+relay.Setting+" / 3 · 3 为收框，可空出桥块安装位";
        if(Time.unscaledTime<noticeUntil) progress=notice;
        DrawText(new Rect(M+20,y0,LW-40,36),progress,bodyBright);

        bool bridgePanel=RoomIndex==0&&InspectRelay;
        if(Row1) {
            if(bridgePanel) {
                GUI.enabled=!moving&&!relay.Moving&&!Journey.Travelling;
                if(KeyButton(new Rect(M+20,y1,200,bh),BridgeSelected?"操作瑞兽":"瑞兽 · 当前","T",!BridgeSelected)) SelectBridge(false);
                GUI.enabled=GUI.enabled&&Journey.Bridge.Acquired;
                if(KeyButton(new Rect(M+232,y1,200,bh),BridgeSelected?"桥块 · 当前":"操作桥块","T",BridgeSelected)) SelectBridge(true);
                GUI.enabled=GUI.enabled&&(Journey.Bridge.Installed||relay.Setting==3);
                if(KeyButton(new Rect(M+444,y1,240,bh),Journey.Bridge.Installed?"收回桥块":"安装桥块","I")) ToggleBridge();
                GUI.enabled=true;
                DrawText(new Rect(M+700,y1,fullW-720,bh),!Journey.Bridge.Acquired?"桥块位于借影室":(Journey.Bridge.Installed?"桥块占用槽位 · 收回后才能升框":(relay.Setting==3?"安装后可旋转、沿深度移动":"先把借影室齿轮拨到 3 档")),small);
            } else if(RoomIndex==1) {
                GUI.enabled=Solved&&!Journey.Bridge.Acquired&&!Journey.Travelling;
                if(KeyButton(new Rect(M+fullW-20-300,y1,300,46),Journey.Bridge.Acquired?"桥块已携带":(Solved?"取得桥块":"解开本室后取得桥块"),"I")) Journey.Bridge.Collect();
                GUI.enabled=true;
                DrawText(new Rect(M+20,y1,fullW-340,46),Solved?"房间里还藏着一个不同的封印。":"先把两扇窗对齐，再找找别的东西。",small);
            }
        }

        GUI.enabled=!moving && (!Journey || !Journey.Travelling);
        if(ExtraControls) {
            int slot=RemoteControls?relay.Setting:(BridgeControls?Journey.Bridge.Slot:(InspectingCourtyard?ArtifactSlot:PillarSlot));
            bool free=(!RemoteControls||(!relay.Moving&&!Journey.Bridge.Installed))&&(!BridgeControls||Journey.Bridge.Installed);
            GUI.enabled=GUI.enabled&&slot>0&&free;
            if(KeyButton(new Rect(M+20,y2,260,50),RemoteControls?"齿轮退一档":(BridgeControls?"桥块向前":(InspectingCourtyard?"瑞兽移左座":"石柱左移")),"A")) MoveSelected(-1);
            GUI.enabled=!moving&&slot<(RemoteControls?3:2)&&(!Journey||!Journey.Travelling)&&free;
            if(KeyButton(new Rect(M+292,y2,260,50),RemoteControls?"齿轮进一档":(BridgeControls?"桥块向后":(InspectingCourtyard?"瑞兽移右座":"石柱右移")),"D")) MoveSelected(1);
            if(InspectOffset) {
                GUI.enabled=!moving&&DepthUnlocked&&ArtifactDepth<2&&!Journey.Travelling;
                if(KeyButton(new Rect(M+564,y2,240,50),"向前滑动","W")) MoveDepth(1);
                GUI.enabled=!moving&&DepthUnlocked&&ArtifactDepth>0&&!Journey.Travelling;
                if(KeyButton(new Rect(M+816,y2,240,50),"向后滑动","S")) MoveDepth(-1);
                GUI.enabled=true;
            } else
            DrawText(new Rect(M+576,y2,fullW-596,50),RemoteControls?(Journey.Bridge.Installed?"桥块占位，请先回中庭收回":"0 原位 · 1/2 后推 · 3 收框"):(BridgeControls?"桥块深度 "+(slot+1)+" / 3 · 留意另一面的支脚":"位置 "+(slot+1)+" / 3 · 按 A/D 移动所选物件"),small);
        }

        GUI.enabled=!moving&&!RemoteControls&&(!BridgeControls||Journey.Bridge.Installed)&&(!Journey||!Journey.Travelling);
        if(KeyButton(new Rect(M+20,y3,190,58),"左转","Q")) Turn(-1);
        if(KeyButton(new Rect(M+220,y3,190,58),"右转","E")) Turn(1);
        GUI.enabled=!moving&&(RemoteControls?(relay.CanUndo&&!Journey.Bridge.Installed):(history.Count>0&&!(RoomIndex==0&&history.Peek().bridgeInstalled&&relay.Setting!=3)))&&(!Journey||!Journey.Travelling);
        if(KeyButton(new Rect(M+420,y3,160,58),"撤销","Z")) Undo();
        GUI.enabled=!Journey||!Journey.Travelling;
        if(KeyButton(new Rect(M+590,y3,160,58),"复位","R")) ResetPuzzle();
        if(KeyButton(new Rect(M+760,y3,160,58),"线索","H")) ShowHint();
        DrawText(new Rect(M+LW-166,y3+13,150,40),RemoteControls?"远端同步":moves+" 步操作",small);
        DrawText(new Rect(M+20,y4,fullW-40,22),"右键环顾 · 滚轮缩放 · 1/2 前往房间 · Tab 切换视图 · Esc 退出",small);
        GUI.matrix=old;
    }
    // Canvas follows the window; panels anchor to edges so any aspect fits.
    static float CanvasHeight { get { return Mathf.Clamp(1600f*Screen.height/Mathf.Max(1,Screen.width),900f,1300f); } }
    static float CanvasScale { get { return Mathf.Min(Screen.width/1600f,Screen.height/CanvasHeight); } }
    // c is in canvas pixels measured from the top-left; the result is a normalized screen rect.
    static Rect CanvasViewport(Rect c) {
        float k=CanvasScale,h=CanvasHeight;
        float ox=(Screen.width-1600*k)*.5f,oy=(Screen.height-h*k)*.5f;
        float yTop=oy+c.y*k,w=c.width*k,ht=c.height*k;
        return new Rect((ox+c.x*k)/Screen.width,(Screen.height-yTop-ht)/Screen.height,w/Screen.width,ht/Screen.height);
    }
    // Notebook: lists mechanisms, jumps with the same calls as the buttons.
    struct Entry { public string name, where; public bool done, reachable, isRoom; public int room, view; }
    // view: 0 door/borrow room, 1 alcove, 2 final seal, 3 offset seal
    Entry[] NotebookEntries() {
        var r0=Journey.Rooms[0];var r1=Journey.Rooms[1];var relay=Journey.Relay;
        bool open1=r0.Solved;
        return new[]{
            new Entry{name="中庭",where="房间",done=true,reachable=true,room=0,view=0,isRoom=true},
            new Entry{name="借影室",where="房间",done=true,reachable=open1,room=1,view=0,isRoom=true},
            new Entry{name="大门",where="中庭",done=r0.Solved,reachable=true,room=0,view=0},
            new Entry{name="石龛",where="中庭 · Tab",done=r0.Alcove&&r0.Alcove.Found,reachable=true,room=0,view=1},
            new Entry{name="终印",where="中庭 · 借影室齿轮",done=relay.Solved,reachable=true,room=0,view=2},
            new Entry{name="偏位",where="中庭 · Tab",done=r0.Offset&&r0.Offset.Solved,reachable=true,room=0,view=3},
            new Entry{name="门洞",where="中庭 · Tab",done=r0.Gate&&r0.Gate.Solved,reachable=true,room=0,view=4},
            new Entry{name="暗记",where="借影室",done=r1.PillarAlcove&&r1.PillarAlcove.Found,reachable=open1,room=1,view=0},
        };
    }
    public void JumpToEntry(int index) {
        var entries=NotebookEntries();if(index<0||index>=entries.Length) return;
        var e=entries[index];
        if(Journey.Travelling||!e.reachable) return;
        if(Journey.Current!=e.room&&!Journey.Visit(e.room)) return;
        if(e.isRoom) return; // room chips only travel
        var target=Journey.Rooms[e.room];
        if(e.room==0) {
            if(e.view==0) target.Inspect(false);
            else if(e.view==1) target.Inspect(true);
            else if(e.view==2) target.InspectRelayStation(true);
            else if(e.view==3) target.InspectOffsetSeal(true);
            else if(e.view==4) target.InspectGateSeal(true);
        } else target.InspectRelayStation(false);
    }
    public int NotebookSolvedCount() {int n=0;foreach(var e in NotebookEntries()) if(!e.isRoom&&e.done) n++;return n;}
    void DrawNotebook(float x,float y,float w) {
        var entries=NotebookEntries();
        GUI.DrawTexture(new Rect(x,y,w,48),panel);
        DrawText(new Rect(x+12,y+2,108,44),"机关簿 "+NotebookSolvedCount()+"/6",small);
        float cx=x+124,cw=(w-124-8)/entries.Length;
        for(int i=0;i<entries.Length;i++) {
            var e=entries[i];
            GUI.enabled=e.reachable&&!Journey.Travelling;
            bool here=Journey.Current==e.room&&((e.room==0&&((e.view==4&&InspectGate)||(e.view==3&&InspectOffset)||(e.view==2&&InspectRelay)||(e.view==1&&InspectAlcove)||(e.view==0&&!InspectOffset&&!InspectRelay&&!InspectAlcove&&!InspectGate)))||(e.room==1&&!InspectRelay));
            if(GUI.Button(new Rect(cx+i*cw,y+6,cw-10,38),(e.done?"●":"○")+e.name+(here?"◂":""),chip)) JumpToEntry(i);
        }
        GUI.enabled=true;
    }
    string HintText() {
        var relay=Journey.Relay;
        int shownHint=InspectGate?gateHint:InspectOffset?offsetHint:InspectRelay?relayHint:(InspectAlcove?alcoveHint:hint);
        if(shownHint>0&&(InspectGate?!Gate.Solved:InspectOffset?!Offset.Solved:InspectRelay?!relay.Solved:(InspectAlcove?!Alcove.Found:!Solved))) {
            string[] hints=InspectAlcove?new[]{"","石龛旁的框架有与瑞兽相同的刻纹。","不用搬走边框，试着把瑞兽移到它的旁边。","侧窗先找合适的朝向，再让正窗中的轮廓接合。"}:(RoomIndex==0?new[]{"","两扇窗看的是同一件瑞兽，分别亮起还不够。","正反轮廓很像，侧面的阶梯却会改变朝向。","瑞兽的前后看起来相似，哪一面不同？边转边看两扇窗。"}:new[]{"","机关读取的是轮廓，没有要求它来自同一件物体。","移动石柱时，哪一扇窗改变了？先保住另一扇窗。","先转动瑞兽满足侧窗，再移动石柱补全正窗。"});
            if(InspectGate) hints=new[]{"","门洞石也是投影的一部分，它的孔要留空。","瑞兽站在门洞正前方会挡住光。试着移到旁边。","先看正窗：孔亮了，封印还缺哪一块？再看侧窗要什么朝向。"};
            else if(InspectOffset) hints=new[]{"","两扇窗各自满足什么？先看哪一扇亮了，哪一扇没亮。","正窗只看左右和朝向，侧窗只看前后和朝向。","先让两扇窗里朝向对的那一扇保持住，再想另一扇还差哪条轴。"};
            else if(InspectRelay) hints=new[]{"","铜轴通向另一间房。拨动齿轮时，留意中庭石框。","石框前后移动，正窗不变，侧窗位置会改变。","先在借影室选合适的石框档位，再回中庭调整瑞兽。"};
            if(InspectRelay&&Journey.Bridge.Acquired) hints=new[]{"","可以借石框，也可以收框后用桥块代替它。","桥块多了一条支脚，看看它能否进入已有的遮影。","先调整瑞兽，再选桥块改变朝向和深度；两面都要满足。"};
            return "线索 "+shownHint+" / 3 · "+hints[shownHint];
        }
        return null;
    }
    // Fit CJK text with the real font before drawing.
    static void DrawText(Rect rect,string text,GUIStyle source) {
        var content=new GUIContent(text);GUIStyle style=source;
        if(source.CalcHeight(content,rect.width)>rect.height-8||(!source.wordWrap&&source.CalcSize(content).x>rect.width)) {
            style=new GUIStyle(source);
            while(style.fontSize>12&&(style.CalcHeight(content,rect.width)>rect.height-8||(!style.wordWrap&&style.CalcSize(content).x>rect.width))) style.fontSize--;
        }
        rect.y+=4;rect.height-=8;
        GUI.Label(rect,content,style);
    }
    void DrawLegend(float x,float y,float w) {
        GUI.DrawTexture(new Rect(x,y,18,18),paper);
        GUI.DrawTexture(new Rect(x+2,y+2,14,14),cyanSwatch);
        DrawText(new Rect(x+26,y-4,190,28),"此刻的影子",caption);
        GUI.DrawTexture(new Rect(x+190,y,18,18),paper);
        GUI.DrawTexture(new Rect(x+192,y+2,14,14),brownSwatch);
        DrawText(new Rect(x+216,y-4,200,28),"目标轮廓",caption);
        DrawText(new Rect(x,y+26,w,28),"把两边的形状对齐，其余区域不限。",caption);
    }
    /// <summary>
    /// Current silhouette beside the target silhouette; nothing marks the difference.
    /// </summary>
    void DrawWindow(float x,float y,float w,string name,ProjectionRule rule) {
        DrawText(new Rect(x,y,w-220,32),name,large);
        DrawText(new Rect(x+w-220,y+2,220,30),rule.Passed?"● 已对齐":("○ 未对齐 · 形似 "+Mathf.RoundToInt(rule.Match*100f)+"%"),status);
        float imgW=Mathf.Min((w-20)/2f,196f);
        for(int i=0;i<2;i++) {
            float ix=x+i*(imgW+20);
            GUI.DrawTexture(new Rect(ix-4,y+34,imgW+8,imgW+8),accent);
            GUI.DrawTexture(new Rect(ix,y+38,imgW,imgW),paper);
            GUI.DrawTexture(new Rect(ix,y+38,imgW,imgW),i==0?rule.Display:rule.Target,ScaleMode.ScaleToFit);
            DrawText(new Rect(ix,y+38+imgW+4,imgW,28),i==0?"此刻的影子":"目标轮廓",caption);
        }
    }
}
}
