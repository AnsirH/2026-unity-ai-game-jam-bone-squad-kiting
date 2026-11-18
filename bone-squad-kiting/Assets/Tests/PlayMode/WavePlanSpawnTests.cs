using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using BoneSquadKiting.Core;
using BoneSquadKiting.Data;
using BoneSquadKiting.Game;
using BoneSquadKiting.Squad;
using BoneSquadKiting.Stage;

/// <summary>A stage whose waves come from a wave plan asset: mixed kinds, rows by progress, one-off bursts.</summary>
public class WavePlanSpawnTests
{
    FormationController _fc;
    GameDirector _dir;
    WaveSpawner _waves;
    WavePlan _plan;
    MobDefinition _melee, _ranged;

    // A corridor 10 wide and 100 long. The squad starts near z = 95 and the end point is near z = 5.
    static readonly Vector2 Start = new(5f, 95f), End = new(5f, 5f);

    [SetUp]
    public void SetUp()
    {
        _fc = SquadTestRig.MakeFc();
        _fc.Grid = WalkGrid.Open(Vector2.zero, new Vector2(10, 100), 1f);
        _fc.StartAnchor = Start;

        _dir = _fc.gameObject.AddComponent<GameDirector>();
        _dir.enabled = false;
        _dir.Squad = _fc;
        _dir.HasBoss = false;
        _dir.ClearRule = StageClearRule.ReachEnd;
        _dir.UnitFireRange = 0f; _dir.SquadEngageRange = 0f;    // the squad does not fight

        var end = new GameObject("end"); end.transform.SetParent(_fc.transform); end.transform.position = new Vector3(End.x, 0, End.y);
        var stage = _fc.gameObject.AddComponent<StageSetup>();
        stage.Squad = _fc; stage.EndPoint = end.transform; stage.EndRadius = 2f;
        _dir.Stage = stage;

        _melee = ScriptableObject.CreateInstance<MobDefinition>(); _melee.Speed = 0f;
        _ranged = ScriptableObject.CreateInstance<MobDefinition>(); _ranged.Speed = 0f;

        _plan = ScriptableObject.CreateInstance<WavePlan>();
        _plan.FirstWaveDelay = 1f;
        _plan.Sections.Add(Row(0f, 4f, (_melee, 5), (_ranged, 2)));
        _plan.Sections.Add(Row(0.5f, 2f, (_melee, 12)));

        _waves = _fc.gameObject.AddComponent<WaveSpawner>();
        _waves.Plan = _plan;
        _dir.Waves = _waves;
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_fc.gameObject);
        Object.DestroyImmediate(_plan); Object.DestroyImmediate(_melee); Object.DestroyImmediate(_ranged);
    }

    static WaveSection Row(float from, float interval, params (MobDefinition mob, int count)[] mobs)
    {
        var row = new WaveSection { FromProgress = from, Interval = interval };
        foreach (var (mob, count) in mobs) row.Mobs.Add(new WaveEntry { Mob = mob, Count = count });
        return row;
    }

    void Run(float seconds) { for (float t = 0; t < seconds; t += 0.02f) _dir.Step(0.02f); }

    int Alive(MobDefinition def)
    {
        int n = 0;
        foreach (var m in _dir.Mobs.Mobs) if (m.Alive && m.Def == def) n++;
        return n;
    }

    [Test]
    public void AWave_IsMadeOfEveryKindInTheRow()
    {
        _dir.Restart();
        Run(1.2f);
        Assert.AreEqual(1, _waves.WavesSpawned);
        Assert.AreEqual(5, Alive(_melee));
        Assert.AreEqual(2, Alive(_ranged));
        Assert.AreEqual(7, _waves.MobsSpawned);
    }

    [Test]
    public void Waves_FollowTheRowIntervalWhileStandingStill()
    {
        var times = new List<float>();
        _dir.WaveStarted += (n, mobs) => times.Add(_dir.Elapsed);
        _dir.Restart();
        Run(10f);
        Assert.AreEqual(3, times.Count, "waves at 1 s, 5 s and 9 s");
        Assert.AreEqual(4f, times[1] - times[0], 0.05f);
        Assert.AreEqual(0, _waves.SectionIndex);
    }

    [Test]
    public void FurtherAlong_TheLaterRowApplies()
    {
        _dir.Restart();
        _fc.Anchor = _fc.Destination = new Vector2(5f, 30f);                      // about 72% of the way
        Run(1.2f);
        Assert.AreEqual(1, _waves.SectionIndex);
        Assert.AreEqual(12, Alive(_melee));
        Assert.AreEqual(0, Alive(_ranged));
    }

    [Test]
    public void ABurst_ArrivesOnce_TheFirstTimeTheSquadGetsThatFar()
    {
        var burst = new WaveBurst { AtProgress = 0.4f };
        burst.Mobs.Add(new WaveEntry { Mob = _ranged, Count = 9 });
        _plan.Bursts.Add(burst);
        _plan.FirstWaveDelay = 100f;                            // no regular wave during this test

        _dir.Restart();
        Run(1f);
        Assert.AreEqual(0, Alive(_ranged), "not there yet");

        _fc.Anchor = _fc.Destination = new Vector2(5f, 55f);                      // about 44%
        Run(0.1f);
        Assert.AreEqual(9, Alive(_ranged));
        Assert.AreEqual(0, _waves.WavesSpawned, "a burst is not a wave of the regular pace");

        _fc.Anchor = _fc.Destination = new Vector2(5f, 90f);                      // back toward the start, then forward again
        Run(0.1f);
        _fc.Anchor = _fc.Destination = new Vector2(5f, 50f);
        Run(0.1f);
        Assert.AreEqual(9, Alive(_ranged), "only the first time");
    }

    [Test]
    public void EditingThePlanWhilePlaying_AppliesFromTheNextWave()
    {
        _dir.Restart();
        Run(1.2f);
        Assert.AreEqual(5, Alive(_melee));
        _plan.Sections[0].Mobs[0].Count = 20;
        Run(4f);
        Assert.AreEqual(25, Alive(_melee));
    }

    [Test]
    public void Hold_PausesTheTimer_AndSpawnNow_BringsTheNextWaveAtOnce()
    {
        _dir.Restart();
        _waves.Hold = true;
        Run(5f);
        Assert.AreEqual(0, _waves.WavesSpawned);
        _waves.Hold = false;
        _waves.SpawnNow();
        Run(0.05f);
        Assert.AreEqual(1, _waves.WavesSpawned);
    }

    [Test]
    public void WithoutAPlan_TheOldCurveSettingsStillWork()
    {
        _waves.Plan = null;
        _waves.Mob = _melee; _waves.FirstWaveDelay = 1f;
        _waves.Interval = AnimationCurve.Linear(0f, 4f, 1f, 1f);
        _waves.Count = AnimationCurve.Linear(0f, 4f, 1f, 20f);
        _dir.Restart();
        Run(1.2f);
        Assert.AreEqual(4, Alive(_melee));
    }
}
