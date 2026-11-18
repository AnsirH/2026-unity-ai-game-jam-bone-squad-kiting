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

/// <summary>A progress stage: endless waves paced by how far the squad has advanced, clear rules, the stop command.</summary>
public class StageFlowTests
{
    FormationController _fc;
    GameDirector _dir;
    WaveSpawner _waves;
    StageSetup _stage;
    MobDefinition _mob;
    WalkGrid _grid;

    // A corridor 10 wide and 100 long. The squad starts near z = 95 and the end point is near z = 5.
    static readonly Vector2 Start = new(5f, 95f), End = new(5f, 5f);

    [SetUp]
    public void SetUp()
    {
        _fc = SquadTestRig.MakeFc();
        _grid = WalkGrid.Open(Vector2.zero, new Vector2(10, 100), 1f);
        _fc.Grid = _grid; _fc.StartAnchor = Start;

        _dir = _fc.gameObject.AddComponent<GameDirector>();
        _dir.enabled = false;
        _dir.Squad = _fc;
        _dir.HasBoss = false;
        _dir.ClearRule = StageClearRule.ReachEnd;
        _dir.UnitFireRange = 0f; _dir.SquadEngageRange = 0f;    // the squad does not fight unless a test turns it on

        var end = new GameObject("end"); end.transform.SetParent(_fc.transform); end.transform.position = new Vector3(End.x, 0, End.y);
        _stage = _fc.gameObject.AddComponent<StageSetup>();
        _stage.Squad = _fc; _stage.EndPoint = end.transform; _stage.EndRadius = 2f;
        _dir.Stage = _stage;

        _mob = ScriptableObject.CreateInstance<MobDefinition>();
        _mob.Speed = 0f;                                        // mobs wait where they spawn so tests control the pace
        _waves = _fc.gameObject.AddComponent<WaveSpawner>();
        _waves.Mob = _mob;
        _waves.FirstWaveDelay = 1f;
        _waves.Interval = AnimationCurve.Linear(0f, 4f, 1f, 1f);
        _waves.Count = AnimationCurve.Linear(0f, 4f, 1f, 20f);
        _dir.Waves = _waves;
    }

    [TearDown]
    public void TearDown() { Object.DestroyImmediate(_fc.gameObject); Object.DestroyImmediate(_mob); }

    void Run(float seconds) { for (float t = 0; t < seconds; t += 0.02f) _dir.Step(0.02f); }

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
    public void Waves_KeepComing_AtASteadyPaceWhileTheSquadStandsStill()
    {
        var waves = new List<(float time, int mobs)>();
        _dir.WaveStarted += (n, mobs) => waves.Add((_dir.Elapsed, mobs));
        _dir.Restart();

        Run(0.9f);
        Assert.AreEqual(0, waves.Count, "nothing before the first-wave delay");
        Run(13f);                                              // first at 1s, then every 4s: 1, 5, 9, 13
        Assert.AreEqual(4, waves.Count);
        foreach (var w in waves) Assert.AreEqual(4, w.mobs, "wave size at the start point");
        Assert.That(waves[2].time - waves[1].time, Is.EqualTo(4f).Within(0.05f));
        Assert.That(waves[3].time - waves[2].time, Is.EqualTo(4f).Within(0.05f), "standing still does not shorten the gap");
        Assert.AreEqual(16, _dir.Mobs.AliveCount, "nobody is killing them, so they add up");
    }

    [Test]
    public void Progress_GrowsTowardTheEndPoint_AndNeverGoesBack()
    {
        _dir.Restart();
        Run(0.1f);
        Assert.That(_waves.MaxProgress, Is.EqualTo(0f).Within(0.02f));

        _dir.IssueOrder(SquadOrder.Move(new Vector2(5f, 50f)));
        Run(8f);
        Assert.That(_waves.MaxProgress, Is.EqualTo(0.5f).Within(0.03f), "halfway down the corridor");

        _dir.IssueOrder(SquadOrder.Move(Start));
        Run(8f);
        Assert.That(_waves.ProgressAt(_fc.Anchor), Is.LessThan(0.05f), "walked back to the start");
        Assert.That(_waves.MaxProgress, Is.EqualTo(0.5f).Within(0.03f), "retreating does not make it easier");
    }

