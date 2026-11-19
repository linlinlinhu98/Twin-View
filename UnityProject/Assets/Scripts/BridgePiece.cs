using UnityEngine;

namespace TwinView {
public sealed class BridgePiece : MonoBehaviour {
    public JourneyController Journey;
    public Transform Shape;
    public bool Acquired { get; private set; }
    public bool Installed { get; private set; }
    public int Pose { get; private set; }
    // Starts one rail away from the working depth: legal but wrong, so the depth lesson is needed.
    public const int StartDepth=0, SolvedDepth=1;
    public int Slot { get; private set; }=StartDepth;
    public bool Collect() {
        if(Acquired||Journey.Current!=1||!Journey.Rooms[1].Solved||Journey.Travelling) return false;
        Acquired=true;Apply(false,0,StartDepth);return true;
    }
    // Conservative bounding-box check against the guardian.
    public bool CanPlace(int pose,int slot,int guardianPose,int guardianSlot,int guardianDepth=ChamberController.DefaultDepth) {
        var artifact=Journey.Rooms[0].Artifact;
        var gm=Matrix4x4.TRS(ChamberController.ArtifactPosition(guardianSlot,guardianDepth),Quaternion.Euler(0,guardianPose*90,0),artifact.localScale);
        Bounds occupied=WorldBounds(artifact.GetComponent<MeshFilter>().sharedMesh.bounds,gm);
        var bm=Matrix4x4.TRS(new Vector3(1.15f,1.65f,1.23f+(slot-1)*.35f),Quaternion.Euler(0,pose*90,0),Vector3.one);
        foreach(var mesh in Shape.GetComponentsInChildren<MeshFilter>(true)) {
            var relative=Shape.worldToLocalMatrix*mesh.transform.localToWorldMatrix;
            Bounds part=WorldBounds(mesh.sharedMesh.bounds,bm*relative);
            if(part.min.x<occupied.max.x-.003f&&part.max.x>occupied.min.x+.003f&&part.min.y<occupied.max.y-.003f&&part.max.y>occupied.min.y+.003f&&part.min.z<occupied.max.z-.003f&&part.max.z>occupied.min.z+.003f) return false;
        }
        return true;
    }
    static Bounds WorldBounds(Bounds local,Matrix4x4 matrix) {
        var b=new Bounds(matrix.MultiplyPoint3x4(local.min),Vector3.zero);
        for(int i=0;i<8;i++) b.Encapsulate(matrix.MultiplyPoint3x4(new Vector3((i&1)==0?local.min.x:local.max.x,(i&2)==0?local.min.y:local.max.y,(i&4)==0?local.min.z:local.max.z)));
        return b;
    }
    public void Apply(bool installed,int pose,int slot) {
        Installed=installed;Pose=(pose%4+4)%4;Slot=Mathf.Clamp(slot,0,2);
        Shape.gameObject.SetActive(installed||!Acquired);
        Shape.position=installed?new Vector3(1.15f,1.65f,1.23f+(Slot-1)*.35f):new Vector3(-2.6f,1.25f,8);
        Shape.rotation=Quaternion.Euler(0,Pose*90,0);Shape.localScale=Vector3.one*(installed?1:.32f);
        foreach(var r in Shape.GetComponentsInChildren<Renderer>(true)) r.gameObject.layer=installed?13:11;
        if(Journey&&Journey.Relay) Journey.Relay.Refresh();
    }
}
}
