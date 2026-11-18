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
        _def.Patterns = ScriptableObject.CreateInstance<AttackSequence>();
        _def.Patterns.Patterns = new[] { pattern };

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
}
