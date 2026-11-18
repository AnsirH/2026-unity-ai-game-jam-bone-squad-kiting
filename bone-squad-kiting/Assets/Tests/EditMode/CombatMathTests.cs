using System.Collections.Generic;
using NUnit.Framework;
using BoneSquadKiting.Core;

public class CombatMathTests
{
    [Test]
    public void Dps_IsSumOverWindow()
    {
        var ev = new List<(float t, float d)> { (0.0f, 33), (1.0f, 33), (2.0f, 33) };
        Assert.That(CombatMath.Dps(ev, now: 2.0f), Is.EqualTo(99f / GameConstants.DpsWindow).Within(0.01f));
    }

    [Test]
    public void Dps_IgnoresEventsOlderThanWindow()
    {
        var ev = new List<(float t, float d)> { (0.0f, 33), (3.0f, 33) };
        Assert.That(CombatMath.Dps(ev, now: 3.0f), Is.EqualTo(33f / GameConstants.DpsWindow).Within(0.01f));
    }

    [Test]
    public void Phase2_TriggersBelow54Percent()
    {
        Assert.IsFalse(CombatMath.IsPhase2(0.55f));
        Assert.IsFalse(CombatMath.IsPhase2(0.54f));
        Assert.IsTrue(CombatMath.IsPhase2(0.53f));
    }
}
