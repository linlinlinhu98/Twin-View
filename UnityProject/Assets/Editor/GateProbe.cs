using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TwinView;

// Measurement tool: reads the gate stone's opening with the game's shader into Validation/gate-probe.json.
public static class GateProbe {
    const string Dir = "Assets/Art/Gate";
    static Mesh LoadMesh() {
        byte[] raw = File.ReadAllBytes(Dir + "/Gate.bytes");
        using (var reader = new BinaryReader(new MemoryStream(raw))) {
            int nv = reader.ReadInt32(), ni = reader.ReadInt32();
            var v = new Vector3[nv]; var n = new Vector3[nv]; var uv = new Vector2[nv]; var index = new int[ni];
            for (int i = 0; i < nv; i++) v[i] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            for (int i = 0; i < nv; i++) n[i] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            for (int i = 0; i < nv; i++) uv[i] = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            for (int i = 0; i < ni; i++) index[i] = reader.ReadInt32();
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Generated/Gate.asset");
            if (!mesh) { mesh = new Mesh(); AssetDatabase.CreateAsset(mesh, "Assets/Generated/Gate.asset"); }
            mesh.Clear(); mesh.name = "Lux3D Gate stone";
            mesh.indexFormat = nv > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.vertices = v; mesh.normals = n; mesh.uv = uv; mesh.triangles = index;
            mesh.RecalculateBounds(); EditorUtility.SetDirty(mesh);
            return mesh;
        }
    }
    static Mesh LoadGuardian() { return AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Generated/Guardian.asset"); }

    static Camera Sensor(Vector3 pos, Vector3 target) {
        var c = new GameObject("sensor").AddComponent<Camera>();
        c.enabled = false; c.orthographic = true; c.orthographicSize = 1.65f; c.aspect = 1;
        c.clearFlags = CameraClearFlags.SolidColor; c.backgroundColor = Color.black; c.cullingMask = 1 << 8;
        c.transform.position = pos; c.transform.LookAt(target); c.nearClipPlane = .1f; c.farClipPlane = 30;
        c.targetTexture = new RenderTexture(128, 128, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        c.targetTexture.antiAliasing = 1; c.targetTexture.Create();
        return c;
    }
    static GameObject Piece(string name, Mesh mesh, float scale) {
        var g = new GameObject(name); g.layer = 8;
        g.AddComponent<MeshFilter>().sharedMesh = mesh; g.AddComponent<MeshRenderer>().sharedMaterial = new Material(Shader.Find("Standard"));
        g.transform.localScale = Vector3.one * scale; return g;
    }
    static int Count(bool[] m) { int n = 0; foreach (var b in m) if (b) n++; return n; }
    static int Clear(bool[] m, int x0, int x1, int y0, int y1) { int n = 0; for (int y = y0; y < y1; y++) for (int x = x0; x < x1; x++) if (!m[y * 128 + x]) n++; return n; }

    [MenuItem("Twin View/Probe gate stone")]
    public static void Run() {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var shader = Shader.Find("Hidden/TwinView/ArtifactMask");
        var gate = Piece("gate", LoadMesh(), 5f); var guardian = Piece("guardian", LoadGuardian(), 5f);
        var front = Sensor(new Vector3(0, 1.65f, -6), new Vector3(0, 1.65f, 0));
        var side = Sensor(new Vector3(6, 1.65f, 0), new Vector3(0, 1.65f, 0));
        var rule = new ProjectionRule();
        var sb = new StringBuilder("{\n");
        var b = gate.GetComponent<MeshFilter>().sharedMesh.bounds;
        sb.Append("  \"gate_mesh_bounds_game_units\": [" + (b.size.x * 5).ToString("F3") + ", " + (b.size.y * 5).ToString("F3") + ", " + (b.size.z * 5).ToString("F3") + "],\n");
        sb.Append("  \"window_pixels\": 16384,\n  \"gate_alone\": [\n");
        guardian.SetActive(false);
        for (int pose = 0; pose < 4; pose++) {
            gate.transform.position = new Vector3(0, 1.65f, 0); gate.transform.rotation = Quaternion.Euler(0, pose * 90, 0);
            bool[] f = rule.Read(front, shader), s = rule.Read(side, shader);
            // The opening: the lit (not covered) pixels that sit between covered pixels on the same row, below the lintel.
            int fo = Opening(f), so = Opening(s);
            sb.Append("    {\"pose\": " + pose + ", \"front_covered\": " + Count(f) + ", \"side_covered\": " + Count(s) + ", \"front_opening_px\": " + fo + ", \"side_opening_px\": " + so + "}" + (pose < 3 ? "," : "") + "\n");
        }
        sb.Append("  ],\n  \"gate_with_guardian_behind_opening\": [\n");
        guardian.SetActive(true);
        // How much of the gate opening stays lit when the guardian stands on each seat/depth? (gate on the middle rail, pose 0)
        gate.transform.position = new Vector3(0, 1.65f, 0); gate.transform.rotation = Quaternion.identity;
        string rows = "";
        for (int seat = 0; seat < 3; seat++) for (int depth = 0; depth < 3; depth++) for (int gp = 0; gp < 4; gp++) {
            guardian.transform.position = ChamberController.ArtifactPosition(seat, depth); guardian.transform.rotation = Quaternion.Euler(0, gp * 90, 0);
            // Gate alone first (guardian hidden) to find its opening, then both objects together.
            guardian.SetActive(false); bool[] gateF = rule.Read(front, shader);
            guardian.SetActive(true); bool[] both = rule.Read(front, shader);
            int open = 0, stillLit = 0;
            for (int i = 0; i < gateF.Length; i++) if (OpeningMask(gateF, i)) { open++; if (!both[i]) stillLit++; }
            rows += "    {\"guardian_seat\": " + seat + ", \"guardian_depth\": " + depth + ", \"guardian_pose\": " + gp + ", \"gate_opening_px\": " + open + ", \"opening_still_lit_px\": " + stillLit + "},\n";
        }
        sb.Append(rows.TrimEnd('\n', ',') + "\n  ]\n}\n");
        Directory.CreateDirectory("Validation"); File.WriteAllText("Validation/gate-probe.json", sb.ToString());
        AssetDatabase.SaveAssets();
        Debug.Log("GATE_PROBE_DONE");
    }
    // Pixels in the gate's opening: uncovered pixels with covered pixels both left and right on the same row.
    static bool OpeningMask(bool[] m, int i) {
        if (m[i]) return false; int y = i / 128, x = i % 128; bool l = false, r = false;
        for (int k = x - 1; k >= 0; k--) if (m[y * 128 + k]) { l = true; break; }
        for (int k = x + 1; k < 128; k++) if (m[y * 128 + k]) { r = true; break; }
        return l && r;
    }
    static int Opening(bool[] m) { int n = 0; for (int i = 0; i < m.Length; i++) if (OpeningMask(m, i)) n++; return n; }
    // Execute from batch mode.
    public static void RunAndQuit() { Run(); EditorApplication.Exit(0); }
}
