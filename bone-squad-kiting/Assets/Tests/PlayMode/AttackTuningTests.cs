using NUnit.Framework;
using UnityEngine;
using BoneSquadKiting.Combat;
using BoneSquadKiting.Data;
using BoneSquadKiting.Game;
using BoneSquadKiting.Squad;

/// <summary>Damage and attack interval are tuned separately for soldiers and for the squad leader.</summary>
public class AttackTuningTests
{
    class FakeEnemy : IEnemy, IDamageable
    {
        public Vector2 Pos { get; set; }
        public bool Alive => Hp > 0;
        public float Hp = 100000;
        public float Reserved { get; set; }
        public void ApplyDamage(float amount) { Hp -= amount; }
    }

    FormationController _fc;
    ShotSystem _shots;
    EnemyRegistry _reg;
    FakeEnemy _enemy;
    SquadOrder _order;

    [SetUp]
    public void SetUp()
    {
        _fc = SquadTestRig.MakeFc(leader: true);
        _shots = new ShotSystem();
        _reg = new EnemyRegistry();
        _enemy = new FakeEnemy { Pos = _fc.Anchor + new Vector2(4f, 0f) };
        _reg.Register(_enemy);
        _order = SquadOrder.Move(_fc.Anchor);
        foreach (var u in _fc.Units) u.Cd = 1000f;      // only the units a test wakes up attack
    }

    [TearDown] public void TearDown() { if (_fc != null) Object.DestroyImmediate(_fc.gameObject); }

    void Step(float seconds) { for (float t = 0; t < seconds - 1e-4f; t += 0.01f) { _fc.Tick(0.01f); _shots.Tick(_fc.Units, _reg, _order, 0.01f); } }

    [Test]
    public void SoldierAndLeader_DealTheirOwnDamage_AndTheTargetIsReservedByThatMuch()
    {
        _shots.UnitDamage = 10f; _shots.LeaderDamage = 70f;
        var leader = _fc.Units[0]; var soldier = _fc.Units[1];
        Assert.IsTrue(leader.IsLeader); Assert.IsFalse(soldier.IsLeader);

        soldier.Cd = 0f;
        _shots.Tick(_fc.Units, _reg, _order, 0.01f); soldier.Cd = 1000f;
        Assert.AreEqual(10f, _enemy.Reserved, "the soldier's shot is counted on at the soldier's damage");
        Step(1.5f);
        Assert.That(_enemy.Hp, Is.EqualTo(100000 - 10f).Within(0.01f));
        Assert.AreEqual(0f, _enemy.Reserved, "and released when it lands");

        leader.Cd = 0f;
        _shots.Tick(_fc.Units, _reg, _order, 0.01f); leader.Cd = 1000f;
        Assert.AreEqual(70f, _enemy.Reserved);
        Step(1.5f);
        Assert.That(_enemy.Hp, Is.EqualTo(100000 - 80f).Within(0.01f), "the leader's throw deals the leader's damage");
        Assert.AreEqual(0f, _enemy.Reserved);
    }

    [Test]
    public void SoldierAndLeader_AttackAtTheirOwnPace()
    {
        _shots.UnitInterval = 0.5f; _shots.UnitIntervalRandom = 0f;
        _shots.LeaderInterval = 2f; _shots.LeaderIntervalRandom = 0f;
        var leader = _fc.Units[0]; var soldier = _fc.Units[1];
        leader.Cd = 0f; soldier.Cd = 0f;
        Step(4.5f);                                            // stepped in hundredths, so each interval runs a tick long
        Assert.That(soldier.ShotCount, Is.InRange(8, 10), "about every 0.5 s");
        Assert.AreEqual(3, leader.ShotCount, "every 2 s: at 0, 2 and 4");
        Assert.AreEqual(0.5f, soldier.AttackInterval); Assert.AreEqual(2f, leader.AttackInterval, "the view reads these to pace the attack motion");
    }

    [Test]
    public void TheRandomPart_IsAddedOnTop_AndAnAttackMotionStillSetsTheFloor()
    {
        _shots.UnitInterval = 0.5f; _shots.UnitIntervalRandom = 0.3f;
        var soldier = _fc.Units[1];
        for (int i = 0; i < 40; i++)
        {
            soldier.Cd = 0f;
            _shots.Tick(_fc.Units, _reg, _order, 0f);
            Assert.That(soldier.Cd, Is.InRange(0.5f, 0.8f));
        }
        soldier.AttackDuration = 1.2f; soldier.Cd = 0f;       // a motion longer than the interval
        _shots.Tick(_fc.Units, _reg, _order, 0f);
        Assert.AreEqual(1.2f, soldier.Cd, "no new attack before the motion is over");
    }

    [Test]
    public void TheTuningAsset_FeedsTheShots()
    {
        var dir = _fc.gameObject.AddComponent<GameDirector>();
        dir.enabled = false; dir.Squad = _fc; dir.HasBoss = false;
        var t = ScriptableObject.CreateInstance<GameTuning>();
        Assert.AreEqual(33f, t.UnitDamage); Assert.AreEqual(33f, t.LeaderDamage);
        Assert.AreEqual(0.76f, t.UnitAttackInterval); Assert.AreEqual(0.16f, t.UnitAttackIntervalRandom);
        t.UnitDamage = 12f; t.UnitAttackInterval = 0.4f; t.UnitAttackIntervalRandom = 0.1f;
        t.LeaderDamage = 90f; t.LeaderAttackInterval = 3f; t.LeaderAttackIntervalRandom = 0.5f;
        dir.Tuning = t;
        dir.Restart();
        Assert.AreEqual(12f, dir.Shots.UnitDamage); Assert.AreEqual(0.4f, dir.Shots.UnitInterval); Assert.AreEqual(0.1f, dir.Shots.UnitIntervalRandom);
        Assert.AreEqual(90f, dir.Shots.LeaderDamage); Assert.AreEqual(3f, dir.Shots.LeaderInterval); Assert.AreEqual(0.5f, dir.Shots.LeaderIntervalRandom);
        t.UnitDamage = 20f;                                    // changed while playing
        dir.Step(0.02f);
        Assert.AreEqual(20f, dir.Shots.UnitDamage, "follows the asset during play");
    }
}
