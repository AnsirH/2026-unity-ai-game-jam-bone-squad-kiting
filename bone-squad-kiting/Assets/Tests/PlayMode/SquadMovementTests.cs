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
        Assert.That(_fc.Units[12].X, Is.EqualTo(0f).Within(1e-4f));   // center slot sits on the anchor
        Assert.That(_fc.Units[12].Z, Is.EqualTo(3.4f).Within(1e-4f));
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

    [Test]
    public void Rotate_TurnsSlotsAroundAnchor_AndSurvivesFormationChange()
    {
        _fc.SetFormation("line");
        var before = _fc.Units[24].Slot;                       // far end of the line, on +z
        _fc.RotationStepDegrees = 90f;
        _fc.Rotate(1);
        Assert.AreEqual(90f, _fc.RotationDegrees);
        Assert.That(_fc.Units[24].Slot.x, Is.EqualTo(before.y).Within(1e-3f), "+z end swings to +x");
        Assert.AreEqual(new Vector2(0, 3.4f), _fc.Anchor, "the anchor does not move");

        _fc.SetFormation("cross");
        Assert.That(Vector2.Distance(_fc.Units[4].Slot, new Vector2(0.61f, 0)), Is.LessThan(1e-3f), "new formation keeps the rotation");

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
        Assert.IsFalse(_fc.Units[0].Moving);
        _fc.Rotate(1);
        _fc.Tick(0.016f);
        Assert.IsTrue(_fc.Units[0].Moving);
        Assert.IsFalse(_fc.Units[12].Moving, "the center slot stays put");
    }
}
