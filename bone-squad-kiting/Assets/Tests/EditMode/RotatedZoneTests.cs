using NUnit.Framework;
using UnityEngine;
using BoneSquadKiting.Core;
using BoneSquadKiting.Data;

public class RotatedZoneTests
{
    static readonly DangerShape Gate = DangerShape.BandsX(0.83f, 6.4f, 7.1f);   // safe corridor runs along z

    [Test]
    public void ZeroAngle_IsThePlainTest()
    {
        Assert.AreEqual(DangerShapeEval.InDanger(Gate, 1, 2, 3, 4), DangerShapeEval.InDanger(Gate, 1, 2, 3, 4, 0f));
    }

    [Test]
    public void TurnedAQuarter_TheSafeCorridorRunsAlongX()
    {
        Assert.IsFalse(DangerShapeEval.InDanger(Gate, 0, 0, 0f, 2f), "unturned: on the z corridor");
        Assert.IsTrue(DangerShapeEval.InDanger(Gate, 0, 0, 2f, 0f), "unturned: off it");

        Assert.IsTrue(DangerShapeEval.InDanger(Gate, 0, 0, 0f, 2f, 90f), "turned: the old corridor is now danger");
        Assert.IsFalse(DangerShapeEval.InDanger(Gate, 0, 0, 2f, 0f, 90f), "turned: the corridor lies along x");
    }

    [Test]
    public void TurningTheFormationByTheSameAngle_KeepsItSafe([Values(0f, 15f, 37f, 90f, 135f, 250f, -60f)] float angle)
    {
        // The line formation dodges the gate. Whatever angle the gate has turned to, a line turned by the
        // same amount (same sign convention as the mouse wheel) still fits the corridor.
        var d = ScriptableObject.CreateInstance<FormationDefinition>();
        d.Mode = FormationMode.Line; d.Spacing = 0.49f; d.Stagger = 0.12f;
        var line = FormationSlots.Generate(d, GameConstants.SquadSize);
        Object.DestroyImmediate(d);

        foreach (var slot in FormationSlots.Rotate(line, angle))
            Assert.IsFalse(DangerShapeEval.InDanger(Gate, 0, 0, slot.x, slot.y, angle), $"slot {slot} at {angle} degrees");

        if (Mathf.Abs(Mathf.DeltaAngle(0f, angle)) > 30f)
        {
            int caught = 0;
            foreach (var slot in line) if (DangerShapeEval.InDanger(Gate, 0, 0, slot.x, slot.y, angle)) caught++;
            Assert.That(caught, Is.GreaterThan(0), "a line that did not turn is caught");
        }
    }

    [Test]
    public void RotationIsAroundTheZoneCenter()
    {
        Assert.IsFalse(DangerShapeEval.InDanger(Gate, 10, 5, 12f, 5f, 90f), "on the turned corridor through the center");
        Assert.IsTrue(DangerShapeEval.InDanger(Gate, 10, 5, 10f, 7f, 90f));
    }
}
