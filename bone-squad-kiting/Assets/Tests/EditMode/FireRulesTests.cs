using NUnit.Framework;
using UnityEngine;
using BoneSquadKiting.Combat;
using BoneSquadKiting.Squad;

public class FireRulesTests
{
    static readonly SquadOrder[] Orders =
    {
        SquadOrder.Move(Vector2.zero), SquadOrder.AttackMove(Vector2.zero), SquadOrder.Attack(null),
    };

    [Test]
    public void MovingUnit_NeverFires([ValueSource(nameof(Orders))] SquadOrder order)
        => Assert.IsFalse(FireRules.CanFire(new SquadUnit { Moving = true }, order));

    [Test]
    public void StoppedUnit_MayFire([ValueSource(nameof(Orders))] SquadOrder order)
        => Assert.IsTrue(FireRules.CanFire(new SquadUnit { Moving = false }, order));

    [Test]
    public void MoveToward_SetsMovingUntilTheUnitArrives()
    {
        var u = new SquadUnit { X = 0, Z = 0, Slot = Vector2.zero };
        u.MoveToward(new Vector2(1f, 0f), 0.016f);
        Assert.IsTrue(u.Moving);
        for (int i = 0; i < 60; i++) u.MoveToward(new Vector2(1f, 0f), 0.016f);
        Assert.IsFalse(u.Moving);
    }
}
