using NUnit.Framework;
using UnityEngine;
using BoneSquadKiting.Bosses;
using BoneSquadKiting.Combat;
using BoneSquadKiting.Data;
using BoneSquadKiting.Squad;

/// <summary>The Frost Knight fight's rules: throne attacks, the mages, phase 2's charges, lethal slams and groggy.</summary>
public class FrostKnightFightTests
{
    FormationController _fc;
    BossArena _arena;
    Boss _boss;
    EnemyRegistry _enemies;
    EliteSystem _elites;
    FrostKnightFight _fight;
    float _roll;        // what the fight's dice show

    [SetUp]
    public void SetUp()
    {
        _fc = SquadTestRig.MakeFc(leader: true);
        _arena = new GameObject("arena").AddComponent<BossArena>();
        _arena.transform.position = new Vector3(_fc.Anchor.x, 0f, _fc.Anchor.y + 40f);     // the room is ahead of the squad
        _arena.Size = new Vector2(24f, 22f);
        var mage = ScriptableObject.CreateInstance<EliteDefinition>(); mage.MaxHp = 100f;
        var t = _arena.Tuning;
        t.MaxHp = 1000f; t.PhaseOneEndsAt = 0.6f; t.FirstAttackDelay = 0.5f; t.SlamHitSeconds = 1f; t.SlamRecoverSeconds = 0.3f; t.ThroneAttackGap = 0.2f;
        t.Mages = new[] { mage, mage }; t.MageDelay = 0.2f; t.StandUpSeconds = 0.5f;
        t.AttackOrder = "CL"; t.AttackGap = 0.2f; t.ChargeAnimSpeed = 7f; t.LethalWarnSeconds = 1f; t.LethalHoldSeconds = 0.2f;
        t.VanishSeconds = 0.2f; t.GroggySeconds = 1f; t.DieAnimSpeed = 10f;
        _boss = new Boss(); _enemies = new EnemyRegistry(); _elites = new EliteSystem();
        _roll = 0f;
        _fight = new FrostKnightFight(_arena, _boss, _fc, _enemies, _elites, () => _roll);
    }

    [TearDown]
    public void TearDown()
    {
        SquadDamage.Invincible = false;
        Object.DestroyImmediate(_fc.gameObject); Object.DestroyImmediate(_arena.gameObject);
    }

    void Run(float seconds) { for (float t = 0f; t < seconds - 1e-4f; t += 0.02f) _fight.Tick(0.02f); }
    bool RunUntil(System.Func<bool> done, float max = 20f) { for (float t = 0f; t < max; t += 0.02f) { if (done()) return true; _fight.Tick(0.02f); } return done(); }
    void MoveSquad(Vector2 to) { var d = to - _fc.Anchor; foreach (var u in _fc.Units) { u.X += d.x; u.Z += d.y; } _fc.Anchor = to; }
    int Alive() { int n = 0; foreach (var u in _fc.Units) if (!u.Dead) n++; return n; }

