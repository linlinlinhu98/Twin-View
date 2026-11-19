using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TwinView {
// Title and ending; only sets Journey.Started / EndingVisible. --qa skips the title.
public sealed class GameShell : MonoBehaviour {
    public JourneyController Journey;
    Font font;
    GUIStyle huge, big, body, small, button;
    Texture2D veil, card, accent, hover, gold;
    float endTimer;
    bool endShown;
    static bool Qa { get { return Array.IndexOf(Environment.GetCommandLineArgs(), "--qa") >= 0; } }

    void Awake() {
        if(!Journey) return;
        // ?play skips the title (used to verify the in-game view in a browser).
        bool skipTitle = Application.absoluteURL != null && Application.absoluteURL.Contains("play");
        bool wantTitle = !skipTitle && (!Qa || Array.IndexOf(Environment.GetCommandLineArgs(), "--qa-title") >= 0);
        Journey.Started = !wantTitle;
    }
    public void Begin() { if(Journey) Journey.Started = true; }
    public void ShowEnding() { if(Journey) {Journey.EndingVisible = true; endShown = true;} }
    public void HideEnding() { if(Journey) Journey.EndingVisible = false; }

    void Update() {
        if(!Journey) return;
        if(!Journey.Started) {
            if(Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)) Begin();
            if(Input.GetKeyDown(KeyCode.Escape)) Application.Quit();
            return;
        }
        if(Journey.EndingVisible) { if(Input.GetKeyDown(KeyCode.Escape)) Application.Quit(); return; }
        var offset = Journey.Rooms[0].Offset;
        // The ending appears a moment after the last seal opens, never during QA, and only once.
        if(!Qa && !endShown && offset && offset.Solved && Journey.Rooms[0].Gate && Journey.Rooms[0].Gate.Solved) { endTimer += Time.deltaTime; if(endTimer > 2.2f) ShowEnding(); }
    }

    static Texture2D Solid(Color c) { var t = new Texture2D(1, 1); t.SetPixel(0, 0, c); t.Apply(); return t; }
    void Prepare() {
        if(font) return;
        font = Resources.Load<Font>("TwinViewFont");
        if (!font) font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 22);
        veil = Solid(new Color(.02f, .045f, .05f, .80f)); card = Solid(new Color(.055f, .11f, .12f, .985f));
        accent = Solid(new Color(.19f, .32f, .31f)); hover = Solid(new Color(.30f, .45f, .41f)); gold = Solid(new Color(.70f, .57f, .37f));
        body = new GUIStyle(GUI.skin.label) { font = font, fontSize = 26, wordWrap = true, padding = new RectOffset(), margin = new RectOffset() };
        body.normal.textColor = new Color(.88f, .85f, .75f);
        small = new GUIStyle(body) { fontSize = 18 }; small.normal.textColor = new Color(.66f, .72f, .68f);
        big = new GUIStyle(body) { fontSize = 30, wordWrap = false }; big.normal.textColor = new Color(.80f, .70f, .50f);
        huge = new GUIStyle(body) { fontSize = 88, fontStyle = FontStyle.Bold, wordWrap = false }; huge.normal.textColor = new Color(.93f, .90f, .80f);
        button = new GUIStyle(GUI.skin.button) { font = font, fontSize = 26, alignment = TextAnchor.MiddleCenter, wordWrap = false, padding = new RectOffset(8, 8, 4, 4) };
        button.normal.background = accent; button.hover.background = hover; button.active.background = hover;
        button.normal.textColor = new Color(.94f, .9f, .79f); button.hover.textColor = Color.white; button.active.textColor = Color.white;
    }
    // Fit CJK text with the real font.
    static void Label(Rect r, string text, GUIStyle source) {
        var c = new GUIContent(text); var s = source;
        while(s.fontSize > 12 && (s.CalcHeight(c, r.width) > r.height - 8 || (!s.wordWrap && s.CalcSize(c).x > r.width))) {
            if(s == source) s = new GUIStyle(source);
            s.fontSize--;
        }
        r.y += 4; r.height -= 8; GUI.Label(r, c, s);
    }

    void OnGUI() {
        if(!Journey) return;
        bool title = !Journey.Started, ending = Journey.EndingVisible;
        if(!title && !ending) return;
        Prepare(); GUI.depth = -100;
        Matrix4x4 old = GUI.matrix;
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), veil);
        float k = Mathf.Min(Screen.width / 1600f, Screen.height / 900f);
        GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1600 * k) * .5f, (Screen.height - 900 * k) * .5f, 0), Quaternion.identity, new Vector3(k, k, 1));
        if(title) DrawTitle(); else DrawEnding();
        GUI.matrix = old;
    }

    void DrawTitle() {
        GUI.DrawTexture(new Rect(360, 110, 880, 680), card);
        GUI.DrawTexture(new Rect(360, 110, 880, 5), gold);
        Label(new Rect(400, 148, 800, 130), "双面机关匣", huge);
        Label(new Rect(404, 276, 800, 50), "T W I N   V I E W   B O X", big);
        Label(new Rect(404, 350, 790, 56), "一件奇物，两扇窗。机关只读它在两个方向上的影子。", body);
        Label(new Rect(404, 410, 790, 56), "转动它，移动它，让两扇窗同时亮起。", body);
        Label(new Rect(404, 470, 790, 56), "影子只会叠加，不会抵消：房间和别的物件也在投影。", body);
        if(GUI.Button(new Rect(520, 590, 250, 68), "开始  Enter", button)) Begin();
        if(GUI.Button(new Rect(830, 590, 250, 68), "退出  Esc", button)) Application.Quit();
        Label(new Rect(404, 690, 790, 36), "Q/E 旋转 · A/D 移动 · W/S 深度 · Z 撤销 · R 复位 · H 线索 · Tab 切换视图", small);
        Label(new Rect(404, 725, 790, 36), "折脊瑞兽由 Aholo Lux3D 生成，其余部件由程序构建。", small);
    }

    string Mark(bool done, string text) { return (done ? "●  " : "○  ") + text; }
    void DrawEnding() {
        var r0 = Journey.Rooms[0]; var r1 = Journey.Rooms[1]; var relay = Journey.Relay; var bridge = Journey.Bridge;
        GUI.DrawTexture(new Rect(380, 90, 840, 720), card);
        GUI.DrawTexture(new Rect(380, 90, 840, 5), gold);
        Label(new Rect(420, 120, 760, 36), "第一章 · 完", small);
        Label(new Rect(420, 156, 760, 130), "机关匣已展开", huge);
        Label(new Rect(424, 290, 760, 52), "两扇窗，一件奇物。你让它们同时成立。", body);
        Label(new Rect(424, 360, 760, 44), Mark(r0.Solved, "一物两面：两扇窗同时亮起"), body);
        Label(new Rect(424, 408, 760, 44), Mark(r1.Solved, "房间也是道具：借环境的影子"), body);
        Label(new Rect(424, 456, 760, 44), Mark(relay.Solved, "两室同心：借影室的齿轮推动中庭"), body);
        Label(new Rect(424, 504, 760, 44), Mark(r0.Offset && r0.Offset.Solved, "偏位印：保住一面，沿另一条轴移动"), body);
        Label(new Rect(424, 552, 760, 44), Mark(r0.Gate && r0.Gate.Solved, "门洞印：门洞的光没有被挡住"), body);
        Label(new Rect(424, 602, 760, 40), "可选：", small);
        Label(new Rect(424, 630, 760, 44), Mark(r0.Alcove && r0.Alcove.Found, "无名石龛") + "     " + Mark(bridge.Acquired, "桥块（终印的另一种解法）"), body);
        if(GUI.Button(new Rect(430, 712, 220, 60), "继续探索", button)) HideEnding();
        if(GUI.Button(new Rect(675, 712, 220, 60), "重新开始", button)) { HideEnding(); SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex); }
        if(GUI.Button(new Rect(920, 712, 220, 60), "退出  Esc", button)) Application.Quit();
    }
}
}
