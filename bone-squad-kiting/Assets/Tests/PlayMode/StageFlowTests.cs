using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using BoneSquadKiting.Combat;
using BoneSquadKiting.Core;
using BoneSquadKiting.Data;
using BoneSquadKiting.Game;
using BoneSquadKiting.Mobs;
using BoneSquadKiting.Squad;
using BoneSquadKiting.Stage;

/// <summary>A stage fought against rounds of mobs: round order, clear rules, the stop command.</summary>
public class StageFlowTests
{
    FormationController _fc;
    GameDirector _dir;
    RoundSpawner _rounds;
    MobDefinition _mob;
    GameObject _far;

    [SetUp]
    public void SetUp()
    {
        _fc = SquadTestRig.MakeFc();
        _dir = _fc.gameObject.AddComponent<GameDirector>();
        _dir.enabled = false;
        _dir.Squad = _fc;
        _dir.HasBoss = false;
        _dir.UnitFireRange = 30f; _dir.SquadEngageRange = 30f;

        _mob = ScriptableObject.CreateInstance<MobDefinition>();
        _mob.Speed = 0f;                                    // they wait where they spawn so tests control the pace
        _far = new GameObject("spawn"); _far.transform.position = new Vector3(0, 0, -6f);   // 9.4 from the squad

        _rounds = _fc.gameObject.AddComponent<RoundSpawner>();
        _rounds.FirstRoundDelay = 1f;
        for (int i = 0; i < 2; i++)
            _rounds.Rounds.Add(new RoundSpawner.Round { Mob = _mob, Count = 3, SpawnPoints = new[] { _far.transform }, Spread = 1f });
        _dir.Rounds = _rounds;
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_fc.gameObject); Object.DestroyImmediate(_far); Object.DestroyImmediate(_mob);
    }

    void Run(float seconds) { for (float t = 0; t < seconds; t += 0.02f) _dir.Step(0.02f); }
    void KillAll() { foreach (var m in new List<Mob>(_dir.Mobs.Mobs)) m.ApplyDamage(float.MaxValue); }

    [Test]
    public void StageWithoutABoss_RunsWithNoBossAndNoPatterns()
    {
        _dir.Restart();
        Assert.IsNull(_dir.Boss);
        Run(GameConstants.FirstAttackDelay + 1f);
        Assert.IsNull(_dir.Attacks.Current, "no boss, no telegraphs");
        Assert.AreEqual(GamePhase.Play, _dir.Phase);
    }

    [Test]
    public void Rounds_StartInOrder_EachAfterThePreviousIsWipedOut()
    {
        _dir.ClearRule = StageClearRule.KillAll;
        var started = new List<(int, int)>(); int cleared = 0;
        _dir.RoundStarted += (r, total) => started.Add((r, total));
        _dir.AllEnemiesCleared += () => cleared++;
        _dir.UnitFireRange = 0f;                           // the squad does not shoot: the test does the killing
        _dir.Restart();

        Run(0.5f);
        Assert.AreEqual(0, _dir.Mobs.AliveCount, "nothing before the first-round delay");
        Run(1f);
        Assert.AreEqual(3, _dir.Mobs.AliveCount);
        Assert.AreEqual(new[] { (1, 2) }, started.ToArray());

        Run(2f);
        Assert.AreEqual(1, started.Count, "round 2 waits while round 1 lives");
        Assert.IsFalse(_dir.EnemiesCleared);

        KillAll(); Run(0.1f);
        Assert.AreEqual(new[] { (1, 2), (2, 2) }, started.ToArray());
        Assert.AreEqual(3, _dir.Mobs.AliveCount);
        Assert.AreEqual(GamePhase.Play, _dir.Phase);

        KillAll(); Run(0.1f);
        Assert.IsTrue(_dir.EnemiesCleared);
        Assert.AreEqual(1, cleared);
        Assert.AreEqual(GamePhase.Win, _dir.Phase, "KillAll: wiping the last round clears the stage");
    }

    [Test]
    public void KillAllThenReachEnd_NeedsBoth()
    {
        var end = new GameObject("end"); end.transform.SetParent(_fc.transform); end.transform.position = new Vector3(6f, 0, 3.4f);
        var stage = _fc.gameObject.AddComponent<StageSetup>();
        stage.Squad = _fc; stage.EndPoint = end.transform; stage.EndRadius = 1.5f;
        _dir.Stage = stage; _dir.ClearRule = StageClearRule.KillAllThenReachEnd;
        _dir.UnitFireRange = 0f;
        _dir.Restart();

        _dir.IssueOrder(SquadOrder.Move(new Vector2(6f, 3.4f)));
        Run(2f);
        Assert.IsTrue(stage.ReachedEnd);
        Assert.AreEqual(GamePhase.Play, _dir.Phase, "at the end point, but enemies remain");

        _dir.IssueOrder(SquadOrder.Move(new Vector2(0f, 3.4f)));
        Run(2f);
        KillAll(); Run(0.1f); KillAll(); Run(0.1f);
        Assert.IsTrue(_dir.EnemiesCleared);
        Assert.AreEqual(GamePhase.Play, _dir.Phase, "enemies gone, but not at the end point");

        _dir.IssueOrder(SquadOrder.Move(new Vector2(6f, 3.4f)));
        Run(2f);
        Assert.AreEqual(GamePhase.Win, _dir.Phase);
    }

    [Test]
    public void ReachEndRule_IgnoresEnemies()
    {
        var end = new GameObject("end"); end.transform.SetParent(_fc.transform); end.transform.position = new Vector3(6f, 0, 3.4f);
        var stage = _fc.gameObject.AddComponent<StageSetup>();
        stage.Squad = _fc; stage.EndPoint = end.transform; stage.EndRadius = 1.5f;
        _dir.Stage = stage; _dir.ClearRule = StageClearRule.ReachEnd;
        _dir.UnitFireRange = 0f;
        _dir.Restart();
        Run(1.5f);
        Assert.AreEqual(3, _dir.Mobs.AliveCount);
        _dir.IssueOrder(SquadOrder.Move(new Vector2(6f, 3.4f)));
        Run(2f);
        Assert.AreEqual(GamePhase.Win, _dir.Phase);
    }

    [Test]
    public void TheSquadShootsARoundDown_AndClearsTheStage()
    {
        _dir.ClearRule = StageClearRule.KillAll;
        _dir.Restart();
        Run(1.5f);
        Assert.AreEqual(3, _dir.Mobs.AliveCount);
        Run(2f);
        Assert.AreEqual(3, _dir.Mobs.AliveCount, "a holding squad leaves mobs beyond its threat radius alone");

        _dir.IssueOrder(SquadOrder.AttackMove(new Vector2(0f, -6f)));   // attack-move onto them: now they are targets
        Run(8f);                                            // two rounds of three 66-HP mobs, 25 guns
        Assert.AreEqual(GamePhase.Win, _dir.Phase);
        Assert.AreEqual(GameConstants.SquadSize, _dir.AliveCount);
    }

    [Test]
    public void MobsThatReachTheSquad_KillUnits_AndTheSquadReforms()
    {
        _mob.Speed = 3.2f; _mob.AttackDamage = 100f;       // one hit kills
        _rounds.Rounds.Clear();
        _rounds.Rounds.Add(new RoundSpawner.Round { Mob = _mob, Count = 30, SpawnPoints = new[] { _far.transform }, Spread = 2f });
        _dir.ClearRule = StageClearRule.KillAll;
        _dir.UnitFireRange = 0f;
        _dir.Restart();
        Run(8f);
        Assert.That(_dir.AliveCount, Is.LessThan(GameConstants.SquadSize));
        if (_dir.AliveCount > 0)
        {
            float reach = 0; foreach (var u in _fc.Units) if (!u.Dead) reach = Mathf.Max(reach, u.Slot.magnitude);
            float full = 2 * 0.53f * 1.415f;               // corner of the full 5x5
            if (_dir.AliveCount <= 9) Assert.That(reach, Is.LessThan(full - 0.5f), "survivors closed up toward the anchor");
        }
        else Assert.AreEqual(GamePhase.Lose, _dir.Phase);
    }

    [Test]
    public void StopSquad_HaltsTheAnchor_AndTurnsTheOrderIntoAHold()
    {
        _dir.Restart();
        _dir.IssueOrder(SquadOrder.AttackMove(new Vector2(7f, 0f)));
        _dir.UnitFireRange = 0f; _dir.SquadEngageRange = 0f;   // nothing stops it on its own
        Run(0.3f);
        Assert.IsTrue(_fc.IsMoving);
        var where = _fc.Anchor;

        _dir.StopSquad();
        Assert.IsFalse(_fc.IsMoving);
        Assert.AreEqual(OrderKind.Move, _fc.Order.Kind);
        Run(0.5f);
        Assert.AreEqual(where, _fc.Anchor, "it stays where it was stopped");
    }

    [Test]
    public void StopSquad_DoesNothingOutsideAMatch()
    {
        _dir.StopSquad();                                   // still in the menu: must not throw or change anything
        Assert.AreEqual(GamePhase.Menu, _dir.Phase);
    }
}