    [Test]
    public void TheWholeFight_ThroneThenMagesThenChargesSlamsAndGroggy_UntilTheBossDies()
    {
        SquadDamage.Invincible = true;      // this test is about the order of things, not about who gets hurt
        Run(1f);
        Assert.AreEqual(FrostKnightState.Waiting, _fight.State, "nothing happens until the squad walks in");
        Assert.IsFalse(_fight.Targetable);
        Assert.AreEqual("HeadBowed", _fight.Anim);

        MoveSquad(_arena.Center);
        Run(0.1f);
        Assert.AreEqual(FrostKnightState.Throne, _fight.State);
        Assert.IsTrue(_fight.Targetable); Assert.Contains(_boss, _enemies.Enemies);
        Assert.IsTrue(RunUntil(() => _fight.Casting), "a throne attack starts");
        Assert.AreEqual("RaiseHands", _fight.Anim);
        Assert.AreEqual(1, _fight.Telegraphs.Count, "and its zone is warned about");
        int hits = 0; _fight.OnHit += (a, c, h, l) => hits++;
        Assert.IsTrue(RunUntil(() => hits == 1), "it goes off");
        Assert.AreEqual(0, _fight.Telegraphs.Count);

        _boss.ApplyDamage(99999f);
        Assert.AreEqual(600f, _boss.Hp, 0.01f, "phase 1 cannot take it below 60%");
        Assert.IsTrue(RunUntil(() => _fight.State == FrostKnightState.Stunned));
        Assert.IsFalse(_fight.Targetable, "stunned on the throne: the mages are the targets now");
        Assert.AreEqual("HeadBowed", _fight.Anim);
        Assert.IsTrue(RunUntil(() => _fight.Mages.Count == 2), "two mages appear");
        foreach (var m in _fight.Mages) Assert.IsTrue(_arena.Contains(m.Pos), "inside the room");
        Run(1f);
        Assert.AreEqual(FrostKnightState.Stunned, _fight.State, "it waits for the last mage");
        foreach (var m in _elites.Elites) m.ApplyDamage(99999f);
        Assert.IsTrue(RunUntil(() => _fight.State == FrostKnightState.Rising));
        Assert.AreEqual("StandUp", _fight.Anim);
        Assert.AreEqual(2, _fight.Phase);

        Assert.IsTrue(RunUntil(() => _fight.State == FrostKnightState.Charging), "phase 2 opens with a charge");
        Assert.IsFalse(_fight.Targetable, "not yet: it is only just appearing");
        Assert.AreEqual(FrostKnightAttack.Charge, _fight.Attack);
        Assert.IsTrue(RunUntil(() => _fight.Targetable, 0.5f), "in the air with the sword raised it can be hit");
        Assert.IsTrue(_fight.Casting); Assert.Contains(_boss, _enemies.Enemies);
        Assert.IsTrue(RunUntil(() => !_fight.Casting));
        Assert.IsFalse(_fight.Targetable, "once the sword has come down it is out of reach again");
        Assert.That(Mathf.Abs(Vector2.Dot(_boss.Position - _arena.Center, _arena.Right)), Is.GreaterThan(10f), "it shows up at the side of the room");
        Assert.IsTrue(RunUntil(() => _fight.State == FrostKnightState.Slamming), "then the lethal slam");
        Assert.IsTrue(_fight.Telegraphs[0].Pattern.LethalTelegraph);
        Assert.IsTrue(RunUntil(() => _fight.State == FrostKnightState.Groggy), "then it is groggy");
        Assert.IsTrue(_fight.Targetable, "and open to attack");
        Assert.That(Vector2.Distance(_boss.Position, _arena.Center), Is.LessThan(0.1f));
        _boss.ApplyDamage(100f);
        Assert.AreEqual(500f, _boss.Hp, 0.01f, "phase 2 has no floor");
        Assert.IsTrue(RunUntil(() => _fight.State == FrostKnightState.Vanishing), "it gets away again");
        Assert.IsFalse(_fight.Targetable);
        Assert.IsTrue(RunUntil(() => _fight.State == FrostKnightState.Charging), "and the attacks go on");
        Assert.IsTrue(RunUntil(() => _fight.State == FrostKnightState.Groggy));
        Assert.AreEqual(2, _fight.GroggyCount);

        _boss.ApplyDamage(99999f);
        Run(0.1f);
        Assert.AreEqual(FrostKnightState.Dying, _fight.State);
        Assert.AreEqual("Die", _fight.Anim);
        Assert.IsFalse(_fight.Won, "not won until the death is over");
        Assert.IsTrue(RunUntil(() => _fight.Won));
    }

    [Test]
    public void Circle_IsSmallerThanTheRingFormation_AndShrinksWithTheSquad()
    {
        const float ringRadius = 3.7f;                            // the ring formation's (E) radius for a full squad
        _roll = 0.9f;                                             // the circle, not the parallel lines
        SquadDamage.Invincible = true;
        MoveSquad(_arena.Center);
        Assert.IsTrue(RunUntil(() => _fight.Casting));
        Assert.AreEqual(FrostKnightAttack.Circle, _fight.Attack);
        var cast = _fight.Telegraphs[0];
        Assert.AreEqual("ring", cast.Pattern.Recommend, "the HUD recommends the ring formation");
        Assert.That(cast.Shape.RMax, Is.LessThan(ringRadius - 0.3f), "units on the ring stand outside the circle");
        Assert.That(Vector2.Distance(new Vector2(cast.Cx, cast.Cz), _fc.Anchor), Is.LessThan(0.1f), "on the squad");
        Assert.IsTrue(RunUntil(() => !_fight.Casting));

        // Half the squad is gone: the ring is half as wide, and so is the circle.
        int kill = _fc.Units.Count / 2;
        foreach (var u in _fc.Units) { if (kill == 0) break; if (u.IsLeader) continue; u.Dead = true; kill--; }
        float fraction = _fc.AliveCount / (float)_fc.Units.Count;
        Assert.IsTrue(RunUntil(() => _fight.Casting));
        Assert.That(_fight.Telegraphs[0].Shape.RMax, Is.EqualTo(_arena.Tuning.CircleRadius * fraction).Within(0.01f));
        Assert.That(_fight.Telegraphs[0].Shape.RMax, Is.LessThan(ringRadius * fraction - 0.1f));
    }

