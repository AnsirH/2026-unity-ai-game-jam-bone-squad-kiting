using NUnit.Framework;
using UnityEngine;
using BoneSquadKiting.Bosses;
using BoneSquadKiting.Cameras;
using BoneSquadKiting.Combat;
using BoneSquadKiting.Core;
using BoneSquadKiting.Squad;
using BoneSquadKiting.Stage;

/// <summary>Squad movement on a stage's walkable grid: the anchor stays inside, slots outside are pressed in.</summary>
public class SquadBoundaryTests
{
    FormationController _fc;
    GameObject _extra;

    [SetUp] public void SetUp() { _fc = SquadTestRig.MakeFc(); }

    [TearDown]
    public void TearDown()
    {
        if (_fc != null) Object.DestroyImmediate(_fc.gameObject);
        if (_extra != null) Object.DestroyImmediate(_extra);
    }

    // A 20 x 20 room of 1-unit cells with its corner at the origin.
    WalkGrid Room()
    {
        var g = WalkGrid.Open(Vector2.zero, new Vector2(20, 20), 1f);
        _fc.Grid = g;
        return g;
    }

    void Place(Vector2 anchor)
    {
        _fc.StartAnchor = anchor;
        _fc.Init();
    }

    void Run(float seconds) { for (float t = 0; t < seconds; t += 0.016f) _fc.Tick(0.016f); }

    [Test]
    public void Anchor_CannotLeaveTheWalkableArea()
    {
        var g = Room();
        Place(new Vector2(10, 10));
        _fc.SetDestination(new Vector2(40, 10));
        Assert.IsTrue(g.IsWalkable(_fc.Destination), "a click outside becomes the nearest walkable point");
        Run(4f);
        Assert.IsTrue(g.IsWalkable(_fc.Anchor));
        Assert.That(_fc.Anchor.x, Is.GreaterThan(19f), "it went as far as the area allows");
        Assert.IsFalse(_fc.IsMoving);
    }

    [Test]
    public void SlotsOutsideTheArea_ArePressedInTowardTheAnchor()
    {
        var g = Room();
        Place(new Vector2(10, 1.2f));            // close to the z = 0 edge
        _fc.SetFormation("ring");                // radius 3.7: the near half of the ring falls outside
        Run(3f);

        int pressed = 0;
        foreach (var u in _fc.Units)
        {
            Assert.IsTrue(g.IsWalkable(new Vector2(u.X, u.Z)), $"unit {u.Id} stands on walkable ground");
            if (Vector2.Distance(new Vector2(u.X, u.Z), _fc.Anchor + u.Slot) > 0.1f) pressed++;
            else Assert.That((new Vector2(u.X, u.Z) - _fc.Anchor).magnitude, Is.EqualTo(3.7f).Within(0.05f));
        }
        Assert.That(pressed, Is.GreaterThan(5), "units whose slot is outside did not reach it");
        Assert.That(pressed, Is.LessThan(20), "units whose slot is inside stand on it");
    }

    [Test]
    public void PressedUnits_CountAsStopped_AndFire()
    {
        Room();
        Place(new Vector2(10, 1.2f));
        _fc.SetFormation("ring");
        Run(3f);
        foreach (var u in _fc.Units) Assert.IsFalse(u.Moving, $"unit {u.Id} has arrived where it was sent");

        var boss = new Boss();
        var reg = new EnemyRegistry();
        var shots = new ShotSystem { FireRange = 100f };
        reg.Register(boss);
        foreach (var u in _fc.Units) u.Cd = 0;
        _fc.Tick(0.016f); shots.Tick(_fc.Units, reg, SquadOrder.Move(_fc.Anchor), 0.016f);
        Assert.AreEqual(GameConstants.SquadSize, shots.Shots.Count, "every unit fires, including the ones against the wall");
    }

    [Test]
    public void Anchor_SlidesAroundACorner()
    {
        var g = Room();
        for (int z = 6; z < 20; z++) for (int x = 6; x < 20; x++) g.Walkable[g.Index(x, z)] = false; // big block in one corner
        Place(new Vector2(3, 15));
        _fc.SetFormation("dense");
        _fc.SetDestination(new Vector2(15, 3));   // the straight line cuts through the block
        Run(6f);
        Assert.That(Vector2.Distance(_fc.Anchor, new Vector2(15, 3)), Is.LessThan(0.1f), "reached by sliding along the block");
        Assert.IsTrue(g.IsWalkable(_fc.Anchor));
    }

