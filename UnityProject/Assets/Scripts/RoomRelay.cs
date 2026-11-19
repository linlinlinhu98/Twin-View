using System.Collections.Generic;
using UnityEngine;

namespace TwinView {
// Shared physical state, never a "visited room" or "read hint" flag.
public sealed class RoomRelay : MonoBehaviour {
    public JourneyController Journey;
    public Transform Wheel, SealLeft, SealRight, Prize;
    public Camera PreviewCamera;
    public ProjectionRule Front { get; private set; }
    public ProjectionRule Side { get; private set; }
    public int Setting { get; private set; }
    public bool Moving { get; private set; }
    public bool Solved { get; private set; }
    public bool CanUndo { get { return history.Count>0&&!Moving; } }
    public RenderTexture Preview { get { return PreviewCamera.targetTexture; } }
    readonly Stack<int> history=new Stack<int>();
    bool initialized;
    float stable,opening;
    Vector3 frameRest,leftRest,rightRest;
    SecretAlcove Alcove { get { return Journey.Rooms[0].Alcove; } }
    public static Vector3 FrameOffset(int setting) { return setting==3?new Vector3(0,-3.5f,.9f):Vector3.forward*(setting*.45f); }
    public void Initialize() {
        if(initialized) return;
        frameRest=Alcove.Frame.localPosition;leftRest=SealLeft.localPosition;rightRest=SealRight.localPosition;
        Front=new ProjectionRule();Side=new ProjectionRule();
        var owner=Alcove.Owner;var p=owner.Artifact.localPosition;var r=owner.Artifact.localRotation;
        Alcove.Frame.localPosition=frameRest+Vector3.forward*.45f;
        owner.Artifact.localPosition=ChamberController.ArtifactPosition(2);owner.Artifact.localRotation=Quaternion.identity;
        Front.SetTarget(Front.Read(Alcove.FrontCamera,owner.MaskShader));
        Side.SetTarget(Side.Read(Alcove.SideCamera,owner.MaskShader));
        owner.Artifact.localPosition=p;owner.Artifact.localRotation=r;Alcove.Frame.localPosition=frameRest;
        PreviewCamera.enabled=false;PreviewCamera.cullingMask=(1<<8)|(1<<10)|(1<<12)|(1<<13)|1;
        PreviewCamera.targetTexture=new RenderTexture(640,360,24);PreviewCamera.targetTexture.Create();
        initialized=true;Refresh();
    }
    public void SetImmediate(int value) {
        Setting=Mathf.Clamp(value,0,3);Moving=false;stable=0;
        Alcove.Frame.localPosition=frameRest+FrameOffset(Setting);
        Wheel.localRotation=Quaternion.Euler(0,0,-Setting*75);Refresh();
    }
    public bool Shift(int delta) {
        if(!initialized||Moving||Journey.Travelling||Journey.Current!=1||Journey.Rooms[1].IsMoving||Journey.Bridge.Installed) return false;
        int next=Mathf.Clamp(Setting+delta,0,3);if(next==Setting) return false;
        history.Push(Setting);Setting=next;Moving=true;stable=0;return true;
    }
    public void Undo() {
        if(!CanUndo||Journey.Travelling||Journey.Current!=1||Journey.Bridge.Installed) return;
        Setting=history.Pop();Moving=true;stable=0;
    }
    public void Refresh() {
        if(!initialized) return;
        Alcove.Refresh();
        Front.Evaluate(Front.Read(Alcove.FrontCamera,Alcove.Owner.MaskShader));
        Side.Evaluate(Side.Read(Alcove.SideCamera,Alcove.Owner.MaskShader));
        PreviewCamera.Render();
    }
    void Update() {
        if(!initialized) return;
        if(Moving) {
            Vector3 target=frameRest+FrameOffset(Setting);
            Alcove.Frame.localPosition=Vector3.MoveTowards(Alcove.Frame.localPosition,target,(Setting==3||Alcove.Frame.localPosition.y<frameRest.y-.01f?4f:.8f)*Time.deltaTime);
            Wheel.localRotation=Quaternion.RotateTowards(Wheel.localRotation,Quaternion.Euler(0,0,-Setting*75),150*Time.deltaTime);
            Refresh();
            if(Vector3.Distance(Alcove.Frame.localPosition,target)<.001f&&Quaternion.Angle(Wheel.localRotation,Quaternion.Euler(0,0,-Setting*75))<.1f) Moving=false;
        }
        if(!Solved&&!Moving&&!Alcove.Owner.IsMoving&&Front.Passed&&Side.Passed) {
            stable+=Time.deltaTime;if(stable>.45f) Solved=true;
        } else stable=0;
        opening=Mathf.MoveTowards(opening,Solved?1:0,Time.deltaTime*.8f);
        SealLeft.localPosition=leftRest+Vector3.left*(opening*.55f);
        SealRight.localPosition=rightRest+Vector3.right*(opening*.55f);
        Prize.localRotation=Quaternion.Euler(0,Time.time*25,0);
        if(Journey.Current==1&&Journey.Rooms[1].InspectRelay) PreviewCamera.Render();
    }
}
}
