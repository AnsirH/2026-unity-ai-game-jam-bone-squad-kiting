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

        _dir.IssueOrder(SquadOrder.Attack(_dir.Boss)); // boss is always within squad range in the M1 arena
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

    [Test]
    public void AttackMove_StopsWhenAnEnemyIsWithinSquadRange()
    {
        _dir.Restart();
        var start = _fc.Anchor;
        _dir.IssueOrder(SquadOrder.AttackMove(new Vector2(6f, 0f)));
        Assert.IsTrue(_fc.IsMoving);
        Run(0.5f);
        Assert.IsFalse(_fc.IsMoving);
        Assert.AreEqual(start, _fc.Anchor, "boss is already in squad range, so the squad never leaves");
    }

    [Test]
    public void AttackMove_WalksToThePointWhenNoEnemyIsInRange()
    {
        _dir.Restart();
        _dir.Enemies.Unregister(_dir.Boss);
        _dir.IssueOrder(SquadOrder.AttackMove(new Vector2(6f, 0f)));
        Run(2f);
        Assert.That(Vector2.Distance(_fc.Anchor, new Vector2(6f, 0f)), Is.LessThan(0.05f));
    }

    [Test]
    public void RotateFormation_OnlyWhilePlaying_AndRaisesEvent()
    {
        _dir.RotateFormation(1);
        Assert.AreEqual(0f, _fc.RotationDegrees, "ignored in the menu");

        _dir.Restart();
        float reported = -1; _dir.FormationRotated += d => reported = d;
        _dir.RotateFormation(2);
        Assert.AreEqual(30f, _fc.RotationDegrees);
        Assert.AreEqual(30f, reported);
    }

    class FarEnemy : IEnemy
    {
        public Vector2 Pos { get; set; }
        public bool Alive => true;
    }

    [Test]
    public void AttackTarget_WalksTowardFarEnemy_AndStopsAtSquadRange()
    {
        _dir.Restart();
        _dir.Enemies.Unregister(_dir.Boss);
        var far = new FarEnemy { Pos = new Vector2(_fc.Anchor.x - 20f, _fc.Anchor.y) }; // 20 > squad range 15.9
        _dir.Enemies.Register(far);

        _dir.IssueOrder(SquadOrder.Attack(far));
        Assert.IsTrue(_fc.IsMoving, "out of squad range: walk toward the target");
        Run(3f);
        Assert.IsFalse(_fc.IsMoving);
        float dist = (far.Pos - _fc.Anchor).magnitude;
        Assert.That(dist, Is.LessThan(GameConstants.SquadEngageRange));
        Assert.That(dist, Is.GreaterThan(GameConstants.SquadEngageRange - 0.5f), "stops as soon as it is in range");
    }

    [Test]
    public void Ranges_AreTunableAtRuntime()
    {
        _dir.Restart();
        _dir.UnitFireRange = 1f;                       // boss is ~10.8 away: nobody can reach it
        Run(1.5f);
        Assert.AreEqual(GameConstants.BossMax, _dir.Boss.Hp);

        _dir.SquadEngageRange = 5f;                    // boss no longer inside squad range
        _dir.IssueOrder(SquadOrder.Attack(_dir.Boss));
        Assert.IsTrue(_fc.IsMoving, "attack-target now has to walk toward the boss");

        _dir.UnitFireRange = GameConstants.FireRange;  // restoring the range brings the fire back
        _dir.IssueOrder(SquadOrder.Move(_fc.Anchor));
        Run(1.5f);
        Assert.That(_dir.Boss.Hp, Is.LessThan(GameConstants.BossMax));
    }
}
