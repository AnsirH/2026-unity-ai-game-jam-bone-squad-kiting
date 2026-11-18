using System.Linq;
using NUnit.Framework;
using UnityEngine;
using BoneSquadKiting.Core;
using BoneSquadKiting.Data;

public class FormationSlotsTests
{
    const int Full = GameConstants.SquadSize;

    static FormationDefinition Def(FormationMode mode, float spacing = 0, float radius = 0, float stagger = 0)
    {
        var d = ScriptableObject.CreateInstance<FormationDefinition>();
        d.Mode = mode; d.Spacing = spacing; d.Radius = radius; d.Stagger = stagger;
        return d;
    }

    static readonly FormationDefinition Dense = Def(FormationMode.Grid, spacing: 0.53f);
    static readonly FormationDefinition Line = Def(FormationMode.Line, spacing: 0.49f, stagger: 0.12f);
    static readonly FormationDefinition Ring = Def(FormationMode.Ring, radius: 3.7f);
    static readonly FormationDefinition Cross = Def(FormationMode.Cross, spacing: 0.61f);

    static bool Has(Vector2[] slots, float x, float z)
        => slots.Any(s => Mathf.Abs(s.x - x) < 1e-3f && Mathf.Abs(s.y - z) < 1e-3f);

    // --- full squad: same footprint as the prototype ---

    [Test]
    public void Dense_FullSquadIsCentered5x5()
    {
        var s = FormationSlots.Generate(Dense, Full);
        Assert.AreEqual(Full, s.Length);
        Assert.That(s[0], Is.EqualTo(Vector2.zero).Using(TestUtil.V2(1e-4f)), "slot 0 is the anchor");
        for (int z = -2; z <= 2; z++)
            for (int x = -2; x <= 2; x++)
                Assert.IsTrue(Has(s, x * 0.53f, z * 0.53f), $"cell {x},{z}");
    }

    [Test]
    public void Line_FullSquadIsStaggeredColumn()
    {
        var s = FormationSlots.Generate(Line, Full);
        Assert.AreEqual(Full, s.Length);
        Assert.That(s[0], Is.EqualTo(new Vector2(-0.12f, 0f)).Using(TestUtil.V2(1e-4f)), "slot 0 is the middle of the line");
        Assert.IsTrue(Has(s, -0.12f, -5.88f));   // prototype i = 0
        Assert.IsTrue(Has(s, 0f, 0.49f));        // prototype i = 13
        Assert.IsTrue(Has(s, -0.12f, 5.88f));    // prototype i = 24
    }

    [Test]
    public void Ring_FullSquadHasRadius3_7()
    {
        var s = FormationSlots.Generate(Ring, Full);
        Assert.AreEqual(Full, s.Length);
        foreach (var p in s) Assert.That(p.magnitude, Is.EqualTo(3.7f).Within(1e-3f));
    }

    [Test]
    public void Cross_FullSquadHasCenterAndSixPerArm()
    {
        var s = FormationSlots.Generate(Cross, Full);
        Assert.AreEqual(Full, s.Length);
        Assert.That(s[0], Is.EqualTo(Vector2.zero).Using(TestUtil.V2(1e-4f)));
        Assert.IsTrue(Has(s, -0.61f, 0)); Assert.IsTrue(Has(s, 6 * 0.61f, 0));
        Assert.IsTrue(Has(s, 0, -6 * 0.61f)); Assert.IsTrue(Has(s, 0, 6 * 0.61f));
    }

    // --- smaller squads: built outward from the anchor ---

    [Test]
    public void EveryFormation_ReturnsOneDistinctSlotPerUnit([NUnit.Framework.Range(1, 25)] int count)
    {
        foreach (var def in new[] { Dense, Line, Ring, Cross })
        {
            var s = FormationSlots.Generate(def, count);
            Assert.AreEqual(count, s.Length, def.Mode.ToString());
            for (int i = 0; i < count; i++)
                for (int j = i + 1; j < count; j++)
                    Assert.That(Vector2.Distance(s[i], s[j]), Is.GreaterThan(1e-3f), $"{def.Mode} slots {i},{j} overlap");
        }
    }