    [Test]
    public void Anchor_SlidesAlongAStaircaseEdge()
    {
        var g = Room();
        for (int z = 0; z < 20; z++) for (int x = 0; x < 20; x++) if (x + z >= 24) g.Walkable[g.Index(x, z)] = false; // diagonal wall
        Place(new Vector2(4, 4));
        _fc.SetDestination(new Vector2(5, 17.5f));
        Run(3f);
        var start = _fc.Anchor;
        _fc.SetDestination(new Vector2(17.5f, 5));   // along the diagonal wall, brushing its steps
        Run(6f);
        Assert.That(Vector2.Distance(_fc.Anchor, _fc.Destination), Is.LessThan(0.1f), $"got stuck at {_fc.Anchor} coming from {start}");
    }

    [Test]
    public void BoxedInAnchor_GivesUpInsteadOfPushingForever()
    {
        var g = Room();
        for (int z = 0; z < 20; z++) g.Walkable[g.Index(10, z)] = false;   // a wall with no gap
        Place(new Vector2(5, 10));
        _fc.SetDestination(new Vector2(15, 10));   // walkable, but on the far side
        Run(4f);
        Assert.IsFalse(_fc.IsMoving, "stopped at the wall");
        Assert.That(_fc.Anchor.x, Is.LessThan(10f));
        Assert.IsTrue(g.IsWalkable(_fc.Anchor));
    }

    [Test]
    public void WithoutAGrid_ThePrototypeArenaLimitsStillApply()
    {
        _fc.SetDestination(new Vector2(100f, -100f));
        Assert.AreEqual(new Vector2(GameConstants.MoveClampX, GameConstants.MoveClampZMin), _fc.Destination);
    }

    [Test]
    public void StageSetup_HandsTheGridStartPointAndCameraLimitsToTheGame()
    {
        _extra = new GameObject("stage");
        _extra.transform.position = new Vector3(500f, 0f, 500f);
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.transform.SetParent(_extra.transform, false);
        floor.transform.localPosition = new Vector3(0, -0.5f, 0);
        floor.transform.localScale = new Vector3(12, 1, 30);
        floor.layer = 20;
        var baker = _extra.AddComponent<WalkGridBaker>();
        baker.Size = new Vector2(12, 30); baker.CellSize = 0.75f;
        baker.GroundMask = 1 << 20; baker.ObstacleMask = 1 << 21;

        var start = new GameObject("start"); start.transform.SetParent(_extra.transform, false); start.transform.localPosition = new Vector3(0, 0, -12);
        var end = new GameObject("end"); end.transform.SetParent(_extra.transform, false); end.transform.localPosition = new Vector3(0, 0, 12);
        var camGo = new GameObject("cam"); camGo.transform.SetParent(_extra.transform, false);
        camGo.transform.rotation = Quaternion.Euler(60, 180, 0);
        var cam = camGo.AddComponent<EdgeScrollCamera>(); cam.enabled = false;

        var stage = _extra.AddComponent<StageSetup>();   // Awake ran with nothing assigned; apply by hand
        stage.Ground = baker; stage.Squad = _fc; stage.Cam = cam;
        stage.StartPoint = start.transform; stage.EndPoint = end.transform; stage.EndRadius = 2f;
        stage.Apply();

        Assert.AreSame(stage.Grid, _fc.Grid);
        Assert.That(Vector2.Distance(_fc.Anchor, new Vector2(500, 488)), Is.LessThan(0.01f), "squad placed on the start point");
        _fc.Init();
        Assert.That(Vector2.Distance(_fc.Anchor, new Vector2(500, 488)), Is.LessThan(0.01f), "and stays there when the match starts");
        Assert.AreEqual(new Vector2(494, 485), cam.BoundsMin);
        Assert.AreEqual(new Vector2(506, 515), cam.BoundsMax);

        Assert.IsFalse(stage.ReachedEnd);
        _fc.SetDestination(new Vector2(500, 511));
        Run(6f);
        Assert.IsTrue(stage.ReachedEnd, "walking to the end point is detected");
    }
}
