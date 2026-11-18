using NUnit.Framework;
using UnityEngine;
using BoneSquadKiting.Combat;
using BoneSquadKiting.Core;
using BoneSquadKiting.Data;
using BoneSquadKiting.Game;
using BoneSquadKiting.Squad;

public class GameDirectorTests
{
    FormationController _fc;
    GameDirector _dir;

    [SetUp]
    public void SetUp()
    {
        _fc = SquadTestRig.MakeFc();
        _dir = _fc.gameObject.AddComponent<GameDirector>();
        _dir.enabled = false; // tests drive Step() themselves
        _dir.Squad = _fc;
        var seq = ScriptableObject.CreateInstance<AttackSequence>();
        var p = ScriptableObject.CreateInstance<PatternDefinition>();
        p.Time = 2f; p.Shape = DangerShape.Disc(3.10f);
        seq.Patterns = new[] { p };
        _dir.Sequence = seq;
    }

    [TearDown] public void TearDown() { if (_fc != null) Object.Destroy(_fc.gameObject); }

    void Run(float seconds) { for (float t = 0; t < seconds; t += 0.02f) _dir.Step(0.02f); }

    [Test]
    public void StartsInMenu_AndRestartBeginsPlay()
    {
        Assert.AreEqual(GamePhase.Menu, _dir.Phase);
        _dir.Step(0.02f);
        Assert.AreEqual(0f, _dir.Elapsed, "nothing advances before the match starts");

        _dir.Restart();
        Assert.AreEqual(GamePhase.Play, _dir.Phase);
        Assert.AreEqual(GameConstants.SquadSize, _dir.AliveCount);
        Assert.AreEqual(GameConstants.BossMax, _dir.Boss.Hp);
    }

    [Test]
    public void Play_DamagesBossAndReportsDps()
    {
        _dir.Restart();
        Run(2.5f);
        Assert.That(_dir.Boss.Hp, Is.LessThan(GameConstants.BossMax));
        Assert.That(_dir.LastDps, Is.GreaterThan(0f));
        Assert.That(_dir.TotalDamage, Is.EqualTo(GameConstants.BossMax - _dir.Boss.Hp).Within(0.01f));
    }

    [Test]
    public void MoveOrder_SetsDestination_AttackOrderInRangeHolds()
    {
        _dir.Restart();
        _dir.IssueOrder(SquadOrder.Move(new Vector2(4f, 1f)));
        Assert.AreEqual(new Vector2(4f, 1f), _fc.Destination);
        Assert.AreEqual(OrderKind.Move, _fc.Order.Kind);

        _dir.IssueOrder(SquadOrder.Attack(_dir.Boss)); // boss is always in range inside the M1 arena
        Assert.AreEqual(OrderKind.AttackTarget, _fc.Order.Kind);
        Assert.AreEqual(_fc.Anchor, _fc.Destination, "in range: hold position instead of walking to the boss");
    }

    [Test]
    public void Pause_FreezesTime_AndBlocksOrders()
    {
        _dir.Restart();
        Run(0.5f);
        float elapsed = _dir.Elapsed;
        _dir.TogglePause();
        Run(0.5f);
        Assert.AreEqual(elapsed, _dir.Elapsed);

        var before = _fc.Destination;
        _dir.IssueOrder(SquadOrder.Move(new Vector2(4f, 1f)));
        Assert.AreEqual(before, _fc.Destination);

        _dir.TogglePause();
        Run(0.1f);
        Assert.That(_dir.Elapsed, Is.GreaterThan(elapsed));
    }

    [Test]
    public void BossDead_Wins_AndSquadWiped_Loses()
    {
        _dir.Restart();
        _dir.Boss.ApplyDamage(GameConstants.BossMax);
        _dir.Step(0.02f);
        Assert.AreEqual(GamePhase.Win, _dir.Phase);

        _dir.Restart();
        foreach (var u in _fc.Units) u.Dead = true;
        _dir.Step(0.02f);
        Assert.AreEqual(GamePhase.Lose, _dir.Phase);
    }

    [Test]
    public void PhaseTwo_StartsOnceBelowThreshold()
    {
        _dir.Restart();
        int fired = 0; _dir.PhaseTwoStarted += () => fired++;
        _dir.Boss.Hp = GameConstants.BossMax * 0.5f;
        _dir.Step(0.02f); _dir.Step(0.02f);
        Assert.AreEqual(2, _dir.BossPhase);
        Assert.AreEqual(2, _dir.Boss.Phase);
        Assert.AreEqual(1, fired);
    }

    [Test]
    public void StandingStill_LosesUnitsToPatterns_AndEventsFire()
    {
        _dir.Restart();
        int started = 0, resolved = 0, lost = 0;
        _dir.PatternStarted += _ => started++;
        _dir.PatternResolved += (h, l) => { resolved++; lost += l; };
        Run(GameConstants.FirstAttackDelay + 2f + GameConstants.NextAttackPhase1 + 2f + 0.3f); // two disc hits on a dense squad
        Assert.AreEqual(2, started);
        Assert.AreEqual(2, resolved);
        Assert.AreEqual(GameConstants.SquadSize, lost);
        Assert.AreEqual(GamePhase.Lose, _dir.Phase);
    }
}