    [Test]
    public void SmallerSquad_UsesTheInnermostSlotsOfABiggerOne([NUnit.Framework.Range(1, 24)] int count)
    {
        // Ring is excluded: it re-spaces on a smaller circle instead of dropping outer slots.
        foreach (var def in new[] { Dense, Line, Cross })
        {
            var small = FormationSlots.Generate(def, count);
            var big = FormationSlots.Generate(def, count + 1);
            float smallMax = small.Max(p => p.magnitude), bigMax = big.Max(p => p.magnitude);
            Assert.That(smallMax, Is.LessThanOrEqualTo(bigMax + 1e-4f), def.Mode.ToString());
            if (count == 1) Assert.That(small[0].magnitude, Is.LessThan(0.2f), $"{def.Mode}: a lone unit stands at the anchor");
        }
    }

    [Test]
    public void Dense_NineUnitsFormA3x3()
    {
        var s = FormationSlots.Generate(Dense, 9);
        for (int z = -1; z <= 1; z++)
            for (int x = -1; x <= 1; x++)
                Assert.IsTrue(Has(s, x * 0.53f, z * 0.53f), $"cell {x},{z}");
    }

    [Test]
    public void Line_GrowsFromTheMiddleInBothDirections()
    {
        var s = FormationSlots.Generate(Line, 5);
        Assert.That(s.Select(p => p.y).OrderBy(z => z).ToArray(),
            Is.EqualTo(new[] { -0.98f, -0.49f, 0f, 0.49f, 0.98f }).Within(1e-4f));
    }

    [Test]
    public void Ring_ShrinksInProportionToTheSquad()
    {
        Assert.That(FormationSlots.RingRadius(3.7f, 25), Is.EqualTo(3.7f).Within(1e-4f));
        Assert.That(FormationSlots.RingRadius(3.7f, 10), Is.EqualTo(1.48f).Within(1e-4f));
        foreach (var p in FormationSlots.Generate(Ring, 10)) Assert.That(p.magnitude, Is.EqualTo(1.48f).Within(1e-3f));
        // Neighbours stay the same distance apart along the circle, whatever the squad size.
        float full = 2 * Mathf.PI * 3.7f / 25, ten = 2 * Mathf.PI * FormationSlots.RingRadius(3.7f, 10) / 10;
        Assert.That(ten, Is.EqualTo(full).Within(1e-4f));
    }

    [Test]
    public void ZeroUnits_GivesNoSlots() => Assert.AreEqual(0, FormationSlots.Generate(Dense, 0).Length);

    // --- rotation ---

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
        var src = FormationSlots.Generate(Line, Full);
        var first = src[1];
        var r = FormationSlots.Rotate(src, 37f);
        Assert.AreEqual(first, src[1]);
        for (int i = 0; i < src.Length; i++) Assert.That(r[i].magnitude, Is.EqualTo(src[i].magnitude).Within(1e-4f));
        var back = FormationSlots.Rotate(r, -37f);
        Assert.That(back[1], Is.EqualTo(src[1]).Using(TestUtil.V2(1e-4f)));
    }

    // --- assignment ---

    [Test]
    public void AssignNearest_KeepsUnitsThatAlreadyStandOnASlot()
    {
        var slots = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(2, 0) };
        var units = new[] { new Vector2(2, 0), new Vector2(0, 0), new Vector2(1, 0) };
        Assert.AreEqual(new[] { 2, 0, 1 }, FormationSlots.AssignNearest(units, slots));
    }

    [Test]
    public void AssignNearest_GivesEachUnitADifferentSlot_ClosestPairsFirst()
    {
        var slots = new[] { new Vector2(0, 0), new Vector2(10, 0) };
        var units = new[] { new Vector2(1, 0), new Vector2(2, 0) }; // both nearest to slot 0
        var a = FormationSlots.AssignNearest(units, slots);
        Assert.AreEqual(0, a[0], "the closer unit takes the contested slot");
        Assert.AreEqual(1, a[1]);
    }
}