    [Test]
    public void FurtherAlong_WavesAreBiggerAndCloserTogether()
    {
        _dir.Restart();
        _fc.Anchor = _fc.Destination = new Vector2(5f, 14f);    // 90% of the way
        var waves = new List<(float time, int mobs)>();
        _dir.WaveStarted += (n, mobs) => waves.Add((_dir.Elapsed, mobs));
        Run(6f);
        Assert.That(waves.Count, Is.GreaterThanOrEqualTo(3));
        Assert.That(waves[1].mobs, Is.EqualTo(18).Within(1), "4 at the start, 20 at the end, 90% along");
        Assert.That(waves[2].time - waves[1].time, Is.EqualTo(1.3f).Within(0.1f), "4s at the start, 1s at the end");
    }

    [Test]
    public void Mobs_SpawnOnWalkableGround_AwayFromTheSquad()
    {
        _dir.Restart();
        _fc.Anchor = _fc.Destination = new Vector2(5f, 50f);
        Run(6f);
        Assert.That(_dir.Mobs.Mobs.Count, Is.GreaterThan(0));
        int ahead = 0;
        foreach (var m in _dir.Mobs.Mobs)
        {
            Assert.IsTrue(_grid.IsWalkable(m.Pos), $"mob at {m.Pos}");
            Assert.That(Vector2.Distance(m.Pos, _fc.Anchor), Is.GreaterThan(_waves.MinSpawnDistance - _waves.GroupSpread - 1f));
            if (m.Z < 50f) ahead++;
        }
        Assert.That(ahead, Is.GreaterThan(0), "some come from the end-point side");
    }

    [Test]
    public void Waves_StopAtTheAliveCap()
    {
        _waves.Count = AnimationCurve.Constant(0f, 1f, 50f);
        _waves.Interval = AnimationCurve.Constant(0f, 1f, 0.5f);
        _dir.Restart();
        _dir.Mobs.MaxAlive = 120;
        Run(6f);
        Assert.AreEqual(120, _dir.Mobs.AliveCount);
    }

    [Test]
    public void ReachEnd_ClearsTheStage_WhateverIsStillAlive()
    {
        _dir.Restart();
        Run(2f);
        Assert.That(_dir.Mobs.AliveCount, Is.GreaterThan(0));
        _fc.Anchor = new Vector2(5f, 12f);
        _dir.IssueOrder(SquadOrder.Move(End));
        Run(2f);
        Assert.IsTrue(_stage.ReachedEnd);
        Assert.AreEqual(GamePhase.Win, _dir.Phase);
    }

    [Test]
    public void DefeatBossRule_IsNotSatisfiedByReachingTheEnd()
    {
        _dir.HasBoss = true; _dir.ClearRule = StageClearRule.DefeatBoss;
        _dir.Restart();
        _fc.Anchor = _fc.Destination = End;
        Run(0.5f);
        Assert.AreEqual(GamePhase.Play, _dir.Phase);
        _dir.Boss.ApplyDamage(GameConstants.BossMax);
        Run(0.1f);
        Assert.AreEqual(GamePhase.Win, _dir.Phase);
    }

    [Test]
    public void TheSquadShootsWavesDown_WhenTheyComeIntoRange()
    {
        _mob.Speed = 3.2f;                                      // now they walk in
        _dir.UnitFireRange = 6f;
        int died = 0;
        _dir.Restart();
        _dir.Mobs.OnMobDied += _ => died++;
        Run(20f);
        Assert.That(died, Is.GreaterThan(8), "mobs that walked into range were shot");
        Assert.AreEqual(GamePhase.Play, _dir.Phase);
    }

    [Test]
    public void MobsThatReachTheSquad_KillUnits_AndTheSquadReforms()
    {
        _mob.Speed = 3.2f; _mob.AttackDamage = 100f;            // one hit kills; the squad cannot shoot (range 0)
        _dir.Restart();
        Run(25f);
        Assert.That(_dir.AliveCount, Is.LessThan(GameConstants.SquadSize));
        if (_dir.AliveCount == 0) { Assert.AreEqual(GamePhase.Lose, _dir.Phase); return; }
        float reach = 0; foreach (var u in _fc.Units) if (!u.Dead) reach = Mathf.Max(reach, u.Slot.magnitude);
        if (_dir.AliveCount <= 9) Assert.That(reach, Is.LessThan(1.2f), "survivors closed up toward the anchor");
    }

    [Test]
    public void StopSquad_HaltsTheAnchor_AndTurnsTheOrderIntoAHold()
    {
        _dir.Restart();
        _dir.IssueOrder(SquadOrder.AttackMove(new Vector2(5f, 60f)));
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
        _dir.StopSquad();                                       // still in the menu: must not throw or change anything
        Assert.AreEqual(GamePhase.Menu, _dir.Phase);
    }
}
