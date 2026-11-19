using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace TwinView {
// Both rooms coexist in one scene. Travelling follows the open doorway and corridor.
public sealed class JourneyController : MonoBehaviour {
    public ChamberController[] Rooms;
    public Camera ViewCamera;
    public RoomRelay Relay;
    public BridgePiece Bridge;
    public int Current { get; private set; }
    public bool Travelling { get; private set; }
    public bool Started=true, EndingVisible; // set by GameShell; both default to a plain running game
    public GameShell Shell;
    Vector3 travelStart, startLook, currentLook;
    float travelTime;

    void Start() {
        foreach(var room in Rooms) { room.Journey=this;room.Initialize();room.ActiveRoom=false; }
        Rooms[0].Alcove.Initialize();
        if(Rooms[0].Offset) Rooms[0].Offset.Initialize();
        if(Rooms[0].Gate) Rooms[0].Gate.Initialize();
        if(Rooms[1].PillarAlcove) Rooms[1].PillarAlcove.Initialize();
        Relay.Initialize();
        Bridge.Apply(false,0,BridgePiece.StartDepth);
        Rooms[0].ActiveRoom=true;Current=0;
        ViewCamera.transform.position=Rooms[0].ViewPosition;
        currentLook=Rooms[0].ViewTarget;ViewCamera.transform.LookAt(currentLook);
        if(Array.IndexOf(Environment.GetCommandLineArgs(),"--qa")>=0) StartCoroutine(QA());
    }
    public bool Visit(int index) {
        if(index<0||index>=Rooms.Length||index==Current||Travelling) return false;
        if(index>0&&!Rooms[0].Solved) return false;
        travelStart=ViewCamera.transform.position;startLook=currentLook;travelTime=0;
        Rooms[Current].ActiveRoom=false;Current=index;Rooms[Current].ActiveRoom=true;Travelling=true;
        return true;
    }
    void LateUpdate() {
        var target=Rooms[Current];
        if(Travelling) {
            travelTime+=Time.deltaTime;
            float t=Mathf.SmoothStep(0,1,Mathf.Clamp01(travelTime/1.5f));
            ViewCamera.transform.position=Vector3.Lerp(travelStart,target.ViewPosition,t)+Vector3.up*(Mathf.Sin(Mathf.PI*t)*2.5f);
            currentLook=Vector3.Lerp(startLook,target.ViewTarget,t);
            if(t>=1) Travelling=false;
        } else { ViewCamera.transform.position=target.ViewPosition;currentLook=target.ViewTarget; }
        // Cut away the previous room after travelling so its walls never obscure the puzzle.
        ViewCamera.cullingMask=Travelling?~0:((1<<(8+Current))|(1<<(10+Current))|1|(Current==0?(1<<12)|(1<<13)|(1<<14):0));
        ViewCamera.transform.LookAt(currentLook);
    }

    IEnumerator QA() {
        string[] args=Environment.GetCommandLineArgs();int idx=Array.IndexOf(args,"--qa-output");
        string directory=idx>=0&&idx+1<args.Length?args[idx+1]:Application.persistentDataPath;
        Directory.CreateDirectory(directory);
        if(Array.IndexOf(args,"--qa-notebook")>=0) {yield return NotebookQA(directory);yield break;}
        if(Array.IndexOf(args,"--qa-camera")>=0) {yield return CameraQA(directory);yield break;}
        if(Array.IndexOf(args,"--qa-title")>=0) {yield return TitleQA(directory);yield break;}
        if(Array.IndexOf(args,"--qa-bridge")>=0) {yield return BridgeQA(directory);yield break;}
        if(Array.IndexOf(args,"--qa-offset")>=0) {yield return OffsetQA(directory);yield break;}
        var result=new Result();
        if(Array.IndexOf(args,"--qa-early")>=0) {
            Rooms[0].Inspect(true);Rooms[0].ShowHint();yield return new WaitForSeconds(.8f);
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,"early-alcove-hint.png"));yield return new WaitForEndOfFrame();
            Rooms[0].Turn(-1);yield return new WaitForSeconds(.65f);
            Rooms[0].SlideArtifact(1);yield return new WaitForSeconds(2);
            var early=new EarlyResult {found=Rooms[0].Alcove.Found,courtyardUnsolved=!Rooms[0].Solved,borrowRoomUnsolved=!Rooms[1].Solved};
            early.passed=early.found&&early.courtyardUnsolved&&early.borrowRoomUnsolved;
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,"early-alcove.png"));yield return new WaitForEndOfFrame();
            File.WriteAllText(Path.Combine(directory,"early-result.json"),JsonUtility.ToJson(early,true));
            Application.Quit(early.passed?0:1);yield break;
        }
        result.lockedBeforeSolve=!Visit(1);
        yield return new WaitForSeconds(1);
        ScreenCapture.CaptureScreenshot(Path.Combine(directory,"01-courtyard.png"));
        yield return new WaitForEndOfFrame();
        Rooms[0].Turn(1);yield return new WaitForSeconds(.65f);
        Rooms[0].Turn(1);yield return new WaitForSeconds(2);
        result.firstSolved=Rooms[0].Solved;
        result.enteredSecond=Visit(1);yield return new WaitForSeconds(2);
        ScreenCapture.CaptureScreenshot(Path.Combine(directory,"02-borrow-start.png"));
        yield return new WaitForEndOfFrame();
        Rooms[1].MovePillar(1);yield return new WaitForSeconds(.65f);
        Rooms[1].Undo();yield return new WaitForSeconds(.65f);
        result.undoRestored=Rooms[1].PillarSlot==0&&Rooms[1].Pose==2&&Rooms[1].MoveCount==0;
        Rooms[1].Turn(1);yield return new WaitForSeconds(.65f);
        Rooms[1].Turn(1);yield return new WaitForSeconds(.65f);
        Rooms[1].MovePillar(1);yield return new WaitForSeconds(.65f);
        Rooms[1].MovePillar(1);yield return new WaitForSeconds(2);
        result.secondSolved=Rooms[1].Solved&&Rooms[1].Front.Passed&&Rooms[1].Side.Passed;
        ScreenCapture.CaptureScreenshot(Path.Combine(directory,"03-borrow-solved.png"));
        yield return new WaitForSeconds(.5f);
        Rooms[1].ResetPuzzle();yield return new WaitForSeconds(.5f);
        result.resetKeepsRoute=Rooms[1].Solved&&Rooms[1].PillarSlot==0&&Rooms[1].Pose==2;
        result.returnedToFirst=Visit(0);yield return new WaitForSeconds(2);
        Rooms[0].ResetPuzzle();yield return new WaitForSeconds(.5f);
        result.firstRouteRetained=Rooms[0].Solved;
        result.reenteredSecond=Visit(1);yield return new WaitForSeconds(2);
        result.secondStateRetained=Rooms[1].PillarSlot==0&&Rooms[1].Pose==2&&Rooms[1].Solved;
        Visit(0);yield return new WaitForSeconds(2);Rooms[0].Inspect(true);
        Rooms[0].SlideArtifact(1);yield return new WaitForSeconds(.65f);
        Rooms[0].Undo();yield return new WaitForSeconds(.65f);
        result.socketUndo=Rooms[0].ArtifactSlot==1;
        Rooms[0].Turn(-1);yield return new WaitForSeconds(.65f);
        Rooms[0].SlideArtifact(1);yield return new WaitForSeconds(2);
        result.returnKnowledgeUnlock=Rooms[0].Alcove.Found;
        ScreenCapture.CaptureScreenshot(Path.Combine(directory,"04-alcove-found.png"));yield return new WaitForEndOfFrame();
        int[,] sizes={{1280,720},{1024,768},{1920,1080}};
        for(int i=0;i<3;i++) {
            Screen.SetResolution(sizes[i,0],sizes[i,1],false);yield return new WaitForSeconds(.7f);
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,"ui-"+sizes[i,0]+"x"+sizes[i,1]+".png"));yield return new WaitForEndOfFrame();
        }
        Rooms[0].ResetPuzzle();yield return new WaitForSeconds(.5f);result.discoveryRetained=Rooms[0].Alcove.Found;
        result.localRelayBlocked=!Relay.Shift(1);
        Visit(1);yield return new WaitForSeconds(2);Rooms[1].InspectRelayStation(true);
        var alcove=Rooms[0].Alcove;
        var frameStart=alcove.Frame.localPosition;
        bool[] frontBefore=Relay.Front.Read(alcove.FrontCamera,Rooms[0].MaskShader);
        bool[] sideBefore=Relay.Side.Read(alcove.SideCamera,Rooms[0].MaskShader);
        Relay.Shift(1);yield return new WaitForSeconds(.9f);
        result.remoteGeometryMoved=Vector3.Distance(alcove.Frame.localPosition,frameStart+Vector3.forward*.45f)<.001f;
        result.frontPreserved=SameMask(frontBefore,Relay.Front.Read(alcove.FrontCamera,Rooms[0].MaskShader));
        result.sideChanged=!SameMask(sideBefore,Relay.Side.Read(alcove.SideCamera,Rooms[0].MaskShader));
        Relay.Undo();yield return new WaitForSeconds(.9f);
        result.relayUndo=Relay.Setting==0&&Vector3.Distance(alcove.Frame.localPosition,frameStart)<.001f;
        Relay.Shift(1);yield return new WaitForSeconds(.9f);
        Rooms[1].ResetPuzzle();yield return new WaitForSeconds(.2f);result.localResetKeepsRelay=Relay.Setting==1;
        Screen.SetResolution(1280,720,false);yield return new WaitForSeconds(.7f);
        ScreenCapture.CaptureScreenshot(Path.Combine(directory,"05-remote-preview.png"));yield return new WaitForEndOfFrame();
        Visit(0);yield return new WaitForSeconds(2);Rooms[0].InspectRelayStation(true);
        result.relayPersistsOnReturn=Relay.Setting==1;
        Rooms[0].Turn(1);yield return new WaitForSeconds(.65f);
        Rooms[0].Turn(1);yield return new WaitForSeconds(.65f);
        Rooms[0].SlideArtifact(1);yield return new WaitForSeconds(2);
        result.relaySolved=Relay.Solved&&Relay.Front.Passed&&Relay.Side.Passed;
        ScreenCapture.CaptureScreenshot(Path.Combine(directory,"06-relay-solved.png"));yield return new WaitForEndOfFrame();
        Visit(1);yield return new WaitForSeconds(2);Relay.Shift(1);yield return new WaitForSeconds(.9f);
        result.wrongGearRejected=!(Relay.Front.Passed&&Relay.Side.Passed)&&Relay.Solved;
        Relay.Undo();yield return new WaitForSeconds(.9f);Relay.Shift(-1);yield return new WaitForSeconds(.9f);
        Visit(0);yield return new WaitForSeconds(2);Rooms[0].Inspect(true);Rooms[0].Turn(1);yield return new WaitForSeconds(.9f);
        result.oldAlcoveRestored=Relay.Setting==0&&alcove.Front.Passed&&alcove.Side.Passed;
        result.routesRemainOpen=Rooms[0].Solved&&Rooms[1].Solved&&Relay.Solved&&alcove.Found;
        result.passed=result.lockedBeforeSolve&&result.firstSolved&&result.enteredSecond&&result.undoRestored&&result.secondSolved&&result.resetKeepsRoute&&result.returnedToFirst&&result.firstRouteRetained&&result.reenteredSecond&&result.secondStateRetained&&result.socketUndo&&result.returnKnowledgeUnlock&&result.discoveryRetained;
        result.passed &= result.localRelayBlocked&&result.remoteGeometryMoved&&result.frontPreserved&&result.sideChanged&&result.relayUndo&&result.localResetKeepsRelay&&result.relayPersistsOnReturn&&result.relaySolved&&result.wrongGearRejected&&result.oldAlcoveRestored&&result.routesRemainOpen;
        File.WriteAllText(Path.Combine(directory,"runtime-result.json"),JsonUtility.ToJson(result,true));
        Application.Quit(result.passed?0:1);
    }
    static bool SameMask(bool[] a,bool[] b) { for(int i=0;i<a.Length;i++) if(a[i]!=b[i]) return false;return true; }
    // Notebook reads state and navigates with the normal calls.
    IEnumerator NotebookQA(string directory) {
        var r=new NotebookResult();var room=Rooms[0];
        yield return new WaitForSeconds(.5f);
        r.startsAtZero=room.NotebookSolvedCount()==0;
        // Locked room: the jump must do nothing.
        room.JumpToEntry(1);yield return new WaitForSeconds(.4f);
        r.lockedJumpRefused=Current==0&&!Travelling;
        // Jumps inside the courtyard switch the view.
        room.JumpToEntry(3);yield return new WaitForSeconds(.3f);r.jumpAlcove=room.InspectAlcove;
        room.JumpToEntry(4);yield return new WaitForSeconds(.3f);r.jumpFinalSeal=room.InspectRelay&&!room.InspectAlcove;
        room.JumpToEntry(5);yield return new WaitForSeconds(.3f);r.jumpOffset=room.InspectOffset&&!room.InspectRelay;
        room.JumpToEntry(2);yield return new WaitForSeconds(.3f);r.jumpDoor=!room.InspectOffset&&!room.InspectRelay&&!room.InspectAlcove;
        ScreenCapture.CaptureScreenshot(Path.Combine(directory,"notebook-start.png"));yield return new WaitForEndOfFrame();
        // Solve the door; the count rises and the borrow room opens.
        room.Turn(1);yield return new WaitForSeconds(.65f);room.Turn(1);yield return new WaitForSeconds(1.4f);
        r.countAfterDoor=room.NotebookSolvedCount()==1;
        room.JumpToEntry(1);yield return new WaitForSeconds(2f);
        r.jumpBorrowRoom=Current==1&&!Travelling;
        ScreenCapture.CaptureScreenshot(Path.Combine(directory,"notebook-borrow.png"));yield return new WaitForEndOfFrame();
        room.JumpToEntry(5);yield return new WaitForSeconds(2f);
        r.jumpBackToCourtyard=Current==0&&room.InspectOffset;
        r.passed=r.startsAtZero&&r.lockedJumpRefused&&r.jumpAlcove&&r.jumpFinalSeal&&r.jumpOffset&&r.jumpDoor&&r.countAfterDoor&&r.jumpBorrowRoom&&r.jumpBackToCourtyard;
        File.WriteAllText(Path.Combine(directory,"notebook-result.json"),JsonUtility.ToJson(r,true));Application.Quit(r.passed?0:1);
    }
    [Serializable] class NotebookResult { public bool passed,startsAtZero,lockedJumpRefused,jumpAlcove,jumpFinalSeal,jumpOffset,jumpDoor,countAfterDoor,jumpBorrowRoom,jumpBackToCourtyard; }
    IEnumerator CameraQA(string directory) {
        var r=new CameraResult();var room=Rooms[0];
        yield return new WaitForSeconds(.5f);
        room.OrbitBy(500);yield return null;r.orbitRightClamped=Mathf.Abs(room.OrbitDegrees-(room.OrbitBaseDegrees+ChamberController.OrbitRight))<.01f;
        room.OrbitBy(-1000);yield return null;r.orbitLeftClamped=Mathf.Abs(room.OrbitDegrees-(room.OrbitBaseDegrees-ChamberController.OrbitLeft))<.01f;
        ScreenCapture.CaptureScreenshot(Path.Combine(directory,"camera-left-limit.png"));yield return new WaitForEndOfFrame();
        room.OrbitBy(1000);yield return new WaitForSeconds(.2f);
        ScreenCapture.CaptureScreenshot(Path.Combine(directory,"camera-right-limit.png"));yield return new WaitForEndOfFrame();
        room.ZoomBy(100);yield return null;r.zoomInLimited=Mathf.Abs(room.Zoom-.62f)<.01f;
        ScreenCapture.CaptureScreenshot(Path.Combine(directory,"camera-zoom-in.png"));yield return new WaitForEndOfFrame();
        room.ZoomBy(-100);yield return null;r.zoomOutLimited=Mathf.Abs(room.Zoom-1.12f)<.01f;
        ScreenCapture.CaptureScreenshot(Path.Combine(directory,"camera-zoom-out.png"));yield return new WaitForEndOfFrame();
        room.InspectOffsetSeal(true);yield return new WaitForSeconds(.3f);
        r.offsetViewFramed=Mathf.Abs(room.Zoom-.72f)<.01f&&Mathf.Abs(room.OrbitBaseDegrees-(-28f))<.01f;
        ScreenCapture.CaptureScreenshot(Path.Combine(directory,"camera-offset-view.png"));yield return new WaitForEndOfFrame();
        room.Inspect(false);yield return new WaitForSeconds(.3f);
        r.mainViewRestored=Mathf.Abs(room.Zoom-1f)<.01f&&Mathf.Abs(room.OrbitBaseDegrees-35f)<.01f;
        r.passed=r.orbitRightClamped&&r.orbitLeftClamped&&r.zoomInLimited&&r.zoomOutLimited&&r.offsetViewFramed&&r.mainViewRestored;
        File.WriteAllText(Path.Combine(directory,"camera-result.json"),JsonUtility.ToJson(r,true));Application.Quit(r.passed?0:1);
    }
    [Serializable] class CameraResult { public bool passed,orbitRightClamped,orbitLeftClamped,zoomInLimited,zoomOutLimited,offsetViewFramed,mainViewRestored; }
    IEnumerator TitleQA(string directory) {
        var r=new TitleResult();
        yield return new WaitForSeconds(1f);
        r.titleShown=!Started&&!EndingVisible&&Shell!=null;
        ScreenCapture.CaptureScreenshot(Path.Combine(directory,"title-1600x900.png"));yield return new WaitForEndOfFrame();
        Screen.SetResolution(1024,768,false);yield return new WaitForSeconds(.8f);
        ScreenCapture.CaptureScreenshot(Path.Combine(directory,"title-1024x768.png"));yield return new WaitForEndOfFrame();
        Shell.Begin();yield return new WaitForSeconds(.6f);
        r.started=Started&&!EndingVisible;
        Screen.SetResolution(1280,720,false);yield return new WaitForSeconds(.8f);
        Shell.ShowEnding();yield return new WaitForSeconds(.6f);
        r.endingShown=EndingVisible;
        ScreenCapture.CaptureScreenshot(Path.Combine(directory,"ending-unsolved-1280x720.png"));yield return new WaitForEndOfFrame();
        Shell.HideEnding();yield return null;
        r.endingHides=!EndingVisible&&Started;
        r.passed=r.titleShown&&r.started&&r.endingShown&&r.endingHides;
        File.WriteAllText(Path.Combine(directory,"title-result.json"),JsonUtility.ToJson(r,true));Application.Quit(r.passed?0:1);
    }
    [Serializable] class TitleResult { public bool passed,titleShown,started,endingShown,endingHides; }
    // Ability lock end to end: locked rail, final seal, offset, undo/reset.
    IEnumerator OffsetQA(string directory) {
        var r=new OffsetResult();var room=Rooms[0];
        // Visible before the ability arrives.
        room.InspectOffsetSeal(true);yield return new WaitForSeconds(.4f);
        r.stationVisibleAtStart=room.InspectOffset;
        // Locked rail: a depth move changes nothing.
        room.MoveDepth(1);yield return new WaitForSeconds(.5f);
        r.lockedAtStart=!room.DepthUnlocked&&room.ArtifactDepth==ChamberController.DefaultDepth;
        r.gateLockedAtStart=!room.Gate.Solved;
        ScreenCapture.CaptureScreenshot(Path.Combine(directory,"offset-locked.png"));yield return new WaitForEndOfFrame();
        // Open the final seal by the stone-frame route.
        room.Inspect(false);
        room.Turn(1);yield return new WaitForSeconds(.65f);room.Turn(1);yield return new WaitForSeconds(1.4f);
        Visit(1);yield return new WaitForSeconds(1.8f);
        Rooms[1].Turn(1);yield return new WaitForSeconds(.65f);Rooms[1].Turn(1);yield return new WaitForSeconds(.65f);
        Rooms[1].MovePillar(1);yield return new WaitForSeconds(.65f);Rooms[1].MovePillar(1);yield return new WaitForSeconds(1.4f);
        Visit(1);Rooms[1].InspectRelayStation(true);Relay.Shift(1);yield return new WaitForSeconds(1.2f);
        Visit(0);yield return new WaitForSeconds(1.8f);room.InspectRelayStation(true);
        room.SlideArtifact(1);yield return new WaitForSeconds(1.6f); // door solved at pose 0; final seal wants pose 0, seat 2, gear 1
        r.finalSealOpen=Relay.Solved&&Relay.Front.Passed&&Relay.Side.Passed;
        r.unlockedAfterSeal=room.DepthUnlocked;
        // Now the station opens. Keys W/S move the guardian along depth.
        room.Inspect(false);room.Inspect(true);
        room.InspectOffsetSeal(true);yield return new WaitForSeconds(.4f);
        r.stationOpens=room.InspectOffset;
        ScreenCapture.CaptureScreenshot(Path.Combine(directory,"offset-start.png"));yield return new WaitForEndOfFrame();
        // Start layout must be unsolved.
        r.startsUnsolved=!room.Offset.Solved;
        room.MoveDepth(1);yield return new WaitForSeconds(.9f);
        r.depthMoved=room.ArtifactDepth==2;
        r.depthDoesNotSolveAlone=!(room.Offset.Front.Passed&&room.Offset.Side.Passed);
        room.MoveDepth(-1);yield return new WaitForSeconds(.9f);
        room.Undo();yield return new WaitForSeconds(.9f);   // reverts the move back to depth 1
        room.Undo();yield return new WaitForSeconds(.9f);   // reverts the first move: depth 1 again
        r.undoRestoresDepth=room.ArtifactDepth==ChamberController.DefaultDepth;
        // Intended solution: pose 2, left seat, front rail.
        room.Turn(1);yield return new WaitForSeconds(.7f);room.Turn(1);yield return new WaitForSeconds(.7f);
        room.SlideArtifact(-1);yield return new WaitForSeconds(.9f);room.SlideArtifact(-1);yield return new WaitForSeconds(1.0f);
        // Right pose/seat, wrong depth: only the side window disagrees.
        r.frontOnlyDecoy=room.Offset.Front.Passed&&!room.Offset.Side.Passed&&room.ArtifactDepth==ChamberController.DefaultDepth;
        ScreenCapture.CaptureScreenshot(Path.Combine(directory,"offset-front-only.png"));yield return new WaitForEndOfFrame();
        room.MoveDepth(1);yield return new WaitForSeconds(1.1f);
        r.offsetSolved=room.Offset.Solved&&room.Pose==OffsetSeal.TargetPose&&room.ArtifactSlot==OffsetSeal.TargetSlot&&room.ArtifactDepth==OffsetSeal.TargetDepth;
        ScreenCapture.CaptureScreenshot(Path.Combine(directory,"offset-solved.png"));yield return new WaitForEndOfFrame();
        // Reset returns the rail, keeps opened routes.
        room.ResetPuzzle();yield return new WaitForSeconds(.9f);
        r.resetKeepsRoutes=room.ArtifactDepth==ChamberController.DefaultDepth&&room.Offset.Solved&&Relay.Solved&&room.DepthUnlocked;
        // Depth must not create a door shortcut.
        room.Inspect(false);bool shortcut=false;
        foreach(int d in new[]{0,2}) {room.SetDepthImmediate(d);yield return null;if(room.Front.Passed&&room.Side.Passed&&!(room.Pose==0&&room.ArtifactSlot==1)) shortcut=true;}
        room.SetDepthImmediate(ChamberController.DefaultDepth);
        r.noDoorShortcut=!shortcut;
        // Gate seal target needs the front rail.
        room.JumpToEntry(6);yield return new WaitForSeconds(.6f);
        r.gateViewOpens=room.InspectGate;
        r.gatePreexistingSolved=room.Gate.Solved; r.gatePoseBefore=room.Pose; r.gateSeatBefore=room.ArtifactSlot; r.gateDepthBefore=room.ArtifactDepth;
        room.SetStateImmediate(GateSeal.TargetPose,0);room.SetArtifactSlotImmediate(GateSeal.TargetSlot);
        yield return new WaitForSeconds(.8f);
        // The target may already have been reached while experimenting.
        r.gateFrontAtCheck=room.Gate.Front.Passed; r.gateSideAtCheck=room.Gate.Side.Passed; r.gateDepthAtCheck=room.ArtifactDepth;
        // The rail must actually reach the front position.
        room.MoveDepth(1);yield return new WaitForSeconds(1.2f);
        r.gateSolvedWithDepth=room.Gate.Solved&&room.ArtifactDepth==GateSeal.TargetDepth;
        r.gateNeedsDepth=GateSeal.TargetDepth!=ChamberController.DefaultDepth;
        ScreenCapture.CaptureScreenshot(Path.Combine(directory,"gate-solved.png"));yield return new WaitForEndOfFrame();
        room.ResetPuzzle();yield return new WaitForSeconds(.8f);
        r.gateStaysSolved=room.Gate.Solved&&room.ArtifactDepth==ChamberController.DefaultDepth;
        // Middle seat must block the opening in the front window.
        room.InspectGateSeal(true);room.SetStateImmediate(GateSeal.TargetPose,0);room.SetArtifactSlotImmediate(0);
        yield return new WaitForSeconds(.9f);
        r.gateBlockedByCenter=!room.Gate.Front.Passed;
        r.gateMatchAtCenter=room.Gate.Front.Match;
        r.gateOverrideOn=room.Gate.Front.Override;
        r.gateSeatAtCenter=room.ArtifactSlot;r.gatePoseAtCenter=room.Pose;
        ScreenCapture.CaptureScreenshot(Path.Combine(directory,"gate-blocked-center.png"));yield return new WaitForEndOfFrame();
        room.ResetPuzzle();yield return new WaitForSeconds(.6f);
        // Ending as seen after a full run.
        Shell.ShowEnding();yield return new WaitForSeconds(.6f);r.endingShown=EndingVisible;
        ScreenCapture.CaptureScreenshot(Path.Combine(directory,"ending-after-run.png"));yield return new WaitForEndOfFrame();
        Shell.HideEnding();
        r.passed=r.lockedAtStart&&r.stationVisibleAtStart&&r.finalSealOpen&&r.unlockedAfterSeal&&r.stationOpens
                 &&r.startsUnsolved&&r.depthMoved&&r.depthDoesNotSolveAlone&&r.undoRestoresDepth&&r.frontOnlyDecoy
                 &&r.offsetSolved&&r.resetKeepsRoutes&&r.noDoorShortcut&&r.endingShown
                 &&r.gateViewOpens&&r.gateLockedAtStart&&r.gateSolvedWithDepth&&r.gateNeedsDepth&&r.gateStaysSolved&&r.gateBlockedByCenter;
        File.WriteAllText(Path.Combine(directory,"offset-result.json"),JsonUtility.ToJson(r,true));Application.Quit(r.passed?0:1);
    }
    [Serializable] class OffsetResult {
        public float gateMatchAtCenter;public int gateSeatAtCenter,gatePoseAtCenter;
        public bool passed,gatePreexistingSolved,gateFrontAtCheck,gateSideAtCheck;public int gatePoseBefore,gateSeatBefore,gateDepthBefore,gateDepthAtCheck;public bool lockedAtStart,stationVisibleAtStart,finalSealOpen,unlockedAfterSeal,stationOpens,startsUnsolved,endingShown,gateViewOpens,gateLockedAtStart,gateSolvedWithDepth,gateNeedsDepth,gateStaysSolved,gateBlockedByCenter,gateOverrideOn,depthMoved,depthDoesNotSolveAlone,undoRestoresDepth,frontOnlyDecoy,offsetSolved,resetKeepsRoutes,noDoorShortcut;
    }
    IEnumerator BridgeQA(string directory) {
        var r=new BridgeResult();var room=Rooms[0];
        r.collectionLocked=!Bridge.Collect();
        yield return new WaitForSeconds(.6f);
        room.Turn(1);yield return new WaitForSeconds(.65f);
        room.Turn(1);yield return new WaitForSeconds(1.4f);
        Visit(1);yield return new WaitForSeconds(1.8f);
        r.unsolvedCollectionLocked=!Bridge.Collect();
        Rooms[1].Turn(1);yield return new WaitForSeconds(.65f);
        Rooms[1].Turn(1);yield return new WaitForSeconds(.65f);
        Rooms[1].MovePillar(1);yield return new WaitForSeconds(.65f);
        Rooms[1].MovePillar(1);yield return new WaitForSeconds(1.4f);
        r.collected=Bridge.Collect();
        Visit(0);yield return new WaitForSeconds(1.8f);room.InspectRelayStation(true);
        r.frameBlocksInstall=!room.ToggleBridge();
        Visit(1);yield return new WaitForSeconds(1.8f);Rooms[1].InspectRelayStation(true);
        for(int i=0;i<3;i++) {Relay.Shift(1);yield return new WaitForSeconds(1.3f);}
        ScreenCapture.CaptureScreenshot(Path.Combine(directory,"bridge-remote.png"));yield return new WaitForEndOfFrame();
        Visit(0);yield return new WaitForSeconds(1.8f);room.SlideArtifact(1);yield return new WaitForSeconds(.7f);
        r.freshSeal=!Relay.Solved;r.validSpace=Bridge.CanPlace(0,BridgePiece.StartDepth,room.Pose,room.ArtifactSlot);
        r.installed=room.ToggleBridge();room.SelectBridge(true);
        yield return new WaitForSeconds(.7f);
        // Start depth is legal but wrong by design.
        r.startsWrong=Bridge.Installed&&Bridge.Slot==BridgePiece.StartDepth&&!Relay.Solved&&!(Relay.Front.Passed&&Relay.Side.Passed);
        ScreenCapture.CaptureScreenshot(Path.Combine(directory,"bridge-start-depth.png"));yield return new WaitForEndOfFrame();
        // One step fixes the side window, keeps the front.
        room.MoveBridgeDepth(1);yield return new WaitForSeconds(1.6f);
        r.oneStepSolves=Bridge.Slot==BridgePiece.SolvedDepth&&Relay.Solved&&Relay.Front.Passed&&Relay.Side.Passed;
        // One step further the windows disagree; the opened seal stays open.
        room.MoveBridgeDepth(1);yield return new WaitForSeconds(.7f);
        r.wrongDepthRejected=Bridge.Slot==2&&!(Relay.Front.Passed&&Relay.Side.Passed);
        ScreenCapture.CaptureScreenshot(Path.Combine(directory,"bridge-wrong-depth.png"));yield return new WaitForEndOfFrame();
        room.Undo();yield return new WaitForSeconds(1.6f);
        r.undoRestored=Bridge.Installed&&Bridge.Slot==BridgePiece.SolvedDepth;
        r.bridgeSolved=Relay.Solved&&Relay.Front.Passed&&Relay.Side.Passed&&Relay.Setting==3;
        ScreenCapture.CaptureScreenshot(Path.Combine(directory,"bridge-solved-1280x720.png"));yield return new WaitForEndOfFrame();
        Screen.SetResolution(1024,768,false);yield return new WaitForSeconds(.8f);
        ScreenCapture.CaptureScreenshot(Path.Combine(directory,"bridge-solved-1024x768.png"));yield return new WaitForEndOfFrame();
        // Rotation would intersect the guardian.
        int blocked=-1;for(int p=1;p<4;p++) if(!Bridge.CanPlace(p,Bridge.Slot,room.Pose,room.ArtifactSlot)) {blocked=p;break;}
        if(blocked>=0) {room.Turn(blocked);r.collisionRejected=Bridge.Pose==0;}
        Visit(1);yield return new WaitForSeconds(1.8f);r.installedBlocksGear=!Relay.Shift(-1);
        Visit(0);yield return new WaitForSeconds(1.8f);
        r.takeBack=room.ToggleBridge()&&!Bridge.Installed;
        room.Undo();yield return new WaitForSeconds(.8f);r.undoReinstalls=Bridge.Installed;
        room.ResetPuzzle();yield return new WaitForSeconds(.5f);
        r.resetKeepsReward=Bridge.Acquired&&!Bridge.Installed&&Relay.Solved&&room.Solved&&Rooms[1].Solved;
        Visit(1);yield return new WaitForSeconds(1.8f);r.gearFreed=Relay.Shift(-1);yield return new WaitForSeconds(1.3f);
        r.passed=r.collectionLocked&&r.unsolvedCollectionLocked&&r.collected&&r.frameBlocksInstall&&r.freshSeal&&r.validSpace&&r.installed&&r.startsWrong&&r.oneStepSolves&&r.wrongDepthRejected&&r.undoRestored&&r.bridgeSolved&&r.collisionRejected&&r.installedBlocksGear&&r.takeBack&&r.undoReinstalls&&r.resetKeepsReward&&r.gearFreed;
        File.WriteAllText(Path.Combine(directory,"bridge-result.json"),JsonUtility.ToJson(r,true));Application.Quit(r.passed?0:1);
    }
    [Serializable] class BridgeResult {
        public bool passed,collectionLocked,unsolvedCollectionLocked,collected,frameBlocksInstall,freshSeal,validSpace,installed,startsWrong,oneStepSolves,wrongDepthRejected,undoRestored,bridgeSolved,collisionRejected,installedBlocksGear,takeBack,undoReinstalls,resetKeepsReward,gearFreed;
    }
    [Serializable] class Result {
        public bool passed,lockedBeforeSolve,firstSolved,enteredSecond,undoRestored,secondSolved,
        resetKeepsRoute,returnedToFirst,firstRouteRetained,reenteredSecond,secondStateRetained,
        socketUndo,returnKnowledgeUnlock,discoveryRetained,
        localRelayBlocked,remoteGeometryMoved,frontPreserved,sideChanged,relayUndo,localResetKeepsRelay,
        relayPersistsOnReturn,relaySolved,wrongGearRejected,oldAlcoveRestored,routesRemainOpen;
    }
    [Serializable] class EarlyResult { public bool passed,found,courtyardUnsolved,borrowRoomUnsolved; }
}
}
