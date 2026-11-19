using UnityEngine;

namespace TwinView {
// A knowledge lock: no dependency on either room's completion or a hint flag.
public sealed class SecretAlcove : MonoBehaviour {
    public ChamberController Owner;
    public Transform Frame, Lid, Star;
    public Camera FrontCamera, SideCamera;
    public Renderer Face;
    public ProjectionRule Front { get; private set; }
    public ProjectionRule Side { get; private set; }
    public bool Found { get; private set; }
    bool initialized;
    float stable,opening;
    Vector3 lidStart,starStart;
    public void Initialize() {
        if(initialized) return;
        Front=new ProjectionRule();Side=new ProjectionRule();
        Configure(FrontCamera);Configure(SideCamera);
        Vector3 p=Owner.Artifact.localPosition;Quaternion r=Owner.Artifact.localRotation;
        Owner.Artifact.localPosition=ChamberController.ArtifactPosition(2);
        Owner.Artifact.localRotation=Quaternion.Euler(0,90,0);
        Front.SetTarget(Front.Read(FrontCamera,Owner.MaskShader));
        Side.SetTarget(Side.Read(SideCamera,Owner.MaskShader));
        Owner.Artifact.localPosition=p;Owner.Artifact.localRotation=r;
        lidStart=Lid.localPosition;starStart=Star.localPosition;initialized=true;Refresh();
    }
    static void Configure(Camera c) {
        c.enabled=false;c.orthographic=true;c.orthographicSize=1.65f;c.aspect=1;
        c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=Color.black;
        c.cullingMask=(1<<8)|(1<<12)|(1<<13);c.allowHDR=false;c.allowMSAA=false;
        c.targetTexture=new RenderTexture(128,128,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
        c.targetTexture.antiAliasing=1;c.targetTexture.Create();
    }
    public void Refresh() {
        if(!initialized) return;
        Front.Evaluate(Front.Read(FrontCamera,Owner.MaskShader));
        Side.Evaluate(Side.Read(SideCamera,Owner.MaskShader));
        if(Face) Face.sharedMaterial.mainTexture=Front.Display;
    }
    void Update() {
        if(!initialized) return;
        if(!Found&&Owner.ActiveRoom&&!Owner.IsMoving&&(!Owner.Journey||(!Owner.Journey.Travelling&&(!Owner.Journey.Relay||!Owner.Journey.Relay.Moving)))&&Front.Passed&&Side.Passed) {
            stable+=Time.deltaTime;if(stable>.45f) Found=true;
        } else stable=0;
        opening=Mathf.MoveTowards(opening,Found?1:0,Time.deltaTime);
        Lid.localPosition=lidStart+Vector3.down*(.75f*Mathf.SmoothStep(0,1,opening));
        Star.localPosition=starStart+Vector3.up*(.35f*opening);
        Star.localRotation=Quaternion.Euler(0,Found?Time.time*20:0,0);
    }
}
}
