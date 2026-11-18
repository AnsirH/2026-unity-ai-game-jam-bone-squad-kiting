using NUnit.Framework;
using UnityEngine;
using BoneSquadKiting.Bosses;
using BoneSquadKiting.Combat;
using BoneSquadKiting.Core;
using BoneSquadKiting.Data;
using BoneSquadKiting.Game;
using BoneSquadKiting.Squad;

/// <summary>A zone that goes off right next to the squad without catching anyone gives a short damage buff.</summary>
public class NearMissTests
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
    GameDirector _dir;

    [SetUp]
    public void SetUp()
    {
        BossAttackSystem.NearMissMargin = 1f;
        _fc = SquadTestRig.MakeFc();
        _dir = _fc.gameObject.AddComponent<GameDirector>();
        _dir.enabled = false; _dir.Squad = _fc; _dir.HasBoss = false;
        _dir.ClearRule = BoneSquadKiting.Stage.StageClearRule.ReachEnd;
    }

    [TearDown] public void TearDown() { BossAttackSystem.NearMissMargin = 1f; if (_fc != null) Object.DestroyImmediate(_fc.gameObject); }

    void PutSquadAt(float x) { foreach (var u in _fc.Units) { u.X = x; u.Z = 0f; } }

    [Test]
    public void Shapes_NearMeansJustOutsideTheEdge()
    {
        var disc = DangerShape.Disc(3f);
        Assert.IsFalse(DangerShapeEval.NearDanger(disc, 0, 0, 2.9f, 0, 0, 1f), "inside is a hit, not a near miss");
        Assert.IsTrue(DangerShapeEval.NearDanger(disc, 0, 0, 3.5f, 0, 0, 1f));
        Assert.IsFalse(DangerShapeEval.NearDanger(disc, 0, 0, 4.2f, 0, 0, 1f), "too far to count");
        Assert.IsFalse(DangerShapeEval.NearDanger(disc, 0, 0, 3.5f, 0, 0, 0f), "margin 0 switches it off");

        var ring = DangerShape.Annulus(3f, 6f);
        Assert.IsTrue(DangerShapeEval.NearDanger(ring, 0, 0, 2.5f, 0, 0, 1f), "the safe middle, close to the inner edge");
        Assert.IsFalse(DangerShapeEval.NearDanger(ring, 0, 0, 0.5f, 0, 0, 1f), "deep in the safe middle");
        Assert.IsTrue(DangerShapeEval.NearDanger(ring, 0, 0, 6.5f, 0, 0, 1f));

        var bands = DangerShape.BandsX(2f, 6f, 3f);
        Assert.IsTrue(DangerShapeEval.NearDanger(bands, 0, 0, 1.5f, 0, 0, 1f), "the gap between the bands, close to one");
        Assert.IsFalse(DangerShapeEval.NearDanger(bands, 0, 0, 0f, 0, 0, 1f));
        Assert.IsTrue(DangerShapeEval.NearDanger(bands, 0, 0, 4f, 3.5f, 0, 1f), "just past a band's end");
        // A turned zone is judged where it actually lies: a quarter turn swaps the axes.
        Assert.IsTrue(DangerShapeEval.NearDanger(bands, 0, 0, 0f, 1.5f, 90f, 1f));
    }

    [Test]
    public void AZoneThatCatchesNobody_WithUnitsRightOutside_IsANearMiss()
    {
        var sys = new BossAttackSystem();
        int near = 0, calls = 0; sys.OnNearMiss += n => { near = n; calls++; };

        PutSquadAt(3.6f); _fc.Units[0].X = 20f;                 // all but one stand 0.6 outside the edge
        sys.ForceResolve(DangerShape.Disc(3f), 0, 0, _fc.Units);
        Assert.AreEqual(1, calls);
        Assert.AreEqual(GameConstants.SquadSize - 1, near, "the far unit is not counted");

        calls = 0; PutSquadAt(8f);                              // nowhere near
        sys.ForceResolve(DangerShape.Disc(3f), 0, 0, _fc.Units);
        Assert.AreEqual(0, calls, "a comfortable miss is no near miss");

        calls = 0; PutSquadAt(3.6f); _fc.Units[1].X = 1f;       // one unit is caught
        sys.ForceResolve(DangerShape.Disc(3f), 0, 0, _fc.Units);
        Assert.AreEqual(0, calls, "nobody may be hit");

        calls = 0; PutSquadAt(3.6f); BossAttackSystem.NearMissMargin = 0f;
        sys.ForceResolve(DangerShape.Disc(3f), 0, 0, _fc.Units);
        Assert.AreEqual(0, calls, "margin 0 switches near misses off");
    }

    [Test]
    public void ASustainedZone_IsJudgedOnlyWhenItFirstGoesOff()
    {
        var p = ScriptableObject.CreateInstance<PatternDefinition>();
        p.Time = 0.5f; p.Shape = DangerShape.Disc(3f); p.Kind = PatternKind.Sustained;
        p.SustainSeconds = 2f; p.TickInterval = 0.25f; p.TickDamage = 1f;
        var seq = ScriptableObject.CreateInstance<AttackSequence>(); seq.Patterns = new[] { p };
        var sys = new BossAttackSystem { Sequence = seq, NextAttack = 0f, TrackSquad = false };
        int calls = 0; sys.OnNearMiss += n => calls++;
        _fc.Anchor = Vector2.zero;
        sys.Tick(_fc, null, 0.01f);                             // the zone is announced around the anchor
        PutSquadAt(3.5f);                                       // and the squad steps just outside it
        for (float t = 0; t < 3.2f; t += 0.05f) sys.Tick(_fc, null, 0.05f);
        Assert.AreEqual(1, calls, "standing at the edge of a burning zone does not keep paying out");
    }

    [Test]
    public void ANearMiss_BuffsTheSquadsDamageForAWhile_AndAnotherOneStartsTheTimeOver()
    {
        _dir.NearMissDamageMultiplier = 1.2f; _dir.NearMissBuffSeconds = 5f; _dir.NearMissMargin = 1f;
        _dir.Restart();
        int events = 0; _dir.NearMissed += n => events++;
        Assert.AreEqual(1f, _dir.DamageMultiplier);

        PutSquadAt(3.6f);
        _dir.Attacks.ForceResolve(DangerShape.Disc(3f), 0, 0, _fc.Units);
        Assert.AreEqual(1, events);
        Assert.AreEqual(1.2f, _dir.DamageMultiplier, "+20% while the buff lasts");
        Assert.AreEqual(5f, _dir.NearMissBuffLeft);

        for (float t = 0; t < 3f; t += 0.02f) _dir.Step(0.02f);
        Assert.That(_dir.NearMissBuffLeft, Is.EqualTo(2f).Within(0.1f));
        Assert.AreEqual(1.2f, _dir.Shots.DamageMultiplier, "the shots are told");

        PutSquadAt(3.6f);
        _dir.Attacks.ForceResolve(DangerShape.Disc(3f), 0, 0, _fc.Units);
        Assert.AreEqual(5f, _dir.NearMissBuffLeft, "not stacked: the time starts over");
        Assert.AreEqual(1.2f, _dir.DamageMultiplier, "and the bonus stays +20%");

        for (float t = 0; t < 5.2f; t += 0.02f) _dir.Step(0.02f);
        Assert.AreEqual(0f, _dir.NearMissBuffLeft);
        Assert.AreEqual(1f, _dir.DamageMultiplier, "back to normal when it runs out");

        _dir.Attacks.ForceResolve(DangerShape.Disc(3f), 0, 0, _fc.Units);
        _dir.Restart();
        Assert.AreEqual(0f, _dir.NearMissBuffLeft, "a new stage starts without the buff");
    }

    [Test]
    public void ShotsFiredDuringTheBuff_HitHarder_AndOnlyThose()
    {
        var shots = new ShotSystem();
        var reg = new EnemyRegistry();
        var enemy = new FakeEnemy { Pos = _fc.Anchor + new Vector2(4f, 0f) };
        reg.Register(enemy);
        var order = SquadOrder.Move(_fc.Anchor);
        foreach (var u in _fc.Units) u.Cd = 1000f;
        float total = 0f; shots.OnHit += d => total += d;
        void Fly() { for (float t = 0; t < 1.5f; t += 0.01f) { _fc.Tick(0.01f); shots.Tick(_fc.Units, reg, order, 0.01f); } }

        _fc.Units[0].Cd = 0f;                                   // one plain shot
        shots.Tick(_fc.Units, reg, order, 0.01f);
        _fc.Units[0].Cd = 1000f;                                // and no second one
        Fly();
        Assert.That(total, Is.EqualTo(GameConstants.UnitDamage).Within(0.01f));

        shots.DamageMultiplier = 1.2f; total = 0f;
        _fc.Units[0].Cd = 0f; _fc.Units[1].Cd = 1000f;
        shots.Tick(_fc.Units, reg, order, 0.01f);               // fired under the buff...
        shots.DamageMultiplier = 1f;                            // ...which ends while it is in the air
        _fc.Units[0].Cd = 1000f;
        Fly();
        Assert.That(total, Is.EqualTo(GameConstants.UnitDamage * 1.2f).Within(0.01f), "what counts is when it was fired");
        Assert.That(enemy.Hp, Is.EqualTo(100000 - GameConstants.UnitDamage * 2.2f).Within(0.01f));
    }
}
