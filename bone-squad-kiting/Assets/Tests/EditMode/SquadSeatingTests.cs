using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using BoneSquadKiting.Core;
using BoneSquadKiting.Data;
using BoneSquadKiting.Squad;

public class SquadSeatingTests
{
    static Vector2[] Line()
    {
        var d = ScriptableObject.CreateInstance<FormationDefinition>();
        d.Mode = FormationMode.Line; d.Spacing = 0.49f; d.Stagger = 0.12f;
        var s = FormationSlots.Generate(d, GameConstants.SquadSize);
        Object.DestroyImmediate(d);
        return s;
    }

    static float ClosestPair(IReadOnlyList<Vector2> p)
    {
        float best = float.MaxValue;
        for (int i = 0; i < p.Count; i++) for (int j = i + 1; j < p.Count; j++) best = Mathf.Min(best, Vector2.Distance(p[i], p[j]));
        return best;
    }

    [Test]
    public void NoWallInTheWay_EveryUnitStandsOnItsSlot()
    {
        var g = WalkGrid.Open(new Vector2(-20, -20), new Vector2(20, 20), 0.75f);
        var slots = Line(); var anchor = new Vector2(1.3f, -2.1f);
        var seats = SquadSeating.Resolve(g, anchor, slots, 0.5f);
        for (int i = 0; i < slots.Length; i++) Assert.AreEqual(anchor + slots[i], seats[i]);
    }

    [Test]
    public void LineThroughAWall_PressedUnitsGetDistinctWalkableSeats()
    {
        var g = WalkGrid.Open(new Vector2(-10, 0), new Vector2(10, 20), 0.75f);   // wall is everything below z = 0
        var slots = Line(); var anchor = new Vector2(0f, 1.2f);
        var seats = SquadSeating.Resolve(g, anchor, slots, 0.5f);

        int pressed = 0;
        for (int i = 0; i < slots.Length; i++)
        {
            Assert.IsTrue(g.IsWalkable(seats[i]), $"unit {i} at {seats[i]}");
            if (g.IsWalkable(anchor + slots[i])) Assert.AreEqual(anchor + slots[i], seats[i], "units inside keep their slot");
            else pressed++;
        }
        Assert.That(pressed, Is.GreaterThan(5), "the test really presses part of the line");
        // Seats are 0.5 apart. A pressed unit's seat can sit as close as half a seat to a unit standing on
        // its own (off-lattice) slot, so the guarantee is "never stacked", not "a full spacing apart".
        Assert.That(ClosestPair(seats), Is.GreaterThan(0.2f), "nobody shares a spot");
    }

    [Test]
    public void SameInput_SameResult()
    {
        var g = WalkGrid.Open(new Vector2(-10, 0), new Vector2(10, 20), 0.75f);
        var slots = Line(); var anchor = new Vector2(0.4f, 0.9f);
        CollectionAssert.AreEqual(SquadSeating.Resolve(g, anchor, slots, 0.5f), SquadSeating.Resolve(g, anchor, slots, 0.5f));
    }

    [Test]
    public void PressedUnits_StayNearTheWallTheyWerePushedInto()
    {
        var g = WalkGrid.Open(new Vector2(-10, 0), new Vector2(10, 20), 0.75f);
        var slots = Line(); var anchor = new Vector2(0f, 1.2f);
        var seats = SquadSeating.Resolve(g, anchor, slots, 0.5f);
        for (int i = 0; i < slots.Length; i++)
            if (!g.IsWalkable(anchor + slots[i]))
                Assert.That(seats[i].y, Is.LessThan(2.5f), $"pressed unit {i} ended up far from the wall at {seats[i]}");
    }
}
