using NUnit.Framework;
using UnityEngine;
using BoneSquadKiting.Core;
using BoneSquadKiting.Data;

public class FormationSlotsTests
{
    static FormationDefinition Def(FormationMode mode, float spacing = 0, float radius = 0, float stagger = 0)
    {
        var d = ScriptableObject.CreateInstance<FormationDefinition>();
        d.Mode = mode; d.Spacing = spacing; d.Radius = radius; d.Stagger = stagger;
        return d;
    }

    [Test]
    public void Dense_IsCentered5x5()
    {
        var s = FormationSlots.Generate(Def(FormationMode.Grid, spacing: 0.53f));
        Assert.AreEqual(GameConstants.SquadSize, s.Length);
        Assert.That(s[12], Is.EqualTo(Vector2.zero).Using(TestUtil.V2(1e-4f)));
        Assert.That(s[0], Is.EqualTo(new Vector2(-1.06f, -1.06f)).Using(TestUtil.V2(1e-4f)));
        Assert.That(s[24], Is.EqualTo(new Vector2(1.06f, 1.06f)).Using(TestUtil.V2(1e-4f)));
    }

    [Test]
    public void Line_IsStaggeredColumn()
    {
        var s = FormationSlots.Generate(Def(FormationMode.Line, spacing: 0.49f, stagger: 0.12f));
        Assert.AreEqual(GameConstants.SquadSize, s.Length);
        Assert.That(s[0], Is.EqualTo(new Vector2(-0.12f, -5.88f)).Using(TestUtil.V2(1e-4f)));
        Assert.That(s[13], Is.EqualTo(new Vector2(0f, 0.49f)).Using(TestUtil.V2(1e-4f)));
    }

    [Test]
    public void Ring_RadiusIs3_7()
    {
        var s = FormationSlots.Generate(Def(FormationMode.Ring, radius: 3.7f));
        Assert.AreEqual(GameConstants.SquadSize, s.Length);
        foreach (var p in s) Assert.That(p.magnitude, Is.EqualTo(3.7f).Within(1e-3f));
    }

    [Test]
    public void Cross_HasCenterAndArms()
    {
        var s = FormationSlots.Generate(Def(FormationMode.Cross, spacing: 0.61f));
        Assert.AreEqual(GameConstants.SquadSize, s.Length);
        Assert.That(s[0], Is.EqualTo(Vector2.zero).Using(TestUtil.V2(1e-4f)));
        Assert.That(s[1], Is.EqualTo(new Vector2(-0.61f, 0)).Using(TestUtil.V2(1e-4f)));
        Assert.That(s[24], Is.EqualTo(new Vector2(0, 6 * 0.61f)).Using(TestUtil.V2(1e-4f)));
    }

    [Test]
    public void Rotate_PositiveDegreesTurnClockwiseSeenFromAbove()
    {
        var r = FormationSlots.Rotate(new[] { new Vector2(0, 1), new Vector2(1, 0) }, 90f);
        Assert.That(r[0], Is.EqualTo(new Vector2(1, 0)).Using(TestUtil.V2(1e-4f)));   // +z -> +x
        Assert.That(r[1], Is.EqualTo(new Vector2(0, -1)).Using(TestUtil.V2(1e-4f)));  // +x -> -z
    }

    [Test]
    public void Rotate_KeepsDistancesAndDoesNotTouchTheInput()
    {
        var src = FormationSlots.Generate(Def(FormationMode.Line, spacing: 0.49f, stagger: 0.12f));
        var first = src[0];
        var r = FormationSlots.Rotate(src, 37f);
        Assert.AreEqual(first, src[0]);
        for (int i = 0; i < src.Length; i++) Assert.That(r[i].magnitude, Is.EqualTo(src[i].magnitude).Within(1e-4f));
        var back = FormationSlots.Rotate(r, -37f);
        Assert.That(back[0], Is.EqualTo(src[0]).Using(TestUtil.V2(1e-4f)));
    }
}
