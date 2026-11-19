using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace TwinView {
// Final-seal solver report using the game's own projection rule.
public static class SolverReport {
    public sealed class Layout {
        public int gear, guardianPose, guardianSlot, bridgePose, bridgeDepth;
        public bool bridgeInstalled, spaceClear, front, side;
        public float frontDark, frontLight, sideDark, sideLight;
        public bool Pass { get { return front && side && spaceClear; } }
        public string Key { get { return gear + "/" + guardianPose + "/" + guardianSlot + "/" + (bridgeInstalled ? bridgePose + ":" + bridgeDepth : "-"); } }
    }

    // gear 0..2 move the stone frame; gear 3 retracts it and frees the bridge slot.
    public static List<Layout> EnumerateFinalSeal(JourneyController journey) {
        var relay = journey.Relay; var bridge = journey.Bridge; var room = journey.Rooms[0];
        var all = new List<Layout>();
        for (int gear = 0; gear <= 3; gear++)
            for (int pose = 0; pose < 4; pose++)
                for (int slot = 0; slot < 3; slot++) {
                    int bpMax = gear == 3 ? 4 : 1, bsMax = gear == 3 ? 3 : 1;
                    for (int installed = 0; installed < (gear == 3 ? 2 : 1); installed++)
                        for (int bp = 0; bp < bpMax; bp++)
                            for (int bs = 0; bs < bsMax; bs++) {
                                if (installed == 0 && (bp != 0 || bs != 0)) continue; // not installed: one entry only
                                relay.SetImmediate(gear);
                                room.SetStateImmediate(pose, 0); room.SetArtifactSlotImmediate(slot);
                                bridge.Apply(installed == 1, bp, installed == 1 ? bs : BridgePiece.StartDepth);
                                var l = new Layout {
                                    gear = gear, guardianPose = pose, guardianSlot = slot, bridgePose = bp, bridgeDepth = bs,
                                    bridgeInstalled = installed == 1,
                                    spaceClear = installed == 0 || bridge.CanPlace(bp, bs, pose, slot),
                                    front = relay.Front.Passed, side = relay.Side.Passed,
                                    frontDark = relay.Front.DarkCoverage, frontLight = relay.Front.LightOcclusion,
                                    sideDark = relay.Side.DarkCoverage, sideLight = relay.Side.LightOcclusion };
                                all.Add(l);
                            }
                }
        bridge.Apply(false, 0, BridgePiece.StartDepth); relay.SetImmediate(0);
        room.SetStateImmediate(2, 0); room.SetArtifactSlotImmediate(1);
        return all;
    }

    static string F(float v) { return v.ToString("F4", CultureInfo.InvariantCulture); }

    public static void WriteFinalSealReport(JourneyController journey, List<string> checks) {
        var all = EnumerateFinalSeal(journey);
        var solutions = all.FindAll(l => l.Pass);
        var frameRoute = solutions.FindAll(l => !l.bridgeInstalled);
        var bridgeRoute = solutions.FindAll(l => l.bridgeInstalled);
        // Near misses: exactly one window passes and the space is legal. These are the decoys a player meets.
        var oneWindow = all.FindAll(l => l.spaceClear && (l.front ^ l.side));
        var frontOnly = oneWindow.FindAll(l => l.front);
        var sideOnly = oneWindow.FindAll(l => l.side);

        if (frameRoute.Count == 0) throw new Exception("Solver: no stone-frame solution");
        if (bridgeRoute.Count == 0) throw new Exception("Solver: no bridge solution");
        if (solutions.Count != frameRoute.Count + bridgeRoute.Count) throw new Exception("Solver: inconsistent counts");
        foreach (var s in bridgeRoute) if (s.gear != 3) throw new Exception("Solver: bridge solved without retracting the frame: " + s.Key);
        foreach (var s in frameRoute) if (s.gear == 3) throw new Exception("Solver: frame route with the frame retracted: " + s.Key);
        // The bridge must be reachable by one move from its start depth; that is the lesson.
        bool startWrong = all.Exists(l => l.bridgeInstalled && l.gear == 3 && l.bridgeDepth == BridgePiece.StartDepth
                                          && l.bridgePose == 0 && l.spaceClear && !l.Pass
                                          && bridgeRoute.Exists(s => s.guardianPose == l.guardianPose && s.guardianSlot == l.guardianSlot && s.bridgePose == l.bridgePose));
        if (!startWrong) throw new Exception("Solver: bridge start depth is not a legal-but-wrong neighbour of a solution");

        var sb = new StringBuilder();
        sb.Append("{\n");
        sb.Append("  \"states_enumerated\": " + all.Count + ",\n");
        sb.Append("  \"solutions_total\": " + solutions.Count + ",\n");
        sb.Append("  \"solutions_stone_frame_route\": " + frameRoute.Count + ",\n");
        sb.Append("  \"solutions_bridge_route\": " + bridgeRoute.Count + ",\n");
        sb.Append("  \"legal_states_passing_front_window_only\": " + frontOnly.Count + ",\n");
        sb.Append("  \"legal_states_passing_side_window_only\": " + sideOnly.Count + ",\n");
        sb.Append("  \"bridge_start_depth\": " + BridgePiece.StartDepth + ",\n");
        sb.Append("  \"bridge_solved_depth\": " + BridgePiece.SolvedDepth + ",\n");
        sb.Append("  \"bridge_start_is_legal_but_wrong_neighbour_of_a_solution\": " + startWrong.ToString().ToLowerInvariant() + ",\n");
        sb.Append("  \"solutions\": [\n");
        for (int i = 0; i < solutions.Count; i++)
            sb.Append("    \"" + solutions[i].Key + "\"" + (i + 1 < solutions.Count ? "," : "") + "\n");
        sb.Append("  ],\n  \"key_format\": \"gear/guardianPose/guardianSlot/bridgePose:bridgeDepth ('-' = bridge not installed)\"\n}\n");
        Directory.CreateDirectory("Validation");
        File.WriteAllText("Validation/solver-final-seal.json", sb.ToString());

        var csv = new List<string> { "gear,guardian_pose,guardian_slot,bridge_installed,bridge_pose,bridge_depth,pass,space_clear,front,side,front_dark,front_light,side_dark,side_light" };
        foreach (var l in all)
            csv.Add(string.Join(",", new[] { l.gear.ToString(), l.guardianPose.ToString(), l.guardianSlot.ToString(), l.bridgeInstalled.ToString(), l.bridgePose.ToString(), l.bridgeDepth.ToString(),
                l.Pass.ToString(), l.spaceClear.ToString(), l.front.ToString(), l.side.ToString(), F(l.frontDark), F(l.frontLight), F(l.sideDark), F(l.sideLight) }));
        File.WriteAllLines("Validation/solver-final-seal.csv", csv);
        checks.Add("solver: " + all.Count + " final-seal states, " + solutions.Count + " solutions (" + frameRoute.Count + " frame route, " + bridgeRoute.Count
                   + " bridge route); decoys passing one window only: front " + frontOnly.Count + ", side " + sideOnly.Count);
    }
}
}
