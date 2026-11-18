using NUnit.Framework;
using UnityEngine;
using BoneSquadKiting.Bosses;
using BoneSquadKiting.Combat;
using BoneSquadKiting.Core;
using BoneSquadKiting.Data;
using BoneSquadKiting.Mobs;
using BoneSquadKiting.Squad;

public class MobSystemTests
{
    FormationController _fc;
    MobDefinition _def;
    MobSystem _mobs;
    EnemyRegistry _reg;

    [SetUp]
    public void SetUp()
    {
        _fc = SquadTestRig.MakeFc();                    // dense squad around (0, 3.4)
        _def = ScriptableObject.CreateInstance<MobDefinition>();
        _mobs = new MobSystem();
        _reg = new EnemyRegistry();
    }

    [TearDown] public void TearDown() { Object.DestroyImmediate(_fc.gameObject); Object.DestroyImmediate(_def); }

    void Run(float seconds, WalkGrid grid = null, FlowField flow = null)
    {
        for (float t = 0; t < seconds; t += 0.02f) { _fc.Tick(0.02f); _mobs.Tick(_fc, _reg, grid, flow, 0.02f); }
    }

    static float NearestUnitDistance(FormationController fc, Mob m)
    {
        float best = float.MaxValue;
        foreach (var u in fc.Units) if (!u.Dead) best = Mathf.Min(best, Vector2.Distance(m.Pos, new Vector2(u.X, u.Z)));
        return best;
    }

    [Test]
    public void Separate_MovesOverlappingMobsApart_AndLeavesOthersAlone()
    {
        var a = new Mob { Def = _def, X = 0, Z = 0, Health = 1 };
        var b = new Mob { Def = _def, X = 0.2f, Z = 0, Health = 1 };
        MobSystem.Separate(a, b);
        Assert.That(Vector2.Distance(a.Pos, b.Pos), Is.EqualTo(0.6f).Within(1e-4f));
        Assert.That(a.X, Is.EqualTo(-0.2f).Within(1e-4f), "each moved half the overlap");

        var c = new Mob { Def = _def, X = 5, Z = 0, Health = 1 };
        MobSystem.Separate(a, c);
        Assert.AreEqual(5f, c.X);
    }

    [Test]
    public void PushOut_MovesOnlyTheMob()
    {
        var m = new Mob { Def = _def, X = 0.1f, Z = 0, Health = 1 };
        MobSystem.PushOut(m, Vector2.zero, 0.25f);
        Assert.That(m.Pos.magnitude, Is.EqualTo(0.55f).Within(1e-4f));
    }

    [Test]
    public void Mob_WalksToTheSquad_StopsAtAUnit_AndHitsIt()
    {
        var m = _mobs.Spawn(_def, new Vector2(0, -6), _reg);
        Run(6f);
        float d = NearestUnitDistance(_fc, m);
        Assert.That(d, Is.GreaterThan(0.54f), "does not overlap the unit");
        Assert.That(d, Is.LessThan(0.66f), "stands within reach of it");

        float lowest = 100f; foreach (var u in _fc.Units) lowest = Mathf.Min(lowest, u.Hp);
        Assert.That(lowest, Is.LessThan(100f), "it has hit a unit");
        Assert.That((100f - lowest) % 10f, Is.EqualTo(0f).Within(1e-3f), "in whole hits of 10");
    }

    [Test]
    public void Swarm_DoesNotPileUpOnOnePoint()
    {
        for (int i = 0; i < 40; i++) _mobs.Spawn(_def, new Vector2(Random.Range(-1f, 1f), -6 + Random.Range(-1f, 1f)), _reg);
        Run(8f);
        int overlapping = 0;
        for (int i = 0; i < _mobs.Mobs.Count; i++)
            for (int j = i + 1; j < _mobs.Mobs.Count; j++)
                if (Vector2.Distance(_mobs.Mobs[i].Pos, _mobs.Mobs[j].Pos) < 0.3f) overlapping++;   // half a diameter
        Assert.AreEqual(0, overlapping, "no two mobs more than half overlapped");
    }

