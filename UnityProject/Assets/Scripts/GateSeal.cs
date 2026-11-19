using UnityEngine;

namespace TwinView {
// The gate stone works as a viewfinder: the front window only counts what is seen through the opening.
// The guardian has to stand in the doorway so that the part visible inside the hole matches the target.
public sealed class GateSeal : MonoBehaviour {
    public const int TargetPose=1, TargetSlot=1, TargetDepth=2;
    public ChamberController Owner;
    public Transform Gate;
    public Camera FrontCamera, SideCamera;
    public ProjectionRule Front { get; private set; }
    public ProjectionRule Side { get; private set; }
    public bool Solved { get; private set; }

    bool initialized;
    float stable;
    bool[] hole;
    bool[] targetCrop;
    Texture2D cropDisplay;

    public void Initialize() {
        if(initialized) return;
        Front=new ProjectionRule();Side=new ProjectionRule();
        Configure(FrontCamera);Configure(SideCamera);
        int pose=Owner.Pose,seat=Owner.ArtifactSlot,depth=Owner.ArtifactDepth,pillar=Owner.PillarSlot;
        var body=Owner.Artifact.GetComponentsInChildren<Renderer>();

        // The opening is the empty space between the gate's own covered pixels on each row.
        SetVisible(body,false);
        hole=HoleFrom(Front.Read(FrontCamera,Owner.MaskShader));
        SetVisible(body,true);

        // Author the target from the intended arrangement: the guardian standing in the doorway.
        Owner.SetDepthImmediate(TargetDepth);Owner.SetArtifactSlotImmediate(TargetSlot);Owner.SetStateImmediate(TargetPose,pillar);
        targetCrop=And(Front.Read(FrontCamera,Owner.MaskShader),hole);
        Side.SetTarget(Side.Read(SideCamera,Owner.MaskShader));
        Front.SetTarget(targetCrop);

        Owner.SetDepthImmediate(depth);Owner.SetArtifactSlotImmediate(seat);Owner.SetStateImmediate(pose,pillar);
        cropDisplay=new Texture2D(ProjectionRule.Size,ProjectionRule.Size,TextureFormat.RGBA32,false);
        // The target image uses the same frame as the live image, otherwise the two look unrelated.
        var targetPixels=new Color32[hole.Length];
        for(int i=0;i<hole.Length;i++) {
            targetPixels[i]=!hole[i]?new Color32(58,68,66,255):targetCrop[i]?new Color32(132,116,86,255):new Color32(230,224,206,255);
        }
        Front.Target.SetPixels32(targetPixels);Front.Target.Apply();
        cropDisplay.filterMode=FilterMode.Bilinear;
        initialized=true;Refresh();
    }

    static void Configure(Camera c) {
        c.enabled=false;c.orthographic=true;c.orthographicSize=1.65f;c.aspect=1;
        c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=Color.black;
        c.cullingMask=(1<<8)|(1<<14);
        c.allowHDR=false;c.allowMSAA=false;
        c.targetTexture=new RenderTexture(128,128,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
        c.targetTexture.antiAliasing=1;c.targetTexture.Create();
    }

    static void SetVisible(Renderer[] renderers,bool on) { foreach(var r in renderers) r.enabled=on; }
    static bool[] And(bool[] a,bool[] b) { var o=new bool[a.Length];for(int i=0;i<o.Length;i++) o[i]=a[i]&&b[i];return o; }

    static bool[] HoleFrom(bool[] covered) {
        int n=ProjectionRule.Size;
        var hole=new bool[covered.Length];
        for(int y=0;y<n;y++) {
            int lo=-1,hi=-1;
            for(int x=0;x<n;x++) if(covered[y*n+x]) { if(lo<0) lo=x; hi=x; }
            if(lo<0) continue;
            for(int x=lo;x<=hi;x++) hole[y*n+x]=!covered[y*n+x];
        }
        return hole;
    }

    public void Refresh() {
        if(!initialized) return;
        bool[] seen=And(Front.Read(FrontCamera,Owner.MaskShader),hole);
        int need=0,have=0,extra=0,extraHit=0;
        var pixels=new Color32[hole.Length];
        for(int i=0;i<hole.Length;i++) {
            if(targetCrop[i]) { need++; if(seen[i]) have++; }
            else if(hole[i]) { extra++; if(seen[i]) extraHit++; }
            Color32 c;
            if(!hole[i]) c=new Color32(58,68,66,255);            // outside the opening: ignored
            else if(seen[i]) c=new Color32(45,76,75,255);        // visible through the opening
            else c=new Color32(230,224,206,255);
            pixels[i]=c;
        }
        float cover=need>0?(float)have/need:0f;
        float leak=extra>0?(float)extraHit/extra:0f;
        Front.Override=true;
        Front.OverridePassed=cover>=.98f&&leak<=.02f;
        Front.Match=Mathf.Clamp01(cover)*(1f-Mathf.Clamp01(leak));
        if(cropDisplay) { cropDisplay.SetPixels32(pixels);cropDisplay.Apply();Front.Display=cropDisplay; }
        Side.Evaluate(Side.Read(SideCamera,Owner.MaskShader));
    }

    void Update() {
        if(!initialized) return;
        bool ready=!Solved&&Owner.ActiveRoom&&!Owner.IsMoving&&(!Owner.Journey||!Owner.Journey.Travelling);
        if(ready&&Front.Passed&&Side.Passed) {stable+=Time.deltaTime;if(stable>.45f) Solved=true;}
        else stable=0;
    }
}
}