    // Skip to phase 2 with the squad standing at a given place of the room (x to the right, z toward the throne).
    void ToPhaseTwoAt(float x, float z)
    {
        SquadDamage.Invincible = true;
        MoveSquad(_arena.Center);
        Run(0.1f);
        _boss.ApplyDamage(99999f);
        RunUntil(() => _fight.Mages.Count == 2);
        foreach (var m in _elites.Elites) m.ApplyDamage(99999f);
        RunUntil(() => _fight.State == FrostKnightState.Hidden);
        SquadDamage.Invincible = false;
        MoveSquad(_arena.Point(x, z));
    }

    [Test]
    public void Charge_HitsTheTwoRowsAcrossTheRoom_AndSparesTheStripBetweenThem()
    {
        _arena.Tuning.ChargeShift = 0f; _arena.Tuning.ChargeRowOffset = 8f;
        ToPhaseTwoAt(-9f, 0f);                                    // on the line between the rows, far to the left
        Assert.IsTrue(RunUntil(() => _fight.State == FrostKnightState.Charging));
        float hp = 0f; foreach (var u in _fc.Units) hp += u.Hp;
        Assert.IsTrue(RunUntil(() => !_fight.Casting));
        float after = 0f; foreach (var u in _fc.Units) after += u.Hp;
        Assert.AreEqual(hp, after, 0.01f, "between the rows nobody is hurt");

        // The same charge again, now standing in a row at the far right end of the room.
        SetUp2ndCharge(new Vector2(10f, _arena.Tuning.ChargeRowOffset));
        Assert.That(after2, Is.LessThan(before2), "the row is hit along its whole length");
    }

    float before2, after2;
    void SetUp2ndCharge(Vector2 at)
    {
        _arena.Tuning.AttackOrder = "C";
        RunUntil(() => _fight.State == FrostKnightState.Groggy); RunUntil(() => _fight.State == FrostKnightState.Hidden);
        MoveSquad(_arena.Point(at.x, at.y));
        RunUntil(() => _fight.State == FrostKnightState.Charging);
        before2 = 0f; foreach (var u in _fc.Units) before2 += u.Hp;
        RunUntil(() => !_fight.Casting);
        after2 = 0f; foreach (var u in _fc.Units) after2 += u.Hp;
    }

    [Test]
    public void LethalSlam_KillsEveryoneInItsZone_AndNobodyOutsideIt()
    {
        _arena.Tuning.AttackOrder = "L";
        _roll = 0f;                                               // the left half
        ToPhaseTwoAt(6f, 0f);                                     // the squad stands in the right half
        Assert.IsTrue(RunUntil(() => _fight.State == FrostKnightState.Slamming));
        Assert.AreEqual(FrostKnightAttack.LethalLeft, _fight.Attack);
        int alive = Alive();
        // One soldier strays into the left half.
        SquadUnit stray = null; foreach (var u in _fc.Units) if (!u.IsLeader) { stray = u; break; }
        var p = _arena.Point(-6f, 3f); stray.X = p.x; stray.Z = p.y;
        Assert.IsTrue(RunUntil(() => !_fight.Casting));
        Assert.IsTrue(stray.Dead, "killed outright");
        Assert.AreEqual(alive - 1, Alive(), "the rest, outside the zone, are untouched");

        // Next slam (not the same zone twice: the right half this time) catches the leader: the whole squad falls.
        Assert.IsTrue(RunUntil(() => _fight.State == FrostKnightState.Groggy));
        Assert.IsTrue(RunUntil(() => _fight.State == FrostKnightState.Slamming));
        Assert.AreEqual(FrostKnightAttack.LethalRight, _fight.Attack);
        Assert.IsTrue(RunUntil(() => !_fight.Casting));
        Assert.AreEqual(0, Alive(), "the leader was in the zone");
    }
}
