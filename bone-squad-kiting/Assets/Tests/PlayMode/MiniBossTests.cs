using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using BoneSquadKiting.Bosses;
using BoneSquadKiting.Cameras;
using BoneSquadKiting.Combat;
using BoneSquadKiting.Core;
using BoneSquadKiting.Data;
using BoneSquadKiting.Game;
using BoneSquadKiting.Squad;
using BoneSquadKiting.Stage;

/// <summary>A stage with a mini boss: it appears when approached, freezes the game for its intro and death, and gates the end point.</summary>
public class MiniBossTests
{
    FormationController _fc;
    GameDirector _dir;
    StageSetup _stage;
    MiniBossSpawn _spawn;
    EliteDefinition _def;
    WaveSpawner _waves;
    MobDefinition _mob;

    // Corridor 20 wide, 100 long. Squad starts at z = 90; mini boss waits at z = 40; end point at z = 10.
    static readonly Vector2 Start = new(10f, 90f), BossSpot = new(10f, 40f), End = new(10f, 10f);

    [SetUp]
    public void SetUp()
    {
        _fc = SquadTestRig.MakeFc();
        _fc.Grid = WalkGrid.Open(Vector2.zero, new Vector2(20, 100), 1f);
        _fc.StartAnchor = Start;

        _dir = _fc.gameObject.AddComponent<GameDirector>();
        _dir.enabled = false; _dir.Squad = _fc; _dir.HasBoss = false;
        _dir.ClearRule = StageClearRule.DefeatMiniBossThenReachEnd;
        _dir.UnitFireRange = 0f; _dir.SquadEngageRange = 0f;   // the squad holds fire unless a test says otherwise

        var end = new GameObject("end"); end.transform.SetParent(_fc.transform); end.transform.position = new Vector3(End.x, 0, End.y);
        _stage = _fc.gameObject.AddComponent<StageSetup>();
        _stage.Squad = _fc; _stage.EndPoint = end.transform; _stage.EndRadius = 2f;
        _dir.Stage = _stage;

        _def = ScriptableObject.CreateInstance<EliteDefinition>();
        _def.MaxHp = 500f; _def.Speed = 2f; _def.StopDistance = 3f;
        _def.FirstAttackDelay = 1f; _def.AttackGap = 2f; _def.IntroDuration = 2f; _def.DeathDuration = 1.5f;
        var pattern = ScriptableObject.CreateInstance<PatternDefinition>();
        pattern.Time = 1.5f; pattern.Shape = DangerShape.Disc(3.10f); pattern.DisplayName = "slam";
        _def.Patterns = new List<PatternDefinition> { pattern };

        var spawnGo = new GameObject("mini boss spawn"); spawnGo.transform.SetParent(_fc.transform);
        spawnGo.transform.position = new Vector3(BossSpot.x, 0, BossSpot.y);
        _spawn = spawnGo.AddComponent<MiniBossSpawn>();
        _spawn.Definition = _def; _spawn.TriggerRadius = 12f;

        _mob = ScriptableObject.CreateInstance<MobDefinition>(); _mob.Speed = 0f;
        _waves = _fc.gameObject.AddComponent<WaveSpawner>();
        _waves.Mob = _mob; _waves.FirstWaveDelay = 0.5f;
        _waves.Interval = AnimationCurve.Constant(0, 1, 1f); _waves.Count = AnimationCurve.Constant(0, 1, 5f);
        _dir.Waves = _waves;

        _dir.Restart();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_fc.gameObject); Object.DestroyImmediate(_def); Object.DestroyImmediate(_mob);
    }

    void Run(float seconds) { for (float t = 0; t < seconds; t += 0.02f) _dir.Step(0.02f); }
    void Teleport(Vector2 p) { _fc.Anchor = _fc.Destination = p; foreach (var u in _fc.Units) { u.X = p.x + u.Slot.x; u.Z = p.y + u.Slot.y; } }
    Elite Summon() { Teleport(BossSpot + new Vector2(0, 11f)); _dir.Step(0.02f); return _spawn.Elite; }

    [Test]
    public void MiniBoss_IsNotThere_UntilTheSquadComesClose()
    {
        Run(1f);
        Assert.AreEqual(0, _dir.Elites.Elites.Count);
        Assert.IsFalse(_spawn.Triggered);
        Assert.IsFalse(_dir.EndOpen);

        Elite appeared = null; _dir.MiniBossAppeared += e => appeared = e;
        var elite = Summon();
        Assert.IsNotNull(elite);
        Assert.AreSame(elite, appeared);
        Assert.AreEqual(1, _dir.Elites.Elites.Count);
        Assert.That(Vector2.Distance(elite.Pos, BossSpot), Is.LessThan(1f), "it appears on its marker");
        Assert.AreEqual(500f, elite.Health);
    }

    [Test]
    public void TwoPatterns_AreCastInListOrder_ThenFromTheTopAgain()
    {
        // Pinpoint zones: nobody is hit, so the squad lives through several casts.
        _def.Patterns[0].Time = 0.2f; _def.Patterns[0].Shape = DangerShape.Disc(0.01f);
        var second = ScriptableObject.CreateInstance<PatternDefinition>();
        second.Time = 0.2f; second.Shape = DangerShape.Disc(0.01f); second.DisplayName = "cross";
        _def.Patterns.Add(second);
        var cast = new List<PatternDefinition>();
        _dir.PatternStarted += cast.Add;
        Summon();
        Run(10f);
        Assert.That(cast.Count, Is.GreaterThanOrEqualTo(3));
        Assert.AreSame(_def.Patterns[0], cast[0]);
        Assert.AreSame(second, cast[1]);
        Assert.AreSame(_def.Patterns[0], cast[2]);
        Object.DestroyImmediate(second);
    }

    [Test]
    public void Intro_FreezesTheGame_AndIgnoresInput_ThenPlayResumes()
    {
        Vector2 focus = default; float seconds = 0; string title = null;
        _dir.CutsceneStarted += (f, s, t, fx) => { focus = f; seconds = s; title = t; };
        var elite = Summon();
        Assert.IsTrue(_dir.InCutscene);
        Assert.AreEqual(2f, seconds); Assert.AreEqual(_def.DisplayName, title);
        Assert.That(Vector2.Distance(focus, elite.Pos), Is.LessThan(0.01f));

        float elapsed = _dir.Elapsed; var elitePos = elite.Pos; int mobs = _dir.Mobs.AliveCount; var dest = _fc.Destination;
        _dir.IssueOrder(SquadOrder.Move(Start));
        Assert.IsFalse(_dir.TryRevive());
        Run(1.9f);
        Assert.AreEqual(elapsed, _dir.Elapsed, "match time stands still");
        Assert.AreEqual(elitePos, elite.Pos);
        Assert.AreEqual(mobs, _dir.Mobs.AliveCount, "no waves during the intro");
        Assert.AreEqual(dest, _fc.Destination, "orders are ignored");

        Run(0.2f);
        Assert.IsFalse(_dir.InCutscene);
        Run(1f);
        Assert.That(_dir.Elapsed, Is.GreaterThan(elapsed));
        Assert.That(Vector2.Distance(elite.Pos, elitePos), Is.GreaterThan(0.5f), "the mini boss is walking now");
    }

    [Test]
    public void MiniBoss_WalksToTheSquad_AndStopsShort()
    {
        _def.FirstAttackDelay = 1000f;                  // no casting in this test: just the walk
        var elite = Summon();
        Run(2.1f);                                      // intro
        Run(8f);
        float nearest = float.MaxValue;
        foreach (var u in _fc.Units) nearest = Mathf.Min(nearest, Vector2.Distance(elite.Pos, new Vector2(u.X, u.Z)));
        Assert.That(nearest, Is.LessThan(_def.StopDistance + 0.5f), "it closed in");
        Assert.That(nearest, Is.GreaterThan(_def.StopDistance - 1.5f), "but does not walk into the squad");
    }

    [Test]
    public void ItsZone_IsFixedWhereItWasAnnounced_AndHitsWhoeverStaysThere()
    {
        var elite = Summon();
        Run(2.1f);
        for (int i = 0; i < 200 && _dir.Telegraphs.Count == 0; i++) _dir.Step(0.02f);
        Assert.AreEqual(1, _dir.Telegraphs.Count, "its cast shows up as a telegraph");
        var zone = _dir.Telegraphs[0];
        Assert.IsFalse(zone.Tracks);
        Assert.IsTrue(elite.Casting);
        var standing = elite.Pos;
        var announcedAt = new Vector2(zone.Cx, zone.Cz);
        Assert.That(Vector2.Distance(announcedAt, _fc.Anchor), Is.LessThan(0.01f), "centered on the squad at that moment");

        // The squad walks out; the zone stays behind and the mini boss stands still while casting.
        Teleport(_fc.Anchor + new Vector2(0, 8f));
        int hit = -1; _dir.PatternResolved += (h, l) => hit = h;
        Run(1.6f);
        Assert.AreEqual(0, hit, "nobody was left inside");
        Assert.AreEqual(GameConstants.UnitMax, _fc.Units[3].Hp);
        Assert.That(Vector2.Distance(standing, elite.Pos), Is.LessThan(0.3f), "it stood still for the cast (it starts walking again right after)");
    }

    [Test]
    public void StayingInTheZone_CostsHealth()
    {
        Summon();
        Run(2.1f);
        for (int i = 0; i < 200 && _dir.Telegraphs.Count == 0; i++) _dir.Step(0.02f);
        int hit = -1; _dir.PatternResolved += (h, l) => hit = h;
        Run(1.6f);
        Assert.AreEqual(GameConstants.SquadSize, hit);
        Assert.AreEqual(GameConstants.UnitMax - GameConstants.PatternUnitDamage, _fc.Units[3].Hp);
    }

    [Test]
    public void EndPoint_StaysClosed_UntilTheMiniBossIsDead()
    {
        Teleport(End);
        Run(0.2f);
        Assert.IsTrue(_stage.ReachedEnd);
        Assert.AreEqual(GamePhase.Play, _dir.Phase, "untriggered mini boss: the end point is closed");

        var elite = Summon();
        Run(2.1f);
        Teleport(End); Run(0.2f);
        Assert.AreEqual(GamePhase.Play, _dir.Phase, "alive mini boss: still closed");
        Assert.IsFalse(_dir.EndOpen);

        elite.ApplyDamage(float.MaxValue);
        Run(0.1f);
        Assert.IsTrue(_dir.EndOpen);
        Run(1.6f);                                      // death moment
        Run(0.1f);
        Assert.AreEqual(GamePhase.Win, _dir.Phase, "dead mini boss + at the end point");
    }

    [Test]
    public void MiniBossDeath_SweepsTheSwarmAway_StopsTheWaves_AndPlaysItsMoment()
    {
        var elite = Summon();
        Run(2.1f); Run(2f);
        Assert.That(_dir.Mobs.AliveCount, Is.GreaterThan(0), "waves were coming");

        Elite defeated = null; _dir.MiniBossDefeated += e => defeated = e;
        float cutscene = 0; _dir.CutsceneStarted += (f, s, t, fx) => cutscene = s;
        elite.ApplyDamage(float.MaxValue);
        _dir.Step(0.02f);

        Assert.AreSame(elite, defeated);
        Assert.AreEqual(1.5f, cutscene);
        Assert.IsTrue(_dir.InCutscene);
        Assert.AreEqual(0, _dir.Mobs.AliveCount, "the swarm is gone");
        Assert.AreEqual(0, _dir.Shots.Shots.Count, "so are shots in flight");
        Assert.AreEqual(0, _dir.Elites.Elites.Count);
        Assert.IsTrue(_waves.Stopped);

        Run(1.6f); Run(5f);
        Assert.AreEqual(0, _dir.Mobs.AliveCount, "no more waves on the way to the end point");
        Assert.AreEqual(0, _dir.Telegraphs.Count);
    }

    [Test]
    public void ReinforcementLeaderStopsDueWavesEvenWithAnotherArtilleryUnitAlive()
    {
        var other = AddSpawn(BossSpot + new Vector2(0, -20f));
        other.PresentFromStart = true;
        var unseen = AddSpawn(End);
        _spawn.EndsReinforcements = true;
        _spawn.PlayIntro = false;
        _dir.Restart();
        var leader = Summon();
        _waves.SpawnNow();
        _dir.Step(.02f);
        int spawned = _waves.MobsSpawned;
        Assert.Greater(spawned, 0);

        leader.ApplyDamage(float.MaxValue);
        _waves.SpawnNow();
        _dir.Step(.02f);
        Assert.IsTrue(other.Elite.Alive);
        Assert.IsTrue(unseen.Defeated, "unreached encounter retired");
        Assert.IsTrue(_waves.Stopped);
        Assert.IsEmpty(_dir.Mobs.Mobs);
        Run(6f);
        Assert.AreEqual(spawned, _waves.MobsSpawned);
        _dir.Restart();
        _waves.SpawnNow();
        _dir.Step(.02f);
        Assert.Greater(_dir.Mobs.AliveCount, 0);
        Assert.IsFalse(unseen.Triggered);
    }

    [Test]
    public void GroundGuidesFollowApproachAndTheNextClearedWave()
    {
        AddSpawn(End);
        _spawn.PresentFromStart = true;
        _def.AttackRange = _spawn.TriggerRadius + 2f;
        _spawn.ApproachGuide = new GameObject("Dodge");
        _spawn.NextWaveClearGuide = new GameObject("Rescue");
        _spawn.ApproachGuide.transform.SetParent(_fc.transform);
        _spawn.NextWaveClearGuide.transform.SetParent(_fc.transform);
        _dir.Restart();
        _waves.SpawnNow();
        _dir.Step(.02f);
        Assert.IsFalse(_spawn.ApproachGuide.activeSelf);
        Assert.IsFalse(_spawn.NextWaveClearGuide.activeSelf);
        var olderMob = _dir.Mobs.Mobs[0];
        int olderCount = _dir.Mobs.Mobs.Count;

        Teleport(BossSpot + Vector2.up * (_def.AttackRange - .1f));
        _dir.Step(.02f);
        var gun = _spawn.Elite;
        Assert.IsTrue(_spawn.ApproachGuide.activeSelf, "show the dodge before the first shot, even outside the HP-bar radius");
        gun.ApplyDamage(float.MaxValue);
        _dir.Step(.02f);
        _waves.SpawnNow();
        _dir.Step(.02f);
        var targets = _dir.Mobs.Mobs.GetRange(olderCount, _dir.Mobs.Mobs.Count - olderCount);
        Assert.Greater(targets.Count, 1);
        _waves.SpawnNow();
        _dir.Step(.02f);
        for (int i = 1; i < targets.Count; i++) targets[i].ApplyDamage(float.MaxValue);
        _dir.Step(.02f);
        Assert.IsFalse(_spawn.NextWaveClearGuide.activeSelf, "one member of that wave remains");
        targets[0].ApplyDamage(float.MaxValue);
        _dir.Step(.02f);
        Assert.IsTrue(olderMob.Alive);
        Assert.IsTrue(_spawn.NextWaveClearGuide.activeSelf, "unrelated waves do not delay the guide");
        _dir.Restart();
        Assert.IsFalse(_spawn.ApproachGuide.activeSelf);
        Assert.IsFalse(_spawn.NextWaveClearGuide.activeSelf);
    }

    [Test]
    public void TheSquadCanShootTheMiniBossDown()
    {
        var elite = Summon();
        Run(2.1f);
        _dir.UnitFireRange = 40f; _dir.SquadEngageRange = 40f;
        _dir.IssueOrder(SquadOrder.Attack(elite));
        Run(4f);                                        // 500 HP against 25 guns
        Assert.IsFalse(elite.Alive);
        Assert.IsTrue(_spawn.Defeated);
    }

    [Test]
    public void Restart_PutsTheMiniBossBackInHiding()
    {
        Summon();
        _dir.Restart();
        Assert.IsFalse(_spawn.Triggered);
        Assert.AreEqual(0, _dir.Elites.Elites.Count);
        Assert.IsFalse(_dir.InCutscene);
    }

    [Test]
    public void CameraFocus_GoesToThePoint_AndComesBack()
    {
        var go = new GameObject("cam");
        go.transform.rotation = Quaternion.Euler(60, 180, 0);
        var cam = go.AddComponent<EdgeScrollCamera>(); cam.enabled = false;
        cam.BoundsMin = new Vector2(-50, -50); cam.BoundsMax = new Vector2(50, 50);
        cam.LookAt = new Vector2(1, 2); cam.Apply();

        cam.FocusOn(new Vector2(11, 12), 2f);
        for (int i = 0; i < 50; i++) cam.TickFocus(0.02f);       // 1.0s: holding on the point
        Assert.That(Vector2.Distance(cam.LookAt, new Vector2(11, 12)), Is.LessThan(0.01f));
        Assert.IsTrue(cam.Focusing);
        for (int i = 0; i < 55; i++) cam.TickFocus(0.02f);
        Assert.IsFalse(cam.Focusing);
        Assert.AreEqual(new Vector2(1, 2), cam.LookAt, "back where the player left it");
        Object.DestroyImmediate(go);
    }

    // ---- stage lineup features: fixed cannons, several mini bosses per stage, activation chains ----

    MiniBossSpawn AddSpawn(Vector2 at, EliteDefinition def = null)
    {
        var go = new GameObject("extra spawn"); go.transform.SetParent(_fc.transform);
        go.transform.position = new Vector3(at.x, 0, at.y);
        var spawn = go.AddComponent<MiniBossSpawn>();
        spawn.Definition = def != null ? def : _def; spawn.TriggerRadius = 12f;
        return spawn;
    }

    EliteDefinition Cannon()
    {
        var c = Object.Instantiate(_def);
        c.Speed = 0f; c.AttackRange = 14f; c.FirstAttackDelay = 0.5f; c.AttackGap = 1f; c.AttackGapRandom = 0f;
        return c;
    }

    [Test]
    public void PresentFromStart_ItIsThereAtOnce_WithNoIntro()
    {
        _spawn.PresentFromStart = true;
        bool cutscene = false; _dir.CutsceneStarted += (f, s, t, fx) => cutscene = true;
        _dir.Restart();
        _dir.Step(0.02f);                                   // squad is 50 away, far outside the trigger radius
        Assert.IsTrue(_spawn.Triggered);
        Assert.AreEqual(1, _dir.Elites.Elites.Count);
        Assert.IsFalse(cutscene);
        Assert.IsFalse(_dir.InCutscene);
    }

    [Test]
    public void OffWalkableGround_AFixedOneStaysOnItsMarker_AMovingOneIsPutOnTheNearestOpenPoint()
    {
        var offGrid = new Vector2(25f, 40f);                // the corridor is x 0..20
        var fixedSpawn = AddSpawn(offGrid, Cannon()); fixedSpawn.PresentFromStart = true;
        var movingSpawn = AddSpawn(offGrid); movingSpawn.PresentFromStart = true;
        _dir.Restart();
        _dir.Step(0.02f);
        Assert.AreEqual(offGrid, fixedSpawn.Elite.Pos, "a fixed one stands where it was placed");
        Assert.IsTrue(_fc.Grid.IsWalkable(movingSpawn.Elite.Pos), "a moving one is brought onto walkable ground");
        Run(2f);
        Assert.AreEqual(offGrid, fixedSpawn.Elite.Pos, "and it stays there");
    }

    [Test]
    public void FixedMiniBoss_NeverMoves_AndOnlyFiresWhenTheSquadIsInReach()
    {
        var cannon = Cannon();
        _spawn.Definition = cannon; _spawn.PresentFromStart = true;
        _dir.Restart();
        Run(4f);                                            // squad 50 away: out of its 14 reach
        var elite = _spawn.Elite;
        Assert.That(Vector2.Distance(elite.Pos, BossSpot), Is.LessThan(0.01f));
        Assert.AreEqual(0, _dir.Telegraphs.Count, "it holds fire while the squad is out of reach");

        Teleport(BossSpot + new Vector2(0, 10f));
        bool fired = false;
        for (int i = 0; i < 200 && !fired; i++) { _dir.Step(0.02f); fired = _dir.Telegraphs.Count > 0; }
        Assert.IsTrue(fired, "in reach: it fires");
        Run(5f);
        Assert.That(Vector2.Distance(elite.Pos, BossSpot), Is.LessThan(0.01f), "and still has not moved");
        Object.DestroyImmediate(cannon);
    }

    [Test]
    public void NoIntro_WhenTheMarkerSaysSo()
    {
        _spawn.PlayIntro = false;
        Summon();
        Assert.AreEqual(1, _dir.Elites.Elites.Count);
        Assert.IsFalse(_dir.InCutscene);
    }

    [Test]
    public void SeveralMiniBosses_OnlyTheLastDeathSweepsTheStage()
    {
        var second = AddSpawn(BossSpot + new Vector2(0, -15f));
        _dir.Restart();
        var first = Summon(); Run(2.1f); Run(2f);
        Assert.That(_dir.Mobs.AliveCount, Is.GreaterThan(0));

        int cutscenes = 0; _dir.CutsceneStarted += (f, s, t, fx) => cutscenes++;
        int defeated = 0; _dir.MiniBossDefeated += e => defeated++;
        first.ApplyDamage(float.MaxValue);
        _dir.Step(0.02f);
        Assert.AreEqual(1, defeated);
        Assert.AreEqual(0, cutscenes, "not the last one: it just dies");
        Assert.That(_dir.Mobs.AliveCount, Is.GreaterThan(0), "the swarm stays");
        Assert.IsFalse(_waves.Stopped);
        Assert.IsFalse(_dir.EndOpen);

        Teleport(second.Position + new Vector2(0, 11f)); _dir.Step(0.02f);
        Assert.IsTrue(second.Triggered);
        Run(2.1f);
        second.Elite.ApplyDamage(float.MaxValue);
        _dir.Step(0.02f);
        Assert.AreEqual(2, cutscenes, "its intro, then the finish for the last one");
        Assert.AreEqual(0, _dir.Mobs.AliveCount);
        Assert.IsTrue(_waves.Stopped);
        Assert.IsTrue(_dir.EndOpen);
    }

    [Test]
    public void ActivateAfter_StaysAwayUntilTheOthersAreDead()
    {
        var gated = AddSpawn(BossSpot + new Vector2(3f, 0));     // right next to the first one
        gated.ActivateAfter = new[] { _spawn };
        _dir.Restart();
        var first = Summon(); Run(2.1f);
        Assert.IsFalse(gated.Triggered, "close enough, but it waits for the first to die");
        Assert.IsFalse(gated.Unlocked);

        first.ApplyDamage(float.MaxValue);
        Run(0.1f);
        Assert.IsTrue(gated.Unlocked);
        Assert.IsTrue(gated.Triggered, "now it appears");
        Assert.IsFalse(_dir.EndOpen, "and the stage is not over");
        Assert.AreEqual(1, _dir.Elites.Elites.Count);
    }

    [Test]
    public void AttackGapRandom_MakesTheGapsIrregular_WithinItsRange()
    {
        var cannon = Cannon();
        // A pinpoint zone and a ring formation: nobody is ever hit, so the squad survives long enough to count gaps.
        cannon.AttackGap = 1f; cannon.AttackGapRandom = 2f;
        cannon.Patterns[0].Time = 0.2f; cannon.Patterns[0].Shape = DangerShape.Disc(0.01f);
        _spawn.Definition = cannon; _spawn.PresentFromStart = true;
        _dir.Restart();
        _fc.SetFormation("ring");
        Teleport(BossSpot + new Vector2(0, 10f));
        var gaps = new System.Collections.Generic.List<float>();
        float lastResolve = -1f;
        _dir.PatternResolved += (h, l) => lastResolve = _dir.Elapsed;
        _dir.PatternStarted += p => { if (lastResolve >= 0f) gaps.Add(_dir.Elapsed - lastResolve); };
        Run(40f);
        Assert.That(gaps.Count, Is.GreaterThan(8));
        float min = float.MaxValue, max = 0;
        foreach (var g in gaps) { min = Mathf.Min(min, g); max = Mathf.Max(max, g); }
        Assert.That(min, Is.GreaterThanOrEqualTo(0.95f)); Assert.That(max, Is.LessThanOrEqualTo(3.1f));
        Assert.That(max - min, Is.GreaterThan(0.3f), "the gaps are not all the same");
        Object.DestroyImmediate(cannon);   // (the pattern asset it shares with _def is recreated in SetUp)
    }

    // ---- entry routes, progress triggers, intro timing (the mini boss tool's settings) ----

    Transform RoutePoint(Vector2 p)
    {
        var t = new GameObject("route").transform; t.SetParent(_spawn.transform); t.position = new Vector3(p.x, 0f, p.y);
        return t;
    }

    [Test]
    public void EntryRoute_ItAppearsAtTheFirstPoint_WalksThrough_AndOnlyThenFights()
    {
        _spawn.Route = new[] { RoutePoint(new Vector2(-6f, 40f)), RoutePoint(new Vector2(4f, 46f)) };   // from outside the corridor
        _spawn.EntrySpeed = 5f; _spawn.PlayIntro = false;
        var elite = Summon();
        Assert.IsNotNull(elite);
        Assert.IsTrue(elite.Entering);
        Assert.That(Vector2.Distance(elite.Pos, new Vector2(-6f, 40f)), Is.LessThan(0.3f), "appears at the route's first point, walls or not");

        float travelled = 0f; var last = elite.Pos; bool passedSecond = false;
        for (int i = 0; i < 400 && elite.Entering; i++)
        {
            _dir.Step(0.02f);
            travelled += Vector2.Distance(last, elite.Pos); last = elite.Pos;
            if (Vector2.Distance(elite.Pos, new Vector2(4f, 46f)) < 0.3f) passedSecond = true;
            Assert.IsFalse(elite.Casting, "no attacks while walking in");
        }
        Assert.IsFalse(elite.Entering, "arrived");
        Assert.IsTrue(passedSecond, "went through the middle point");
        Assert.That(Vector2.Distance(elite.Pos, BossSpot), Is.LessThan(0.6f), "ends on the marker");
        float routeLength = Vector2.Distance(new Vector2(-6f, 40f), new Vector2(4f, 46f)) + Vector2.Distance(new Vector2(4f, 46f), BossSpot);
        Assert.That(travelled, Is.EqualTo(routeLength).Within(0.7f));
        Run(_def.FirstAttackDelay + 0.5f);
        Assert.IsTrue(elite.Casting || _dir.Telegraphs.Count > 0, "fights once it has arrived");
    }

    [Test]
    public void Intro_CanWaitUntilItArrives()
    {
        _spawn.Route = new[] { RoutePoint(new Vector2(-6f, 40f)) };
        _spawn.EntrySpeed = 8f; _spawn.PlayIntro = true; _spawn.IntroWhen = MiniBossIntroWhen.OnArrive; _spawn.IntroSeconds = 0.6f;
        _spawn.IntroEffectId = "no.such.effect";                 // an unknown effect id must not break anything
        int cutscenes = 0; Vector2 focus = default; float seconds = 0f;
        _dir.CutsceneStarted += (f, s, title, fx) => { cutscenes++; focus = f; seconds = s; };
        var elite = Summon();
        Assert.AreEqual(0, cutscenes, "nothing plays as it appears");
        Assert.IsFalse(_dir.InCutscene);
        for (int i = 0; i < 300 && elite.Entering; i++) _dir.Step(0.02f);
        Assert.AreEqual(1, cutscenes, "the intro plays on arrival");
        Assert.That(Vector2.Distance(focus, BossSpot), Is.LessThan(0.6f), "on the spot it arrived at");
        Assert.AreEqual(0.6f, seconds, "the marker's own intro length");
        Assert.IsTrue(_dir.InCutscene);
    }

    [Test]
    public void Intro_OnAppear_PlaysAtTheRoutesFirstPoint()
    {
        _spawn.Route = new[] { RoutePoint(new Vector2(-6f, 40f)) };
        _spawn.PlayIntro = true; _spawn.IntroWhen = MiniBossIntroWhen.OnAppear;
        Vector2 focus = default; float seconds = 0f; int cutscenes = 0;
        _dir.CutsceneStarted += (f, s, title, fx) => { cutscenes++; focus = f; seconds = s; };
        var elite = Summon();
        Assert.AreEqual(1, cutscenes);
        Assert.That(Vector2.Distance(focus, new Vector2(-6f, 40f)), Is.LessThan(0.3f));
        Assert.AreEqual(_def.IntroDuration, seconds, "0 on the marker = the kind's own length");
        Run(_def.IntroDuration + 6f);   // the intro freezes the game first; the 16-unit walk then takes 4 seconds
        Assert.IsFalse(elite.Entering);
        Assert.AreEqual(1, cutscenes, "and not again on arrival");
    }

    [Test]
    public void AtProgress_AppearsWhenTheSquadHasComeThatFar_NotByDistance()
    {
        _spawn.AtProgress = 0.5f; _spawn.TriggerRadius = 1000f; _spawn.PlayIntro = false;
        _dir.Step(0.02f);
        Assert.IsFalse(_spawn.Triggered, "the huge radius is ignored: progress decides");
        Teleport(new Vector2(10f, 70f)); Run(0.1f);            // a quarter of the way
        Assert.IsFalse(_spawn.Triggered);
        Teleport(new Vector2(10f, 48f)); Run(0.1f);            // past halfway
        Assert.IsTrue(_spawn.Triggered);
        Assert.That(Vector2.Distance(_spawn.Elite.Pos, BossSpot), Is.LessThan(0.6f));
    }

    [Test]
    public void PresentFromStart_IgnoresTheRoute_AndSpawnMiniBossForcesOneOut()
    {
        _spawn.PresentFromStart = true;
        _spawn.Route = new[] { RoutePoint(new Vector2(-6f, 40f)) };
        _dir.Restart();
        _dir.Step(0.02f);
        Assert.IsTrue(_spawn.Triggered);
        Assert.IsFalse(_spawn.Elite.Entering, "a fixed one is simply there");
        Assert.That(Vector2.Distance(_spawn.Elite.Pos, BossSpot), Is.LessThan(0.6f));

        var go = new GameObject("second"); go.transform.SetParent(_fc.transform); go.transform.position = new Vector3(10f, 0f, 20f);
        var second = go.AddComponent<MiniBossSpawn>(); second.Definition = _def; second.TriggerRadius = 1f; second.PlayIntro = false;
        _dir.Restart(); _dir.Step(0.02f);
        Assert.IsFalse(second.Triggered);
        Assert.IsNotNull(_dir.SpawnMiniBoss(second), "forced out from the tool");
        Assert.IsTrue(second.Triggered);
        Assert.IsNull(_dir.SpawnMiniBoss(second), "only once");
    }
}
