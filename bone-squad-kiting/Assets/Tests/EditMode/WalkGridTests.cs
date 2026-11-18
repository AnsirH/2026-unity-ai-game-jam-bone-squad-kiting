using NUnit.Framework;
using UnityEngine;
using BoneSquadKiting.Core;

public class WalkGridTests
{
    [Test]
    public void Open_CoversTheRectangle_AndMapsPointsToCells()
    {
        var g = WalkGrid.Open(new Vector2(-2, -1), new Vector2(2, 1), 0.5f);
        Assert.AreEqual(8, g.Width); Assert.AreEqual(4, g.Height);
        Assert.AreEqual(new Vector2Int(0, 0), g.Cell(new Vector2(-1.9f, -0.9f)));
        Assert.AreEqual(new Vector2Int(7, 3), g.Cell(new Vector2(1.9f, 0.9f)));
        Assert.That(g.CellCenter(0, 0), Is.EqualTo(new Vector2(-1.75f, -0.75f)));
        Assert.IsTrue(g.IsWalkable(Vector2.zero));
        Assert.IsFalse(g.IsWalkable(new Vector2(5, 0)), "outside the grid is never walkable");
    }

    [Test]
    public void Slide_StopsAtWalls_AndSlidesAlongThem()
    {
        var g = WalkGrid.Open(Vector2.zero, new Vector2(4, 4), 1f);
        g.Walkable[g.Index(2, 1)] = false;            // a pillar at cell (2,1)
        var from = new Vector2(1.5f, 1.5f);

        Assert.AreEqual(new Vector2(1.5f, 2.5f), g.Slide(from, new Vector2(1.5f, 2.5f)), "free move");
        Assert.AreEqual(new Vector2(1.5f, 1.8f), g.Slide(from, new Vector2(2.5f, 1.8f)), "x blocked: keeps the z part");
        Assert.AreEqual(from, g.Slide(from, new Vector2(2.5f, 1.5f)), "fully blocked: stays put");
        Assert.AreEqual(new Vector2(1.5f, 0.5f), g.Slide(from, new Vector2(-3f, 0.5f)), "leaving the grid is blocked like a wall");
    }

    [Test]
    public void NearestWalkable_ReturnsThePointOrTheClosestOpenCell()
    {
        var g = WalkGrid.Open(Vector2.zero, new Vector2(3, 1), 1f);
        g.Walkable[g.Index(1, 0)] = false;
        Assert.AreEqual(new Vector2(0.2f, 0.3f), g.NearestWalkable(new Vector2(0.2f, 0.3f)));
        Assert.AreEqual(new Vector2(0.5f, 0.5f), g.NearestWalkable(new Vector2(1.2f, 0.5f)), "blocked cell: nearest open center");
        Assert.AreEqual(new Vector2(2.5f, 0.5f), g.NearestWalkable(new Vector2(9f, 0.5f)), "off the grid: nearest open center");
    }

    [Test]
    public void LastWalkableToward_ReturnsTheTargetWhenTheWayIsOpen_OtherwiseStopsAtTheWall()
    {
        var g = WalkGrid.Open(Vector2.zero, new Vector2(10, 4), 1f);
        for (int z = 0; z < 4; z++) g.Walkable[g.Index(6, z)] = false;   // a wall at x in [6,7)
        var from = new Vector2(2.5f, 2.5f);

        Assert.AreEqual(new Vector2(5.2f, 1.5f), g.LastWalkableToward(from, new Vector2(5.2f, 1.5f)));

        var stopped = g.LastWalkableToward(from, new Vector2(8.5f, 2.5f));
        Assert.That(stopped.x, Is.LessThan(6f)); Assert.That(stopped.x, Is.GreaterThan(5.5f), "right up against the wall");
        Assert.That(stopped.y, Is.EqualTo(2.5f).Within(1e-4f), "stays on the line from the anchor");

        var outside = g.LastWalkableToward(from, new Vector2(2.5f, 9f));
        Assert.That(outside.y, Is.LessThan(4f)); Assert.That(outside.y, Is.GreaterThan(3.5f), "the grid edge works like a wall");
        Assert.AreEqual(from, g.LastWalkableToward(from, from));
    }
}
