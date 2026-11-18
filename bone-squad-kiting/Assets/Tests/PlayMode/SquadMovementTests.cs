using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using BoneSquadKiting.Core;
using BoneSquadKiting.Squad;

public class SquadMovementTests
{
    FormationController _fc;

    [SetUp] public void SetUp() { _fc = SquadTestRig.MakeFc(); }
    [TearDown] public void TearDown() { if (_fc != null) Object.Destroy(_fc.gameObject); }

    IEnumerator Run(float seconds)
    {
        for (float t = 0; t < seconds; t += 0.016f) { _fc.Tick(0.016f); yield return null; }
    }

    [Test]
    public void Init_PlacesFullSquadInDenseAtStart()
    {
        Assert.AreEqual(GameConstants.SquadSize, _fc.Units.Count);
        Assert.AreEqual(new Vector2(0, 3.4f), _fc.Anchor);
        Assert.That(_fc.Units[0].X, Is.EqualTo(0f).Within(1e-4f));   // slot 0 sits on the anchor
        Assert.That(_fc.Units[0].Z, Is.EqualTo(3.4f).Within(1e-4f));
    }

    [UnityTest]
    public IEnumerator Anchor_MovesTowardDestination()
    {
        _fc.SetDestination(new Vector2(5f, 2f));
        yield return Run(2f);
        Assert.That(Vector2.Distance(_fc.Anchor, new Vector2(5f, 2f)), Is.LessThan(0.05f));
    }

    [Test]
    public void Destination_IsClampedToCommanderArea()
    {
        _fc.SetDestination(new Vector2(100f, -100f));
        Assert.AreEqual(new Vector2(GameConstants.MoveClampX, GameConstants.MoveClampZMin), _fc.Destination);
    }

    [UnityTest]
    public IEnumerator Units_ConvergeToSlotsAfterFormationChange()
    {
        Assert.IsTrue(_fc.SetFormation("ring"));
        _fc.SetDestination(new Vector2(-3f, 1f));
        yield return Run(3f);
        foreach (var u in _fc.Units)
        {
            Vector2 want = _fc.Anchor + u.Slot;
            Assert.That(Vector2.Distance(new Vector2(u.X, u.Z), want), Is.LessThan(0.05f), "unit " + u.Id);
        }
    }

    [Test]
    public void SetFormation_RejectsUnknownId()
    {
        Assert.IsFalse(_fc.SetFormation("nope"));
        Assert.AreEqual("dense", _fc.Formation);
    }

    [Test]
    public void DeadUnits_DoNotMove()
    {
        var u = _fc.Units[0];
        u.Dead = true;
        float x = u.X, z = u.Z;
        _fc.SetDestination(new Vector2(5f, 2f));
        for (int i = 0; i < 60; i++) _fc.Tick(0.016f);
        Assert.AreEqual(x, u.X); Assert.AreEqual(z, u.Z);
    }

    static bool HasSlot(FormationController fc, Vector2 slot)
    {
        foreach (var u in fc.Units) if (!u.Dead && Vector2.Distance(u.Slot, slot) < 1e-3f) return true;
        return false;
    }

    [Test]
    public void Rotate_TurnsSlotsAroundAnchor_AndSurvivesFormationChange()
    {
        _fc.SetFormation("line");
        Assert.IsTrue(HasSlot(_fc, new Vector2(-0.12f, 5.88f)), "far end of the line is on +z");
        _fc.RotationStepDegrees = 90f;
        _fc.Rotate(1);
        Assert.AreEqual(90f, _fc.RotationDegrees);
        Assert.IsTrue(HasSlot(_fc, new Vector2(5.88f, 0.12f)), "+z end swings to +x");
        Assert.AreEqual(new Vector2(0, 3.4f), _fc.Anchor, "the anchor does not move");

        _fc.SetFormation("cross");
        Assert.IsTrue(HasSlot(_fc, new Vector2(0.61f, 0)), "new formation keeps the rotation");

        _fc.Rotate(-1);
        Assert.AreEqual(0f, _fc.RotationDegrees);
        _fc.Rotate(-1);
        Assert.AreEqual(270f, _fc.RotationDegrees, "wraps into 0..360");

        _fc.Init();
        Assert.AreEqual(0f, _fc.RotationDegrees, "restart resets rotation");
    }

    [Test]
    public void Rotate_MakesUnitsMove_SoTheyHoldFire()
    {
        for (int i = 0; i < 10; i++) _fc.Tick(0.016f);
        foreach (var u in _fc.Units) Assert.IsFalse(u.Moving);
        _fc.Rotate(1);
        _fc.Tick(0.016f);
        Assert.IsFalse(_fc.Units[0].Moving, "the unit on the anchor stays put");
        int moving = 0; foreach (var u in _fc.Units) if (u.Moving) moving++;
        Assert.AreEqual(GameConstants.SquadSize - 1, moving);
    }

    [Test]
    public void FormationChange_EveryUnitGetsItsOwnSlot()
    {
        foreach (var id in new[] { "line", "ring", "cross", "dense" })
        {
            _fc.SetFormation(id);
            for (int i = 0; i < _fc.Units.Count; i++)
                for (int j = i + 1; j < _fc.Units.Count; j++)
                    Assert.That(Vector2.Distance(_fc.Units[i].Slot, _fc.Units[j].Slot), Is.GreaterThan(1e-3f), $"{id}: units {i},{j} share a slot");
        }
    }

    [Test]
    public void Reform_ClosesUpAroundTheAnchor_WhenUnitsDie()
    {
        _fc.SetFormation("ring");
        for (int i = 0; i < 15; i++) _fc.Units[i].Dead = true;   // 10 survivors
        _fc.Reform();
        float expected = FormationSlots.RingRadius(3.7f, 10);
        foreach (var u in _fc.Units)
            if (!u.Dead) Assert.That(u.Slot.magnitude, Is.EqualTo(expected).Within(1e-3f));

        _fc.SetFormation("dense");
        float reach = 0; foreach (var u in _fc.Units) if (!u.Dead) reach = Mathf.Max(reach, u.Slot.magnitude);
        Assert.That(reach, Is.LessThan(2 * 0.53f + 1e-3f), "10 units fit within two cells of the anchor");
        Assert.IsTrue(HasSlot(_fc, Vector2.zero), "the anchor slot is always filled");
    }

    [Test]
    public void Reform_MovesSurvivorsAsLittleAsPossible()
    {
        for (int i = 0; i < 10; i++) _fc.Tick(0.016f);
        var corner = _fc.Units[24];                      // outermost slot of the dense grid
        Assert.That(corner.Slot.magnitude, Is.GreaterThan(1.4f));
        corner.Dead = true;
        var before = new Vector2[24];
        for (int i = 0; i < 24; i++) before[i] = _fc.Units[i].Slot;
        _fc.Reform();
        for (int i = 0; i < 24; i++) Assert.AreEqual(before[i], _fc.Units[i].Slot, $"unit {i} keeps its slot");
    }
}
