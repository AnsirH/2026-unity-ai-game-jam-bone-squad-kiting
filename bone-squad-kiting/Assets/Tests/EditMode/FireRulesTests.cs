using NUnit.Framework;
using UnityEngine;
using BoneSquadKiting.Combat;
using BoneSquadKiting.Squad;

public class FireRulesTests
{
    static readonly SquadUnit Unit = new();

    [Test]
    public void MoveOrder_BlocksFireOnlyWhileMoving()
    {
        var move = SquadOrder.Move(Vector2.zero);
        Assert.IsFalse(FireRules.CanFire(Unit, move, squadMoving: true));
        Assert.IsTrue(FireRules.CanFire(Unit, move, squadMoving: false));
    }

    [Test]
    public void AttackOrders_AreNotBlockedByMovement()
    {
        Assert.IsTrue(FireRules.CanFire(Unit, SquadOrder.AttackMove(Vector2.zero), squadMoving: true));
        Assert.IsTrue(FireRules.CanFire(Unit, SquadOrder.Attack(null), squadMoving: true));
    }
}
