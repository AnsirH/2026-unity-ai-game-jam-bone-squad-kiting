using NUnit.Framework;
using UnityEngine;
using BoneSquadKiting.Bosses;
using BoneSquadKiting.Core;
using BoneSquadKiting.Data;
using BoneSquadKiting.Squad;

public class PatternResolveTests
{
    FormationController _fc;

    [SetUp] public void SetUp() { _fc = SquadTestRig.MakeFc(); }
    [TearDown] public void TearDown() { if (_fc != null) Object.Destroy(_fc.gameObject); }

    static AttackSequence Seq(params (float time, DangerShape shape)[] patterns)
    {
        var seq = ScriptableObject.CreateInstance<AttackSequence>();
        seq.Patterns = new PatternDefinition[patterns.Length];
        for (int i = 0; i < patterns.Length; i++)
        {
            var p = ScriptableObject.CreateInstance<PatternDefinition>();
            p.Time = patterns[i].time; p.Shape = patterns[i].shape; p.DisplayName = "p" + i;
            seq.Patterns[i] = p;
        }
        return seq;
    }

    [Test]
    public void Disc_DamagesOnlyUnitsInside()
    {
        foreach (var u in _fc.Units) { u.X = 0; u.Z = 0; }
        _fc.Units[0].X = 10f; // outside the 3.10 radius
        var sys = new BossAttackSystem();
        int hit = -1, lost = -1; sys.OnResolved += (h, l) => { hit = h; lost = l; };

        sys.ForceResolve(DangerShape.Disc(3.10f), 0, 0, _fc.Units);

        Assert.AreEqual(GameConstants.UnitMax, _fc.Units[0].Hp);
        Assert.That(_fc.Units[1].Hp, Is.EqualTo(GameConstants.UnitMax - GameConstants.PatternUnitDamage).Within(0.01f));
        Assert.AreEqual(GameConstants.SquadSize - 1, hit);
        Assert.AreEqual(0, lost);
    }

    [Test]
    public void SecondHit_KillsUnit()
    {
        foreach (var u in _fc.Units) { u.X = 0; u.Z = 0; }
        var sys = new BossAttackSystem();
        int lost = 0; sys.OnResolved += (h, l) => lost = l;
        sys.ForceResolve(DangerShape.Disc(3.10f), 0, 0, _fc.Units);
        sys.ForceResolve(DangerShape.Disc(3.10f), 0, 0, _fc.Units);
        Assert.IsTrue(_fc.Units[0].Dead);
        Assert.AreEqual(GameConstants.SquadSize, lost);
    }

    [Test]
    public void Schedule_StartsAfterFirstDelay_ThenResolvesAfterPatternTime()
    {
        var sys = new BossAttackSystem { Sequence = Seq((2.0f, DangerShape.Disc(3.10f)), (1.0f, DangerShape.Disc(3.10f))) };
        var boss = new Boss();
        PatternDefinition started = null; int resolved = 0;
        sys.OnStart += p => started = p; sys.OnResolved += (h, l) => resolved++;
        float dt = 0.02f, t = 0;

        for (; t < GameConstants.FirstAttackDelay - 0.1f; t += dt) sys.Tick(_fc, boss, dt);
        Assert.IsNull(started, "no pattern before the first delay");

        for (; t < GameConstants.FirstAttackDelay + 0.1f; t += dt) sys.Tick(_fc, boss, dt);
        Assert.AreSame(sys.Sequence.Patterns[0], started);
        Assert.AreEqual(0, resolved);

        for (; t < GameConstants.FirstAttackDelay + 2.2f; t += dt) sys.Tick(_fc, boss, dt);
        Assert.AreEqual(1, resolved);
        Assert.IsNull(sys.Current);
        Assert.That(sys.NextAttack, Is.EqualTo(GameConstants.NextAttackPhase1).Within(0.2f));
    }

    [Test]
    public void LowBossHp_ShortensGapBetweenAttacks()
    {
        var sys = new BossAttackSystem { Sequence = Seq((0.5f, DangerShape.Disc(1f))) };
        var boss = new Boss { Hp = GameConstants.BossMax * 0.3f };
        for (float t = 0; t < GameConstants.FirstAttackDelay + 0.6f; t += 0.02f) sys.Tick(_fc, boss, 0.02f);
        Assert.IsNull(sys.Current);
        Assert.That(sys.NextAttack, Is.EqualTo(GameConstants.NextAttackPhase2).Within(0.1f));
    }

    [Test]
    public void Zone_FollowsSquadUntilLockTime_ThenStays()
    {
        var sys = new BossAttackSystem { Sequence = Seq((2.0f, DangerShape.Disc(3.10f))) };
        var boss = new Boss();
        float dt = 0.02f;
        for (float t = 0; t < GameConstants.FirstAttackDelay + 0.05f; t += dt) sys.Tick(_fc, boss, dt);
        Assert.IsNotNull(sys.Current);

        _fc.Anchor = new Vector2(5f, 0f);                       // squad jumps away mid-telegraph
        for (int i = 0; i < 50; i++) sys.Tick(_fc, boss, dt);   // 1.0s: still tracking
        Assert.That(sys.Current.Value.Cx, Is.EqualTo(5f).Within(0.05f));

        for (int i = 0; i < 32; i++) sys.Tick(_fc, boss, dt);   // now inside the last 0.38s
        float lockedX = sys.Current.Value.Cx;
        _fc.Anchor = new Vector2(-5f, 0f);
        for (int i = 0; i < 5; i++) sys.Tick(_fc, boss, dt);
        Assert.That(sys.Current.Value.Cx, Is.EqualTo(lockedX).Within(1e-4f));
    }
}