    [Test]
    public void MobsKillAUnit_AndReportIt()
    {
        foreach (var u in _fc.Units) u.Hp = 10f;   // one hit each
        for (int i = 0; i < 10; i++) _mobs.Spawn(_def, new Vector2(i * 0.7f - 3f, 1.0f), _reg);
        int killed = 0;
        for (float t = 0; t < 4f; t += 0.02f)
        {
            _fc.Tick(0.02f); _mobs.Tick(_fc, _reg, null, null, 0.02f);
            killed += _mobs.UnitsKilledLastTick;
        }
        Assert.That(killed, Is.GreaterThan(0));
        Assert.AreEqual(GameConstants.SquadSize - killed, _fc.AliveCount);
    }

    [Test]
    public void FlowField_LeadsMobsAroundAWall()
    {
        // Room 20 x 20, a wall across the middle with a gap at the far right. Squad below, mob above.
        var grid = WalkGrid.Open(new Vector2(-10, -10), new Vector2(10, 10), 1f);
        for (int x = 0; x < 17; x++) grid.Walkable[grid.Index(x, 10)] = false;
        _fc.Grid = grid; _fc.StartAnchor = new Vector2(-5, -5); _fc.Init();
        var flow = new FlowField(grid);
        var m = _mobs.Spawn(_def, new Vector2(-5, 5), _reg);

        Run(14f, grid, flow);

        Assert.That(m.Z, Is.LessThan(0f), "it got past the wall");
        Assert.That(NearestUnitDistance(_fc, m), Is.LessThan(0.7f), "and reached the squad");
    }

    [Test]
    public void WithoutAFlowField_TheWallStopsTheMob_ButItNeverEntersIt()
    {
        var grid = WalkGrid.Open(new Vector2(-10, -10), new Vector2(10, 10), 1f);
        for (int x = 0; x < 20; x++) grid.Walkable[grid.Index(x, 10)] = false;   // no gap at all
        _fc.Grid = grid; _fc.StartAnchor = new Vector2(0, -5); _fc.Init();
        var m = _mobs.Spawn(_def, new Vector2(0, 5), _reg);
        for (float t = 0; t < 5f; t += 0.02f)
        {
            _fc.Tick(0.02f); _mobs.Tick(_fc, _reg, grid, null, 0.02f);
            Assert.IsTrue(grid.IsWalkable(m.Pos), $"mob entered a blocked cell at {m.Pos}");
        }
        Assert.That(m.Z, Is.GreaterThan(1f));
    }

    [Test]
    public void DeadMobs_AreRemovedAndUnregistered()
    {
        var m = _mobs.Spawn(_def, new Vector2(0, -6), _reg);
        int died = 0; _mobs.OnMobDied += _ => died++;
        Assert.AreEqual(1, _reg.Enemies.Count);
        m.ApplyDamage(1000f);
        Run(0.1f);
        Assert.AreEqual(0, _mobs.Mobs.Count);
        Assert.AreEqual(0, _reg.Enemies.Count);
        Assert.AreEqual(1, died);
    }

    [Test]
    public void Spawn_RespectsTheAliveCap()
    {
        _mobs.MaxAlive = 3;
        for (int i = 0; i < 5; i++) _mobs.Spawn(_def, new Vector2(i * 2, -6), _reg);
        Assert.AreEqual(3, _mobs.Mobs.Count);
    }

    [Test]
    public void ElitesPushMobsOut()
    {
        var boss = new Boss();
        _reg.Register(boss);
        var m = _mobs.Spawn(_def, boss.Pos + new Vector2(0.2f, 0), _reg);
        Run(0.1f);
        Assert.That(Vector2.Distance(m.Pos, boss.Pos), Is.GreaterThanOrEqualTo(boss.Radius + m.Radius - 0.05f));
    }
}
