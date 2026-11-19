using UnityEngine;

namespace TwinView {
// Conditions are spatial masks, evaluated from the displayed 3D mesh.
public sealed class ProjectionRule {
    public const int Size = 128;
    public bool[] Dark, Light, Goal; // Goal = the silhouette the player has to match (all accepted arrangements)
    public Texture2D Target;
    public float DarkCoverage, LightOcclusion;
    public float Match; // one aggregate number 0..1; never says WHERE the difference is
    public bool Override;        // a station may supply its own pass rule, e.g. the gate viewfinder
    public bool OverridePassed;
    public bool Passed { get { return Override ? OverridePassed : (DarkCoverage >= .98f && LightOcclusion <= .015f); } }
    public Texture2D Display;
    readonly Texture2D readback = new Texture2D(Size, Size, TextureFormat.RGB24, false, true);

    public bool[] Read(Camera camera, Shader shader) {
        camera.RenderWithShader(shader, "");
        RenderTexture old = RenderTexture.active;
        RenderTexture.active = camera.targetTexture;
        readback.ReadPixels(new Rect(0,0,Size,Size),0,0); readback.Apply();
        RenderTexture.active = old;
        Color32[] colors = readback.GetPixels32(); bool[] mask = new bool[colors.Length];
        for (int i=0;i<mask.Length;i++) mask[i]=colors[i].r>127;
        return mask;
    }
    public void SetTarget(bool[] goal, bool[] alternate = null) {
        bool[] inner = new bool[goal.Length], outer = new bool[goal.Length];
        for (int i=0;i<goal.Length;i++) {
            inner[i]=goal[i] && (alternate==null || alternate[i]);
            outer[i]=goal[i] || (alternate!=null && alternate[i]);
        }
        Dark = Erode(inner, 2); Light = new bool[goal.Length];
        bool[] air = new bool[goal.Length]; for(int i=0;i<air.Length;i++) air[i]=!outer[i];
        Light = Erode(air,2);
        Goal = outer; // shown to the player as the target silhouette
        Display = new Texture2D(Size,Size,TextureFormat.RGBA32,false);
        Display.filterMode=FilterMode.Bilinear;
        Target = new Texture2D(Size,Size,TextureFormat.RGBA32,false);
        Target.filterMode=FilterMode.Bilinear;
        Color32[] goalPixels=new Color32[Goal.Length];
        for(int i=0;i<Goal.Length;i++) goalPixels[i]=Goal[i]?new Color32(132,116,86,255):new Color32(230,224,206,255);
        Target.SetPixels32(goalPixels);Target.Apply();
    }
    static bool[] Erode(bool[] src,int radius) {
        bool[] dst=new bool[src.Length];
        for(int y=radius;y<Size-radius;y++) for(int x=radius;x<Size-radius;x++) {
            bool inside=true;
            for(int dy=-radius;dy<=radius && inside;dy++) for(int dx=-radius;dx<=radius;dx++)
                if(!src[(y+dy)*Size+x+dx]) { inside=false; break; }
            dst[y*Size+x]=inside;
        }
        return dst;
    }
    public void Evaluate(bool[] actual) {
        int darkN=0,darkHit=0,lightN=0,lightHit=0;
        Color32[] display=new Color32[actual.Length];
        for(int i=0;i<actual.Length;i++) {
            if(Dark[i]) { darkN++; if(actual[i]) darkHit++; }
            if(Light[i]) { lightN++; if(actual[i]) lightHit++; }
            // Only the real silhouette is drawn. The player compares it with the target image beside it;
            // nothing marks which piece is missing or extra.
            Color32 c=actual[i]?new Color32(45,76,75,255):new Color32(230,224,206,255);
            display[i]=c;
        }
        DarkCoverage=darkN>0?(float)darkHit/darkN:0;
        LightOcclusion=lightN>0?(float)lightHit/lightN:1;
        Match=Mathf.Clamp01(DarkCoverage)*Mathf.Clamp01(1f-LightOcclusion);
        Display.SetPixels32(display); Display.Apply();
    }
}
}
