using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using BoneSquadKiting.Combat;
using BoneSquadKiting.Squad;

public class StationaryAttackStreakTests
{
    sealed class Enemy : IEnemy, IDamageable
    {
        public Vector2 Pos => new(0f, 1f);
        public bool Alive => true;
        public void ApplyDamage(float amount) { }
    }

    readonly Enemy _enemy = new();
    readonly SquadOrder _order = SquadOrder.Move(Vector2.zero);

    void Hit(ShotSystem system, SquadUnit unit, IList<SquadUnit> units)
    {
        system.Shots.Add(new Shot { Owner = unit.Id, AttackStreakEpoch = unit.AttackStreakEpoch,
            Target = _enemy, X = 0f, Z = 1f, Life = 2f, Damage = 1f });
        system.Tick(units, new EnemyRegistry(), _order, .01f);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void HitsIncreaseIndividualSpeedAndClampAtCap(bool leader)
    {
        var system = new ShotSystem { StationaryRampEnabled = true };
        var unit = new SquadUnit { Id = 1, IsLeader = leader, Cd = 1000f };
        var other = new SquadUnit { Id = 2, Cd = 1000f };
        var units = new[] { unit, other };
        Hit(system, unit, units);
        Assert.That(unit.AttackSpeedMultiplier, Is.EqualTo(1.01f).Within(.00001f));
        Assert.That(other.AttackSpeedMultiplier, Is.EqualTo(1f));
        for (int i = 0; i < 100; i++) Hit(system, unit, units);
        Assert.That(unit.AttackSpeedMultiplier, Is.EqualTo(1.25f));
        Assert.That(unit.AtAttackSpeedCap, Is.True);
    }

    [Test]
    public void MovementDecaysSpeedAndOldFlightCannotRestoreStacks()
    {
        var system = new ShotSystem { StationaryRampEnabled = true };
        var unit = new SquadUnit { Id = 1, Cd = 1000f };
        var units = new[] { unit };
        Hit(system, unit, units);
        int oldEpoch = unit.AttackStreakEpoch;
        unit.Moving = true;
        system.Tick(units, new EnemyRegistry(), _order, .01f);
        Assert.That(unit.AttackSpeedMultiplier, Is.EqualTo(1.009f).Within(.00001f));
        Assert.That(unit.AtAttackSpeedCap, Is.False);
        unit.Moving = false;
        system.Shots.Add(new Shot { Owner = 1, AttackStreakEpoch = oldEpoch,
            Target = _enemy, X = 0f, Z = 1f, Life = 2f, Damage = 1f });
        system.Tick(units, new EnemyRegistry(), _order, .01f);
        Assert.That(unit.StationaryHits, Is.Zero);
        Assert.That(unit.AttackSpeedMultiplier, Is.EqualTo(1.009f).Within(.00001f));
    }

    [Test]
    public void MovementDecayLeavesCapAndStopsAtBaseSpeed()
    {
        var system = new ShotSystem { StationaryRampEnabled = true, MovingSpeedDecayPerSecond = .1f };
        var unit = new SquadUnit { Id = 1, Moving = true, Cd = 1000f,
            AttackSpeedMultiplier = 1.25f, AtAttackSpeedCap = true };
        var units = new[] { unit };
        system.Tick(units, new EnemyRegistry(), _order, .5f);
        Assert.That(unit.AttackSpeedMultiplier, Is.EqualTo(1.2f).Within(.00001f));
        Assert.That(unit.AtAttackSpeedCap, Is.False);
        system.Tick(units, new EnemyRegistry(), _order, 10f);
        Assert.That(unit.AttackSpeedMultiplier, Is.EqualTo(1f));
    }

    [Test]
    public void ResumingAttackGrowsFromRemainingSpeedWithoutJumping()
    {
        var system = new ShotSystem { StationaryRampEnabled = true, MovingSpeedDecayPerSecond = .1f };
        var unit = new SquadUnit { Id = 1, Moving = true, Cd = 1000f, AttackSpeedMultiplier = 1.25f };
        var units = new[] { unit };
        system.Tick(units, new EnemyRegistry(), _order, 1f);
        unit.Moving = false;
        var registry = new EnemyRegistry(); registry.Register(_enemy);
        system.Tick(units, registry, _order, .01f);
        Assert.That(unit.AttackSpeedMultiplier, Is.EqualTo(1.15f).Within(.00001f));
        Hit(system, unit, units);
        Assert.That(unit.AttackSpeedMultiplier, Is.EqualTo(1.15f * 1.01f).Within(.00001f));
    }

    [Test]
    public void NoTargetResetsAfterGraceButCooldownAgainstTargetKeepsStacks()
    {
        var system = new ShotSystem { StationaryRampEnabled = true, AttackStreakGraceSeconds = 1f };
        var unit = new SquadUnit { Id = 1, Cd = 1000f };
        var units = new[] { unit };
        Hit(system, unit, units);
        var registry = new EnemyRegistry(); registry.Register(_enemy);
        system.Tick(units, registry, _order, 2f);
        Assert.That(unit.StationaryHits, Is.EqualTo(1), "a slow leader's cooldown is still continuous attacking");
        system.Tick(units, new EnemyRegistry(), _order, .5f);
        Assert.That(unit.StationaryHits, Is.EqualTo(1));
        system.Tick(units, new EnemyRegistry(), _order, .51f);
        Assert.That(unit.StationaryHits, Is.Zero);
    }

    [Test]
    public void CounterCountsLandedHitsOnlyAndExpires()
    {
        var system = new ShotSystem { HitCounterHoldSeconds = 2f };
        var unit = new SquadUnit { Id = 1, Cd = 1000f };
        var units = new[] { unit };
        system.Shots.Add(new Shot { Owner = 1, Target = _enemy, Life = 0f, Damage = 1f });
        system.Tick(units, new EnemyRegistry(), _order, .01f);
        Assert.That(system.ConsecutiveHits, Is.Zero);
        Hit(system, unit, units); Hit(system, unit, units);
        Assert.That(system.ConsecutiveHits, Is.EqualTo(2));
        Assert.That(system.HitSerial, Is.EqualTo(2));
        system.Tick(units, new EnemyRegistry(), _order, 2.1f);
        Assert.That(system.ConsecutiveHits, Is.Zero);
        Assert.That(unit.AttackSpeedMultiplier, Is.EqualTo(1f), "disabled ramp leaves combat speed unchanged");
    }

    [Test]
    public void NewAttackScalesCooldownAndWindupAndLatchesSpeed()
    {
        var system = new ShotSystem { StationaryRampEnabled = true, SpeedGainPerHit = .1f,
            UnitInterval = 1f, UnitIntervalRandom = 0f };
        var unit = new SquadUnit { Id = 1, Cd = 1000f };
        var units = new[] { unit };
        Hit(system, unit, units);
        unit.Cd = 0f; unit.AttackDuration = .8f; unit.AttackWindup = .4f;
        unit.HasReleaseEvent = true;
        var registry = new EnemyRegistry(); registry.Register(_enemy);
        system.Tick(units, registry, _order, .01f);
        Assert.That(unit.Cd, Is.EqualTo(1f / 1.1f).Within(.0001f));
        Assert.That(unit.WindupLeft, Is.EqualTo(.8f / 1.1f).Within(.0001f));
        Assert.That(unit.AttackCycleSpeed, Is.EqualTo(1.1f).Within(.0001f));
        Hit(system, unit, units);
        Assert.That(unit.AttackSpeedMultiplier, Is.GreaterThan(unit.AttackCycleSpeed));
        Assert.That(unit.AttackCycleSpeed, Is.EqualTo(1.1f).Within(.0001f), "in-flight animation keeps its release timing");
    }
}
