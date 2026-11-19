using UnityEngine;

namespace TwinView {
// Knowledge lock in the borrow room: guardian + pillar form a different seal, no progress check.
public sealed class PillarAlcove : MonoBehaviour {
    public const int TargetPose = 2, TargetPillar = 0;
    public ChamberController Owner;
    public Camera FrontCamera, SideCamera;
    public Transform Lid, Token;
    public ProjectionRule Front { get; private set; }
    public ProjectionRule Side { get; private set; }
    public bool Found { get; private set; }
    bool initialized;
    float stable, opening;
    Vector3 lidStart, tokenStart;

    public void Initialize() {
        if(initialized) return;
        Front = new ProjectionRule(); Side = new ProjectionRule();
        Configure(FrontCamera); Configure(SideCamera);
        int pose = Owner.Pose, pillar = Owner.PillarSlot;
        Owner.SetStateImmediate(TargetPose, TargetPillar);
        Front.SetTarget(Front.Read(FrontCamera, Owner.MaskShader));
        Side.SetTarget(Side.Read(SideCamera, Owner.MaskShader));
        Owner.SetStateImmediate(pose, pillar);
        if (Lid) lidStart = Lid.localPosition;
        if (Token) tokenStart = Token.localPosition;
        initialized = true; Refresh();
    }
    static void Configure(Camera c) {
        c.enabled = false; c.orthographic = true; c.orthographicSize = 1.65f; c.aspect = 1;
        c.clearFlags = CameraClearFlags.SolidColor; c.backgroundColor = Color.black;
        c.cullingMask = 1 << 9; // borrow room: guardian and pillar share layer 9; walls (11) must not project
        c.allowHDR = false; c.allowMSAA = false;
        c.targetTexture = new RenderTexture(128, 128, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        c.targetTexture.antiAliasing = 1; c.targetTexture.Create();
    }
    public void Refresh() {
        if (!initialized) return;
        Front.Evaluate(Front.Read(FrontCamera, Owner.MaskShader));
        Side.Evaluate(Side.Read(SideCamera, Owner.MaskShader));
    }
    void Update() {
        if (!initialized) return;
        bool ready = !Found && Owner.ActiveRoom && !Owner.IsMoving && (!Owner.Journey || !Owner.Journey.Travelling);
        if (ready && Front.Passed && Side.Passed) { stable += Time.deltaTime; if (stable > .45f) Found = true; }
        else stable = 0;
        opening = Mathf.MoveTowards(opening, Found ? 1 : 0, Time.deltaTime);
        if (Lid) Lid.localPosition = lidStart + Vector3.down * (.38f * Mathf.SmoothStep(0, 1, opening));
        if (Token) Token.localPosition = tokenStart + Vector3.up * (.3f * opening);
    }
}
}
