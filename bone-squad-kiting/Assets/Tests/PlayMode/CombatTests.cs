using NUnit.Framework;
using UnityEngine;
using BoneSquadKiting.Bosses;
using BoneSquadKiting.Combat;
using BoneSquadKiting.Core;
using BoneSquadKiting.Squad;

public class CombatTests
{
    class FakeEnemy : IEnemy, IDamageable
    {
        public Vector2 Pos { get; set; }
        public bool Alive => Hp > 0;
        public float Hp = MaxHp;
        public const float MaxHp = 100000; // large enough that no enemy dies mid-test and frees the squad to retarget
        public void ApplyDamage(float amount) { Hp -= amount; }
    }

    FormationController _fc;

    [SetUp] public void SetUp() { _fc = SquadTestRig.MakeFc(); }
    [TearDown] public void TearDown() { if (_fc != null) Object.Destroy(_fc.gameObject); }

    void Run(ShotSystem shots, EnemyRegistry reg, SquadOrder order, float seconds)
    {
        for (float t = 0; t < seconds; t += 0.016f) { _fc.Tick(0.016f); shots.Tick(_fc.Units, reg, order, 0.016f); }
    }

    [Test]
    public void Boss_TakesDamageFromAutoFire()
    {
        var boss = new Boss();
        var reg = new EnemyRegistry(); reg.Register(boss);
        var shots = new ShotSystem();
        float dealt = 0; shots.OnHit += d => dealt += d;

        Run(shots, reg, SquadOrder.Move(_fc.Anchor), 3f);

        Assert.That(boss.Hp, Is.LessThan(GameConstants.BossMax));
        Assert.That(GameConstants.BossMax - boss.Hp, Is.EqualTo(dealt).Within(0.01f));
        Assert.That(dealt % GameConstants.UnitDamage, Is.EqualTo(0f).Within(0.01f));
    }

    [Test]
    public void MoveOrder_HoldsFireWhileMoving_ThenFiresOnArrival()
    {
        var boss = new Boss();
        var reg = new EnemyRegistry(); reg.Register(boss);
        var shots = new ShotSystem();
        var order = SquadOrder.Move(new Vector2(6f, 3.4f)); // ~0.8s walk at anchor speed 7.4
        _fc.Order = order; _fc.SetDestination(order.Point);

        Run(shots, reg, order, 0.6f);
        Assert.IsTrue(_fc.IsMoving);
        Assert.AreEqual(0, shots.Shots.Count, "no shots while a Move is in progress");
        Assert.AreEqual(GameConstants.BossMax, boss.Hp);

        Run(shots, reg, order, 2f);
        Assert.IsFalse(_fc.IsMoving);
        Assert.That(boss.Hp, Is.LessThan(GameConstants.BossMax), "auto-fire resumes after arriving");
    }

    [Test]
    public void AttackMove_AlsoHoldsFireWhileMoving()
    {
        var boss = new Boss();
        var reg = new EnemyRegistry(); reg.Register(boss);
        var shots = new ShotSystem();
        var order = SquadOrder.AttackMove(new Vector2(6f, 3.4f));
        _fc.Order = order; _fc.SetDestination(order.Point);

        Run(shots, reg, order, 0.6f);
        Assert.AreEqual(0, shots.Shots.Count, "moving units never fire, whatever the order");
    }

    [Test]
    public void FormationChange_SilencesUnitsUntilTheyReachTheirSlots()
    {
        var boss = new Boss();
        var reg = new EnemyRegistry(); reg.Register(boss);
        var shots = new ShotSystem();
        var order = SquadOrder.Move(_fc.Anchor);
        Run(shots, reg, order, 0.05f);          // settle: everyone is on a slot and may fire
        shots.Shots.Clear();
        foreach (var u in _fc.Units) u.Cd = 0;   // all ready to fire again

        _fc.SetFormation("ring");
        _fc.Tick(0.016f); shots.Tick(_fc.Units, reg, order, 0.016f);
        Assert.AreEqual(0, shots.Shots.Count, "units sliding to new slots do not fire");

        Run(shots, reg, order, 2f);
        Assert.That(boss.Hp, Is.LessThan(GameConstants.BossMax), "they fire again once in place");
    }

    [Test]
    public void AttackTargetOrder_FocusesTheOrderedEnemy()
    {
        var near = new FakeEnemy { Pos = new Vector2(0, 0) };
        var far = new FakeEnemy { Pos = new Vector2(0, -6) };
        var reg = new EnemyRegistry(); reg.Register(near); reg.Register(far);

        Run(new ShotSystem(), reg, SquadOrder.Attack(far), 2f);

        Assert.That(far.Hp, Is.LessThan(FakeEnemy.MaxHp));
        Assert.AreEqual(FakeEnemy.MaxHp, near.Hp);
    }

    [Test]
    public void WithoutTargetOrder_NearestEnemyIsShot()
    {
        var near = new FakeEnemy { Pos = new Vector2(0, 0) };
        var far = new FakeEnemy { Pos = new Vector2(0, -6) };
        var reg = new EnemyRegistry(); reg.Register(near); reg.Register(far);

        Run(new ShotSystem(), reg, SquadOrder.AttackMove(_fc.Anchor), 2f);

        Assert.That(near.Hp, Is.LessThan(FakeEnemy.MaxHp));
        Assert.AreEqual(FakeEnemy.MaxHp, far.Hp);
    }

    [Test]
    public void DeadUnits_DoNotFire_AndOutOfRangeEnemyIsIgnored()
    {
        foreach (var u in _fc.Units) u.Dead = true;
        var boss = new Boss();
        var reg = new EnemyRegistry(); reg.Register(boss);
        var shots = new ShotSystem();
        Run(shots, reg, SquadOrder.Move(_fc.Anchor), 1f);
        Assert.AreEqual(GameConstants.BossMax, boss.Hp);

        foreach (var u in _fc.Units) u.Dead = false;
        var distant = new FakeEnemy { Pos = new Vector2(0, 3.4f - 30f) };
        var reg2 = new EnemyRegistry(); reg2.Register(distant);
        Run(shots, reg2, SquadOrder.Attack(distant), 1f);
        Assert.AreEqual(FakeEnemy.MaxHp, distant.Hp);
    }
}
