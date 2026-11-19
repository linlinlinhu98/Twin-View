using UnityEngine;

namespace TwinView {
// Ability lock. The guardian can only move along the depth axis after the final seal is open
// (the "rail clamp"). This seal reads the guardian alone, so nothing else can help solve it.
//
// Target (all three axes matter, each one fixes a different thing):
//   front window : the guardian must stand on the left seat (x). Pose 0 and 2 look the same from the front.
//   side window  : the guardian must be turned a half circle (pose 2) AND moved to the front rail (depth 2).
public sealed class OffsetSeal : MonoBehaviour {
    public const int TargetPose=2, TargetSlot=0, TargetDepth=2;
    public ChamberController Owner;
    public Camera FrontCamera, SideCamera;
    public ProjectionRule Front { get; private set; }
    public ProjectionRule Side { get; private set; }
    public bool Solved { get; private set; }
    bool initialized;
    float stable;

    public void Initialize() {
        if(initialized) return;
        Front=new ProjectionRule();Side=new ProjectionRule();
        Configure(FrontCamera);Configure(SideCamera);
        var art=Owner.Artifact;Vector3 p=art.localPosition;Quaternion r=art.localRotation;
        art.localPosition=ChamberController.ArtifactPosition(TargetSlot,TargetDepth);
        art.localRotation=Quaternion.Euler(0,TargetPose*90,0);
        bool[] frontGoal=Front.Read(FrontCamera,Owner.MaskShader);
        bool[] sideGoal=Side.Read(SideCamera,Owner.MaskShader);
        art.localRotation=Quaternion.Euler(0,((TargetPose+2)%4)*90,0);
        bool[] frontReverse=Front.Read(FrontCamera,Owner.MaskShader); // front view cannot tell the two apart
        Front.SetTarget(frontGoal,frontReverse);Side.SetTarget(sideGoal);
        art.localPosition=p;art.localRotation=r;
        initialized=true;Refresh();
    }
    static void Configure(Camera c) {
        c.enabled=false;c.orthographic=true;c.orthographicSize=1.65f;c.aspect=1;
        c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=Color.black;
        c.cullingMask=1<<8; // guardian only
        c.allowHDR=false;c.allowMSAA=false;
        c.targetTexture=new RenderTexture(128,128,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
        c.targetTexture.antiAliasing=1;c.targetTexture.Create();
    }
    public void Refresh() {
        if(!initialized) return;
        Front.Evaluate(Front.Read(FrontCamera,Owner.MaskShader));
        Side.Evaluate(Side.Read(SideCamera,Owner.MaskShader));
    }
    void Update() {
        if(!initialized) return;
        bool ready=!Solved&&Owner.ActiveRoom&&Owner.DepthUnlocked&&!Owner.IsMoving&&(!Owner.Journey||!Owner.Journey.Travelling);
        if(ready&&Front.Passed&&Side.Passed) {stable+=Time.deltaTime;if(stable>.45f) Solved=true;}
        else stable=0;
    }
}
}
