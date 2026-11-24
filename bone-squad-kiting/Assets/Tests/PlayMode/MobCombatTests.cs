using NUnit.Framework;
using UnityEngine;
using BoneSquadKiting.Bosses;
using BoneSquadKiting.Combat;
using BoneSquadKiting.Core;
using BoneSquadKiting.Data;
using BoneSquadKiting.Mobs;
using BoneSquadKiting.Squad;

/// <summary>How the squad shoots once mobs exist: target priority by order, reserved damage, homing.</summary>
public class MobCombatTests
{
    FormationController _fc;
    MobDefinition _def;
    MobSystem _mobs;
    EnemyRegistry _reg;
    ShotSystem _shots;
    Boss _boss;

    [SetUp]
    public void SetUp()
    {
        _fc = SquadTestRig.MakeFc();
        _def = ScriptableObject.CreateInstance<MobDefinition>();
        _def.Speed = 0f;                                   // mobs stand still unless a test says otherwise
        _mobs = new MobSystem();
        _reg = new EnemyRegistry();
        _shots = new ShotSystem { FireRange = 20f };
        _boss = new Boss();
        _reg.Register(_boss);
        for (int i = 0; i < 5; i++) _fc.Tick(0.02f);       // settle so units count as stopped
        foreach (var u in _fc.Units) u.Cd = 0;
    }

    [TearDown] public void TearDown() { Object.DestroyImmediate(_fc.gameObject); Object.DestroyImmediate(_def); }

    void Run(SquadOrder order, float seconds)
    {
        for (float t = 0; t < seconds; t += 0.02f)
        {
            _fc.Tick(0.02f); _mobs.Tick(_fc, _reg, null, null, 0.02f); _shots.Tick(_fc.Units, _reg, order, 0.02f);
        }
    }

    Mob Tough(Vector2 pos) { var m = _mobs.Spawn(_def, pos, _reg); m.Health = 100000f; return m; }

    [Test]
    public void MoveOrder_ShootsMobsInsideTheThreatRadius_BeforeTheBoss()
    {
        var near = Tough(_fc.Anchor + new Vector2(0, -2f));   // within 4 of every unit in the dense squad
        Run(SquadOrder.Move(_fc.Anchor), 1.5f);
        Assert.That(near.Health, Is.LessThan(100000f));
        Assert.AreEqual(GameConstants.BossMax, _boss.Hp, "the boss is left alone while a mob is this close");
    }

    [Test]
    public void MoveOrder_IgnoresDistantMobs_AndShootsTheBoss()
    {
        var far = Tough(_fc.Anchor + new Vector2(9f, 0));   // outside every unit's threat radius of 4
        Run(SquadOrder.Move(_fc.Anchor), 1.5f);
        Assert.AreEqual(100000f, far.Health);
        Assert.That(_boss.Hp, Is.LessThan(GameConstants.BossMax));
    }

    [Test]
    public void AttackMove_PrefersMobsNearTheClickedPoint()
    {
        var atPoint = Tough(_fc.Anchor + new Vector2(8f, 0));
        var closer = Tough(_fc.Anchor + new Vector2(-6f, 0));     // nearer to most units, but not near the point
        Run(SquadOrder.AttackMove(atPoint.Pos), 1.5f);
        Assert.That(atPoint.Health, Is.LessThan(100000f));
        Assert.AreEqual(100000f, closer.Health);
        Assert.AreEqual(GameConstants.BossMax, _boss.Hp);
    }

    [Test]
    public void AttackMove_FallsBackToAnyMobInRange_ThenTheBoss()
    {
        var elsewhere = Tough(_fc.Anchor + new Vector2(-6f, 0));
        Run(SquadOrder.AttackMove(_fc.Anchor + new Vector2(8f, 0)), 1.5f);   // nothing at the point
        Assert.That(elsewhere.Health, Is.LessThan(100000f));

        elsewhere.ApplyDamage(float.MaxValue);
        Run(SquadOrder.AttackMove(_fc.Anchor + new Vector2(8f, 0)), 2f);
        Assert.That(_boss.Hp, Is.LessThan(GameConstants.BossMax), "no mobs left: the boss");
    }

    [Test]
    public void AttackTarget_OnTheBoss_IgnoresMobsNextToTheSquad()
    {
        var near = Tough(_fc.Anchor + new Vector2(0, -3f));
        Run(SquadOrder.Attack(_boss), 1.5f);
        Assert.AreEqual(100000f, near.Health);
        Assert.That(_boss.Hp, Is.LessThan(GameConstants.BossMax));
    }

