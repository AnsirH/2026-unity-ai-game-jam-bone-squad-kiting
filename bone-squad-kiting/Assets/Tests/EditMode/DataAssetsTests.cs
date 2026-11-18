using NUnit.Framework;
using UnityEditor;
using UnityEngine.InputSystem;
using BoneSquadKiting.Core;
using BoneSquadKiting.Data;

/// <summary>Pins the shipped data assets to the prototype's numbers.</summary>
public class DataAssetsTests
{
    static T Load<T>(string path) where T : UnityEngine.Object
    {
        var a = AssetDatabase.LoadAssetAtPath<T>(path);
        Assert.IsNotNull(a, "Missing asset: " + path);
        return a;
    }

    [Test]
    public void BossMain_MatchesPrototypeAttackTable()
    {
        var seq = Load<AttackSequence>("Assets/Data/Sequences/BossMain.asset");
        var ids = new[] { PatternId.Core, PatternId.Gate, PatternId.Quadrant, PatternId.Shell,
                          PatternId.Core, PatternId.Quadrant, PatternId.Gate, PatternId.Shell };
        var times = new[] { 2.15f, 2.05f, 2.0f, 2.1f, 1.85f, 1.8f, 1.8f, 1.8f };
        Assert.AreEqual(8, seq.Patterns.Length);
        for (int i = 0; i < 8; i++)
        {
            Assert.IsNotNull(seq.Patterns[i], "slot " + i);
            Assert.AreEqual(ids[i], seq.Patterns[i].Id, "id at slot " + i);
            Assert.That(seq.Patterns[i].Time, Is.EqualTo(times[i]).Within(1e-4f), "time at slot " + i);
        }
    }

    [Test]
    public void Patterns_HavePrototypeShapesAndRecommendations()
    {
        var seq = Load<AttackSequence>("Assets/Data/Sequences/BossMain.asset");
        foreach (var p in seq.Patterns)
        {
            var s = p.Shape;
            switch (p.Id)
            {
                case PatternId.Core:
                    Assert.AreEqual("ring", p.Recommend);
                    Assert.AreEqual(DangerShapeType.Disc, s.Type);
                    Assert.That(s.RMax, Is.EqualTo(3.10f).Within(1e-4f));
                    break;
                case PatternId.Gate:
                    Assert.AreEqual("line", p.Recommend);
                    Assert.AreEqual(DangerShapeType.CrossBandsX, s.Type);
                    Assert.That(s.Inner, Is.EqualTo(0.83f).Within(1e-4f));
                    Assert.That(s.Outer, Is.EqualTo(6.4f).Within(1e-4f));
                    Assert.That(s.HalfZ, Is.EqualTo(7.1f).Within(1e-4f));
                    break;
                case PatternId.Quadrant:
                    Assert.AreEqual("cross", p.Recommend);
                    Assert.AreEqual(DangerShapeType.QuadrantBoxes, s.Type);
                    Assert.That(s.Inner, Is.EqualTo(0.56f).Within(1e-4f));
                    Assert.That(s.Outer, Is.EqualTo(5.7f).Within(1e-4f));
                    break;
                case PatternId.Shell:
                    Assert.AreEqual("dense", p.Recommend);
                    Assert.AreEqual(DangerShapeType.Annulus, s.Type);
                    Assert.That(s.RMin, Is.EqualTo(1.73f).Within(1e-4f));
                    Assert.That(s.ROut, Is.EqualTo(6.05f).Within(1e-4f));
                    break;
            }
        }
    }

    [Test]
    public void Formations_MatchPrototypeValues()
    {
        var dense = Load<FormationDefinition>("Assets/Data/Formations/dense.asset");
        var line = Load<FormationDefinition>("Assets/Data/Formations/line.asset");
        var ring = Load<FormationDefinition>("Assets/Data/Formations/ring.asset");
        var cross = Load<FormationDefinition>("Assets/Data/Formations/cross.asset");

        Assert.AreEqual(FormationMode.Grid, dense.Mode);  Assert.That(dense.Spacing, Is.EqualTo(0.53f).Within(1e-4f));
        Assert.AreEqual(FormationMode.Line, line.Mode);   Assert.That(line.Spacing, Is.EqualTo(0.49f).Within(1e-4f));
        Assert.That(line.Stagger, Is.EqualTo(0.12f).Within(1e-4f));
        Assert.AreEqual(FormationMode.Ring, ring.Mode);   Assert.That(ring.Radius, Is.EqualTo(3.7f).Within(1e-4f));
        Assert.AreEqual(FormationMode.Cross, cross.Mode); Assert.That(cross.Spacing, Is.EqualTo(0.61f).Within(1e-4f));

        Assert.AreEqual("dense", dense.Id); Assert.AreEqual(Key.Q, dense.HotKey);
        Assert.AreEqual("line", line.Id);   Assert.AreEqual(Key.W, line.HotKey);
        Assert.AreEqual("ring", ring.Id);   Assert.AreEqual(Key.E, ring.HotKey);
        Assert.AreEqual("cross", cross.Id); Assert.AreEqual(Key.R, cross.HotKey);
    }
}
