using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using BoneSquadKiting.Core;
using BoneSquadKiting.Data;

namespace BoneSquadKiting.EditorTools
{
    /// <summary>
    /// Writes the default pattern / formation / sequence assets with the prototype's values.
    /// Existing assets are updated in place so their GUIDs (and scene references) survive.
    /// Run from the menu, or headless: -executeMethod BoneSquadKiting.EditorTools.DefaultDataBuilder.Build
    /// </summary>
    public static class DefaultDataBuilder
    {
        const string Root = "Assets/Data";

        [MenuItem("BoneSquadKiting/Rebuild Default Data")]
        public static void Build()
        {
            EnsureFolder(Root, "Patterns"); EnsureFolder(Root, "Formations"); EnsureFolder(Root, "Sequences");

            Formation("dense", "밀집", Key.Q, FormationMode.Grid, spacing: 0.53f);
            Formation("line", "일자", Key.W, FormationMode.Line, spacing: 0.49f, stagger: 0.12f);
            Formation("ring", "원형", Key.E, FormationMode.Ring, radius: 3.7f);
            Formation("cross", "십자가", Key.R, FormationMode.Cross, spacing: 0.61f);

            var seq = Upsert<AttackSequence>(Root + "/Sequences/BossMain.asset");
            seq.Patterns = new[]
            {
                Pattern(0, PatternId.Core, 2.15f), Pattern(1, PatternId.Gate, 2.05f),
                Pattern(2, PatternId.Quadrant, 2.0f), Pattern(3, PatternId.Shell, 2.1f),
                Pattern(4, PatternId.Core, 1.85f), Pattern(5, PatternId.Quadrant, 1.8f),
                Pattern(6, PatternId.Gate, 1.8f), Pattern(7, PatternId.Shell, 1.8f),
            };
            EditorUtility.SetDirty(seq);

            // The basic swarm enemy. Values are MobDefinition's defaults (starting numbers from the M2 design).
            EnsureFolder(Root, "Mobs");
            var grunt = Upsert<MobDefinition>(Root + "/Mobs/Grunt.asset");
            grunt.DisplayName = "잡몹";
            EditorUtility.SetDirty(grunt);

            // A basic mini boss, reusing three of the boss's patterns. Its zones are fixed where they are announced.
            EnsureFolder(Root, "Elites");
            var brute = Upsert<EliteDefinition>(Root + "/Elites/Brute.asset");
            brute.DisplayName = "미니 보스"; brute.Patterns = new List<PatternDefinition> { seq.Patterns[0], seq.Patterns[3], seq.Patterns[1] };   // core, shell, gate
            EditorUtility.SetDirty(brute);

            // The five mini bosses of the stage lineup. Each casts one pattern; everything but the pattern,
            // the name and the cannon's settings is the EliteDefinition default (starting values, not tuned).
            // The telekinetic mage is meant to cast rotating zones, which do not exist yet: it uses the plain
            // gate pattern for now.
            Elite("Catapult", "투석병", seq.Patterns[0], cannon: true);
            Elite("DarkFlameMage", "검정불꽃마법사", seq.Patterns[3]);
            Elite("SpearThrower", "창투척병", seq.Patterns[1]);
            Elite("ShadowSwordsman", "그림자검사", seq.Patterns[2]);
            // The telekinetic mage's zones turn (ZoneRotationSpeed on the mini boss). Three variants so every
            // kind of rotating zone can be tried; which mini boss uses which is a placeholder choice.
            var sustainedGate = Upsert<PatternDefinition>(Root + "/Patterns/Gate_Sustained.asset");
            CopyPattern(seq.Patterns[1], sustainedGate);
            sustainedGate.DisplayName = "쌍벽 회전 (지속)"; sustainedGate.Detail = "벽이 돌면서 계속 휩씁니다. 일자로 따라 도세요!";
            sustainedGate.Kind = PatternKind.Sustained; sustainedGate.SustainSeconds = 4f; sustainedGate.TickInterval = 0.5f; sustainedGate.TickDamage = 15f;
            EditorUtility.SetDirty(sustainedGate);

            Elite("TelekineticMage", "염동력 마법사", sustainedGate, rotation: 30f);                 // sustained, turning gate
            Elite("TelekineticMageGate", "염동력 마법사", seq.Patterns[1], rotation: 40f);            // burst, turning gate
            Elite("TelekineticMageQuadrant", "염동력 마법사", seq.Patterns[2], rotation: -40f);       // burst, turning quadrants, the other way

            // The shared tuning asset. Created once with the values in use so far; after that it is edited by
            // hand (often while playing), so an existing one is left alone.
            if (AssetDatabase.LoadAssetAtPath<GameTuning>(Root + "/GameTuning.asset") == null)
            {
                var tuning = Upsert<GameTuning>(Root + "/GameTuning.asset");
                EditorUtility.SetDirty(tuning);
            }

            // The shared art set. Created once with the squad models if they are in the project; after that it
            // is the artists' to edit, so an existing one is left alone.
            EnsureFolder(Root, "Art");
            if (AssetDatabase.LoadAssetAtPath<ArtSet>(Root + "/Art/ArtSet.asset") == null)
            {
                var art = Upsert<ArtSet>(Root + "/Art/ArtSet.asset");
                art.UnitPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Character/Soldier/Prefabs/Soldier.prefab");
                art.LeaderPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Character/Commander/Prefabs/Commander.prefab");
                // The models are much bigger than the game's unit spacing (0.5): shrink them to fit.
                art.UnitScale = 0.36f; art.LeaderScale = 0.17f;
                // State names as they are in the models' Animators today. Missing ones are skipped at runtime.
                // Soldier attacks use an upper-body overlay; UnitVisual enables that layer only while firing.
                art.UnitStates = new AnimStates { Idle = "Idle", Run = "Soldier_MeshyAI_Run02", Attack = "RunAndShoot_UpperBody", Die = "" };
                art.LeaderStates = new AnimStates { Idle = "Idle", Run = "Soldier_MeshyAI_Run02", Attack = "Commander_Thrrow", Die = "" };
                EditorUtility.SetDirty(art);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("DefaultDataBuilder: wrote 4 formations, 8 patterns, 1 sequence.");
        }

        static void CopyPattern(PatternDefinition from, PatternDefinition to)
        {
            to.Id = from.Id; to.DisplayName = from.DisplayName; to.Advice = from.Advice; to.Detail = from.Detail;
            to.Recommend = from.Recommend; to.Time = from.Time; to.Shape = from.Shape; to.ScaleWithSquad = from.ScaleWithSquad;
        }

        static void Elite(string file, string name, PatternDefinition pattern, bool cannon = false, float rotation = 0f)
        {
            var e = Upsert<EliteDefinition>($"{Root}/Elites/{file}.asset");
            e.DisplayName = name; e.Patterns = new List<PatternDefinition> { pattern };
            e.ZoneRotationSpeed = rotation;
            if (cannon)
            {
                // A fixed emplacement: does not walk, fires irregularly, weaker than a walking mini boss.
                e.Speed = 0f; e.MaxHp = 1500f; e.Radius = 0.9f;
                e.AttackGap = 3.5f; e.AttackGapRandom = 3f; e.AttackRange = 14f;
            }
            EditorUtility.SetDirty(e);
        }

        static void Formation(string id, string name, Key key, FormationMode mode,
                              float spacing = 0, float stagger = 0, float radius = 0)
        {
            var f = Upsert<FormationDefinition>($"{Root}/Formations/{id}.asset");
            f.Id = id; f.DisplayName = name; f.HotKey = key; f.Mode = mode;
            f.Spacing = spacing; f.Stagger = stagger; f.Radius = radius;
            EditorUtility.SetDirty(f);
        }

        static PatternDefinition Pattern(int slot, PatternId id, float time)
        {
            var p = Upsert<PatternDefinition>($"{Root}/Patterns/{slot:00}_{id}.asset");
            p.Id = id; p.Time = time;
            p.ScaleWithSquad = id == PatternId.Core; // shrinks with the squad, like the ring that dodges it
            switch (id)
            {
                case PatternId.Core:
                    p.DisplayName = "중앙 핵폭발"; p.Advice = "E · 원형"; p.Recommend = "ring";
                    p.Detail = "중앙이 폭발합니다. 원형으로 흩어지세요!"; p.Shape = DangerShape.Disc(3.10f); break;
                case PatternId.Gate:
                    p.DisplayName = "쌍벽 압착"; p.Advice = "W · 일자"; p.Recommend = "line";
                    p.Detail = "좌우가 휩쓸립니다. 일자로 줄이세요!"; p.Shape = DangerShape.BandsX(0.83f, 6.4f, 7.1f); break;
                case PatternId.Quadrant:
                    p.DisplayName = "사분면 폭격"; p.Advice = "R · 십자가"; p.Recommend = "cross";
                    p.Detail = "십자 통로만 안전합니다!"; p.Shape = DangerShape.Quadrants(0.56f, 5.7f); break;
                case PatternId.Shell:
                    p.DisplayName = "외곽 소거"; p.Advice = "Q · 밀집"; p.Recommend = "dense";
                    p.Detail = "바깥부터 폭발합니다. 중심으로 모이세요!"; p.Shape = DangerShape.Annulus(1.73f, 6.05f); break;
            }
            EditorUtility.SetDirty(p);
            return p;
        }

        static T Upsert<T>(string path) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a != null) return a;
            a = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(a, path);
            return a;
        }

        static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Data")) AssetDatabase.CreateFolder("Assets", "Data");
            if (!AssetDatabase.IsValidFolder(parent + "/" + child)) AssetDatabase.CreateFolder(parent, child);
        }
    }
}