    [Test]
    public void ReservedDamage_StopsTheSquadOverkillingOneMob()
    {
        _def.Hp = 66f;                                                   // two shots
        var mob = _mobs.Spawn(_def, _fc.Anchor + new Vector2(0, -3f), _reg);
        _shots.Tick(_fc.Units, _reg, SquadOrder.Move(_fc.Anchor), 0.0001f);   // every unit is ready to fire

        int atMob = 0, atBoss = 0;
        foreach (var s in _shots.Shots) { if (s.Target == mob) atMob++; else if (s.Target == _boss) atBoss++; }
        Assert.AreEqual(2, atMob, "exactly the shots needed to kill it");
        Assert.AreEqual(GameConstants.SquadSize - 2, atBoss, "everyone else shoots the boss");
        Assert.That(mob.Reserved, Is.EqualTo(66f).Within(0.01f));

        Run(SquadOrder.Move(_fc.Anchor), 1f);
        Assert.IsFalse(mob.Alive);
    }

    [Test]
    public void Reservation_IsReleasedWhenAShotExpires()
    {
        var mob = Tough(_fc.Anchor + new Vector2(0, -3f));
        _shots.Tick(_fc.Units, _reg, SquadOrder.Move(_fc.Anchor), 0.0001f);
        Assert.That(mob.Reserved, Is.GreaterThan(0f));
        foreach (var u in _fc.Units) u.Dead = true;                       // nobody fires again
        for (int i = 0; i < 100; i++) _shots.Tick(_fc.Units, _reg, SquadOrder.Move(_fc.Anchor), 0.02f);
        Assert.AreEqual(0, _shots.Shots.Count);
        Assert.That(mob.Reserved, Is.EqualTo(0f).Within(0.01f));
    }

    [Test]
    public void AFocusOrderRedirectsWindupsButPreservesShotsAlreadyInFlight()
    {
        var near = Tough(_fc.Anchor + Vector2.right * 2f);
        var clicked = Tough(_fc.Anchor + Vector2.left * 4f);
        foreach (var u in _fc.Units) u.AttackWindup = 0.5f;
        _shots.Tick(_fc.Units, _reg, SquadOrder.Attack(near), .001f);
        _shots.ReleasePending(_fc.Units[0]);
        float launched = _shots.Shots[0].Reserve;
        float total = near.Reserved;
        _shots.FocusPendingShots(_fc.Units, clicked, _shots.FireRange);
        Assert.AreSame(near, _shots.Shots[0].Target);
        Assert.AreEqual(launched, near.Reserved, .01f);
        Assert.AreEqual(total - launched, clicked.Reserved, .01f);
        _shots.ReleasePending(_fc.Units[1]);
        Assert.AreSame(clicked, _shots.Shots[1].Target);
    }

    [Test]
    public void HomingShots_HitAMobThatMovesSideways()
    {
        var mob = Tough(_fc.Anchor + new Vector2(-3f, -3.5f));
        var order = SquadOrder.Attack(mob);
        int hits = 0; _shots.OnHit += _ => hits++;
        for (float t = 0; t < 2f; t += 0.02f)
        {
            mob.X += 3.2f * 0.02f;                                       // walking across the line of fire
            _fc.Tick(0.02f); _shots.Tick(_fc.Units, _reg, order, 0.02f);
        }
        Assert.That(hits, Is.GreaterThan(30), "shots follow it instead of flying past");
    }

    [Test]
    public void ShotsAtAMobThatDies_Disappear_WithoutHittingAnythingElse()
    {
        var mob = Tough(_fc.Anchor + new Vector2(0, -3.9f));   // tough enough that all 25 units fire at it
        _shots.Tick(_fc.Units, _reg, SquadOrder.Attack(mob), 0.0001f);
        Assert.AreEqual(GameConstants.SquadSize, _shots.Shots.Count);
        mob.ApplyDamage(float.MaxValue);
        float bossBefore = _boss.Hp;
        foreach (var u in _fc.Units) u.Cd = 10f;
        for (int i = 0; i < 20; i++) _shots.Tick(_fc.Units, _reg, SquadOrder.Attack(mob), 0.02f);
        Assert.AreEqual(0, _shots.Shots.Count);
        Assert.AreEqual(bossBefore, _boss.Hp);
    }
}
